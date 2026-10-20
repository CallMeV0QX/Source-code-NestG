using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using app_desktop.Services;

namespace UI_Desktop.Store;

internal static class ActivationLogPopupSupport
{
	internal static async Task DownloadAsync(Control owner, GameActivationLogDownload download, string id, Guid libraryId, Func<string, Task> executeScript, CancellationToken token)
	{
		ActivationLogDownloadOutcome activationLogDownloadOutcome = await download.DownloadAsync(id, libraryId, () => ActivationLogFolderPicker.Choose(owner), token);
		if (!owner.IsDisposed && !token.IsCancellationRequested)
		{
			string text;
			switch (activationLogDownloadOutcome)
			{
			case ActivationLogDownloadOutcome.Busy:
				return;
			case ActivationLogDownloadOutcome.Exported:
				text = "Đã tải file log.";
				break;
			case ActivationLogDownloadOutcome.Cancelled:
				text = "";
				break;
			case ActivationLogDownloadOutcome.Unavailable:
				text = "Log của lần ACTIVE này không còn sẵn sàng. Hãy thử ACTIVE lại.";
				break;
			default:
				text = "Không thể tải file log. Hãy chọn thư mục có quyền ghi rồi thử lại.";
				break;
			}
			string value = text;
			await executeScript($"window.__nestgActivation?.logDownloadFinished({JsonSerializer.Serialize(id)}, {JsonSerializer.Serialize(value)});");
		}
	}
}
