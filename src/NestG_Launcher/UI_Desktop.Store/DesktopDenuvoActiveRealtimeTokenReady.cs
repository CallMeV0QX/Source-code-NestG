using System;

namespace UI_Desktop.Store;

internal sealed class DesktopDenuvoActiveRealtimeTokenReady
{
	public Guid ActiveRequestId { get; set; }

	public Guid AssignmentId { get; set; }

	public Guid LibraryId { get; set; }

	public string GameName { get; set; } = string.Empty;

	public int ConfigRevisionNumber { get; set; }

	public string PackageDownloadUrl { get; set; } = string.Empty;

	public string? GameInstallPath { get; set; }

	public string ConfigRelativePath { get; set; } = "steam_settings/configs.user.ini";

	public DateTime ExpiresAt { get; set; }

	public string Message { get; set; } = string.Empty;
}
