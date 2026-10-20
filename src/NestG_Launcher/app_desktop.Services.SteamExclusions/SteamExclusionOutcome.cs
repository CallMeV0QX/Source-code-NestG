namespace app_desktop.Services.SteamExclusions;

internal enum SteamExclusionOutcome
{
	NoSteam,
	AlreadyCovered,
	Deferred,
	Declined,
	Unavailable,
	Unknown,
	Blocked,
	Added,
	Failed,
	TimedOut
}
