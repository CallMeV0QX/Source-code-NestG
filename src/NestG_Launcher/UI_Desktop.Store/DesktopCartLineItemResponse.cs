using System;
using System.Collections.Generic;

namespace UI_Desktop.Store;

internal sealed class DesktopCartLineItemResponse
{
	public Guid CartItemId { get; set; }

	public Guid GameId { get; set; }

	public Guid VersionId { get; set; }

	public string Slug { get; set; } = string.Empty;

	public string Name { get; set; } = string.Empty;

	public string VersionName { get; set; } = string.Empty;

	public string PosterImageUrl { get; set; } = string.Empty;

	public List<string> CategoryNames { get; set; } = new List<string>();

	public int Quantity { get; set; }

	public decimal UnitPrice { get; set; }

	public string UnitPriceText { get; set; } = string.Empty;

	public decimal LineTotal { get; set; }

	public string LineTotalText { get; set; } = string.Empty;
}
