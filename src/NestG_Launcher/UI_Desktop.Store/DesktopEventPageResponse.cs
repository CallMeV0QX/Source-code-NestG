using System;
using System.Collections.Generic;

namespace UI_Desktop.Store;

internal sealed class DesktopEventPageResponse
{
	public DateTime ServerTimeUtc { get; set; }

	public DesktopChestEventResponse? RandomSteamGame { get; set; }

	public DesktopNightMarketResponse? NightMarket { get; set; }

	public List<DesktopEventPendingActionResponse> PendingActions { get; set; } = new List<DesktopEventPendingActionResponse>();

	public List<DesktopChestHistoryItemResponse> ChestHistory { get; set; } = new List<DesktopChestHistoryItemResponse>();
}
