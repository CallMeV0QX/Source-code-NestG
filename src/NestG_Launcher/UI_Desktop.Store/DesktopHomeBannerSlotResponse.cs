using System;

namespace UI_Desktop.Store;

internal sealed class DesktopHomeBannerSlotResponse
{
	public string SlotKey { get; set; } = string.Empty;

	public string SlotName { get; set; } = string.Empty;

	public int DisplayOrder { get; set; }

	public string CssClass { get; set; } = string.Empty;

	public int RecommendedWidth { get; set; }

	public int RecommendedHeight { get; set; }

	public decimal AspectRatio { get; set; }

	public string ImageUrl { get; set; } = string.Empty;

	public string? AltText { get; set; }

	public string? TargetUrl { get; set; }

	public Guid? TargetGameId { get; set; }

	public decimal CropFocusX { get; set; }

	public decimal CropFocusY { get; set; }

	public DateTime UpdatedAt { get; set; }
}
