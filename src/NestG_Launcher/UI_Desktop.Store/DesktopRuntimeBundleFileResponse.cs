namespace UI_Desktop.Store;

internal sealed class DesktopRuntimeBundleFileResponse
{
	public string RelativePath { get; set; } = string.Empty;

	public string InstallPath { get; set; } = string.Empty;

	public long Size { get; set; }

	public string Sha256 { get; set; } = string.Empty;

	public string DownloadUrl { get; set; } = string.Empty;
}
