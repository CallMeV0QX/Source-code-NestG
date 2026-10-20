using System;

namespace UI_Desktop.Store;

internal sealed class StorefrontSaleGameResponse
{
	public Guid GameId { get; set; }

	public Guid? VersionId { get; set; }

	public string Slug { get; set; } = string.Empty;

	public string Name { get; set; } = string.Empty;

	public string PosterImageUrl { get; set; } = string.Empty;

	public string Tag { get; set; } = "Steam";

	public string EditionLabel { get; set; } = "Standard";

	public string? VersionName { get; set; }

	public string CurrentPriceText { get; set; } = string.Empty;

	public string? ReferencePriceText { get; set; }

	public string? DiscountText { get; set; }

	public string? SaleName { get; set; }

	public DateTime? SaleEndsAt { get; set; }

	public string? SaleEndsAtText { get; set; }
}
