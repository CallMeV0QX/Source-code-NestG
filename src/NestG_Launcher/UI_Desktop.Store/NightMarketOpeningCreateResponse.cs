using System;

namespace UI_Desktop.Store;

internal sealed class NightMarketOpeningCreateResponse
{
	public Guid OpeningId { get; set; }

	public int SlotNumber { get; set; }

	public bool RewardReady { get; set; }

	public bool Reused { get; set; }

	public string Message { get; set; } = string.Empty;
}
