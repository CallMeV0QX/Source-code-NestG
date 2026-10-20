using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace app_desktop.Services;

internal sealed class DesktopSupportApiClient
{
	private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
	{
		PropertyNameCaseInsensitive = true
	};

	private readonly HttpClient httpClient;

	public DesktopSupportApiClient(string apiBaseUrl)
	{
		httpClient = new HttpClient
		{
			BaseAddress = new Uri(NormalizeBaseUrl(apiBaseUrl)),
			Timeout = TimeSpan.FromSeconds(60.0)
		};
		httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("NestGLauncherDesktop/1.0");
	}

	public async Task<DesktopSupportConversationDetailResponse> GetCurrentAsync(string accessToken, CancellationToken cancellationToken = default(CancellationToken))
	{
		return Deserialize<DesktopSupportConversationDetailResponse>(await GetStringAsync("api/support/conversations/current", accessToken, cancellationToken), "API hỗ trợ không trả về cuộc trò chuyện hợp lệ.");
	}

	public async Task<DesktopSupportConversationResponse?> GetCurrentStateAsync(string accessToken, CancellationToken cancellationToken = default(CancellationToken))
	{
		using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, "api/support/conversations/current-state");
		request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
		using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken);
		if (response.StatusCode == HttpStatusCode.NoContent)
		{
			return null;
		}
		string rawContent = await response.Content.ReadAsStringAsync(cancellationToken);
		if (!response.IsSuccessStatusCode)
		{
			throw new InvalidOperationException(ParseErrorMessage(rawContent, response.ReasonPhrase));
		}
		return Deserialize<DesktopSupportConversationResponse>(rawContent, "API hỗ trợ không trả về trạng thái cuộc trò chuyện hợp lệ.");
	}

	public async Task<DesktopSupportMutationResponse> SendMessageAsync(Guid conversationId, string body, Guid clientMessageId, string accessToken, CancellationToken cancellationToken = default(CancellationToken))
	{
		using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, $"api/support/conversations/{conversationId:D}/messages")
		{
			Content = JsonContent.Create(new { body, clientMessageId }, null, JsonOptions)
		};
		request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
		using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken);
		string rawContent = await response.Content.ReadAsStringAsync(cancellationToken);
		if (!response.IsSuccessStatusCode)
		{
			throw new InvalidOperationException(ParseErrorMessage(rawContent, response.ReasonPhrase));
		}
		return Deserialize<DesktopSupportMutationResponse>(rawContent, "API hỗ trợ không trả về tin nhắn hợp lệ.");
	}

	public async Task<DesktopSupportMessageReadPayload> MarkReadAsync(Guid conversationId, Guid? messageId, string accessToken, CancellationToken cancellationToken = default(CancellationToken))
	{
		using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, $"api/support/conversations/{conversationId:D}/read")
		{
			Content = JsonContent.Create(new { messageId }, null, JsonOptions)
		};
		request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
		using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken);
		string rawContent = await response.Content.ReadAsStringAsync(cancellationToken);
		if (!response.IsSuccessStatusCode)
		{
			throw new InvalidOperationException(ParseErrorMessage(rawContent, response.ReasonPhrase));
		}
		return Deserialize<DesktopSupportMessageReadPayload>(rawContent, "API hỗ trợ không trả về trạng thái đã đọc hợp lệ.");
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

	private static T Deserialize<T>(string rawContent, string fallbackMessage)
	{
		T val = JsonSerializer.Deserialize<T>(rawContent, JsonOptions);
		if (val == null)
		{
			throw new InvalidOperationException(fallbackMessage);
		}
		return val;
	}

	private static string ParseErrorMessage(string rawContent, string? fallbackReason)
	{
		if (string.IsNullOrWhiteSpace(rawContent))
		{
			return LocalizeSupportError(fallbackReason);
		}
		try
		{
			using JsonDocument jsonDocument = JsonDocument.Parse(rawContent);
			if (jsonDocument.RootElement.TryGetProperty("message", out var value) && value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString()))
			{
				return LocalizeSupportError(value.GetString());
			}
		}
		catch (JsonException)
		{
		}
		return LocalizeSupportError(fallbackReason);
	}

	private static string LocalizeSupportError(string? message)
	{
		if (string.IsNullOrWhiteSpace(message))
		{
			return "Không thể kết nối tới dịch vụ hỗ trợ.";
		}
		return message.Trim() switch
		{
			"Bad Request" => "Yêu cầu chat hỗ trợ không hợp lệ.", 
			"Unauthorized" => "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.", 
			"Forbidden" => "Bạn không có quyền truy cập hội thoại hỗ trợ này.", 
			"Not Found" => "Không tìm thấy hội thoại hỗ trợ.", 
			"Conflict" => "Tin nhắn đã được ghi nhận trước đó.", 
			"Internal Server Error" => "Dịch vụ hỗ trợ đang gặp lỗi. Vui lòng thử lại sau.", 
			"Khong tim thay hoi thoai ho tro." => "Không tìm thấy hội thoại hỗ trợ.", 
			"Admin duoc assign khong hop le." => "Admin được gán không hợp lệ.", 
			"Trang thai ho tro khong hop le." => "Trạng thái hỗ trợ không hợp lệ.", 
			"Phien dang nhap khong hop le." => "Phiên đăng nhập không hợp lệ.", 
			"Noi dung tin nhan khong duoc de trong." => "Nội dung tin nhắn không được để trống.", 
			"Tin nhan da duoc ghi nhan truoc do." => "Tin nhắn đã được ghi nhận trước đó.", 
			"Khong the danh dau da doc." => "Không thể đánh dấu đã đọc.", 
			"Khong tim thay tin nhan ho tro." => "Không tìm thấy tin nhắn hỗ trợ.", 
			_ => message.Trim(), 
		};
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
