using System;

namespace UI_Desktop.Store;

internal sealed class StoreHeroBannerSlot
{
	public string SlotKey { get; set; }

	public int DisplayOrder { get; set; }

	public string CssClass { get; set; }

	public string ImageUrl { get; set; }

	public string? AltText { get; set; }

	public decimal CropFocusX { get; set; }

	public decimal CropFocusY { get; set; }

	public static StoreHeroBannerSlot FromApi(DesktopHomeBannerSlotResponse item)
	{
		return new StoreHeroBannerSlot
		{
			SlotKey = item.SlotKey.Trim(),
			DisplayOrder = item.DisplayOrder,
			CssClass = item.CssClass.Trim(),
			ImageUrl = item.ImageUrl.Trim(),
			AltText = (string.IsNullOrWhiteSpace(item.AltText) ? null : item.AltText.Trim()),
			CropFocusX = Math.Clamp(item.CropFocusX, 0m, 100m),
			CropFocusY = Math.Clamp(item.CropFocusY, 0m, 100m)
		};
	}
}
