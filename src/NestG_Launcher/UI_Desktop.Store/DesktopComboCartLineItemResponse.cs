using System;
using System.Collections.Generic;

namespace UI_Desktop.Store;

internal sealed class DesktopComboCartLineItemResponse
{
	public Guid ComboCartItemId { get; set; }

	public Guid ComboId { get; set; }

	public Guid RevisionId { get; set; }

	public string Slug { get; set; } = string.Empty;

	public string Name { get; set; } = string.Empty;

	public string PosterImageUrl { get; set; } = string.Empty;

	public List<string> CategoryNames { get; set; } = new List<string>();

	public int ComponentCount { get; set; }

	public int Quantity { get; set; } = 1;

	public decimal UnitPrice { get; set; }

	public string UnitPriceText { get; set; } = string.Empty;

	public decimal LineTotal { get; set; }

	public string LineTotalText { get; set; } = string.Empty;
}
