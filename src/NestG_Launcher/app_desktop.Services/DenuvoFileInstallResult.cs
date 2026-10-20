using System.Collections.Generic;

namespace app_desktop.Services;

internal sealed record DenuvoFileInstallResult(IReadOnlyList<string> WrittenFiles, IReadOnlyList<string> OverwrittenFiles, string BackupDirectory, string? ConfigFullPath);
