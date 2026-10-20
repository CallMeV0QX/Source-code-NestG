namespace app_desktop.Services.CloudRedirect;

internal sealed record CloudRedirectRuntimeStatus(bool SteamFound, bool RuntimeInstalled, bool IsEnabled, string Provider, string SteamDirectory, string Message, bool CanRollback, bool ProviderReady = false);
