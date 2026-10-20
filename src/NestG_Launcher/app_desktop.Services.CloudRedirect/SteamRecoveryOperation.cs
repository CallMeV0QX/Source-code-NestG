using System;
using System.Collections.Generic;

namespace app_desktop.Services.CloudRedirect;

internal sealed class SteamRecoveryOperation
{
	public int Schema { get; set; } = 1;

	public string Id { get; set; } = Guid.NewGuid().ToString("N");

	public string SteamDirectory { get; set; } = "";

	public string TransactionPath { get; set; } = "";

	public CloudRedirectHealthMarker? HealthMarker { get; set; }

	public SteamRecoveryPhase Phase { get; set; }

	public SteamRecoveryOutcome Outcome { get; set; }

	public bool StartRequested { get; set; }

	public bool LaunchAcknowledgementRequired { get; set; }

	public bool LaunchAcknowledged { get; set; }

	public bool RequiresSteamRestart { get; set; }

	public bool StartupFailed { get; set; }

	public bool CloudInitializationFailed { get; set; }

	public bool RollbackSucceeded { get; set; }

	public bool RollbackFailed { get; set; }

	public bool LoadersDetached { get; set; }

	public bool RecoveryAttempted { get; set; }

	public string Message { get; set; } = "";

	public bool AllowBetterFallback { get; set; }

	public bool BetterOnly { get; set; }

	public bool FallbackAttempted { get; set; }

	public string FallbackTransactionPath { get; set; } = "";

	public int SteamAppId { get; set; }

	public Guid LibraryId { get; set; }

	public IReadOnlyList<SteamActivationFileStamp> ActivationFiles { get; set; } = Array.Empty<SteamActivationFileStamp>();

	public BetterSteamToolsLogMarker? BetterLogMarker { get; set; }
}
