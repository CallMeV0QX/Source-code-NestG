using System;

namespace UI_Desktop.Store;

internal sealed class DesktopLibraryPypassFixDeliveryResponse
{
	public Guid PackageId { get; set; }

	public string? CacheValidationToken { get; set; }

	public string DeliveryMode { get; set; } = "server_proxy";

	public string DownloadUrl { get; set; } = string.Empty;

	public string FileName { get; set; } = string.Empty;

	public long? SizeBytes { get; set; }

	public long MaxPackageSizeBytes { get; set; } = 11811160064L;

	public int PreferredConnectionCount { get; set; } = 4;

	public bool ResumeEnabled { get; set; } = true;

	public int CacheRetentionHours { get; set; } = 72;

	public string? PackageVersion { get; set; }

	public bool IsPasswordProtected { get; set; }

	public string? ZipPassword { get; set; }
}
