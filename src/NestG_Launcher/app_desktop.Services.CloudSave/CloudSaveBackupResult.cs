using System;

namespace app_desktop.Services.CloudSave;

internal sealed record CloudSaveBackupResult(string SnapshotId, uint SteamAccountId, int SteamAppId, int FileCount, long ArchiveSize, string DriveArchiveFileId, DateTimeOffset CompletedAtUtc);
