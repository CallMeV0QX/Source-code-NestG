using System;

namespace app_desktop.Services.CloudSave;

internal sealed class CloudSaveCompletionMarker
{
	public int FormatVersion { get; set; }

	public string SnapshotId { get; set; } = string.Empty;

	public string OperationId { get; set; } = string.Empty;

	public Guid NestGUserId { get; set; }

	public Guid LibraryId { get; set; }

	public uint SteamAccountId { get; set; }

	public int SteamAppId { get; set; }

	public string ArchiveFileId { get; set; } = string.Empty;

	public long ArchiveSize { get; set; }

	public string ArchiveSha256 { get; set; } = string.Empty;

	public int FileCount { get; set; }

	public DateTimeOffset CompletedAtUtc { get; set; }
}
