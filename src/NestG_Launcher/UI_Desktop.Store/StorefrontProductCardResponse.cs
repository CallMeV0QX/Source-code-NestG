using System;
using System.Collections.Generic;

namespace UI_Desktop.Store;

internal sealed class StorefrontProductCardResponse
{
	public Guid GameId { get; set; }

	public Guid? VersionId { get; set; }

	public string Slug { get; set; } = string.Empty;

	public string Name { get; set; } = string.Empty;

	public string? PosterImageUrl { get; set; }

	public string? SpotlightImageUrl { get; set; }

	public string Badge { get; set; } = string.Empty;

	public string Tag { get; set; } = string.Empty;

	public bool IsTrending { get; set; }

	public bool IsFree { get; set; }

	public List<string> CategoryNames { get; set; } = new List<string>();

	public decimal? CurrentPrice { get; set; }

	public decimal? ReferencePrice { get; set; }

	public string CurrentPriceText { get; set; } = string.Empty;

	public string? ReferencePriceText { get; set; }

	public string? DiscountText { get; set; }

	public bool IsOnSale { get; set; }

	public string? SaleName { get; set; }

	public DateTime? SaleEndsAt { get; set; }
}
