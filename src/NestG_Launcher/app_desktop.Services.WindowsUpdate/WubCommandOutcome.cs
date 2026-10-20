namespace app_desktop.Services.WindowsUpdate;

internal enum WubCommandOutcome
{
	Completed,
	Cancelled,
	Failed,
	TimedOut,
	Missing
}
