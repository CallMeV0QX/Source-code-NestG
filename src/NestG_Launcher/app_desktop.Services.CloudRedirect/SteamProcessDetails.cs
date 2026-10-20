using System;

namespace app_desktop.Services.CloudRedirect;

internal sealed record SteamProcessDetails(SteamProcessState State, string? ExecutablePath = null, DateTime StartedAtUtc = default(DateTime), string? FailureStage = null, int NativeError = 0);
