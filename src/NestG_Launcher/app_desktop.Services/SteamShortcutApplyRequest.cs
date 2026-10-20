namespace app_desktop.Services;

internal sealed record SteamShortcutApplyRequest(string GameName, string GameInstallPath, string? MainExeRelativePath, string? SteamShortcutName, string? LaunchArguments, string LaunchMode);
