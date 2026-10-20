using System;

namespace app_desktop.Services.CloudRedirect;

internal sealed record SteamProcessObservation(SteamProcessState State, CloudRedirectSteamSession? Session = null)
{
	public DateTime? RuntimeStartedAtUtc { get; init; }
}
