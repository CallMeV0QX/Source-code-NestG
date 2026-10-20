using System.Collections.Generic;

namespace UI_Desktop.Store;

internal sealed class StorefrontCategorySectionResponse
{
	public string Name { get; set; } = string.Empty;

	public string Slug { get; set; } = string.Empty;

	public List<StorefrontProductCardResponse> Games { get; set; } = new List<StorefrontProductCardResponse>();
}
