using System.Collections.Generic;

namespace UI_Desktop.Store;

internal sealed class DesktopRuntimeBundleResponse
{
	public string BundleId { get; set; } = string.Empty;

	public string Version { get; set; } = string.Empty;

	public string Channel { get; set; } = string.Empty;

	public string RuntimeMode { get; set; } = string.Empty;

	public string Architecture { get; set; } = string.Empty;

	public string MinimumDesktopVersion { get; set; } = string.Empty;

	public string OpenSteamToolCommit { get; set; } = string.Empty;

	public string CloudRedirectVersion { get; set; } = string.Empty;

	public List<DesktopRuntimeBundleFileResponse> Files { get; set; } = new List<DesktopRuntimeBundleFileResponse>();
}
