using System;

namespace app_desktop.Services;

internal sealed record DenuvoFixCooldownGuideState(Guid LibraryId, string GameName, string VersionName, string? GameExePath, DateTime CooldownUntilUtc);
