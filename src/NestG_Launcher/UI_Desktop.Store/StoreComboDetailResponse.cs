using System;
using System.Collections.Generic;
using System.Text.Json;

namespace UI_Desktop.Store;

internal sealed class StoreComboDetailResponse
{
	public Guid ComboId { get; set; }

	public Guid RevisionId { get; set; }

	public int RevisionNumber { get; set; }

	public string Name { get; set; } = string.Empty;

	public string Slug { get; set; } = string.Empty;

	public decimal Price { get; set; }

	public string? ShortDescription { get; set; }

	public string? PosterUrl { get; set; }

	public string? CoverUrl { get; set; }

	public string? SeoTitle { get; set; }

	public string? SeoDescription { get; set; }

	public JsonElement Design { get; set; }

	public List<StoreComboComponentResponse> Components { get; set; } = new List<StoreComboComponentResponse>();

	public List<StoreComboMediaResponse> Media { get; set; } = new List<StoreComboMediaResponse>();

	public List<StoreComboCategoryResponse> Categories { get; set; } = new List<StoreComboCategoryResponse>();

	public bool CanPurchase { get; set; }

	public DateTime PublishedAt { get; set; }
}
