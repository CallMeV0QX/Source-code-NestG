using System;
using System.Collections.Generic;
using System.Linq;

namespace UI_Desktop.Store;

internal sealed class StoreCatalogPageData
{
	public IReadOnlyList<StoreGame> Games { get; set; }

	public int Page { get; set; }

	public int PageSize { get; set; }

	public int TotalCount { get; set; }

	public bool HasMore { get; set; }

	public int? NextPage { get; set; }

	public static StoreCatalogPageData FromApi(StorefrontHomeCatalogPageResponse response)
	{
		IEnumerable<StorefrontProductCardResponse> source;
		if (response.Games.Count <= 0)
		{
			source = response.Categories.SelectMany((StorefrontCategorySectionResponse section) => section.Games);
		}
		else
		{
			IEnumerable<StorefrontProductCardResponse> games = response.Games;
			source = games;
		}
		StoreGame[] games2 = (from game in source.Select(StoreGame.FromApi)
			group game by game.GameId into @group
			select @group.First()).ToArray();
		return new StoreCatalogPageData
		{
			Games = games2,
			Page = response.Page,
			PageSize = response.PageSize,
			TotalCount = response.TotalCount,
			HasMore = response.HasMore,
			NextPage = response.NextPage
		};
	}
}
