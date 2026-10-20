using System.Collections.Generic;

namespace UI_Desktop.Store;

internal sealed class DesktopSalePortraitResponse
{
	public List<DesktopSalePortraitItemResponse> Items { get; set; } = new List<DesktopSalePortraitItemResponse>();
}
