namespace UI_Desktop.Store;

internal sealed class DesktopCheckoutStatusResponse
{
	public string OrderCode { get; set; } = string.Empty;

	public string OrderStatus { get; set; } = string.Empty;

	public string PaymentStatus { get; set; } = string.Empty;

	public bool IsPaid { get; set; }

	public bool IsExpired { get; set; }

	public string StatusTitle { get; set; } = string.Empty;

	public string StatusDescription { get; set; } = string.Empty;
}
