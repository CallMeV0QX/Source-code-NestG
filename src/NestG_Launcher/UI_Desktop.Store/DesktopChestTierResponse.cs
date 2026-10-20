using System;

namespace UI_Desktop.Store;

internal sealed class DesktopChestTierResponse
{
	public Guid TierId { get; set; }

	public string Code { get; set; } = string.Empty;

	public string DisplayName { get; set; } = string.Empty;

	public string Description { get; set; } = string.Empty;

	public decimal Price { get; set; }

	public string PriceText { get; set; } = string.Empty;

	public string Currency { get; set; } = string.Empty;

	public string ArtworkPath { get; set; } = string.Empty;

	public int PoolGameCount { get; set; }

	public int AvailableEntryCount { get; set; }

	public long? CycleNumber { get; set; }

	public bool CanOpen { get; set; }

	public string UnavailableReason { get; set; } = string.Empty;
}
