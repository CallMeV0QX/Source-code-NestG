using System.Collections.Generic;

namespace UI_Desktop.Store;

internal sealed class StorefrontHomeCatalogPageResponse
{
	public List<StorefrontProductCardResponse> Games { get; set; } = new List<StorefrontProductCardResponse>();

	public int Page { get; set; }

	public int PageSize { get; set; }

	public int TotalCount { get; set; }

	public int FromItem { get; set; }

	public int ToItem { get; set; }

	public bool HasMore { get; set; }

	public int? NextPage { get; set; }

	public List<StorefrontCategorySectionResponse> Categories { get; set; } = new List<StorefrontCategorySectionResponse>();
}
