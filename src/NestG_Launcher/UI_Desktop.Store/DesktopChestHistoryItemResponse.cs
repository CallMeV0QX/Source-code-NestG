using System;

namespace UI_Desktop.Store;

internal sealed class DesktopChestHistoryItemResponse
{
	public Guid OpeningId { get; set; }

	public string TierCode { get; set; } = string.Empty;

	public string TierName { get; set; } = string.Empty;

	public string OrderCode { get; set; } = string.Empty;

	public decimal Price { get; set; }

	public string PriceText { get; set; } = string.Empty;

	public string Status { get; set; } = string.Empty;

	public string StatusText { get; set; } = string.Empty;

	public DateTime OpenedAtUtc { get; set; }

	public bool CanReveal { get; set; }

	public DesktopEventRewardResponse Reward { get; set; } = new DesktopEventRewardResponse();
}
