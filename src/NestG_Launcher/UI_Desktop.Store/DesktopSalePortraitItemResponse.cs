using System;

namespace UI_Desktop.Store;

internal sealed class DesktopSalePortraitItemResponse
{
	public Guid GameId { get; set; }

	public string? PortraitImageUrl { get; set; }

	public DateTime UpdatedAt { get; set; }
}
