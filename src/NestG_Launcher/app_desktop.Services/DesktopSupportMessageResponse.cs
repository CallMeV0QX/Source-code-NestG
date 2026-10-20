using System;

namespace app_desktop.Services;

internal sealed class DesktopSupportMessageResponse
{
	public Guid MessageId { get; set; }

	public Guid ConversationId { get; set; }

	public Guid? SenderUserId { get; set; }

	public string SenderRole { get; set; } = string.Empty;

	public string SenderDisplayName { get; set; } = string.Empty;

	public string MessageType { get; set; } = string.Empty;

	public string? Body { get; set; }

	public Guid? ClientMessageId { get; set; }

	public bool IsInternal { get; set; }

	public bool IsDeleted { get; set; }

	public DateTime CreatedAt { get; set; }
}
