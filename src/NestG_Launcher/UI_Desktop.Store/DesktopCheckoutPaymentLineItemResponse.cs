namespace UI_Desktop.Store;

internal sealed class DesktopCheckoutPaymentLineItemResponse
{
	public string Name { get; set; } = string.Empty;

	public string VersionName { get; set; } = string.Empty;

	public int Quantity { get; set; }

	public decimal UnitPrice { get; set; }

	public string UnitPriceText { get; set; } = string.Empty;

	public decimal LineTotal { get; set; }

	public string LineTotalText { get; set; } = string.Empty;
}
