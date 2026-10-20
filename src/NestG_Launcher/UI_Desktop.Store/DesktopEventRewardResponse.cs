using System;

namespace UI_Desktop.Store;

internal sealed class DesktopEventRewardResponse
{
	public Guid GameId { get; set; }

	public Guid VersionId { get; set; }

	public Guid LibraryId { get; set; }

	public string GameName { get; set; } = string.Empty;

	public string VersionName { get; set; } = string.Empty;

	public int? SteamAppId { get; set; }

	public string ImageUrl { get; set; } = string.Empty;
}
