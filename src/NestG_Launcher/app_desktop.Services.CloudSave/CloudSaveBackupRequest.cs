using System;

namespace app_desktop.Services.CloudSave;

internal sealed record CloudSaveBackupRequest(Guid NestGUserId, Guid LibraryId, int SteamAppId, string GameName);
