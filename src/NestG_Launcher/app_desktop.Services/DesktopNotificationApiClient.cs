using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using app_desktop.Models;

namespace app_desktop.Services;

internal sealed class DesktopNotificationApiClient
{
	private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
	{
		PropertyNameCaseInsensitive = true
	};

	private readonly HttpClient httpClient;

	public DesktopNotificationApiClient(string apiBaseUrl)
	{
		httpClient = new HttpClient
		{
			BaseAddress = new Uri(NormalizeBaseUrl(apiBaseUrl)),
			Timeout = TimeSpan.FromSeconds(30.0)
		};
		httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("NestGLauncherDesktop/1.0");
	}

	public async Task<DesktopNotificationFeed> GetFeedAsync(string accessToken, string platform, string releaseChannel, string currentVersion, int currentBuildNumber, CancellationToken cancellationToken = default(CancellationToken))
	{
		string endpoint = "api/desktop/notifications?platform=" + Uri.EscapeDataString(platform) + "&releaseChannel=" + Uri.EscapeDataString(releaseChannel) + "&currentVersion=" + Uri.EscapeDataString(currentVersion) + $"&currentBuildNumber={Math.Max(0, currentBuildNumber)}";
		DesktopNotificationFeed? obj = JsonSerializer.Deserialize<DesktopNotificationFeed>(await GetStringAsync(endpoint, accessToken, cancellationToken), JsonOptions) ?? throw new InvalidOperationException("API thông báo không trả về dữ liệu hợp lệ.");
		DesktopNotificationFeed desktopNotificationFeed = obj;
		if (desktopNotificationFeed.Notifications == null)
		{
			desktopNotificationFeed.Notifications = new List<DesktopNotificationItem>();
		}
		return obj;
	}

	public Task MarkReadAsync(string accessToken, Guid notificationId, CancellationToken cancellationToken = default(CancellationToken))
	{
		return PostAsync($"api/desktop/notifications/{notificationId:D}/read", accessToken, null, cancellationToken);
	}

	public Task MarkAllReadAsync(string accessToken, IReadOnlyCollection<Guid> notificationIds, CancellationToken cancellationToken = default(CancellationToken))
	{
		return PostAsync("api/desktop/notifications/read-all", accessToken, new
		{
			notificationIds = notificationIds.ToArray()
		}, cancellationToken);
	}

	private async Task<string> GetStringAsync(string endpoint, string accessToken, CancellationToken cancellationToken)
	{
		using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, endpoint);
		request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
		using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken);
		string text = await response.Content.ReadAsStringAsync(cancellationToken);
		if (!response.IsSuccessStatusCode)
		{
			throw new InvalidOperationException(ParseErrorMessage(text, response.ReasonPhrase));
		}
		return text;
	}

	private async Task PostAsync(string endpoint, string accessToken, object? payload, CancellationToken cancellationToken)
	{
		using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, endpoint);
		request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
		request.Content = ((payload == null) ? JsonContent.Create(new { }) : JsonContent.Create(payload, null, JsonOptions));
		using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken);
		string rawContent = await response.Content.ReadAsStringAsync(cancellationToken);
		if (!response.IsSuccessStatusCode)
		{
			throw new InvalidOperationException(ParseErrorMessage(rawContent, response.ReasonPhrase));
		}
	}

	private static string ParseErrorMessage(string rawContent, string? fallbackReason)
	{
		if (string.IsNullOrWhiteSpace(rawContent))
		{
			return fallbackReason ?? "Không thể tải thông báo.";
		}
		try
		{
			using JsonDocument jsonDocument = JsonDocument.Parse(rawContent);
			if (jsonDocument.RootElement.TryGetProperty("message", out var value) && value.ValueKind == JsonValueKind.String)
			{
				return value.GetString() ?? fallbackReason ?? "Không thể tải thông báo.";
			}
			if (jsonDocument.RootElement.TryGetProperty("title", out var value2) && value2.ValueKind == JsonValueKind.String)
			{
				return value2.GetString() ?? fallbackReason ?? "Không thể tải thông báo.";
			}
		}
		catch (JsonException)
		{
		}
		return fallbackReason ?? "Không thể tải thông báo.";
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
