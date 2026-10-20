using System;

namespace app_desktop.Services;

internal sealed class DesktopDenuvoActiveRealtimeCooldownUnlocked
{
	public Guid LibraryId { get; set; }

	public Guid GameId { get; set; }

	public string GameName { get; set; } = string.Empty;

	public Guid VersionId { get; set; }

	public string VersionName { get; set; } = string.Empty;

	public DateTime UnlockedAt { get; set; }

	public string Message { get; set; } = string.Empty;
}
