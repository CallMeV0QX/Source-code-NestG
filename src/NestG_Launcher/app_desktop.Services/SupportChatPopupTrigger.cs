using System;
using System.Collections.Generic;

namespace app_desktop.Services;

internal sealed class SupportChatPopupTrigger
{
	private readonly HashSet<Guid> presentedMessageIds = new HashSet<Guid>();

	private readonly HashSet<Guid> presentedConversationsWithoutMessage = new HashSet<Guid>();

	public bool TryPresentMessage(DesktopSupportMessageReceivedPayload payload, Guid currentUserId, bool isChatOpen)
	{
		if (!isChatOpen && currentUserId != Guid.Empty && payload.Conversation.UserId == currentUserId && payload.Message.MessageId != Guid.Empty && payload.Message.ConversationId == payload.Conversation.ConversationId && string.Equals(payload.Message.SenderRole, "admin", StringComparison.OrdinalIgnoreCase) && !payload.Message.IsInternal && !payload.Message.IsDeleted)
		{
			return presentedMessageIds.Add(payload.Message.MessageId);
		}
		return false;
	}

	public bool TryPresentUnread(DesktopSupportConversationResponse? conversation, Guid currentUserId, bool isChatOpen)
	{
		if (isChatOpen || currentUserId == Guid.Empty || conversation == null || conversation.UserId != currentUserId || conversation.CustomerUnreadCount <= 0)
		{
			return false;
		}
		Guid? lastMessageId = conversation.LastMessageId;
		if (lastMessageId.HasValue)
		{
			Guid valueOrDefault = lastMessageId.GetValueOrDefault();
			if (valueOrDefault != Guid.Empty)
			{
				return presentedMessageIds.Add(valueOrDefault);
			}
		}
		return presentedConversationsWithoutMessage.Add(conversation.ConversationId);
	}

	public void Reset()
	{
		presentedMessageIds.Clear();
		presentedConversationsWithoutMessage.Clear();
	}
}
