using System;
using System.IO;
using System.Net.Http;
using app_desktop.Services.CloudRedirect;

namespace app_desktop.Services;

internal static class GameActivationUserMessage
{
	internal static string FromException(Exception exception)
	{
		if (!(exception is SteamRecoveryPendingException))
		{
			if (!(exception is ActivationRequires64BitWindowsException))
			{
				if (!(exception is RuntimeBundleUnavailableException))
				{
					if (!(exception is UnsupportedSteamBuildException))
					{
						if (exception is CloudRedirectReadinessException ex)
						{
							CloudRedirectReadinessResult readiness = ex.Readiness;
							if ((object)readiness != null)
							{
								RuntimeDllOwnershipIssue ownershipIssue = readiness.OwnershipIssue;
								if ((object)ownershipIssue != null)
								{
									return ownershipIssue.Reason switch
									{
										RuntimeDllOwnershipFailure.UnknownHash => "Steam có thành phần hỗ trợ chưa được NestG xác minh. Launcher đã giữ nguyên file; cần kiểm tra trước khi ACTIVE.", 
										RuntimeDllOwnershipFailure.Unreadable => "Không đọc được thành phần hỗ trợ Steam để xác minh. Hãy kiểm tra file đang bị khóa hoặc quyền truy cập.", 
										_ => "Đường dẫn thành phần hỗ trợ Steam không hợp lệ. Launcher đã giữ nguyên dữ liệu; cần kiểm tra trước khi ACTIVE.", 
									};
								}
								if (readiness.State == CloudRedirectReadinessState.SteamMissing)
								{
									return "Không tìm thấy Steam. Hãy kiểm tra thư mục cài Steam trước khi ACTIVE.";
								}
							}
							return "Cấu hình hoặc thành phần Cloud Save cần được kiểm tra trước khi ACTIVE. Launcher đã giữ nguyên dữ liệu.";
						}
						if (!(exception is UnauthorizedAccessException))
						{
							if (!(exception is TimeoutException) && !(exception is HttpRequestException))
							{
								if (!(exception is InvalidDataException))
								{
									if (!(exception is IOException))
									{
										if (exception is InvalidOperationException)
										{
											if (exception.Message.Contains("Steam App ID", StringComparison.OrdinalIgnoreCase))
											{
												return "Dữ liệu game chưa có Steam App ID hợp lệ. Vui lòng liên hệ quản trị viên.";
											}
											if (exception.Message.Contains("đăng nhập", StringComparison.OrdinalIgnoreCase) || exception.Message.Contains("dang nhap", StringComparison.OrdinalIgnoreCase))
											{
												return "Phiên đăng nhập đã hết hạn. Hãy đăng nhập lại để Active game.";
											}
											if (exception.Message.Contains("provider", StringComparison.OrdinalIgnoreCase) || exception.Message.Contains("cấu hình CloudRedirect", StringComparison.OrdinalIgnoreCase) || exception.Message.Contains("cau hinh CloudRedirect", StringComparison.OrdinalIgnoreCase))
											{
												return "Cấu hình Cloud Save hiện hữu cần được kiểm tra trước khi Active.";
											}
											if (exception.Message.Contains("Steam vẫn đang chạy", StringComparison.OrdinalIgnoreCase) || exception.Message.Contains("Steam van dang chay", StringComparison.OrdinalIgnoreCase))
											{
												return "Steam chưa đóng hoàn toàn. Hãy đóng Steam thủ công rồi thử Active lại.";
											}
										}
										return "ACTIVE chưa hoàn tất. Hãy tải file log và gửi cho bộ phận hỗ trợ để kiểm tra.";
									}
									return "Launcher không thể chuẩn bị hoặc cập nhật file cần thiết. Hãy kiểm tra quyền ghi và thử lại.";
								}
								return "File kích hoạt hoặc thành phần Cloud Save không hợp lệ. Launcher đã dừng trước khi hoàn tất thay đổi.";
							}
							return DesktopUserMessage.FromException(exception, "Chưa thể kết nối máy chủ để Active game. Vui lòng thử lại.");
						}
						if (exception.Message.Contains("đăng nhập", StringComparison.OrdinalIgnoreCase))
						{
							return "Phiên đăng nhập đã hết hạn. Hãy đăng nhập lại để Active game.";
						}
						return "Launcher không có quyền truy cập file cần thiết. Hãy kiểm tra quyền truy cập thư mục Steam và thử lại.";
					}
					return "Phiên bản Steam này chưa được BetterSteamTools hiện tại hỗ trợ. Cần cập nhật bộ hỗ trợ Steam của NestG trước khi Active.";
				}
				return "Máy chủ chưa cung cấp bộ hỗ trợ Steam cần thiết. Vui lòng báo quản trị viên cập nhật server NestG.";
			}
			return "Bộ kích hoạt yêu cầu Windows 64-bit.";
		}
		return "Chưa xác minh được trạng thái Steam. Launcher giữ nguyên file đang có; hãy kiểm tra Steam và quyền truy cập rồi thử ACTIVE lại.";
	}
}
