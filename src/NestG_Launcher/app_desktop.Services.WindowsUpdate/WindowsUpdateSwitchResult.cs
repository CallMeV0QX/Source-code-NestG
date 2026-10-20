namespace app_desktop.Services.WindowsUpdate;

internal sealed record WindowsUpdateSwitchResult(WindowsUpdateBlockStatus Status, bool PreparationAttempted = false, bool PackageReady = true, bool CommandAttempted = false);
