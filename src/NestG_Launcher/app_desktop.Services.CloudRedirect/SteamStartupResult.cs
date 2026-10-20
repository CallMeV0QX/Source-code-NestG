namespace app_desktop.Services.CloudRedirect;

internal sealed record SteamStartupResult(bool IsRunning, bool StartupFailed, CloudRedirectHealthResult? Health, CloudRedirectSteamSession? Session, string Message)
{
	public bool ProcessStateUnknown { get; init; }

	public bool BetterRuntimeReady { get; init; }

	public bool CoreInitializationFailed { get; init; }
}
