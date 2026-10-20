using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace app_desktop.Services.CloudSave;

internal sealed class GoogleDriveAuthService
{
	private sealed record OAuthCallback(TcpClient Client, NetworkStream Stream, string? Code, string? Error);

	private const string AuthorizationEndpoint = "https://accounts.google.com/o/oauth2/v2/auth";

	private const string TokenEndpoint = "https://oauth2.googleapis.com/token";

	private const string DriveScope = "https://www.googleapis.com/auth/drive.file";

	private static readonly TimeSpan AccessTokenSafetyWindow = TimeSpan.FromMinutes(2.0);

	private readonly string clientId;

	private readonly CloudSaveTokenStore tokenStore;

	private readonly HttpClient httpClient;

	private readonly Action<string> openBrowser;

	private readonly TimeSpan authorizationTimeout;

	private readonly string? clientSecret;

	private readonly Action<GoogleDriveOAuthException> reportFailure;

	private readonly TimeSpan tokenRequestTimeout;

	private readonly SemaphoreSlim gate = new SemaphoreSlim(1, 1);

	internal bool IsConfigured => !string.IsNullOrWhiteSpace(clientId);

	internal GoogleDriveAuthService(string clientId, CloudSaveTokenStore tokenStore, HttpClient? httpClient = null, Action<string>? openBrowser = null, TimeSpan? authorizationTimeout = null, string? clientSecret = null, Action<GoogleDriveOAuthException>? reportFailure = null, TimeSpan? tokenRequestTimeout = null)
	{
		this.clientId = clientId?.Trim() ?? string.Empty;
		this.tokenStore = tokenStore ?? throw new ArgumentNullException("tokenStore");
		this.httpClient = httpClient ?? new HttpClient
		{
			Timeout = TimeSpan.FromSeconds(45.0)
		};
		this.openBrowser = openBrowser ?? ((Action<string>)delegate(string uri)
		{
			Process.Start(new ProcessStartInfo
			{
				FileName = uri,
				UseShellExecute = true
			});
		});
		this.authorizationTimeout = authorizationTimeout ?? TimeSpan.FromMinutes(3.0);
		this.clientSecret = (string.IsNullOrWhiteSpace(clientSecret) ? null : clientSecret.Trim());
		this.reportFailure = reportFailure ?? ((Action<GoogleDriveOAuthException>)delegate(GoogleDriveOAuthException error)
		{
			DesktopDiagnosticLog.Write("CloudSaveOAuth", "Failed", error.DiagnosticDetails);
		});
		this.tokenRequestTimeout = tokenRequestTimeout ?? TimeSpan.FromSeconds(45.0);
	}

	internal bool HasStoredCredential(Guid nestGUserId)
	{
		return tokenStore.HasCredential(nestGUserId);
	}

	internal async Task<string?> GetConnectionIdAsync(Guid nestGUserId, CancellationToken cancellationToken)
	{
		return (await tokenStore.LoadAsync(nestGUserId, cancellationToken))?.ConnectionId;
	}

	internal async Task<string> GetAccessTokenAsync(Guid nestGUserId, bool allowInteractive, bool forceRefresh = false, CancellationToken cancellationToken = default(CancellationToken))
	{
		if (!IsConfigured)
		{
			throw GoogleDriveOAuthException.Local("configuration", "not_configured");
		}
		await gate.WaitAsync(cancellationToken);
		try
		{
			GoogleDriveCredential credential = await tokenStore.LoadAsync(nestGUserId, cancellationToken);
			if (credential != null && (credential.NestGUserId != nestGUserId || !string.Equals(credential.OAuthClientId, clientId, StringComparison.Ordinal)))
			{
				credential = null;
			}
			if (!forceRefresh && credential != null && !string.IsNullOrWhiteSpace(credential.AccessToken) && credential.AccessTokenExpiresAtUtc > DateTimeOffset.UtcNow.Add(AccessTokenSafetyWindow))
			{
				return credential.AccessToken;
			}
			if (credential != null && !string.IsNullOrWhiteSpace(credential.RefreshToken))
			{
				try
				{
					credential = await RefreshAsync(credential, cancellationToken);
					await SaveCredentialAsync(credential, cancellationToken);
					return credential.AccessToken;
				}
				catch (GoogleDriveCredentialRevokedException obj)
				{
					reportFailure(obj);
					await tokenStore.ClearAsync(nestGUserId);
				}
			}
			if (!allowInteractive)
			{
				throw GoogleDriveOAuthException.Local("authorization", "sign_in_required");
			}
			return (await AuthorizeInteractivelyAsync(nestGUserId, cancellationToken)).AccessToken;
		}
		catch (GoogleDriveOAuthException obj2)
		{
			reportFailure(obj2);
			throw;
		}
		finally
		{
			gate.Release();
		}
	}

	internal async Task DisconnectAsync(Guid nestGUserId)
	{
		await gate.WaitAsync();
		try
		{
			await tokenStore.ClearAsync(nestGUserId);
		}
		finally
		{
			gate.Release();
		}
	}

	private async Task SaveCredentialAsync(GoogleDriveCredential credential, CancellationToken cancellationToken)
	{
		try
		{
			await tokenStore.SaveAsync(credential, cancellationToken);
		}
		catch (OperationCanceledException)
		{
			throw;
		}
		catch
		{
			throw GoogleDriveOAuthException.Local("credential_store", "credential_store_failed");
		}
	}

	private async Task<GoogleDriveCredential> AuthorizeInteractivelyAsync(Guid nestGUserId, CancellationToken cancellationToken)
	{
		using TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
		try
		{
			listener.Start(4);
		}
		catch
		{
			throw GoogleDriveOAuthException.Local("callback_listener", "network_error");
		}
		int port = ((IPEndPoint)listener.LocalEndpoint).Port;
		string redirectUri = $"http://127.0.0.1:{port}/oauth2/callback/";
		string text = Base64Url(RandomNumberGenerator.GetBytes(32));
		string verifier = Base64Url(RandomNumberGenerator.GetBytes(64));
		string challenge = Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
		string obj2 = BuildAuthorizationUri(redirectUri, text, challenge);
		try
		{
			openBrowser(obj2);
		}
		catch
		{
			throw GoogleDriveOAuthException.Local("authorization", "browser_unavailable");
		}
		using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		timeout.CancelAfter(authorizationTimeout);
		OAuthCallback callback = null;
		try
		{
			callback = await ReceiveCallbackAsync(listener, text, timeout.Token);
			if (!string.IsNullOrWhiteSpace(callback.Error))
			{
				throw GoogleDriveOAuthException.Local("authorization", "access_denied");
			}
			if (string.IsNullOrWhiteSpace(callback.Code))
			{
				throw GoogleDriveOAuthException.Local("callback", "invalid_callback");
			}
			GoogleDriveCredential token = await ExchangeCodeAsync(nestGUserId, callback.Code, verifier, redirectUri, timeout.Token);
			GoogleDriveCredential googleDriveCredential = token;
			googleDriveCredential.AccountEmail = await TryGetAccountEmailAsync(token.AccessToken, timeout.Token);
			await SaveCredentialAsync(token, timeout.Token);
			await TryWriteBrowserResponseAsync(callback.Stream, success: true);
			return token;
		}
		catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
		{
			if ((object)callback != null)
			{
				await TryWriteBrowserResponseAsync(callback.Stream, success: false);
			}
			throw GoogleDriveOAuthException.Local("authorization", "timeout");
		}
		catch
		{
			if ((object)callback != null)
			{
				await TryWriteBrowserResponseAsync(callback.Stream, success: false);
			}
			throw;
		}
		finally
		{
			callback?.Client.Dispose();
		}
	}

	private string BuildAuthorizationUri(string redirectUri, string state, string challenge)
	{
		Dictionary<string, string> source = new Dictionary<string, string>
		{
			["client_id"] = clientId,
			["redirect_uri"] = redirectUri,
			["response_type"] = "code",
			["scope"] = "https://www.googleapis.com/auth/drive.file",
			["access_type"] = "offline",
			["prompt"] = "consent",
			["code_challenge"] = challenge,
			["code_challenge_method"] = "S256",
			["state"] = state
		};
		return "https://accounts.google.com/o/oauth2/v2/auth?" + string.Join("&", source.Select((KeyValuePair<string, string> item) => Uri.EscapeDataString(item.Key) + "=" + Uri.EscapeDataString(item.Value)));
	}

	private async Task<GoogleDriveCredential> ExchangeCodeAsync(Guid nestGUserId, string code, string verifier, string redirectUri, CancellationToken cancellationToken)
	{
		Dictionary<string, string> parameters = new Dictionary<string, string>
		{
			["client_id"] = clientId,
			["code"] = code,
			["code_verifier"] = verifier,
			["redirect_uri"] = redirectUri,
			["grant_type"] = "authorization_code"
		};
		using JsonDocument jsonDocument = await SendTokenRequestAsync(parameters, interactive: true, cancellationToken);
		string accessToken = ReadRequiredString(jsonDocument.RootElement, "access_token");
		string refreshToken = ReadRequiredString(jsonDocument.RootElement, "refresh_token");
		int num = ReadExpiresIn(jsonDocument.RootElement);
		return new GoogleDriveCredential
		{
			NestGUserId = nestGUserId,
			OAuthClientId = clientId,
			ConnectionId = Guid.NewGuid().ToString("N"),
			AccessToken = accessToken,
			RefreshToken = refreshToken,
			AccessTokenExpiresAtUtc = DateTimeOffset.UtcNow.AddSeconds(num)
		};
	}

	private async Task<GoogleDriveCredential> RefreshAsync(GoogleDriveCredential credential, CancellationToken cancellationToken)
	{
		Dictionary<string, string> parameters = new Dictionary<string, string>
		{
			["client_id"] = clientId,
			["refresh_token"] = credential.RefreshToken,
			["grant_type"] = "refresh_token"
		};
		using JsonDocument jsonDocument = await SendTokenRequestAsync(parameters, interactive: false, cancellationToken);
		credential.AccessToken = ReadRequiredString(jsonDocument.RootElement, "access_token");
		credential.AccessTokenExpiresAtUtc = DateTimeOffset.UtcNow.AddSeconds(ReadExpiresIn(jsonDocument.RootElement));
		if (jsonDocument.RootElement.TryGetProperty("refresh_token", out var value) && value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString()))
		{
			credential.RefreshToken = ReadRequiredString(jsonDocument.RootElement, "refresh_token");
		}
		return credential;
	}

	private async Task<string?> TryGetAccountEmailAsync(string accessToken, CancellationToken cancellationToken)
	{
		using CancellationTokenSource metadataTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		metadataTimeout.CancelAfter(TimeSpan.FromSeconds(3.0));
		try
		{
			using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, "https://www.googleapis.com/drive/v3/about?fields=user(emailAddress)");
			request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
			using HttpResponseMessage response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, metadataTimeout.Token);
			if (!response.IsSuccessStatusCode)
			{
				return null;
			}
			using JsonDocument jsonDocument = JsonDocument.Parse(await ReadBoundedStringAsync(response.Content, metadataTimeout.Token));
			JsonElement value;
			JsonElement value2;
			return (jsonDocument.RootElement.TryGetProperty("user", out value) && value.TryGetProperty("emailAddress", out value2) && value2.ValueKind == JsonValueKind.String) ? value2.GetString() : null;
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			throw;
		}
		catch
		{
			return null;
		}
	}

	private static async Task<OAuthCallback> ReceiveCallbackAsync(TcpListener listener, string expectedState, CancellationToken cancellationToken)
	{
		for (int attempt = 0; attempt < 16; attempt++)
		{
			TcpClient client = await listener.AcceptTcpClientAsync(cancellationToken);
			bool accepted = false;
			try
			{
				try
				{
					NetworkStream stream = client.GetStream();
					using CancellationTokenSource requestTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
					requestTimeout.CancelAfter(TimeSpan.FromSeconds(5.0));
					byte[] headers = new byte[16384];
					int length = 0;
					string headerText = string.Empty;
					while (length < headers.Length)
					{
						int num = await stream.ReadAsync(headers.AsMemory(length), requestTimeout.Token);
						if (num == 0)
						{
							break;
						}
						length += num;
						headerText = Encoding.ASCII.GetString(headers, 0, length);
						if (headerText.Contains("\r\n\r\n", StringComparison.Ordinal))
						{
							break;
						}
					}
					string[] array = headerText.Split("\r\n", 2)[0].Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
					if (!headerText.Contains("\r\n\r\n", StringComparison.Ordinal) || array.Length != 3 || array[0] != "GET" || !array[1].StartsWith("/oauth2/callback/?", StringComparison.Ordinal) || !Uri.TryCreate("http://127.0.0.1" + array[1], UriKind.Absolute, out Uri result) || result.AbsolutePath != "/oauth2/callback/")
					{
						await TryWriteBrowserResponseAsync(stream, success: false);
						continue;
					}
					Dictionary<string, string> dictionary = ParseQuery(result.Query);
					string valueOrDefault = dictionary.GetValueOrDefault("state");
					if (valueOrDefault != null && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(valueOrDefault), Encoding.UTF8.GetBytes(expectedState)))
					{
						accepted = true;
						return new OAuthCallback(client, stream, dictionary.GetValueOrDefault("code"), dictionary.GetValueOrDefault("error"));
					}
					await TryWriteBrowserResponseAsync(stream, success: false);
				}
				catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
				{
					goto end_IL_0099;
				}
				catch (IOException)
				{
					goto end_IL_0099;
				}
				catch (UriFormatException)
				{
					goto end_IL_0099;
				}
				catch (ArgumentException)
				{
					goto end_IL_0099;
				}
				end_IL_0099:;
			}
			finally
			{
				if (!accepted)
				{
					client.Dispose();
				}
			}
		}
		throw GoogleDriveOAuthException.Local("callback", "invalid_callback");
	}

	private static Dictionary<string, string> ParseQuery(string query)
	{
		Dictionary<string, string> dictionary = new Dictionary<string, string>(StringComparer.Ordinal);
		string[] array = query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries);
		for (int i = 0; i < array.Length; i++)
		{
			string[] array2 = array[i].Split('=', 2);
			string key = Uri.UnescapeDataString(array2[0].Replace('+', ' '));
			string value = ((array2.Length == 2) ? Uri.UnescapeDataString(array2[1].Replace('+', ' ')) : string.Empty);
			if (!dictionary.TryAdd(key, value))
			{
				throw new ArgumentException("Duplicate OAuth callback parameter.");
			}
		}
		return dictionary;
	}

	private static async Task TryWriteBrowserResponseAsync(NetworkStream stream, bool success)
	{
		using CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2.0));
		try
		{
			await WriteBrowserResponseAsync(stream, success, timeout.Token);
		}
		catch
		{
		}
	}

	private static async Task WriteBrowserResponseAsync(NetworkStream stream, bool success, CancellationToken cancellationToken)
	{
		string value = (success ? "Đã kết nối Google Drive" : "Không thể kết nối Google Drive");
		string value2 = (success ? "Kết nối đã được xác nhận. Quay lại NestG Launcher để theo dõi việc lưu bản backup." : "Kết nối chưa hoàn tất. Hãy quay lại NestG Launcher để xem hướng xử lý.");
		string s = $"<!doctype html><html lang=\"vi\"><head><meta charset=\"utf-8\"><title>{value}</title>\n<style>body{{font-family:Segoe UI,sans-serif;background:#07111e;color:#edf7ff;display:grid;place-items:center;min-height:100vh;margin:0}}main{{max-width:520px;padding:32px;border:1px solid #31506b;border-radius:12px;background:#102033}}h1{{font-size:24px}}p{{color:#bed1df;line-height:1.55}}</style></head>\n<body><main><h1>{value}</h1><p>{value2}</p></main><script>history.replaceState(null,'',location.pathname);</script></body></html>";
		byte[] body = Encoding.UTF8.GetBytes(s);
		byte[] bytes = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: text/html; charset=utf-8\r\n" + $"Content-Length: {body.Length}\r\n" + "Cache-Control: no-store\r\nReferrer-Policy: no-referrer\r\nContent-Security-Policy: default-src 'none'; style-src 'unsafe-inline'; script-src 'unsafe-inline'; frame-ancestors 'none'\r\nConnection: close\r\n\r\n");
		await stream.WriteAsync(bytes, cancellationToken);
		await stream.WriteAsync(body, cancellationToken);
		await stream.FlushAsync(cancellationToken);
	}

	private async Task<JsonDocument> SendTokenRequestAsync(Dictionary<string, string> parameters, bool interactive, CancellationToken cancellationToken)
	{
		string stage = (interactive ? "token_exchange" : "token_refresh");
		using CancellationTokenSource requestTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		requestTimeout.CancelAfter(tokenRequestTimeout);
		try
		{
			if (clientSecret != null)
			{
				parameters["client_secret"] = clientSecret;
			}
			using FormUrlEncodedContent content = new FormUrlEncodedContent(parameters);
			using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, "https://oauth2.googleapis.com/token")
			{
				Content = content
			};
			using HttpResponseMessage response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, requestTimeout.Token);
			string text = await ReadBoundedStringAsync(response.Content, requestTimeout.Token);
			if (!response.IsSuccessStatusCode)
			{
				throw GoogleDriveOAuthException.FromResponse(response.StatusCode, text, interactive);
			}
			JsonDocument jsonDocument = JsonDocument.Parse(text);
			try
			{
				if (jsonDocument.RootElement.ValueKind != JsonValueKind.Object || !ReadRequiredString(jsonDocument.RootElement, "token_type").Equals("Bearer", StringComparison.OrdinalIgnoreCase))
				{
					throw GoogleDriveOAuthException.Local(stage, "invalid_response");
				}
				ReadRequiredString(jsonDocument.RootElement, "access_token");
				ReadExpiresIn(jsonDocument.RootElement);
				if (interactive)
				{
					ReadRequiredString(jsonDocument.RootElement, "refresh_token");
				}
				return jsonDocument;
			}
			catch
			{
				jsonDocument.Dispose();
				throw;
			}
		}
		catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
		{
			throw GoogleDriveOAuthException.Local(stage, "timeout");
		}
		catch (HttpRequestException)
		{
			throw GoogleDriveOAuthException.Local(stage, "network_error");
		}
		catch (InvalidDataException)
		{
			throw GoogleDriveOAuthException.Local(stage, "invalid_response");
		}
		catch (IOException)
		{
			throw GoogleDriveOAuthException.Local(stage, "invalid_response");
		}
		catch (JsonException)
		{
			throw GoogleDriveOAuthException.Local(stage, "invalid_response");
		}
	}

	private static string ReadRequiredString(JsonElement root, string name)
	{
		if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String)
		{
			string @string = value.GetString();
			if (@string != null)
			{
				int length = @string.Length;
				if (length > 0 && length <= 16384 && !@string.Any(char.IsWhiteSpace) && !@string.Any(char.IsControl))
				{
					return value.GetString();
				}
			}
		}
		throw GoogleDriveOAuthException.Local("token_response", "invalid_response");
	}

	private static int ReadExpiresIn(JsonElement root)
	{
		if (root.TryGetProperty("expires_in", out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var value2) && value2 > 0 && value2 <= 86400)
		{
			return value2;
		}
		throw GoogleDriveOAuthException.Local("token_response", "invalid_response");
	}

	private static async Task<string> ReadBoundedStringAsync(HttpContent content, CancellationToken cancellationToken)
	{
		if (content.Headers.ContentLength > 65536)
		{
			throw new InvalidDataException("OAuth response too large.");
		}
		string @string;
		await using (Stream stream = await content.ReadAsStreamAsync(cancellationToken))
		{
			using MemoryStream buffer = new MemoryStream();
			byte[] chunk = new byte[4096];
			while (true)
			{
				int num = await stream.ReadAsync(chunk, cancellationToken);
				if (num == 0)
				{
					break;
				}
				if (buffer.Length + num > 65536)
				{
					throw new InvalidDataException("OAuth response too large.");
				}
				buffer.Write(chunk, 0, num);
			}
			@string = Encoding.UTF8.GetString(buffer.ToArray());
		}
		return @string;
	}

	private static string Base64Url(byte[] bytes)
	{
		return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-')
			.Replace('/', '_');
	}
}
