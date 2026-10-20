using System.Collections.Generic;

namespace app_desktop.Services;

internal sealed record PypassFixInstallResult(string TargetDirectory, IReadOnlyList<string> WrittenFiles, IReadOnlyList<string> OverwrittenFiles, string BackupDirectory, string LogFilePath, string? PackageSha256, string? PackageVersion, string? GameExePath, IReadOnlyList<string> Messages);
