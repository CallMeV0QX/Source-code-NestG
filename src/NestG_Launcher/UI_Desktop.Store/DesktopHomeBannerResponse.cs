using System.Collections.Generic;

namespace UI_Desktop.Store;

internal sealed class DesktopHomeBannerResponse
{
	public List<DesktopHomeBannerSlotResponse> Items { get; set; } = new List<DesktopHomeBannerSlotResponse>();
}
