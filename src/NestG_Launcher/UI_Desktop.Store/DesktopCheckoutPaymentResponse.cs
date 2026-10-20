using System;
using System.Collections.Generic;

namespace UI_Desktop.Store;

internal sealed class DesktopCheckoutPaymentResponse
{
	public bool Success { get; set; }

	public string Message { get; set; } = string.Empty;

	public string OrderCode { get; set; } = string.Empty;

	public bool ReusedExistingOrder { get; set; }

	public string PageTitle { get; set; } = string.Empty;

	public string PaymentTransactionCode { get; set; } = string.Empty;

	public string OrderStatus { get; set; } = string.Empty;

	public string PaymentStatus { get; set; } = string.Empty;

	public string StatusTitle { get; set; } = string.Empty;

	public string StatusDescription { get; set; } = string.Empty;

	public string ReceiverBankName { get; set; } = string.Empty;

	public string ReceiverBankShortName { get; set; } = string.Empty;

	public string ReceiverAccountNumber { get; set; } = string.Empty;

	public string ReceiverAccountName { get; set; } = string.Empty;

	public string TransferContent { get; set; } = string.Empty;

	public string QrImageUrl { get; set; } = string.Empty;

	public decimal Subtotal { get; set; }

	public string SubtotalText { get; set; } = string.Empty;

	public decimal DiscountAmount { get; set; }

	public string DiscountAmountText { get; set; } = string.Empty;

	public decimal TotalAmount { get; set; }

	public string TotalAmountText { get; set; } = string.Empty;

	public string? PromoCode { get; set; }

	public DateTime CreatedAt { get; set; }

	public string CreatedAtText { get; set; } = string.Empty;

	public DateTime? ExpiresAt { get; set; }

	public string? ExpiresAtText { get; set; }

	public DateTime? PaidAt { get; set; }

	public string? PaidAtText { get; set; }

	public bool IsPaid { get; set; }

	public bool IsExpired { get; set; }

	public string PollStatusUrl { get; set; } = string.Empty;

	public bool HasQrCode { get; set; } = true;

	public bool IsWalletTopUp { get; set; }

	public bool IsWalletPayment { get; set; }

	public List<DesktopCheckoutPaymentLineItemResponse> Items { get; set; } = new List<DesktopCheckoutPaymentLineItemResponse>();
}
