using System.Collections.Generic;

namespace UI_Desktop.Store;

internal sealed class DesktopLibraryListResponse
{
	public int TotalItems { get; set; }

	public List<DesktopLibraryListItemResponse> Items { get; set; } = new List<DesktopLibraryListItemResponse>();
}
