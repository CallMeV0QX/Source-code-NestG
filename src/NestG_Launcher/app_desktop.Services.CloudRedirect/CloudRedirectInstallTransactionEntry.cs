namespace app_desktop.Services.CloudRedirect;

internal sealed class CloudRedirectInstallTransactionEntry
{
	public string OriginalPath { get; set; } = string.Empty;

	public bool Existed { get; set; }

	public string? BackupPath { get; set; }

	public string PreImageSha256 { get; set; } = string.Empty;

	public string PostImageSha256 { get; set; } = string.Empty;

	public bool PostImageDeleted { get; set; }
}
