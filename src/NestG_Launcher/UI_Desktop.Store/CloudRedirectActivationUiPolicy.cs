using app_desktop.Services;
using app_desktop.Services.CloudRedirect;

namespace UI_Desktop.Store;

internal static class CloudRedirectActivationUiPolicy
{
	internal static CloudRedirectActivationUiState Resolve(SteamInstallResult result)
	{
		if (result.AttemptFailed)
		{
			return CloudRedirectActivationUiState.AttemptFailed;
		}
		if (result.RollbackFailed)
		{
			return CloudRedirectActivationUiState.RollbackFailed;
		}
		if (result.VerificationPending)
		{
			return CloudRedirectActivationUiState.VerificationPending;
		}
		if (result.ActivationSucceeded)
		{
			return CloudRedirectActivationUiState.Success;
		}
		if (result.AutoRecoveryAttempted)
		{
			if (!result.SteamRecovered)
			{
				return CloudRedirectActivationUiState.HealthFailed;
			}
			return CloudRedirectActivationUiState.RolledBack;
		}
		if (result.WasRolledBack)
		{
			return CloudRedirectActivationUiState.RolledBack;
		}
		if (result.BetterOnly)
		{
			return CloudRedirectActivationUiState.HealthFailed;
		}
		CloudRedirectHealthResult? cloudRedirectHealth = result.CloudRedirectHealth;
		if ((object)cloudRedirectHealth != null && cloudRedirectHealth.IsDefinitiveFailure)
		{
			return CloudRedirectActivationUiState.HealthFailed;
		}
		if (result.IsFound && result.IsSteamRestarted)
		{
			return CloudRedirectActivationUiState.NeedsCloudCheck;
		}
		if (!result.IsFound)
		{
			return CloudRedirectActivationUiState.SteamMissing;
		}
		return CloudRedirectActivationUiState.NeedsSteamRestart;
	}
}
