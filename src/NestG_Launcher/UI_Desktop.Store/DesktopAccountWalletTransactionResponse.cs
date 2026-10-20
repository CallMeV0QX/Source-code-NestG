using System;

namespace UI_Desktop.Store;

internal sealed class DesktopAccountWalletTransactionResponse
{
	public Guid WalletTransactionId { get; set; }

	public string TransactionType { get; set; } = string.Empty;

	public string Direction { get; set; } = string.Empty;

	public string DirectionText { get; set; } = string.Empty;

	public string DirectionTone { get; set; } = string.Empty;

	public decimal Amount { get; set; }

	public string AmountText { get; set; } = string.Empty;

	public string Title { get; set; } = string.Empty;

	public string Subtitle { get; set; } = string.Empty;

	public string Status { get; set; } = string.Empty;

	public string StatusText { get; set; } = string.Empty;

	public string StatusTone { get; set; } = string.Empty;

	public string? OrderCode { get; set; }

	public DateTime CreatedAt { get; set; }

	public string CreatedAtText { get; set; } = string.Empty;

	public string? ActionUrl { get; set; }

	public string ActionLabel { get; set; } = string.Empty;
}
