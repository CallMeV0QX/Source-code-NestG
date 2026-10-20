using System;

namespace app_desktop.Services.CloudRedirect;

internal sealed record BetterSteamToolsLogMarker(DateTime CapturedAtUtc)
{
	public long? Length { get; init; }

	public string? PrefixSha256 { get; init; }
}
