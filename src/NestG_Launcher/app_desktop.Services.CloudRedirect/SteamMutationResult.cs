namespace app_desktop.Services.CloudRedirect;

internal sealed record SteamMutationResult<T>(T? Value, SteamCloseResult Closure);
