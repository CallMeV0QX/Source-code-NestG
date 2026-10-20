using System;

namespace app_desktop.Services.CloudRedirect;

internal sealed record CloudRedirectSteamSession(int ProcessId, DateTime StartedAtUtc);
