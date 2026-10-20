using System.Collections.Generic;

namespace app_desktop.Services;

internal sealed class DesktopSupportConversationDetailResponse
{
	public DesktopSupportConversationResponse Conversation { get; set; } = new DesktopSupportConversationResponse();

	public List<DesktopSupportMessageResponse> Messages { get; set; } = new List<DesktopSupportMessageResponse>();
}
