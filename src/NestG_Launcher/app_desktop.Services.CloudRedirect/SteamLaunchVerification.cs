using System;
using System.Threading.Tasks;

namespace app_desktop.Services.CloudRedirect;

internal sealed class SteamLaunchVerification
{
	private SteamRecoveryPhase? preparedPhase;

	internal void Reset()
	{
		preparedPhase = null;
	}

	internal async Task<bool> PrepareAsync(SteamRecoveryOperation operation, SteamRecoveryPhase phase, Func<Task<bool>> closeSteam)
	{
		if (preparedPhase == phase)
		{
			return true;
		}
		if (!(await closeSteam().ConfigureAwait(continueOnCapturedContext: false)))
		{
			return false;
		}
		CloudRedirectHealthMarker healthMarker = ((phase == SteamRecoveryPhase.VerifyActivation) ? CloudRedirectRuntimeService.CaptureHealthMarker(operation.SteamDirectory) : null);
		SteamRecoveryPhase steamRecoveryPhase = phase;
		bool flag = ((steamRecoveryPhase == SteamRecoveryPhase.VerifyActivation || steamRecoveryPhase == SteamRecoveryPhase.VerifyBetter) ? true : false);
		BetterSteamToolsLogMarker betterLogMarker = (flag ? BetterSteamToolsHealthReader.Capture(operation.SteamDirectory) : null);
		operation.HealthMarker = healthMarker;
		operation.BetterLogMarker = betterLogMarker;
		operation.StartRequested = false;
		operation.LaunchAcknowledged = false;
		operation.LaunchAcknowledgementRequired = true;
		preparedPhase = phase;
		return true;
	}
}
