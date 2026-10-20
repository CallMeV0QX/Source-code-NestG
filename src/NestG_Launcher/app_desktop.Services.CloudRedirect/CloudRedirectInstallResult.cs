using System.Collections.Generic;

namespace app_desktop.Services.CloudRedirect;

internal sealed record CloudRedirectInstallResult(bool Succeeded, string Message, string? SteamDirectory, string? LogFilePath, IReadOnlyList<string> Messages);
