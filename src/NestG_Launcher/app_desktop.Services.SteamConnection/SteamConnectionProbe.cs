using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace app_desktop.Services.SteamConnection;

internal sealed class SteamConnectionProbe : ISteamConnectionProbe, IDisposable
{
	private readonly HttpClient client;

	internal SteamConnectionProbe(HttpMessageHandler? handler = null)
	{
		if (handler == null)
		{
			handler = new SocketsHttpHandler
			{
				UseProxy = false,
				UseCookies = false,
				AllowAutoRedirect = false,
				ConnectTimeout = TimeSpan.FromSeconds(5.0),
				MaxConnectionsPerServer = 2,
				MaxResponseDrainSize = 0,
				PooledConnectionLifetime = TimeSpan.Zero
			};
		}
		client = new HttpClient(handler)
		{
			Timeout = Timeout.InfiniteTimeSpan
		};
	}

	public async Task<IReadOnlyDictionary<string, SteamConnectionProbeResult>> CheckAsync(CancellationToken cancellationToken)
	{
		return (await Task.WhenAll<KeyValuePair<string, SteamConnectionProbeResult>>(CheckOneAsync("login", new Uri("https://steamcommunity.com/login/home/"), image: false, cancellationToken), CheckOneAsync("store", new Uri("https://store.steampowered.com/"), image: false, cancellationToken), CheckOneAsync("images", new Uri("https://cdn.akamai.steamstatic.com/steam/apps/730/header.jpg"), image: true, cancellationToken)).ConfigureAwait(continueOnCapturedContext: false)).ToDictionary((KeyValuePair<string, SteamConnectionProbeResult> item) => item.Key, (KeyValuePair<string, SteamConnectionProbeResult> item) => item.Value);
	}

	private async Task<KeyValuePair<string, SteamConnectionProbeResult>> CheckOneAsync(string key, Uri endpoint, bool image, CancellationToken cancellationToken, int redirects = 0)
	{
		using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		deadline.CancelAfter(TimeSpan.FromSeconds(12.0));
		try
		{
			using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, endpoint)
			{
				Version = HttpVersion.Version11,
				VersionPolicy = HttpVersionPolicy.RequestVersionExact
			};
			request.Headers.CacheControl = new CacheControlHeaderValue
			{
				NoCache = true,
				NoStore = true
			};
			request.Headers.ConnectionClose = true;
			request.Headers.UserAgent.ParseAdd("NestG-SteamConnectivity/1.0");
			using HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token).ConfigureAwait(continueOnCapturedContext: false);
			int statusCode = (int)response.StatusCode;
			if (statusCode >= 300 && statusCode < 400)
			{
				Uri location = response.Headers.Location;
				Uri uri = (((object)location == null) ? null : new Uri(endpoint, location));
				if (redirects < 3 && uri?.Scheme == "https" && uri.Host == endpoint.Host && uri.IsDefaultPort)
				{
					response.Dispose();
					return await CheckOneAsync(key, uri, image, deadline.Token, redirects + 1).ConfigureAwait(continueOnCapturedContext: false);
				}
				return new KeyValuePair<string, SteamConnectionProbeResult>(key, new SteamConnectionProbeResult("unknown", "redirect"));
			}
			if (!response.IsSuccessStatusCode)
			{
				return new KeyValuePair<string, SteamConnectionProbeResult>(key, new SteamConnectionProbeResult("failed", "http-" + (int)response.StatusCode));
			}
			if (image)
			{
				MediaTypeHeaderValue? contentType = response.Content.Headers.ContentType;
				if (contentType == null || contentType.MediaType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) != true)
				{
					return new KeyValuePair<string, SteamConnectionProbeResult>(key, new SteamConnectionProbeResult("failed", "invalid-image"));
				}
				using Stream body = await response.Content.ReadAsStreamAsync(deadline.Token).ConfigureAwait(continueOnCapturedContext: false);
				byte[] prefix = new byte[16];
				int count;
				int num;
				for (count = 0; count < prefix.Length; count += num)
				{
					num = await body.ReadAsync(prefix.AsMemory(count), deadline.Token).ConfigureAwait(continueOnCapturedContext: false);
					if (num == 0)
					{
						break;
					}
				}
				if (count < 3 || prefix[0] != byte.MaxValue || prefix[1] != 216 || prefix[2] != byte.MaxValue)
				{
					return new KeyValuePair<string, SteamConnectionProbeResult>(key, new SteamConnectionProbeResult("failed", "invalid-image"));
				}
			}
			else
			{
				MediaTypeHeaderValue? contentType2 = response.Content.Headers.ContentType;
				if (contentType2 == null || contentType2.MediaType?.Equals("text/html", StringComparison.OrdinalIgnoreCase) != true)
				{
					return new KeyValuePair<string, SteamConnectionProbeResult>(key, new SteamConnectionProbeResult("unknown", "unexpected-content"));
				}
			}
			return new KeyValuePair<string, SteamConnectionProbeResult>(key, new SteamConnectionProbeResult("ok", "reachable"));
		}
		catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
		{
			return new KeyValuePair<string, SteamConnectionProbeResult>(key, new SteamConnectionProbeResult("failed", "timeout"));
		}
		catch (HttpRequestException ex2)
		{
			return new KeyValuePair<string, SteamConnectionProbeResult>(key, new SteamConnectionProbeResult("failed", ex2.HttpRequestError switch
			{
				HttpRequestError.NameResolutionError => "dns", 
				HttpRequestError.SecureConnectionError => "tls", 
				_ => (ex2.InnerException is SocketException) ? "socket" : "network", 
			}));
		}
		catch (IOException)
		{
			return new KeyValuePair<string, SteamConnectionProbeResult>(key, new SteamConnectionProbeResult("failed", "interrupted"));
		}
	}

	public void Dispose()
	{
		client.Dispose();
	}
}
