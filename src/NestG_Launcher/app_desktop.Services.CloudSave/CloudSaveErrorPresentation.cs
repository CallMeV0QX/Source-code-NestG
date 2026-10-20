using System;
using System.Collections.Generic;

namespace app_desktop.Services.CloudSave;

internal sealed record CloudSaveErrorPresentation(string Title, string Summary, string Status, string Hint, IReadOnlyList<string> TechnicalDetails, string State = "error")
{
	internal static CloudSaveErrorPresentation FromException(Exception exception)
	{
		CloudSaveProgressStage cloudSaveProgressStage = ((exception is CloudSaveFailureException ex) ? ex.Stage : CloudSaveProgressStage.Connecting);
		Exception ex3 = ((exception is CloudSaveFailureException ex2) ? ex2.InnerException : exception);
		bool flag = exception is CloudSaveFailureException ex4 && ex4.IsRestore;
		if (ex3 is CloudSaveRestoreRollbackException ex5)
		{
			return new CloudSaveErrorPresentation("Rollback restore cần kiểm tra", "Restore bị gián đoạn và rollback tự động chưa hoàn tất.", "Giữ Steam đóng", "Giữ Steam đóng. Không mở game hoặc khôi phục lần nữa cho đến khi kiểm tra bản dự phòng và trạng thái Folder provider.", new _003C_003Ez__ReadOnlySingleElementList<string>("Bản dự phòng: " + ex5.SafetyBackupPath));
		}
		if (ex3 is CloudSaveRestorePreflightException ex6)
		{
			return new CloudSaveErrorPresentation("Provider hiện tại chưa hỗ trợ restore", ex6.Message, "Save chưa thay đổi", "Restore cần Folder provider cục bộ đã cấu hình hợp lệ. Launcher giữ nguyên provider và cấu hình CloudRedirect hiện tại.", Array.Empty<string>());
		}
		if (ex3 is CloudSaveRestoreTargetException ex7)
		{
			return new CloudSaveErrorPresentation("Chưa xác định được game hoặc Steam account", ex7.Message, "Save chưa thay đổi", "Kiểm tra bản cài game theo Steam App ID và đăng nhập đúng Steam account của bản lưu trước khi thử lại.", Array.Empty<string>());
		}
		if (ex3 is CloudSaveNoDataException)
		{
			return new CloudSaveErrorPresentation("Chưa có dữ liệu save", "Game chưa có dữ liệu save để sao lưu. Lần này chưa tạo hoặc tải bản sao lưu lên Google Drive.", "Chưa có save để sao lưu", "Nếu chưa ACTIVE, hãy bấm ACTIVE trước. Cài game → chơi và tạo save → lưu tiến độ trong game → bấm CLOUD SAVE → chọn Sao lưu lên Google Drive. Launcher sẽ thoát game, chờ đồng bộ và đóng Steam trước khi backup.", Array.Empty<string>(), "warning");
		}
		GoogleDriveOAuthException ex8 = ex3 as GoogleDriveOAuthException;
		bool flag2 = cloudSaveProgressStage == CloudSaveProgressStage.Connecting;
		bool flag3 = (uint)(cloudSaveProgressStage - 3) <= 1u;
		bool flag4 = flag3;
		bool flag5 = flag && cloudSaveProgressStage == CloudSaveProgressStage.Inspecting;
		flag3 = ((cloudSaveProgressStage == CloudSaveProgressStage.Downloading || cloudSaveProgressStage == CloudSaveProgressStage.Listing) ? true : false);
		bool flag6 = flag3 || flag5;
		bool flag7 = cloudSaveProgressStage == CloudSaveProgressStage.Restoring;
		flag3 = (uint)(cloudSaveProgressStage - 6) <= 2u;
		bool flag8 = flag3;
		string summary = ex8?.UserMessage ?? DesktopUserMessage.FromException(ex3, flag2 ? "Không hoàn tất được kết nối Google Drive. Hãy thử lại." : (flag ? "Không hoàn tất được bản khôi phục. Hãy kiểm tra rồi thử lại." : "Không hoàn tất được bản backup. Hãy kiểm tra rồi thử lại."));
		string text;
		if (flag2)
		{
			switch (ex8?.Reason)
			{
			case "client_secret_missing":
			case "invalid_client":
			case "unauthorized_client":
			case "not_configured":
			case "invalid_request":
				flag3 = true;
				break;
			default:
				flag3 = false;
				break;
			}
			text = (flag3 ? "Cần hoàn tất cấu hình kết nối Drive của launcher trước khi thử lại." : (flag ? "Bấm CLOUD SAVE để kết nối lại Google Drive. Save hiện tại chưa bị thay đổi." : "Bấm CLOUD SAVE để kết nối lại Google Drive. Lần này chưa tạo hoặc tải bản backup lên Drive."));
		}
		else
		{
			text = (flag6 ? "Bấm CLOUD SAVE → Khôi phục bản lưu để tải lại danh sách. Save hiện tại chưa bị thay đổi." : (flag7 ? "Giữ Steam đóng, kiểm tra bản dự phòng restore trên máy và thử lại sau khi CloudRedirect sẵn sàng." : (flag4 ? "Bấm CLOUD SAVE lần nữa để tiếp tục bản backup đang chờ và kiểm tra lại dữ liệu trên Drive." : ((!flag8) ? "Thoát game, chờ CloudRedirect đồng bộ save xong, rồi thoát Steam hoàn toàn và bấm CLOUD SAVE lại." : (flag ? "Hãy lưu tiến độ trong game và kiểm tra cửa sổ game/Steam trước khi thử khôi phục lại." : "Hãy lưu tiến độ trong game, kiểm tra cửa sổ game/Steam có đang chờ phản hồi và bấm CLOUD SAVE để thử lại.")))));
		}
		string hint = text;
		string title = (flag2 ? "Chưa kết nối được Google Drive" : (flag5 ? "Bản lưu không vượt qua kiểm tra" : (flag6 ? "Chưa tải được bản lưu" : (flag7 ? "Chưa khôi phục được bản lưu" : ((!flag8) ? "Chưa hoàn tất Cloud Save" : (flag ? "Chưa chuẩn bị được game để khôi phục" : "Chưa chuẩn bị được save để sao lưu"))))));
		string status = (flag2 ? "Kết nối chưa hoàn tất" : (flag5 ? "Checksum hoặc metadata không khớp" : (flag6 ? "Bản lưu chưa được kiểm tra" : (flag7 ? "Restore chưa hoàn tất" : (flag4 ? "Backup chưa được xác minh" : (flag8 ? "Game hoặc Steam chưa sẵn sàng" : "Chưa tạo được snapshot"))))));
		IReadOnlyList<string> technicalDetails;
		if (ex8 != null)
		{
			IReadOnlyList<string> readOnlyList = new _003C_003Ez__ReadOnlySingleElementList<string>(ex8.DiagnosticDetails);
			technicalDetails = readOnlyList;
		}
		else
		{
			IReadOnlyList<string> readOnlyList = Array.Empty<string>();
			technicalDetails = readOnlyList;
		}
		return new CloudSaveErrorPresentation(title, summary, status, hint, technicalDetails);
	}
}
