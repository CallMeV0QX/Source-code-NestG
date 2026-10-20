using System;

namespace app_desktop.Services;

internal sealed record PypassDirectDownloadProgress(long BytesReceived, long? TotalBytes, int? Percentage, long SpeedBytesPerSecond, TimeSpan? EstimatedRemaining, int ConnectionCount, bool IsResumed, string Message);
