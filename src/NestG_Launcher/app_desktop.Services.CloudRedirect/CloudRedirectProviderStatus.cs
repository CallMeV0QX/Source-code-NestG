namespace app_desktop.Services.CloudRedirect;

internal sealed record CloudRedirectProviderStatus(string Provider, bool IsReady, string Message, string SyncPath, string TokenPath);
