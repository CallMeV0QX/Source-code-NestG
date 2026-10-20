namespace app_desktop.Services;

internal sealed class DesktopSupportMutationResponse
{
	public bool Success { get; set; }

	public string Message { get; set; } = string.Empty;

	public DesktopSupportConversationResponse Conversation { get; set; } = new DesktopSupportConversationResponse();

	public DesktopSupportMessageResponse? SupportMessage { get; set; }
}
