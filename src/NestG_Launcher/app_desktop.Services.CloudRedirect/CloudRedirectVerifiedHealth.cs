using System;

namespace app_desktop.Services.CloudRedirect;

internal sealed class CloudRedirectVerifiedHealth
{
	public string SteamDirectory { get; set; } = string.Empty;

	public int ProcessId { get; set; }

	public DateTime ProcessStartedAtUtc { get; set; }

	public DateTime VerifiedAtUtc { get; set; }

	public string LogPath { get; set; } = string.Empty;

	public long LogLength { get; set; }

	public DateTime LogLastWriteTimeUtc { get; set; }
}
