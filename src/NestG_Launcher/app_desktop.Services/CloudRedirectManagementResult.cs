using System.Collections.Generic;

namespace app_desktop.Services;

internal sealed record CloudRedirectManagementResult(string Message, string BackupDirectory, string TransactionPath, bool IsSteamRestarted, IReadOnlyList<string> Messages);
