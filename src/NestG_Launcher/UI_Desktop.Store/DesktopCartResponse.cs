using System.Collections.Generic;

namespace UI_Desktop.Store;

internal sealed class DesktopCartResponse
{
	public string PageTitle { get; set; } = string.Empty;

	public string UserEmail { get; set; } = string.Empty;

	public List<DesktopCartLineItemResponse> Items { get; set; } = new List<DesktopCartLineItemResponse>();

	public List<DesktopComboCartLineItemResponse> ComboItems { get; set; } = new List<DesktopComboCartLineItemResponse>();

	public List<DesktopCartPromoSuggestionResponse> PromoSuggestions { get; set; } = new List<DesktopCartPromoSuggestionResponse>();

	public string PromoCode { get; set; } = string.Empty;

	public string? PromoMessage { get; set; }

	public string? PromoDescription { get; set; }

	public bool PromoApplied { get; set; }

	public bool PromoInvalid { get; set; }

	public int TotalQuantity { get; set; }

	public int DistinctItemCount { get; set; }

	public decimal Subtotal { get; set; }

	public string SubtotalText { get; set; } = string.Empty;

	public decimal DiscountAmount { get; set; }

	public string DiscountAmountText { get; set; } = string.Empty;

	public decimal TotalAmount { get; set; }

	public string TotalAmountText { get; set; } = string.Empty;

	public decimal WalletBalance { get; set; }

	public string WalletBalanceText { get; set; } = string.Empty;

	public string WalletStatus { get; set; } = "active";

	public bool CanPayWithWallet { get; set; }

	public decimal WalletShortfall { get; set; }

	public string WalletShortfallText { get; set; } = string.Empty;

	public bool IsEmpty { get; set; }
}
