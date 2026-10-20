using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;

namespace UI_Desktop.Store;

internal sealed class StoreGame
{
	public Guid GameId { get; set; }

	public Guid? VersionId { get; set; }

	public string Title { get; set; }

	public string Subtitle { get; set; }

	public string Category { get; set; }

	public IReadOnlyList<string> Genres { get; set; }

	public string BadgeText { get; set; }

	public string? PosterImageUrl { get; set; }

	public string? SpotlightImageUrl { get; set; }

	public string? CurrentPriceTextOverride { get; set; }

	public string? ReferencePriceTextOverride { get; set; }

	public string? DiscountTextOverride { get; set; }

	public bool IsOnSale { get; set; }

	public string? SaleName { get; set; }

	public DateTime? SaleEndsAt { get; set; }

	public decimal OriginalPrice { get; set; }

	public decimal SalePrice { get; set; }

	public int Likes { get; set; }

	public int Dislikes { get; set; }

	public int PurchaseCount { get; set; }

	public string PromoLabel { get; set; }

	public bool IsTrending { get; set; }

	public Color PrimaryColor { get; set; }

	public Color SecondaryColor { get; set; }

	public Color AccentColor { get; set; }

	public int DiscountPercent
	{
		get
		{
			if (!(OriginalPrice <= 0m))
			{
				return (int)Math.Round((1m - SalePrice / OriginalPrice) * 100m, MidpointRounding.AwayFromZero);
			}
			return 0;
		}
	}

	public string OriginalPriceText
	{
		get
		{
			if (string.IsNullOrWhiteSpace(ReferencePriceTextOverride))
			{
				return FormatPrice(OriginalPrice);
			}
			return ReferencePriceTextOverride;
		}
	}

	public string SalePriceText
	{
		get
		{
			if (!(SalePrice <= 0m))
			{
				return FormatPrice(SalePrice);
			}
			return "Miễn phí";
		}
	}

	public string DisplayOriginalPriceText
	{
		get
		{
			if (string.IsNullOrWhiteSpace(ReferencePriceTextOverride))
			{
				return OriginalPriceText;
			}
			return ReferencePriceTextOverride;
		}
	}

	public string DisplaySalePriceText
	{
		get
		{
			if (string.IsNullOrWhiteSpace(CurrentPriceTextOverride))
			{
				return SalePriceText;
			}
			return CurrentPriceTextOverride;
		}
	}

	public string DiscountText
	{
		get
		{
			if (string.IsNullOrWhiteSpace(DiscountTextOverride))
			{
				if (DiscountPercent <= 0)
				{
					return string.Empty;
				}
				return $"-{DiscountPercent}%";
			}
			return DiscountTextOverride;
		}
	}

	public string SaleLimitText
	{
		get
		{
			if (!SaleEndsAt.HasValue)
			{
				if (!IsOnSale)
				{
					return "Giá hiện hành";
				}
				return "Đang sale";
			}
			return SaleEndsAt.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
		}
	}

	public string GenresText => string.Join(", ", Genres);

	public string PurchaseText => $"{PurchaseCount} lượt mua";

	public static StoreGame FromApi(StorefrontProductCardResponse item)
	{
		string[] array = (from name in item.CategoryNames
			where !string.IsNullOrWhiteSpace(name)
			select name.Trim()).Distinct<string>(StringComparer.OrdinalIgnoreCase).ToArray();
		string category = array.FirstOrDefault() ?? item.Tag.Trim();
		decimal num = item.CurrentPrice ?? ParsePrice(item.CurrentPriceText, item.IsFree);
		decimal num2 = item.ReferencePrice ?? ParsePrice(item.ReferencePriceText, isFree: false);
		if (num2 <= 0m)
		{
			num2 = num;
		}
		StoreGamePalette storeGamePalette = StoreGamePalette.FromSeed($"{item.GameId:N}:{item.Name}:{item.Tag}");
		(int, int, int) tuple = BuildStats(item.GameId, item.IsTrending);
		StoreGame storeGame = new StoreGame();
		storeGame.GameId = item.GameId;
		storeGame.VersionId = item.VersionId;
		storeGame.Title = item.Name.Trim();
		storeGame.Subtitle = BuildSubtitle(item, category);
		storeGame.Category = category;
		storeGame.Genres = ((array.Length != 0) ? array : new string[1] { item.Tag.Trim() });
		storeGame.BadgeText = (string.IsNullOrWhiteSpace(item.Badge) ? "Kích hoạt steam cá nhân" : item.Badge.Trim());
		storeGame.PosterImageUrl = item.PosterImageUrl;
		storeGame.SpotlightImageUrl = item.SpotlightImageUrl;
		storeGame.CurrentPriceTextOverride = item.CurrentPriceText;
		storeGame.ReferencePriceTextOverride = item.ReferencePriceText;
		storeGame.DiscountTextOverride = item.DiscountText;
		storeGame.IsOnSale = item.IsOnSale;
		storeGame.SaleName = item.SaleName;
		storeGame.SaleEndsAt = item.SaleEndsAt;
		storeGame.OriginalPrice = num2;
		storeGame.SalePrice = num;
		storeGame.Likes = tuple.Item1;
		storeGame.Dislikes = tuple.Item2;
		storeGame.PurchaseCount = tuple.Item3;
		storeGame.PromoLabel = BuildPromoLabel(item, num2, num);
		storeGame.IsTrending = item.IsTrending;
		storeGame.PrimaryColor = storeGamePalette.PrimaryColor;
		storeGame.SecondaryColor = storeGamePalette.SecondaryColor;
		storeGame.AccentColor = storeGamePalette.AccentColor;
		return storeGame;
	}

	private static string BuildSubtitle(StorefrontProductCardResponse item, string category)
	{
		if (!string.IsNullOrWhiteSpace(item.Tag))
		{
			return item.Tag.Trim();
		}
		if (item.CategoryNames.Count > 1)
		{
			return string.Join(", ", item.CategoryNames.Take(2));
		}
		return category;
	}

	private static string BuildPromoLabel(StorefrontProductCardResponse item, decimal originalPrice, decimal salePrice)
	{
		if (item.IsFree)
		{
			return "FREE";
		}
		if (!string.IsNullOrWhiteSpace(item.DiscountText))
		{
			if (!item.IsTrending)
			{
				return "SALE";
			}
			return "SALE HOT";
		}
		if (item.IsOnSale)
		{
			if (!item.IsTrending)
			{
				return "SALE";
			}
			return "SALE HOT";
		}
		if (originalPrice > salePrice && salePrice > 0m)
		{
			if (!item.IsTrending)
			{
				return "SALE";
			}
			return "SALE HOT";
		}
		if (item.IsTrending)
		{
			return "HOT";
		}
		if (!string.IsNullOrWhiteSpace(item.Tag))
		{
			string text = item.Tag.Trim().ToUpperInvariant();
			if (text.Length > 12)
			{
				return text.Substring(0, 12);
			}
			return text;
		}
		return "STORE";
	}

	private static (int Likes, int Dislikes, int PurchaseCount) BuildStats(Guid gameId, bool isTrending)
	{
		long num = Math.Abs((long)gameId.GetHashCode());
		long num2 = (isTrending ? (10 + num % 25) : (num % 10));
		long num3 = ((num2 == 0L) ? 0 : (num % Math.Max(1L, num2 / 4 + 1)));
		long num4 = (isTrending ? (3 + num % 18) : (num % 7));
		return (Likes: (int)num2, Dislikes: (int)num3, PurchaseCount: (int)num4);
	}

	private static decimal ParsePrice(string? text, bool isFree)
	{
		if (isFree || string.IsNullOrWhiteSpace(text))
		{
			return 0m;
		}
		string text2 = new string(text.Where((char character) => char.IsDigit(character) || character == ',' || character == '.').ToArray());
		if (string.IsNullOrWhiteSpace(text2))
		{
			return 0m;
		}
		text2 = text2.Replace(".", string.Empty).Replace(",", string.Empty);
		if (!decimal.TryParse(text2, out var result))
		{
			return 0m;
		}
		return result;
	}

	private static string FormatPrice(decimal value)
	{
		return string.Format(CultureInfo.InvariantCulture, "{0:N0} đ", value).Replace(",", ".");
	}
}
