using System;
using System.Threading.Tasks;

namespace app_desktop.Services.CloudRedirect;

internal interface ISteamRecoveryActions
{
	string DeferredMessage => "Chưa xác minh được trạng thái Steam. Launcher giữ nguyên file; hãy kiểm tra Steam rồi thử lại.";

	Task<SteamInstallationDisposition> ReconcileInstallationAsync();

	Task<SteamStartupResult?> PrepareVerificationAsync();

	Task<SteamStartupResult> ObserveAsync(string steamDirectory, CloudRedirectHealthMarker? marker);

	Task<bool> CloseSteamAsync(string steamDirectory);

	Task<bool> RollbackAsync(string steamDirectory, string transactionPath);

	Task<bool> DetachLoadersAsync(string steamDirectory, string recoveryId);

	Task<bool> DisableCloudAsync(string steamDirectory, string recoveryId)
	{
		throw new InvalidOperationException("Better fallback is not supported by this recovery host.");
	}

	Task<bool> StartSteamAsync(string steamDirectory);

	Task<bool> PrepareLaunchAsync()
	{
		return Task.FromResult(result: true);
	}
}
