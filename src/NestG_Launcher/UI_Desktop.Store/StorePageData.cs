using System;
using System.Collections.Generic;
using System.Linq;

namespace UI_Desktop.Store;

internal sealed class StorePageData
{
	public string LauncherName { get; set; }

	public string VersionText { get; set; }

	public string UserName { get; set; }

	public string BalanceText { get; set; }

	public string HeroBannerImageUrl { get; set; } = string.Empty;

	public IReadOnlyList<StoreHeroBannerSlot> HeroBannerSlots { get; set; } = Array.Empty<StoreHeroBannerSlot>();

	public IReadOnlyList<string> NavigationItems { get; set; }

	public IReadOnlyList<string> Categories { get; set; }

	public StoreGame? FeaturedGame { get; set; }

	public IReadOnlyList<StoreGame> SpotlightGames { get; set; }

	public IReadOnlyList<StoreSaleGame> SaleGames { get; set; }

	public IReadOnlyList<StoreCategorySection> CategorySections { get; set; }

	public IReadOnlyList<StoreGame> Games { get; set; }

	public IReadOnlyList<StoreComboCard> Combos { get; set; } = Array.Empty<StoreComboCard>();

	public int CatalogPage { get; set; } = 1;

	public int CatalogPageSize { get; set; }

	public int CatalogTotalItems { get; set; }

	public bool CatalogHasMore { get; set; }

	public int? CatalogNextPage { get; set; }

	public int CatalogRenderedCount { get; set; }

	public IReadOnlyList<string> CatalogGenres { get; set; } = Array.Empty<string>();

	public decimal CatalogMaximumPrice { get; set; }

	public static StorePageData FromApi(StorefrontHomeResponse response, IReadOnlyDictionary<Guid, string>? salePortraitImages = null, IReadOnlyList<DesktopHomeBannerSlotResponse>? heroBannerSlots = null, IReadOnlyList<StoreComboCardResponse>? comboCards = null)
	{
		StoreGame[] array = (from game in response.Games.Select(StoreGame.FromApi)
			group game by game.GameId into @group
			select @group.First()).ToArray();
		StoreCategorySection[] array2 = (from section in response.Categories.Select(StoreCategorySection.FromApi)
			where section.Games.Count > 0
			select section).ToArray();
		if (array2.Length == 0)
		{
			array2 = new StoreCategorySection[1]
			{
				new StoreCategorySection
				{
					Name = "Tất cả game",
					Slug = "tat-ca-game",
					Games = array
				}
			};
		}
		StoreGame[] array3 = (from game in response.SpotlightGames.Select(StoreGame.FromApi).Concat(array)
			group game by game.GameId into @group
			select @group.First()).Take(6).ToArray();
		string[] array4 = (from name in response.CatalogCategories.Concat(array2.Select((StoreCategorySection section) => section.Name.Trim()))
			where !string.IsNullOrWhiteSpace(name)
			select name).Distinct<string>(StringComparer.OrdinalIgnoreCase).ToArray();
		StoreSaleGame[] array5 = (from item in response.SaleGames
			select StoreSaleGame.FromApi(item, salePortraitImages) into item
			group item by $"{item.GameId:N}:{item.VersionId?.ToString("N") ?? "default"}" into @group
			select @group.First()).ToArray();
		if (array5.Length == 0)
		{
			array5 = (from game in array.Where((StoreGame game) => game.IsOnSale || !string.IsNullOrWhiteSpace(game.DiscountText)).Take(8)
				select StoreSaleGame.FromGame(game, salePortraitImages)).ToArray();
		}
		StorePageData storePageData = new StorePageData();
		storePageData.LauncherName = "NestG Launcher";
		storePageData.VersionText = "v2.2.18";
		storePageData.UserName = "NestG User";
		storePageData.HeroBannerImageUrl = response.HeroBannerImageUrl;
		storePageData.HeroBannerSlots = (from item in heroBannerSlots?.Select(StoreHeroBannerSlot.FromApi)
			where !string.IsNullOrWhiteSpace(item.ImageUrl)
			orderby item.DisplayOrder
			select item).ToArray() ?? Array.Empty<StoreHeroBannerSlot>();
		storePageData.BalanceText = "0 đ";
		storePageData.NavigationItems = new _003C_003Ez__ReadOnlyArray<string>(new string[3] { "CỬA HÀNG", "THƯ VIỆN", "TÀI KHOẢN" });
		string text = "Tất cả";
		string[] array6 = array4;
		int num = 0;
		string[] array7 = new string[1 + array6.Length];
		array7[num] = text;
		num++;
		ReadOnlySpan<string> readOnlySpan = new ReadOnlySpan<string>(array6);
		readOnlySpan.CopyTo(new Span<string>(array7).Slice(num, readOnlySpan.Length));
		num += readOnlySpan.Length;
		storePageData.Categories = new _003C_003Ez__ReadOnlyArray<string>(array7);
		storePageData.FeaturedGame = array3.FirstOrDefault();
		storePageData.SpotlightGames = ((array3.Length != 0) ? ((IReadOnlyList<StoreGame>)array3) : ((IReadOnlyList<StoreGame>)array.Take(6).ToArray()));
		storePageData.SaleGames = array5;
		storePageData.CategorySections = array2;
		storePageData.Games = array;
		storePageData.Combos = (from item in comboCards?.Select(StoreComboCard.FromApi)
			where item.ComboId != Guid.Empty
			select item).ToArray() ?? Array.Empty<StoreComboCard>();
		storePageData.CatalogPage = response.CatalogPage;
		storePageData.CatalogPageSize = response.CatalogPageSize;
		storePageData.CatalogTotalItems = response.CatalogTotalItems;
		storePageData.CatalogHasMore = response.CatalogHasMore;
		storePageData.CatalogNextPage = response.CatalogNextPage;
		storePageData.CatalogRenderedCount = Math.Min(array.Length, 24);
		storePageData.CatalogGenres = response.CatalogGenres;
		storePageData.CatalogMaximumPrice = response.CatalogMaximumPrice;
		return storePageData;
	}
}
