using System;

namespace app_desktop.Services;

internal sealed class DesktopSupportMessageReadPayload
{
	public Guid ConversationId { get; set; }

	public Guid ReaderUserId { get; set; }

	public string ReaderRole { get; set; } = string.Empty;

	public Guid? LastReadMessageId { get; set; }

	public int CustomerUnreadCount { get; set; }

	public int AdminUnreadCount { get; set; }
}
