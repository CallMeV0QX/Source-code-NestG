using System;

namespace app_desktop.Services;

internal sealed record DesktopFileDownloadProgress(long BytesReceived, long? TotalBytes, int? Percentage, long SpeedBytesPerSecond, TimeSpan? EstimatedRemaining);
