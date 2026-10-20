using System;

namespace UI_Desktop.Store;

internal sealed class StoreComboCardResponse
{
	public Guid ComboId { get; set; }

	public string Name { get; set; } = string.Empty;

	public string Slug { get; set; } = string.Empty;

	public string Status { get; set; } = string.Empty;

	public decimal Price { get; set; }

	public string? PosterUrl { get; set; }

	public bool IsTrending { get; set; }

	public int ComponentCount { get; set; }

	public int RevisionNumber { get; set; }

	public DateTime UpdatedAt { get; set; }

	public DateTime? PublishedAt { get; set; }
}
