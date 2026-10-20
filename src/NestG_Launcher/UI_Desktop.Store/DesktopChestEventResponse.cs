using System;
using System.Collections.Generic;

namespace UI_Desktop.Store;

internal sealed class DesktopChestEventResponse
{
	public Guid EventId { get; set; }

	public string Code { get; set; } = string.Empty;

	public string Title { get; set; } = string.Empty;

	public string Subtitle { get; set; } = string.Empty;

	public string Description { get; set; } = string.Empty;

	public string HeroArtworkPath { get; set; } = string.Empty;

	public string Status { get; set; } = string.Empty;

	public DateTime? StartsAtUtc { get; set; }

	public DateTime? EndsAtUtc { get; set; }

	public List<DesktopChestTierResponse> Tiers { get; set; } = new List<DesktopChestTierResponse>();
}
