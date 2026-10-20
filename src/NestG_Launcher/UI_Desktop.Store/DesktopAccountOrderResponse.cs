using System;

namespace UI_Desktop.Store;

internal sealed class DesktopAccountOrderResponse
{
	public string OrderCode { get; set; } = string.Empty;

	public string Status { get; set; } = string.Empty;

	public string StatusText { get; set; } = string.Empty;

	public DateTime CreatedAt { get; set; }

	public string CreatedAtText { get; set; } = string.Empty;

	public decimal TotalAmount { get; set; }

	public string TotalAmountText { get; set; } = string.Empty;

	public string LineSummary { get; set; } = string.Empty;

	public string? ActionUrl { get; set; }

	public string ActionLabel { get; set; } = string.Empty;
}
