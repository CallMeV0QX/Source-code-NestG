using System;

namespace app_desktop.Models;

internal sealed class DesktopAppUpdateInfo
{
	public bool IsUpdateAvailable { get; set; }

	public bool IsForceUpdate { get; set; }

	public Guid DesktopAppVersionId { get; set; }

	public string Platform { get; set; } = string.Empty;

	public string Version { get; set; } = string.Empty;

	public int BuildNumber { get; set; }

	public string ReleaseChannel { get; set; } = string.Empty;

	public string DownloadUrl { get; set; } = string.Empty;

	public string FileName { get; set; } = string.Empty;

	public long Size { get; set; }

	public string SizeText { get; set; } = string.Empty;

	public string? ChecksumSha256 { get; set; }

	public string? ReleaseNotes { get; set; }

	public string? MinSupportedVersion { get; set; }

	public DateTime? ReleasedAt { get; set; }
}
