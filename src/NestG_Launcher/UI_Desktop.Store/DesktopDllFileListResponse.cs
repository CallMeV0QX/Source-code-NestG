using System.Collections.Generic;

namespace UI_Desktop.Store;

internal sealed class DesktopDllFileListResponse
{
	public int TotalFiles { get; set; }

	public List<DesktopDllFileItemResponse> Items { get; set; } = new List<DesktopDllFileItemResponse>();
}
