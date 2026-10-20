using System;

namespace app_desktop.Services;

internal sealed class DesktopSupportConversationResponse
{
	public Guid ConversationId { get; set; }

	public Guid UserId { get; set; }

	public string UserEmail { get; set; } = string.Empty;

	public string UserDisplayName { get; set; } = string.Empty;

	public Guid? AssignedAdminUserId { get; set; }

	public string? AssignedAdminEmail { get; set; }

	public string? AssignedAdminDisplayName { get; set; }

	public string? Subject { get; set; }

	public string Status { get; set; } = string.Empty;

	public string StatusText { get; set; } = string.Empty;

	public string Priority { get; set; } = string.Empty;

	public string Source { get; set; } = string.Empty;

	public Guid? LastMessageId { get; set; }

	public DateTime? LastMessageAt { get; set; }

	public string? LastMessagePreview { get; set; }

	public int CustomerUnreadCount { get; set; }

	public int AdminUnreadCount { get; set; }

	public DateTime CreatedAt { get; set; }

	public DateTime UpdatedAt { get; set; }
}
