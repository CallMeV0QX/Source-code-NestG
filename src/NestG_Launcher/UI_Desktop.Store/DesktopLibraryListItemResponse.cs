using System;

namespace UI_Desktop.Store;

internal sealed class DesktopLibraryListItemResponse
{
	public Guid LibraryId { get; set; }

	public Guid GameId { get; set; }

	public Guid VersionId { get; set; }

	public string GameName { get; set; } = string.Empty;

	public string VersionName { get; set; } = string.Empty;

	public string Category { get; set; } = string.Empty;

	public string Badge { get; set; } = string.Empty;

	public string CurrentPriceText { get; set; } = string.Empty;

	public string? PosterImageUrl { get; set; }

	public string? SpotlightImageUrl { get; set; }

	public bool IsTrending { get; set; }

	public string OrderCode { get; set; } = string.Empty;

	public DateTime AcquiredAt { get; set; }

	public string AcquiredAtText { get; set; } = string.Empty;
}
