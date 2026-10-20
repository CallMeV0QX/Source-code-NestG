using System;

namespace app_desktop.Services.CloudSave;

internal sealed record CloudSaveRestoreResult(string SnapshotId, uint SteamAccountId, int SteamAppId, int FileCount, string SafetyBackupPath, DateTimeOffset AppliedAtUtc);
