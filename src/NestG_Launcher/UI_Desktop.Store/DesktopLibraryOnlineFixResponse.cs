using System;

namespace UI_Desktop.Store;

internal sealed class DesktopLibraryOnlineFixResponse
{
	public bool HasOnlineFix { get; set; }

	public bool RequiresFixAfterActivation { get; set; }

	public Guid? AssignmentId { get; set; }

	public Guid? OnlinePackageId { get; set; }

	public string Status { get; set; } = string.Empty;

	public string StatusText { get; set; } = string.Empty;

	public string? PackageDisplayName { get; set; }

	public string? PackageVersion { get; set; }

	public string? PackageSourceType { get; set; }

	public string? PackageSha256 { get; set; }

	public int? SteamAppId { get; set; }

	public string? DefaultInstallFolderName { get; set; }

	public string? MainExeRelativePath { get; set; }

	public string? SteamShortcutName { get; set; }

	public string? LaunchArguments { get; set; }

	public string LaunchMode { get; set; } = "none";

	public DateTime? LastDownloadAt { get; set; }

	public DateTime? LastInstallAt { get; set; }

	public string? GameInstallPath { get; set; }

	public string? PackageDownloadUrl { get; set; }

	public string? LastError { get; set; }
}
