using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using app_desktop.Services.CloudRedirect;

namespace app_desktop.Services;

internal static class GameActivationAttempts
{
	internal static async Task<SteamInstallResult> RunAsync(string steamDirectory, Func<bool, CancellationToken, IProgress<GameActivationProgress>?, Task<SteamInstallResult>> attempt, CancellationToken cancellationToken, IProgress<GameActivationProgress>? progress = null, GameActivationLog? activationLog = null)
	{
		List<string> messages = new List<string>();
		for (int number = 1; number <= 2; number++)
		{
			cancellationToken.ThrowIfCancellationRequested();
			bool betterOnly = number == 2;
			string mode = (betterOnly ? "Better + Lua + Manifest" : "Better + Lua + Manifest + CloudRedirect");
			GameActivationProgressStage stage = GameActivationProgressStage.Checking;
			InlineProgress<GameActivationProgress> arg = new InlineProgress<GameActivationProgress>(delegate(GameActivationProgress update)
			{
				if (stage != update.Stage)
				{
					DesktopDiagnosticLog.Write("GameActivation", "Stage", $"Attempt={number}; Mode={mode}; Stage={update.Stage}");
					activationLog?.Add($"Attempt={number}; Mode={mode}; Stage={update.Stage}");
				}
				stage = update.Stage;
				progress?.Report(update);
			});
			DesktopDiagnosticLog.Write("GameActivation", "AttemptStarted", $"Attempt={number}; Mode={mode}");
			activationLog?.Add($"Attempt={number}; Mode={mode}; Stage=Checking; Started");
			SteamInstallResult steamInstallResult;
			try
			{
				steamInstallResult = await attempt(betterOnly, cancellationToken, arg).ConfigureAwait(continueOnCapturedContext: false);
				cancellationToken.ThrowIfCancellationRequested();
				messages.AddRange(steamInstallResult.Messages);
			}
			catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
			{
				activationLog?.Exception(ex, $"Attempt={number}; Mode={mode}; Stage={stage}");
				string text = GameActivationUserMessage.FromException(ex);
				messages.Add($"Lượt {number} ({mode}), {stage}: {text} [{ex.GetType().Name}]");
				DesktopDiagnosticLog.Write("GameActivation", "AttemptException", $"Attempt={number}; Mode={mode}; Stage={stage}; ErrorType={ex.GetType().Name}");
				steamInstallResult = new SteamInstallResult(steamDirectory, IsFound: true, Array.Empty<string>(), Array.Empty<string>(), IsSteamRestarted: false)
				{
					FailureMessage = text
				};
			}
			cancellationToken.ThrowIfCancellationRequested();
			bool activationSucceeded = steamInstallResult.ActivationSucceeded;
			activationLog?.Add($"Attempt={number}; Mode={mode}; Stage={stage}; Success={activationSucceeded}; SteamRunning={steamInstallResult.IsSteamRestarted}; BetterVerified={steamInstallResult.BetterRuntimeVerified}; CloudReady={steamInstallResult.CloudRedirectHealth?.IsReady}");
			foreach (string message in steamInstallResult.Messages)
			{
				activationLog?.Add(message);
			}
			if (!activationSucceeded)
			{
				activationLog?.Add("Failure=" + steamInstallResult.FailureMessage);
			}
			CloudRedirectHealthResult cloudRedirectHealth = steamInstallResult.CloudRedirectHealth;
			if ((object)cloudRedirectHealth != null)
			{
				activationLog?.Add($"CloudHealth: Ready={cloudRedirectHealth.IsReady}; DefinitiveFailure={cloudRedirectHealth.IsDefinitiveFailure}; Message={cloudRedirectHealth.Message}");
			}
			messages.Add($"Lượt {number} ({mode}): {(activationSucceeded ? "thành công" : "chưa hoàn tất")}.");
			DesktopDiagnosticLog.Write("GameActivation", "AttemptFinished", $"Attempt={number}; Mode={mode}; Success={activationSucceeded}");
			if (activationSucceeded || number == 2)
			{
				if (activationSucceeded)
				{
					progress?.Report(new GameActivationProgress(GameActivationProgressStage.Completed, "Active thành công.", null));
				}
				return steamInstallResult with
				{
					BetterOnly = betterOnly,
					AttemptCount = number,
					AttemptFailed = !activationSucceeded,
					VerificationPending = false,
					Messages = messages.ToArray(),
					FailureMessage = (activationSucceeded ? "" : (string.IsNullOrWhiteSpace(steamInstallResult.FailureMessage) ? "Không thể xác nhận Steam và bộ kích hoạt đã sẵn sàng sau hai lượt ACTIVE. Hãy kiểm tra Steam rồi thử lại." : steamInstallResult.FailureMessage))
				};
			}
			progress?.Report(new GameActivationProgress(GameActivationProgressStage.Retrying, "Đang thử lại không dùng CloudRedirect…", 10));
		}
		throw new InvalidOperationException("Invalid activation attempt count.");
	}
}
