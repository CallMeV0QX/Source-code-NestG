using System;
using System.Net;
using System.Text.Json;

namespace app_desktop.Services.CloudSave;

internal class GoogleDriveOAuthException : InvalidOperationException
{
	internal string Stage { get; }

	internal string ErrorCode { get; }

	internal string Reason { get; }

	internal int? HttpStatus { get; }

	internal string ReferenceId { get; } = Guid.NewGuid().ToString("N").Substring(0, 12);

	internal string UserMessage { get; }

	internal string DiagnosticDetails => $"Stage={Stage}; HTTP={HttpStatus?.ToString() ?? "none"}; Error={ErrorCode}; Reason={Reason}; Ref={ReferenceId}";

	private protected GoogleDriveOAuthException(string stage, string error, string reason, int? status, string message)
		: base(message)
	{
		Stage = stage;
		ErrorCode = error;
		Reason = reason;
		HttpStatus = status;
		UserMessage = message;
	}

	internal static GoogleDriveOAuthException Local(string stage, string reason)
	{
		return new GoogleDriveOAuthException(stage, "local_error", reason, null, reason switch
		{
			"not_configured" => "Cloud Save chưa được cấu hình Google Drive Client ID.", 
			"browser_unavailable" => "Không mở được trình duyệt. Hãy kiểm tra trình duyệt mặc định rồi bấm CLOUD SAVE lại.", 
			"timeout" => "Đã hết thời gian kết nối Google Drive. Hãy bấm CLOUD SAVE để bắt đầu lại.", 
			"network_error" => "Không kết nối được Google Drive. Hãy kiểm tra mạng rồi bấm CLOUD SAVE lại.", 
			"credential_store_failed" => "Không lưu được phiên Google Drive trên máy. Hãy kiểm tra quyền ghi và dung lượng ổ đĩa.", 
			"sign_in_required" => "Phiên Google Drive cần kết nối lại. Hãy bấm CLOUD SAVE.", 
			"access_denied" => "Google chưa cấp quyền Drive hoặc bạn đã hủy. Hãy bấm CLOUD SAVE để kết nối lại.", 
			"client_changed" => "Cấu hình Google Drive đã thay đổi. Hãy bấm CLOUD SAVE để kết nối lại.", 
			"invalid_callback" => "Phản hồi đăng nhập Google không hợp lệ. Hãy bấm CLOUD SAVE để bắt đầu lại.", 
			"invalid_response" => "Google trả về phiên đăng nhập không hợp lệ. Hãy bấm CLOUD SAVE để bắt đầu lại.", 
			_ => "Không hoàn tất được kết nối Google Drive. Hãy bấm CLOUD SAVE để thử lại.", 
		});
	}

	internal static GoogleDriveOAuthException FromResponse(HttpStatusCode status, string payload, bool interactive)
	{
		string text = "unknown_error";
		string text2 = "unknown_error";
		try
		{
			using JsonDocument jsonDocument = JsonDocument.Parse(payload);
			JsonElement rootElement = jsonDocument.RootElement;
			if (rootElement.ValueKind == JsonValueKind.Object && rootElement.TryGetProperty("error", out var value) && value.ValueKind == JsonValueKind.String)
			{
				text = value.GetString() switch
				{
					"invalid_request" => "invalid_request", 
					"invalid_client" => "invalid_client", 
					"invalid_grant" => "invalid_grant", 
					"unauthorized_client" => "unauthorized_client", 
					"access_denied" => "access_denied", 
					"invalid_scope" => "invalid_scope", 
					"temporarily_unavailable" => "temporarily_unavailable", 
					"server_error" => "server_error", 
					_ => "unknown_error", 
				};
				text2 = text;
				bool flag = ((text == "invalid_request" || text == "invalid_client") ? true : false);
				if (flag && rootElement.TryGetProperty("error_description", out var value2) && value2.ValueKind == JsonValueKind.String)
				{
					string @string = value2.GetString();
					if (@string != null && @string.Contains("client_secret", StringComparison.OrdinalIgnoreCase) && @string.Contains("missing", StringComparison.OrdinalIgnoreCase))
					{
						text2 = "client_secret_missing";
					}
				}
			}
		}
		catch (JsonException)
		{
		}
		string stage = (interactive ? "token_exchange" : "token_refresh");
		if (!interactive && status == HttpStatusCode.BadRequest && text == "invalid_grant")
		{
			return new GoogleDriveCredentialRevokedException(stage, text, text2, (int)status);
		}
		string text3;
		switch (text2)
		{
		case "client_secret_missing":
			text3 = "Google yêu cầu Client Secret cho OAuth client này. Cần kiểm tra cấu hình OAuth Desktop của launcher.";
			break;
		case "invalid_client":
		case "unauthorized_client":
			text3 = "Google không chấp nhận cấu hình ứng dụng. Cần kiểm tra OAuth Client của bản launcher.";
			break;
		case "invalid_grant":
			text3 = "Mã đăng nhập Google đã hết hiệu lực hoặc không khớp phiên. Hãy bấm CLOUD SAVE để đăng nhập lại.";
			break;
		case "invalid_scope":
			text3 = "Quyền Google Drive của ứng dụng chưa được cấu hình đúng.";
			break;
		case "access_denied":
			text3 = "Google từ chối cấp quyền Drive. Hãy kiểm tra tài khoản được phép sử dụng ứng dụng.";
			break;
		case "invalid_request":
			text3 = "Google từ chối yêu cầu đăng nhập. Cần kiểm tra cấu hình OAuth của launcher.";
			break;
		default:
			text3 = "Google chưa hoàn tất kết nối Drive. Hãy thử lại sau ít phút.";
			break;
		}
		string message = text3;
		if (status == HttpStatusCode.TooManyRequests)
		{
			message = "Google đang giới hạn yêu cầu. Hãy chờ một lát rồi bấm CLOUD SAVE lại.";
		}
		else if (status >= HttpStatusCode.InternalServerError)
		{
			message = "Dịch vụ Google đang gặp lỗi tạm thời. Hãy thử lại sau ít phút.";
		}
		return new GoogleDriveOAuthException(stage, text, text2, (int)status, message);
	}
}
