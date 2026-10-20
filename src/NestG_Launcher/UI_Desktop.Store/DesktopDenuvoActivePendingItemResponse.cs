using System;

namespace UI_Desktop.Store;

internal sealed class DesktopDenuvoActivePendingItemResponse
{
	public Guid ActiveRequestId { get; set; }

	public Guid AssignmentId { get; set; }

	public Guid LibraryId { get; set; }

	public Guid GameId { get; set; }

	public string GameName { get; set; } = string.Empty;

	public Guid VersionId { get; set; }

	public string VersionName { get; set; } = string.Empty;

	public string Status { get; set; } = string.Empty;

	public int? SteamAppId { get; set; }

	public string? DefaultInstallFolderName { get; set; }

	public string? GameInstallPath { get; set; }

	public string ConfigRelativePath { get; set; } = "steam_settings/configs.user.ini";

	public string? MainExeRelativePath { get; set; }

	public string? SteamShortcutName { get; set; }

	public string? LaunchArguments { get; set; }

	public string LaunchMode { get; set; } = "non_steam_shortcut";

	public int? TokenRevisionNumber { get; set; }

	public DateTime? TokenIssuedAt { get; set; }

	public DateTime? TokenExpiresAt { get; set; }

	public string PackageDownloadUrl { get; set; } = string.Empty;

	public string? LastError { get; set; }

	public string? CancelReason { get; set; }

	public DateTime? CancelledAt { get; set; }
}
