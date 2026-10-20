using System;

namespace app_desktop.Services;

internal sealed record PypassFixInstallProgress(string Stage, int? Percentage, long BytesReceived, long? TotalBytes, string Message, long SpeedBytesPerSecond = 0L, TimeSpan? EstimatedRemaining = null, int ConnectionCount = 0, bool IsResumed = false, bool IsIndeterminate = false);
