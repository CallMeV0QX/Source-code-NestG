using System;

namespace app_desktop.Models;

internal sealed class DesktopNotificationItem
{
	public Guid NotificationId { get; set; }

	public string Type { get; set; } = string.Empty;

	public string Title { get; set; } = string.Empty;

	public string Message { get; set; } = string.Empty;

	public string? ImageUrl { get; set; }

	public string? ActionUrl { get; set; }

	public string? ActionLabel { get; set; }

	public Guid? GameId { get; set; }

	public Guid? DesktopAppVersionId { get; set; }

	public string Priority { get; set; } = string.Empty;

	public bool IsPopup { get; set; }

	public bool IsPinned { get; set; }

	public bool IsRead { get; set; }

	public DateTime? ReadAt { get; set; }

	public DateTime? PublishedAt { get; set; }

	public DateTime CreatedAt { get; set; }
}
