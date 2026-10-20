using System;
using System.Collections.Generic;

namespace app_desktop.Services.CloudRedirect;

internal sealed class CloudRedirectInstalledGame
{
	public Guid LibraryId { get; set; }

	public Guid GameId { get; set; }

	public int SteamAppId { get; set; }

	public string GameName { get; set; } = string.Empty;

	public DateTime InstalledAtUtc { get; set; }

	public List<string> InstalledFiles { get; set; } = new List<string>();
}
