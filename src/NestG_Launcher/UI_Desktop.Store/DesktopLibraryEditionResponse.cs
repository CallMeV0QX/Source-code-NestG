using System;

namespace UI_Desktop.Store;

internal sealed class DesktopLibraryEditionResponse
{
	public Guid? VersionId { get; set; }

	public string Name { get; set; } = string.Empty;

	public string Note { get; set; } = string.Empty;

	public string CurrentPriceText { get; set; } = string.Empty;

	public string? ReferencePriceText { get; set; }

	public string? DiscountText { get; set; }

	public string? PriceText { get; set; }

	public bool IsOwned { get; set; }

	public bool IsSelected { get; set; }
}
