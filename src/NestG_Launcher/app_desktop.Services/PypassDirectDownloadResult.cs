namespace app_desktop.Services;

internal sealed record PypassDirectDownloadResult(string FullPath, long SizeBytes, bool WasResumed);
