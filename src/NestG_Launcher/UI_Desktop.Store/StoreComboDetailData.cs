using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace UI_Desktop.Store;

internal sealed class StoreComboDetailData
{
	public Guid ComboId { get; set; }

	public Guid RevisionId { get; set; }

	public int RevisionNumber { get; set; }

	public string Name { get; set; } = string.Empty;

	public string Slug { get; set; } = string.Empty;

	public decimal Price { get; set; }

	public string PriceText => StoreComboCard.FormatMoney(Price);

	public string? ShortDescription { get; set; }

	public string? PosterUrl { get; set; }

	public string? CoverUrl { get; set; }

	public JsonElement Design { get; set; }

	public IReadOnlyList<StoreComboComponent> Components { get; set; } = Array.Empty<StoreComboComponent>();

	public IReadOnlyList<StoreComboMedia> Media { get; set; } = Array.Empty<StoreComboMedia>();

	public IReadOnlyList<StoreComboCategory> Categories { get; set; } = Array.Empty<StoreComboCategory>();

	public bool CanPurchase { get; set; }

	public DateTime PublishedAt { get; set; }

	public static StoreComboDetailData FromApi(StoreComboDetailResponse response)
	{
		return new StoreComboDetailData
		{
			ComboId = response.ComboId,
			RevisionId = response.RevisionId,
			RevisionNumber = response.RevisionNumber,
			Name = (response.Name?.Trim() ?? "Combo Game"),
			Slug = (response.Slug?.Trim() ?? string.Empty),
			Price = Math.Max(0m, response.Price),
			ShortDescription = (string.IsNullOrWhiteSpace(response.ShortDescription) ? null : response.ShortDescription.Trim()),
			PosterUrl = response.PosterUrl,
			CoverUrl = response.CoverUrl,
			Design = response.Design.Clone(),
			Components = (from item in response.Components
				orderby item.DisplayOrder
				select new StoreComboComponent(item.GameId, item.VersionId, item.GameName?.Trim() ?? "Game", item.VersionName?.Trim(), item.PhotoUrl, item.DisplayOrder)).ToArray(),
			Media = (from item in response.Media
				orderby item.DisplayOrder
				select new StoreComboMedia(item.MediaId, item.MediaType?.Trim() ?? "gallery", item.Url?.Trim() ?? string.Empty, item.AltText?.Trim(), item.DisplayOrder)).ToArray(),
			Categories = response.Categories.Select((StoreComboCategoryResponse item) => new StoreComboCategory(item.CategoryId, item.Name?.Trim() ?? "Danh mục", item.Slug?.Trim())).ToArray(),
			CanPurchase = response.CanPurchase,
			PublishedAt = response.PublishedAt
		};
	}
}
