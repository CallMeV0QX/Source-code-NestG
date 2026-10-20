using System;

namespace app_desktop.Services;

internal sealed record DenuvoFixConfigUpdateResult(Guid LibraryId, Guid AssignmentId, string GameName, int ConfigRevisionNumber, string ConfigFullPath, string? BackupPath);
