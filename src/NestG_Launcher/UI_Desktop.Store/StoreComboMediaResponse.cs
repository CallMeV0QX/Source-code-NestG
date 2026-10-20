using System;

namespace UI_Desktop.Store;

internal sealed class StoreComboMediaResponse
{
	public Guid? MediaId { get; set; }

	public string MediaType { get; set; } = string.Empty;

	public string Url { get; set; } = string.Empty;

	public string? AltText { get; set; }

	public int DisplayOrder { get; set; }
}
