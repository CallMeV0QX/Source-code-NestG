using System;

namespace UI_Desktop.Store;

internal sealed class DesktopDllFileItemResponse
{
	public string FileName { get; set; } = string.Empty;

	public string DownloadUrl { get; set; } = string.Empty;

	public long Size { get; set; }

	public string SizeText { get; set; } = string.Empty;

	public DateTime LastModifiedAt { get; set; }
}
