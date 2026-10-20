using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using app_desktop.Services.CloudRedirect;

namespace app_desktop.Services;

internal sealed record SteamInstallResult(string? SteamDirectory, bool IsFound, IReadOnlyList<string> CopiedFiles, IReadOnlyList<string> Messages, bool IsSteamRestarted)
{
	public bool VerificationPending { get; init; }

	public bool BetterOnly { get; init; }

	public bool BetterRuntimeVerified { get; init; }

	public int AttemptCount { get; init; }

	public bool AttemptFailed { get; init; }

	public string FailureMessage { get; init; } = "";

	internal bool ActivationSucceeded
	{
		get
		{
			if (!AttemptFailed && !VerificationPending && IsFound && IsSteamRestarted && !WasRolledBack && !RollbackFailed && !AutoRecoveryAttempted && BetterRuntimeVerified)
			{
				if (!BetterOnly)
				{
					return CloudRedirectHealth?.IsReady ?? false;
				}
				return true;
			}
			return false;
		}
	}

	public CloudRedirectHealthMarker? HealthMarker { get; init; }

	public CloudRedirectHealthResult? CloudRedirectHealth { get; init; }

	public string? CloudRedirectBackupDirectory { get; init; }

	public string? CloudRedirectTransactionPath { get; init; }

	public string? CloudRedirectProvider { get; init; }

	public bool WasRolledBack { get; init; }

	public bool SteamStartupFailed { get; init; }

	public bool AutoRecoveryAttempted { get; init; }

	public string RecoveryOperationId { get; init; } = "";

	public bool SteamRecovered { get; init; }

	public bool LoadersDetached { get; init; }

	public string RecoveryMessage { get; init; } = "";

	public bool RollbackFailed { get; init; }

	public string? RollbackError { get; init; }

	public CloudRedirectReadinessResult? CloudRedirectReadiness { get; init; }

	public CloudRedirectReadinessAction CloudRedirectAction { get; init; }

	public bool RuntimeChanged { get; init; }

	public bool ConfigurationChanged { get; init; }

	public bool ActivationChanged { get; init; }

	public bool RecoveredInterruptedTransaction { get; init; }

	[CompilerGenerated]
	private bool PrintMembers(StringBuilder builder)
	{
		RuntimeHelpers.EnsureSufficientExecutionStack();
		builder.Append("SteamDirectory = ");
		builder.Append((object?)SteamDirectory);
		builder.Append(", IsFound = ");
		builder.Append(IsFound.ToString());
		builder.Append(", CopiedFiles = ");
		builder.Append(CopiedFiles);
		builder.Append(", Messages = ");
		builder.Append(Messages);
		builder.Append(", IsSteamRestarted = ");
		builder.Append(IsSteamRestarted.ToString());
		builder.Append(", VerificationPending = ");
		builder.Append(VerificationPending.ToString());
		builder.Append(", BetterOnly = ");
		builder.Append(BetterOnly.ToString());
		builder.Append(", BetterRuntimeVerified = ");
		builder.Append(BetterRuntimeVerified.ToString());
		builder.Append(", AttemptCount = ");
		builder.Append(AttemptCount.ToString());
		builder.Append(", AttemptFailed = ");
		builder.Append(AttemptFailed.ToString());
		builder.Append(", FailureMessage = ");
		builder.Append((object?)FailureMessage);
		builder.Append(", HealthMarker = ");
		builder.Append(HealthMarker);
		builder.Append(", CloudRedirectHealth = ");
		builder.Append(CloudRedirectHealth);
		builder.Append(", CloudRedirectBackupDirectory = ");
		builder.Append((object?)CloudRedirectBackupDirectory);
		builder.Append(", CloudRedirectTransactionPath = ");
		builder.Append((object?)CloudRedirectTransactionPath);
		builder.Append(", CloudRedirectProvider = ");
		builder.Append((object?)CloudRedirectProvider);
		builder.Append(", WasRolledBack = ");
		builder.Append(WasRolledBack.ToString());
		builder.Append(", SteamStartupFailed = ");
		builder.Append(SteamStartupFailed.ToString());
		builder.Append(", AutoRecoveryAttempted = ");
		builder.Append(AutoRecoveryAttempted.ToString());
		builder.Append(", RecoveryOperationId = ");
		builder.Append((object?)RecoveryOperationId);
		builder.Append(", SteamRecovered = ");
		builder.Append(SteamRecovered.ToString());
		builder.Append(", LoadersDetached = ");
		builder.Append(LoadersDetached.ToString());
		builder.Append(", RecoveryMessage = ");
		builder.Append((object?)RecoveryMessage);
		builder.Append(", RollbackFailed = ");
		builder.Append(RollbackFailed.ToString());
		builder.Append(", RollbackError = ");
		builder.Append((object?)RollbackError);
		builder.Append(", CloudRedirectReadiness = ");
		builder.Append(CloudRedirectReadiness);
		builder.Append(", CloudRedirectAction = ");
		builder.Append(CloudRedirectAction.ToString());
		builder.Append(", RuntimeChanged = ");
		builder.Append(RuntimeChanged.ToString());
		builder.Append(", ConfigurationChanged = ");
		builder.Append(ConfigurationChanged.ToString());
		builder.Append(", ActivationChanged = ");
		builder.Append(ActivationChanged.ToString());
		builder.Append(", RecoveredInterruptedTransaction = ");
		builder.Append(RecoveredInterruptedTransaction.ToString());
		return true;
	}
}
