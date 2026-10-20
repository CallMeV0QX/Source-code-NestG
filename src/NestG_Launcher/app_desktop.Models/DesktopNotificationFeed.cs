using System;
using System.Collections.Generic;

namespace app_desktop.Models;

internal sealed class DesktopNotificationFeed
{
	public DesktopAppUpdateInfo? Update { get; set; }

	public List<DesktopNotificationItem> Notifications { get; set; } = new List<DesktopNotificationItem>();

	public int UnreadCount { get; set; }

	public DateTime ServerTime { get; set; }
}
