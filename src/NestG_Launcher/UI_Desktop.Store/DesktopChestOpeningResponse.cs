using System;

namespace UI_Desktop.Store;

internal sealed class DesktopChestOpeningResponse
{
	public Guid OpeningId { get; set; }

	public Guid TierId { get; set; }

	public string TierCode { get; set; } = string.Empty;

	public string TierName { get; set; } = string.Empty;

	public string Status { get; set; } = string.Empty;

	public string OrderCode { get; set; } = string.Empty;

	public decimal Price { get; set; }

	public string PriceText { get; set; } = string.Empty;

	public DateTime ReservationExpiresAtUtc { get; set; }

	public bool RewardReady { get; set; }

	public bool IsRevealed { get; set; }

	public bool IsConfirmed { get; set; }

	public DesktopEventRewardResponse? Reward { get; set; }
}
