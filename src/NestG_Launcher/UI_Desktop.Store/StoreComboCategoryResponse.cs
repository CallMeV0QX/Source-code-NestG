using System;

namespace UI_Desktop.Store;

internal sealed class StoreComboCategoryResponse
{
	public Guid CategoryId { get; set; }

	public string Name { get; set; } = string.Empty;

	public string? Slug { get; set; }
}
