namespace app_desktop.Services.CloudSave;

internal sealed class CloudSaveSnapshotMetadataFile
{
	public string Name { get; set; } = string.Empty;

	public long Size { get; set; }

	public string Sha256 { get; set; } = string.Empty;
}
