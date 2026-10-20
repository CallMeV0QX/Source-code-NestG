using System;
using System.Collections.Generic;

namespace UI_Desktop.Store;

internal sealed class StorefrontHomeResponse
{
	public string[] CatalogCategories { get; set; } = Array.Empty<string>();

	public string[] CatalogGenres { get; set; } = Array.Empty<string>();

	public decimal CatalogMaximumPrice { get; set; }

	public string HeroBannerImageUrl { get; set; } = string.Empty;

	public List<StorefrontProductCardResponse> Games { get; set; } = new List<StorefrontProductCardResponse>();

	public List<StorefrontProductCardResponse> SpotlightGames { get; set; } = new List<StorefrontProductCardResponse>();

	public List<StorefrontSaleGameResponse> SaleGames { get; set; } = new List<StorefrontSaleGameResponse>();

	public List<StorefrontCategorySectionResponse> Categories { get; set; } = new List<StorefrontCategorySectionResponse>();

	public int CatalogPage { get; set; } = 1;

	public int CatalogPageSize { get; set; }

	public int CatalogTotalItems { get; set; }

	public bool CatalogHasMore { get; set; }

	public int? CatalogNextPage { get; set; }
}
