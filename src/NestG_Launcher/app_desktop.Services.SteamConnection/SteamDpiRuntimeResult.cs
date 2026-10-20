namespace app_desktop.Services.SteamConnection;

internal sealed record SteamDpiRuntimeResult(bool Running, string Code = "ok", bool DnsReady = false, bool DpiRunning = false, bool RecoveryPending = false, string? Stage = null, string? Cause = null);
