namespace app_desktop.Services.SteamExclusions;

internal enum SteamExclusionToggleOutcome
{
	Enabled,
	Disabled,
	NoSteam,
	Declined,
	Busy,
	Failed,
	TimedOut,
	VerificationFailed,
	Blocked,
	Unavailable,
	NeedsElevation,
	OwnershipFailed,
	Unknown,
	InvalidPaths,
	NoManagedPaths
}
