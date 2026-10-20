using System;
using System.Collections.Generic;

namespace UI_Desktop.Store;

internal sealed class DesktopAccountResponse
{
	public Guid UserId { get; set; }

	public string UserEmail { get; set; } = string.Empty;

	public string DisplayName { get; set; } = string.Empty;

	public bool EmailVerified { get; set; }

	public DateTime CreatedAt { get; set; }

	public string CreatedAtText { get; set; } = string.Empty;

	public DateTime? LastLoginAt { get; set; }

	public string? LastLoginAtText { get; set; }

	public int TotalOrders { get; set; }

	public int TotalLibraryItems { get; set; }

	public int PendingOrders { get; set; }

	public int PaidOrders { get; set; }

	public decimal WalletBalance { get; set; }

	public string WalletBalanceText { get; set; } = string.Empty;

	public string WalletStatus { get; set; } = "active";

	public string WalletStatusText { get; set; } = string.Empty;

	public decimal MinimumTopUpAmount { get; set; }

	public decimal MaximumTopUpAmount { get; set; }

	public List<DesktopAccountWalletQuickAmountResponse> QuickAmounts { get; set; } = new List<DesktopAccountWalletQuickAmountResponse>();

	public List<DesktopAccountWalletTransactionResponse> WalletTransactions { get; set; } = new List<DesktopAccountWalletTransactionResponse>();

	public List<DesktopAccountOrderResponse> RecentOrders { get; set; } = new List<DesktopAccountOrderResponse>();

	public List<DesktopAccountOrderResponse> Orders { get; set; } = new List<DesktopAccountOrderResponse>();

	public List<DesktopAccountLibraryItemResponse> LibraryItems { get; set; } = new List<DesktopAccountLibraryItemResponse>();
}
