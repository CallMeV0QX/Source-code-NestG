namespace app_desktop.Services.Fix54;

internal sealed record Fix54Result(Fix54Outcome Outcome, string Message, int ExaminedFiles, int ChangedFiles, string? BackupDirectory, bool RollbackCompleted)
{
	public bool RollbackAttempted { get; init; }
}
