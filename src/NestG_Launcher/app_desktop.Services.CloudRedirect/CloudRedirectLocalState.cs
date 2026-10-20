using System;
using System.Collections.Generic;

namespace app_desktop.Services.CloudRedirect;

internal sealed class CloudRedirectLocalState
{
	public string BundleId { get; set; } = "opensteamtool-cloud";

	public string BundleVersion { get; set; } = string.Empty;

	public string OpenSteamToolCommit { get; set; } = string.Empty;

	public string CloudRedirectVersion { get; set; } = string.Empty;

	public string SteamDirectory { get; set; } = string.Empty;

	public string SyncPath { get; set; } = string.Empty;

	public DateTime LastInstalledAtUtc { get; set; }

	public List<CloudRedirectInstalledGame> Games { get; set; } = new List<CloudRedirectInstalledGame>();
}
