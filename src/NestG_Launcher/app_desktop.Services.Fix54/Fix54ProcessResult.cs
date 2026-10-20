namespace app_desktop.Services.Fix54;

internal sealed record Fix54ProcessResult(int ExitCode, string StandardOutput, string StandardError, bool OutputTruncated);
