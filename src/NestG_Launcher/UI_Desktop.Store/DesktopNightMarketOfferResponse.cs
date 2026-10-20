using System;

namespace UI_Desktop.Store;

internal sealed class DesktopNightMarketOfferResponse
{
	public Guid OpeningId { get; set; }

	public Guid GameId { get; set; }

	public Guid VersionId { get; set; }

	public string GameName { get; set; } = string.Empty;

	public string VersionName { get; set; } = string.Empty;

	public int? SteamAppId { get; set; }

	public string ImageUrl { get; set; } = string.Empty;

	public decimal BasePrice { get; set; }

	public string BasePriceText { get; set; } = string.Empty;

	public decimal DiscountPercent { get; set; }

	public string DiscountPercentText { get; set; } = string.Empty;

	public decimal DiscountAmount { get; set; }

	public string DiscountAmountText { get; set; } = string.Empty;

	public decimal FinalPrice { get; set; }

	public string FinalPriceText { get; set; } = string.Empty;

	public DateTime ExpiresAtUtc { get; set; }

	public bool CanPurchase { get; set; }

	public bool IsPurchased { get; set; }

	public Guid? LibraryId { get; set; }
}
