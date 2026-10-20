namespace app_desktop.Services;

internal sealed record DesktopPackageUpdateStageResult(string StageDirectory, string InstallDirectory, string RestartExecutablePath, string UpdaterExecutablePath, string LogFilePath);
