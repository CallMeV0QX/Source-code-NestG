using System;

namespace app_desktop.Services.CloudRedirect;

internal sealed record BetterSteamToolsHealth(bool IsReady, bool CoreFailed, bool CloudFailed)
{
	public DateTime? InitializationStartedAtUtc { get; init; }

	public bool CloudReady { get; init; }
}
