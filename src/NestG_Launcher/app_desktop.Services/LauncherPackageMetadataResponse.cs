namespace app_desktop.Services;

internal sealed class LauncherPackageMetadataResponse
{
	public string FileName { get; set; } = string.Empty;

	public long Size { get; set; }

	public string SizeText { get; set; } = string.Empty;

	public string DownloadUrl { get; set; } = string.Empty;
}
