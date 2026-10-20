using System;
using System.Collections.Generic;

namespace app_desktop.Services;

internal sealed record DenuvoFixInstallResult(string TargetDirectory, IReadOnlyList<string> WrittenFiles, IReadOnlyList<string> OverwrittenFiles, string BackupDirectory, string LogFilePath, string? PackageSha256, string? PackageVersion, int? ConfigRevisionNumber, string ConfigRelativePath, string? ConfigFullPath, string? MainExeRelativePath, string? GameExePath, IReadOnlyList<string> Messages, Guid? ActiveRequestId = null);
