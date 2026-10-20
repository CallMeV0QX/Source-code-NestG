using System;

namespace UI_Desktop.Store;

internal sealed class DesktopLibraryDenuvoFixResponse
{
	public bool HasDenuvoFix { get; set; }

	public Guid? AssignmentId { get; set; }

	public Guid? FixItemId { get; set; }

	public string Status { get; set; } = string.Empty;

	public string StatusText { get; set; } = string.Empty;

	public string? PackageDisplayName { get; set; }

	public string? PackageVersion { get; set; }

	public string? PackageSourceType { get; set; }

	public int? SteamAppId { get; set; }

	public string? DefaultInstallFolderName { get; set; }

	public string ConfigRelativePath { get; set; } = "steam_settings/configs.user.ini";

	public string? MainExeRelativePath { get; set; }

	public string? SteamShortcutName { get; set; }

	public string? LaunchArguments { get; set; }

	public string LaunchMode { get; set; } = "non_steam_shortcut";

	public int? ActiveConfigRevisionNumber { get; set; }

	public int? LastAppliedRevisionNumber { get; set; }

	public DateTime? LastDownloadAt { get; set; }

	public DateTime? LastInstallAt { get; set; }

	public DateTime? LastUpdateCheckAt { get; set; }

	public string? GameInstallPath { get; set; }

	public string? LastError { get; set; }

	public string? PackageDownloadUrl { get; set; }

	public string? ConfigDownloadUrl { get; set; }
}
