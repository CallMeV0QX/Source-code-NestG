using System;

namespace UI_Desktop.Store;

internal sealed class DesktopNightMarketSlotResponse
{
	public Guid UserSlotId { get; set; }

	public int SlotNumber { get; set; }

	public string CardLevelCode { get; set; } = string.Empty;

	public string CardLevelName { get; set; } = string.Empty;

	public string StyleKey { get; set; } = string.Empty;

	public string State { get; set; } = string.Empty;

	public decimal? OpeningPrice { get; set; }

	public string OpeningPriceText { get; set; } = string.Empty;

	public string Currency { get; set; } = string.Empty;

	public bool CanOpen { get; set; }

	public string UnavailableReason { get; set; } = string.Empty;

	public Guid? OpeningId { get; set; }

	public string? OpeningOrderCode { get; set; }

	public bool RewardReady { get; set; }

	public DesktopNightMarketOfferResponse? Offer { get; set; }
}
