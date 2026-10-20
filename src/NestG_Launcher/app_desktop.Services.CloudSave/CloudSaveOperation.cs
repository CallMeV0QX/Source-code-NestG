using System;

namespace app_desktop.Services.CloudSave;

internal sealed class CloudSaveOperation
{
	public string OperationId { get; set; } = string.Empty;

	public string SnapshotId { get; set; } = string.Empty;

	public Guid NestGUserId { get; set; }

	public Guid LibraryId { get; set; }

	public string GameName { get; set; } = string.Empty;

	public uint SteamAccountId { get; set; }

	public int SteamAppId { get; set; }

	public string ArchivePath { get; set; } = string.Empty;

	public long ArchiveSize { get; set; }

	public string ArchiveSha256 { get; set; } = string.Empty;

	public string ArchiveMd5 { get; set; } = string.Empty;

	public int FileCount { get; set; }

	public CloudSaveOperationStage Stage { get; set; }

	public string? DriveSnapshotFolderId { get; set; }

	public string? DriveConnectionId { get; set; }

	public string? DriveArchiveFileId { get; set; }

	public string? UploadSessionUri { get; set; }

	public long UploadedBytes { get; set; }

	public DateTimeOffset CreatedAtUtc { get; set; }

	public DateTimeOffset UpdatedAtUtc { get; set; }
}
