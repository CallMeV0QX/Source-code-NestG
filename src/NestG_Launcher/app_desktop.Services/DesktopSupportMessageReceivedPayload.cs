namespace app_desktop.Services;

internal sealed class DesktopSupportMessageReceivedPayload
{
	public DesktopSupportConversationResponse Conversation { get; set; } = new DesktopSupportConversationResponse();

	public DesktopSupportMessageResponse Message { get; set; } = new DesktopSupportMessageResponse();
}
