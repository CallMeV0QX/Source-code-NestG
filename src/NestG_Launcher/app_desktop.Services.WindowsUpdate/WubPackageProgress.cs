namespace app_desktop.Services.WindowsUpdate;

internal sealed record WubPackageProgress(string Message, int? Percent = null, WubPackagePhase Phase = WubPackagePhase.Downloading);
