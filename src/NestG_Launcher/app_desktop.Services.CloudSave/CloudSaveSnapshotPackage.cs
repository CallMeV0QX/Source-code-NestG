using System;

namespace app_desktop.Services.CloudSave;

internal sealed record CloudSaveSnapshotPackage(string OperationId, string SnapshotId, uint SteamAccountId, int SteamAppId, string ArchivePath, long ArchiveSize, string ArchiveSha256, string ArchiveMd5, int FileCount, DateTimeOffset CreatedAtUtc);
