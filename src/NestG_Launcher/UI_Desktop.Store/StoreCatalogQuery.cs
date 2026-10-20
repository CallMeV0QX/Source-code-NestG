using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace UI_Desktop.Store;

internal sealed record StoreCatalogQuery(string? Q = null, string? Genre = null, string? Category = null, string Sort = "default", decimal? MaximumPrice = null)
{
	internal StoreCatalogQuery Bounded()
	{
		string q = Limit(Q);
		string genre = Filter(Genre);
		string category = Filter(Category);
		bool flag;
		switch (Sort)
		{
		case "title":
		case "price-asc":
		case "price-desc":
			flag = true;
			break;
		default:
			flag = false;
			break;
		}
		return new StoreCatalogQuery(q, genre, category, flag ? Sort : "default", MaximumPrice.HasValue ? new decimal?(Math.Clamp(MaximumPrice.Value, 0m, 1000000000000m)) : ((decimal?)null));
	}

	private static string? Limit(string? value)
	{
		if (!string.IsNullOrWhiteSpace(value))
		{
			return value.Trim().Substring(0, Math.Min(value.Trim().Length, 120));
		}
		return null;
	}

	private static string? Filter(string? value)
	{
		if (!(Normalize(value) == "all"))
		{
			return Limit(value);
		}
		return null;
	}

	internal string ToQueryString()
	{
		return string.Join("&", new string[5]
		{
			"q=" + Uri.EscapeDataString(Q ?? ""),
			"genre=" + Uri.EscapeDataString(Genre ?? ""),
			"category=" + Uri.EscapeDataString(Category ?? ""),
			"sort=" + Uri.EscapeDataString(Sort),
			"maximumPrice=" + MaximumPrice?.ToString(CultureInfo.InvariantCulture)
		}.Where((string item) => !item.EndsWith('=')));
	}

	internal static string Normalize(string? value)
	{
		return new string((from character in (value ?? "").Trim().ToLowerInvariant().Replace('đ', 'd')
				.Normalize(NormalizationForm.FormD)
			where CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark
			select character).ToArray());
	}

	internal StorefrontHomeCatalogPageResponse PageLegacy(IReadOnlyList<StorefrontProductCardResponse> cards, int page, int size)
	{
		string q = Normalize(Q);
		string genre = Normalize(Genre);
		string category = Normalize(Category);
		StorefrontProductCardResponse[] array = cards.DistinctBy((StorefrontProductCardResponse item) => item.GameId).Where(delegate(StorefrontProductCardResponse item)
		{
			bool flag = q.Length == 0 || Normalize(item.Name + " " + item.Tag + " " + string.Join(" ", item.CategoryNames)).Contains(q);
			if (flag)
			{
				string text = genre;
				bool flag2 = (((text != null && text.Length == 0) || text == "all") ? true : false);
				flag = flag2 || item.CategoryNames.Prepend(item.Tag).Any((string name) => Normalize(name) == genre);
			}
			bool flag3 = flag;
			if (flag3)
			{
				string text = category;
				bool flag2 = (((text != null && text.Length == 0) || text == "all") ? true : false);
				flag3 = flag2 || item.CategoryNames.Any((string name) => Normalize(name) == category);
			}
			return flag3 && (!MaximumPrice.HasValue || StoreGame.FromApi(item).SalePrice <= MaximumPrice.Value);
		}).ToArray();
		object source = Sort switch
		{
			"title" => array.OrderBy<StorefrontProductCardResponse, string>((StorefrontProductCardResponse item) => item.Name, StringComparer.Create(CultureInfo.GetCultureInfo("vi-VN"), ignoreCase: true)).ThenBy((StorefrontProductCardResponse item) => item.GameId), 
			"price-asc" => from item in array
				orderby StoreGame.FromApi(item).SalePrice, item.GameId
				select item, 
			"price-desc" => from item in array
				orderby StoreGame.FromApi(item).SalePrice descending, item.GameId
				select item, 
			_ => array, 
		};
		page = Math.Clamp(page, 1, 1000000);
		size = Math.Clamp(size, 1, 60);
		int num = (page - 1) * size;
		List<StorefrontProductCardResponse> list = ((IEnumerable<StorefrontProductCardResponse>)source).Skip(num).Take(size).ToList();
		bool flag4 = num + list.Count < array.Length;
		return new StorefrontHomeCatalogPageResponse
		{
			Games = list,
			Page = page,
			PageSize = size,
			TotalCount = array.Length,
			HasMore = flag4,
			NextPage = (flag4 ? new int?(page + 1) : ((int?)null))
		};
	}
}
