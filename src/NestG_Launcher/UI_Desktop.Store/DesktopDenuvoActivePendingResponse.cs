using System.Collections.Generic;

namespace UI_Desktop.Store;

internal sealed class DesktopDenuvoActivePendingResponse
{
	public List<DesktopDenuvoActivePendingItemResponse> Items { get; set; } = new List<DesktopDenuvoActivePendingItemResponse>();
}
