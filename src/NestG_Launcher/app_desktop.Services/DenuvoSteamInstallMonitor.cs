using System;
using System.Threading;
using System.Threading.Tasks;
using UI_Desktop.Store;

namespace app_desktop.Services;

internal sealed class DenuvoSteamInstallMonitor(StorefrontApiClient apiClient, Func<DesktopDenuvoActivePendingItemResponse, string?>? findInstallDirectory = null)
{
	public async Task<bool> SyncAsync(DesktopDenuvoActivePendingItemResponse item, string accessToken, CancellationToken cancellationToken)
	{
		string text = item.Status.Trim().ToLowerInvariant();
		bool flag;
		switch (text)
		{
		case "waiting_user_active_game":
		case "waiting_steam_download":
		case "waiting_user_load_key":
			flag = true;
			break;
		default:
			flag = false;
			break;
		}
		if (!flag)
		{
			return false;
		}
		string text2 = ((findInstallDirectory != null) ? findInstallDirectory(item) : DenuvoFixInstallService.FindSteamGameInstallDirectory(item.SteamAppId, item.DefaultInstallFolderName, item.GameName));
		if (string.IsNullOrWhiteSpace(text2))
		{
			return false;
		}
		if (text != "waiting_user_load_key")
		{
			await apiClient.PostDenuvoActiveSteamGameInstalledAsync(item.ActiveRequestId, new DesktopDenuvoActiveClientStateRequest
			{
				GameInstallPath = text2,
				ClientVersion = $"{DesktopAppContext.Settings.CurrentVersion}+{DesktopAppContext.Settings.CurrentBuildNumber}",
				Message = "Launcher đã phát hiện thư mục game Steam, đang chờ user yêu cầu nạp game."
			}, accessToken, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
		return true;
	}
}
