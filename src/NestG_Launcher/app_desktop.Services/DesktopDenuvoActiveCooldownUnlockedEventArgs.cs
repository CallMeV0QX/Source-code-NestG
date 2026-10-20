using System;

namespace app_desktop.Services;

internal sealed class DesktopDenuvoActiveCooldownUnlockedEventArgs(Guid libraryId, Guid gameId, string gameName, Guid versionId, string versionName, DateTime unlockedAt, string message) : EventArgs()
{
	public Guid LibraryId { get; } = libraryId;

	public Guid GameId { get; } = gameId;

	public string GameName { get; } = gameName;

	public Guid VersionId { get; } = versionId;

	public string VersionName { get; } = versionName;

	public DateTime UnlockedAt { get; } = unlockedAt;

	public string Message { get; } = message;

	public bool Handled { get; set; }
}
