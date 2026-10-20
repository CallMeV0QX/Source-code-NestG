using System;

namespace UI_Desktop.Store;

internal sealed class DesktopEventCheckoutResponse
{
	public string EventActionType { get; set; } = string.Empty;

	public Guid ReferenceId { get; set; }

	public DesktopCheckoutPaymentResponse Checkout { get; set; } = new DesktopCheckoutPaymentResponse();
}
