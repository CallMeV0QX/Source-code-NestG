using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using app_desktop.Models;

namespace app_desktop.Services;

internal sealed class DesktopAuthApiClient
{
	private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
	{
		PropertyNameCaseInsensitive = true
	};

	private readonly HttpClient httpClient;

	public DesktopAuthApiClient(string apiBaseUrl)
	{
		httpClient = new HttpClient
		{
			BaseAddress = new Uri(NormalizeBaseUrl(apiBaseUrl)),
			Timeout = TimeSpan.FromSeconds(30.0)
		};
		httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("NestGLauncherDesktop/1.0");
	}

	public Task<AuthActionResult> LoginAsync(string email, string password, CancellationToken cancellationToken = default(CancellationToken))
	{
		return PostAsync("api/desktop-auth/login", new { email, password }, cancellationToken);
	}

	public Task<AuthActionResult> RegisterAsync(string displayName, string? phone, string email, string password, CancellationToken cancellationToken = default(CancellationToken))
	{
		return PostAsync("api/desktop-auth/register", new { displayName, phone, email, password }, cancellationToken);
	}

	public Task<AuthActionResult> VerifyEmailOtpAsync(string email, string otp, CancellationToken cancellationToken = default(CancellationToken))
	{
		return PostAsync("api/desktop-auth/verify-email-otp", new { email, otp }, cancellationToken);
	}

	public Task<AuthActionResult> ResendEmailOtpAsync(string email, CancellationToken cancellationToken = default(CancellationToken))
	{
		return PostAsync("api/desktop-auth/resend-email-otp", new { email }, cancellationToken);
	}

	public Task<AuthActionResult> ForgotPasswordAsync(string email, CancellationToken cancellationToken = default(CancellationToken))
	{
		return PostAsync("api/desktop-auth/forgot-password", new { email }, cancellationToken);
	}

	public Task<AuthActionResult> ResetPasswordAsync(string email, string otp, string password, CancellationToken cancellationToken = default(CancellationToken))
	{
		return PostAsync("api/desktop-auth/reset-password", new { email, otp, password }, cancellationToken);
	}

	public Task<AuthActionResult> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default(CancellationToken))
	{
		return PostAsync("api/desktop-auth/refresh", new { refreshToken }, cancellationToken);
	}

	private async Task<AuthActionResult> PostAsync(string endpoint, object payload, CancellationToken cancellationToken)
	{
		using HttpResponseMessage response = await httpClient.PostAsJsonAsync(endpoint, payload, JsonOptions, cancellationToken);
		string text = await response.Content.ReadAsStringAsync(cancellationToken);
		if (!response.IsSuccessStatusCode)
		{
			var (errorCode, message) = ParseErrorResponse(text, response.ReasonPhrase);
			return AuthActionResult.FromError(errorCode, message);
		}
		if (string.IsNullOrWhiteSpace(text))
		{
			return AuthActionResult.FromError(null, "Máy chủ không trả về dữ liệu hợp lệ.");
		}
		try
		{
			using JsonDocument jsonDocument = JsonDocument.Parse(text);
			JsonElement rootElement = jsonDocument.RootElement;
			if (rootElement.ValueKind != JsonValueKind.Object)
			{
				return AuthActionResult.FromError(null, "Phản hồi từ máy chủ không đúng định dạng.");
			}
			if (rootElement.TryGetProperty("accessToken", out var value))
			{
				AuthSession authSession = JsonSerializer.Deserialize<AuthSession>(text, JsonOptions);
				return (authSession == null) ? AuthActionResult.FromError(null, "Không đọc được thông tin phiên đăng nhập.") : AuthActionResult.FromSession(authSession);
			}
			if (rootElement.TryGetProperty("purpose", out value))
			{
				OtpChallengeInfo otpChallengeInfo = JsonSerializer.Deserialize<OtpChallengeInfo>(text, JsonOptions);
				return (otpChallengeInfo == null) ? AuthActionResult.FromError(null, "Không đọc được thử thách OTP.") : AuthActionResult.FromChallenge(otpChallengeInfo);
			}
			return AuthActionResult.FromError(null, "Phản hồi thành công nhưng không có dữ liệu cần thiết.");
		}
		catch (JsonException)
		{
			return AuthActionResult.FromError(null, "Không đọc được dữ liệu trả về từ máy chủ.");
		}
	}

	private static (string? ErrorCode, string ErrorMessage) ParseErrorResponse(string rawContent, string? fallbackReason)
	{
		if (string.IsNullOrWhiteSpace(rawContent))
		{
			return (ErrorCode: null, ErrorMessage: fallbackReason ?? "Có lỗi xảy ra khi gọi API.");
		}
		try
		{
			using JsonDocument jsonDocument = JsonDocument.Parse(rawContent);
			JsonElement rootElement = jsonDocument.RootElement;
			string item = TryReadString(rootElement, "message") ?? TryReadString(rootElement, "title") ?? TryReadValidationMessage(rootElement) ?? fallbackReason ?? "Có lỗi xảy ra khi gọi API.";
			return (ErrorCode: TryReadString(rootElement, "code"), ErrorMessage: item);
		}
		catch (JsonException)
		{
			return (ErrorCode: null, ErrorMessage: fallbackReason ?? rawContent);
		}
	}

	private static string? TryReadValidationMessage(JsonElement root)
	{
		if (!root.TryGetProperty("errors", out var value) || value.ValueKind != JsonValueKind.Object)
		{
			return null;
		}
		foreach (JsonProperty item in value.EnumerateObject())
		{
			if (item.Value.ValueKind != JsonValueKind.Array)
			{
				continue;
			}
			foreach (JsonElement item2 in item.Value.EnumerateArray())
			{
				if (item2.ValueKind == JsonValueKind.String)
				{
					return item2.GetString();
				}
			}
		}
		return null;
	}

	private static string? TryReadString(JsonElement root, string propertyName)
	{
		if (!root.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.String)
		{
			return null;
		}
		return value.GetString();
	}

	private static string NormalizeBaseUrl(string apiBaseUrl)
	{
		if (string.IsNullOrWhiteSpace(apiBaseUrl))
		{
			return "https://localhost:7023/";
		}
		if (!apiBaseUrl.EndsWith("/", StringComparison.Ordinal))
		{
			return apiBaseUrl + "/";
		}
		return apiBaseUrl;
	}
}
