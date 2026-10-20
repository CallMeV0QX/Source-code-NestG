using System;

namespace app_desktop.Services.CloudSave;

internal sealed class CloudSaveSnapshotFile
{
	public string RelativePath { get; set; } = string.Empty;

	public long Size { get; set; }

	public string Sha1 { get; set; } = string.Empty;

	public string Sha256 { get; set; } = string.Empty;

	public DateTimeOffset LastWriteTimeUtc { get; set; }
}
