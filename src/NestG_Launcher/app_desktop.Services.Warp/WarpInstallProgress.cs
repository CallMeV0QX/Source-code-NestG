namespace app_desktop.Services.Warp;

internal sealed record WarpInstallProgress(string Message, int? Percent = null, WarpInstallPhase Phase = WarpInstallPhase.Downloading);
