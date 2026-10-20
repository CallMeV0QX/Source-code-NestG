using System;

namespace app_desktop.Services.SteamExclusions;

internal sealed record SteamExclusionConsent(string[] ApprovedPaths, DateTimeOffset RetryAfterUtc)
{
	internal static SteamExclusionConsent Empty => new SteamExclusionConsent(Array.Empty<string>(), DateTimeOffset.MinValue);
}
