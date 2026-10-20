namespace UI_Desktop.Store;

internal sealed class DesktopDownloadedFileResponse
{
	public string FileName { get; set; } = string.Empty;

	public string FullPath { get; set; } = string.Empty;

	public string ContentType { get; set; } = string.Empty;

	public string? Sha256 { get; set; }

	public int? ConfigRevisionNumber { get; set; }

	public bool IsZipPasswordProtected { get; set; }

	public string? ZipPassword { get; set; }
}
