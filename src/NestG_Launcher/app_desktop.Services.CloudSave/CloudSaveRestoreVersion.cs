using System;

namespace app_desktop.Services.CloudSave;

internal sealed record CloudSaveRestoreVersion(string SnapshotId, string OperationId, string MarkerFileId, string SnapshotFolderId, string ArchiveFileId, Guid NestGUserId, Guid LibraryId, uint SteamAccountId, int SteamAppId, int FileCount, long ArchiveSize, string ArchiveSha256, DateTimeOffset CompletedAtUtc);
