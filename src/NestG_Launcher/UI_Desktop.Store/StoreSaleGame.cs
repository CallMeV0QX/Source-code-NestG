using System;
using System.Collections.Generic;
using System.Drawing;

namespace UI_Desktop.Store;

internal sealed class StoreSaleGame
{
	public Guid GameId { get; set; }

	public Guid? VersionId { get; set; }

	public string Slug { get; set; }

	public string Title { get; set; }

	public string Tag { get; set; }

	public string EditionLabel { get; set; }

	public string? VersionName { get; set; }

	public string? PosterImageUrl { get; set; }

	public string? PortraitImageUrl { get; set; }

	public string CurrentPriceText { get; set; }

	public string? ReferencePriceText { get; set; }

	public string? DiscountText { get; set; }

	public string? SaleName { get; set; }

	public DateTime? SaleEndsAt { get; set; }

	public string? SaleEndsAtText { get; set; }

	public Color PrimaryColor { get; set; }

	public Color SecondaryColor { get; set; }

	public static StoreSaleGame FromApi(StorefrontSaleGameResponse item, IReadOnlyDictionary<Guid, string>? salePortraitImages = null)
	{
		string text = (string.IsNullOrWhiteSpace(item.Name) ? "Steam Product" : item.Name.Trim());
		StoreGamePalette storeGamePalette = StoreGamePalette.FromSeed($"{item.GameId:N}:{text}:{item.EditionLabel}");
		return new StoreSaleGame
		{
			GameId = item.GameId,
			VersionId = item.VersionId,
			Slug = (string.IsNullOrWhiteSpace(item.Slug) ? StoreCategorySection.Slugify(text) : item.Slug.Trim()),
			Title = text,
			Tag = (string.IsNullOrWhiteSpace(item.Tag) ? "Steam" : item.Tag.Trim()),
			EditionLabel = (string.IsNullOrWhiteSpace(item.EditionLabel) ? "Standard" : item.EditionLabel.Trim()),
			VersionName = (string.IsNullOrWhiteSpace(item.VersionName) ? null : item.VersionName.Trim()),
			PosterImageUrl = item.PosterImageUrl,
			PortraitImageUrl = ResolvePortraitImageUrl(item.GameId, salePortraitImages),
			CurrentPriceText = (string.IsNullOrWhiteSpace(item.CurrentPriceText) ? "Liên hệ" : item.CurrentPriceText.Trim()),
			ReferencePriceText = (string.IsNullOrWhiteSpace(item.ReferencePriceText) ? null : item.ReferencePriceText.Trim()),
			DiscountText = (string.IsNullOrWhiteSpace(item.DiscountText) ? null : item.DiscountText.Trim()),
			SaleName = (string.IsNullOrWhiteSpace(item.SaleName) ? null : item.SaleName.Trim()),
			SaleEndsAt = item.SaleEndsAt,
			SaleEndsAtText = (string.IsNullOrWhiteSpace(item.SaleEndsAtText) ? null : item.SaleEndsAtText.Trim()),
			PrimaryColor = storeGamePalette.PrimaryColor,
			SecondaryColor = storeGamePalette.SecondaryColor
		};
	}

	public static StoreSaleGame FromGame(StoreGame game, IReadOnlyDictionary<Guid, string>? salePortraitImages = null)
	{
		return new StoreSaleGame
		{
			GameId = game.GameId,
			VersionId = null,
			Slug = StoreCategorySection.Slugify(game.Title),
			Title = game.Title,
			Tag = game.Category,
			EditionLabel = (game.IsOnSale ? "Sale" : "Store"),
			VersionName = null,
			PosterImageUrl = game.PosterImageUrl,
			PortraitImageUrl = ResolvePortraitImageUrl(game.GameId, salePortraitImages),
			CurrentPriceText = game.DisplaySalePriceText,
			ReferencePriceText = ((game.OriginalPrice > game.SalePrice) ? game.OriginalPriceText : null),
			DiscountText = game.DiscountText,
			SaleName = game.SaleName,
			SaleEndsAt = game.SaleEndsAt,
			SaleEndsAtText = null,
			PrimaryColor = game.PrimaryColor,
			SecondaryColor = game.SecondaryColor
		};
	}

	private static string? ResolvePortraitImageUrl(Guid gameId, IReadOnlyDictionary<Guid, string>? salePortraitImages)
	{
		if (salePortraitImages == null || gameId == Guid.Empty || !salePortraitImages.TryGetValue(gameId, out string value) || string.IsNullOrWhiteSpace(value))
		{
			return null;
		}
		return value.Trim();
	}
}
