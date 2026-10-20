namespace app_desktop.Services.Warp;

internal enum WarpInstallOutcome
{
	Ready,
	NeedsCloudflareSetup,
	NeedsRestart,
	Cancelled,
	Failed,
	AlreadyInstalled
}
