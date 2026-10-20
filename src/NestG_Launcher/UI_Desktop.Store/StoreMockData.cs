using System;
using System.Collections.Generic;
using System.Linq;

namespace UI_Desktop.Store;

internal static class StoreMockData
{
	internal static StorePageData Create()
	{
		List<StoreGame> list = new List<StoreGame>();
		list.Add(CreateGame("Farm Manager 2018", "Mua sale nông trại", "Mô phỏng", new _003C_003Ez__ReadOnlyArray<string>(new string[4] { "Farming Sim", "Indie", "Management", "Casual" }), 117000m, 11700m, "SALE HOT", isTrending: true));
		list.Add(CreateGame("Borderlands 4", "Loot shooter bung no", "Hành động", new _003C_003Ez__ReadOnlyArray<string>(new string[3] { "Action", "Co-op", "Shooter" }), 381150m, 377339m, "MỚI", isTrending: true));
		list.Add(CreateGame("Retro Rewind - Video Store Simulator", "Hoai niem video store", "Mô phỏng", new _003C_003Ez__ReadOnlyArray<string>(new string[3] { "Simulator", "Indie", "Business" }), 117000m, 117000m, "MÔ PHỎNG", isTrending: true));
		list.Add(CreateGame("Unspoken", "Kinh dị tâm lý", "Kinh di", new _003C_003Ez__ReadOnlyArray<string>(new string[3] { "Horror", "Mystery", "Indie" }), 8100m, 8100m, "KINH DI", isTrending: false));
		list.Add(CreateGame("Cooking Simulator", "Bếp trưởng tại gia", "Mô phỏng", new _003C_003Ez__ReadOnlyArray<string>(new string[3] { "Cooking", "Simulation", "Casual" }), 188000m, 122000m, "SALE", isTrending: false));
		list.Add(CreateGame("DARK SOULS III", "Chiến đấu không khoan nhượng", "Nhập vai", new _003C_003Ez__ReadOnlyArray<string>(new string[3] { "Action RPG", "Souls-like", "Adventure" }), 520000m, 299000m, "BESTSELLER", isTrending: false));
		List<StoreGame> list2 = list;
		StoreCategorySection[] categorySections = (from game in list2
			group game by game.Category into @group
			select new StoreCategorySection
			{
				Name = @group.Key,
				Slug = StoreCategorySection.Slugify(@group.Key),
				Games = @group.ToArray()
			}).ToArray();
		StoreSaleGame[] saleGames = (from game in list2
			where game.DiscountPercent > 0
			select StoreSaleGame.FromGame(game)).ToArray();
		StorePageData storePageData = new StorePageData();
		storePageData.LauncherName = "NestG Launcher";
		storePageData.VersionText = "v2.2.18";
		storePageData.UserName = "NestG User";
		storePageData.BalanceText = "0 đ";
		storePageData.NavigationItems = new _003C_003Ez__ReadOnlyArray<string>(new string[3] { "CỬA HÀNG", "THƯ VIỆN", "TÀI KHOẢN" });
		storePageData.Categories = new _003C_003Ez__ReadOnlyArray<string>(new string[5] { "Tất cả", "Mô phỏng", "Hành động", "Kinh dị", "Nhập vai" });
		storePageData.FeaturedGame = list2[0];
		storePageData.SpotlightGames = list2.Take(6).ToArray();
		storePageData.SaleGames = saleGames;
		storePageData.CategorySections = categorySections;
		storePageData.Games = list2;
		return storePageData;
	}

	private static StoreGame CreateGame(string title, string subtitle, string category, IReadOnlyList<string> genres, decimal originalPrice, decimal salePrice, string promoLabel, bool isTrending)
	{
		StoreGamePalette storeGamePalette = StoreGamePalette.FromSeed(title);
		int num = Math.Abs(title.GetHashCode());
		return new StoreGame
		{
			GameId = Guid.NewGuid(),
			Title = title,
			Subtitle = subtitle,
			Category = category,
			Genres = genres,
			BadgeText = "Kích hoạt steam cá nhân",
			PosterImageUrl = null,
			SpotlightImageUrl = null,
			OriginalPrice = originalPrice,
			SalePrice = salePrice,
			Likes = 5 + num % 20,
			Dislikes = num % 3,
			PurchaseCount = 1 + num % 15,
			PromoLabel = promoLabel,
			IsTrending = isTrending,
			PrimaryColor = storeGamePalette.PrimaryColor,
			SecondaryColor = storeGamePalette.SecondaryColor,
			AccentColor = storeGamePalette.AccentColor
		};
	}
}
