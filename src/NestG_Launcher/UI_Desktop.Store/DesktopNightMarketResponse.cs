using System;
using System.Collections.Generic;

namespace UI_Desktop.Store;

internal sealed class DesktopNightMarketResponse
{
	public Guid EventId { get; set; }

	public string Code { get; set; } = string.Empty;

	public string Title { get; set; } = string.Empty;

	public string Subtitle { get; set; } = string.Empty;

	public string Description { get; set; } = string.Empty;

	public string Status { get; set; } = string.Empty;

	public long? CycleNumber { get; set; }

	public DateTime? StartsAtUtc { get; set; }

	public DateTime? EndsAtUtc { get; set; }

	public List<DesktopNightMarketSlotResponse> Slots { get; set; } = new List<DesktopNightMarketSlotResponse>();
}
