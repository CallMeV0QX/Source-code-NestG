namespace app_desktop.Services.WindowsUpdate;

internal sealed record WubCommandResult(WubCommandOutcome Outcome, int? ExitCode = null);
