namespace app_desktop.Services.CloudRedirect;

internal sealed record SteamCloseResult(bool IsClosed, SteamProcessState State, string Message);
