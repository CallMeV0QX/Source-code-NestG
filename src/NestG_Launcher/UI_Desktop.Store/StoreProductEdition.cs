using System;

namespace UI_Desktop.Store;

internal sealed class StoreProductEdition
{
	public Guid? VersionId { get; set; }

	public decimal? CurrentPrice { get; set; }

	public string Name { get; set; }

	public string Note { get; set; }

	public string CurrentPriceText { get; set; }

	public string? ReferencePriceText { get; set; }

	public string? DiscountText { get; set; }

	public string? PriceText { get; set; }

	public bool IsActive { get; set; }
}
