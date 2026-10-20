using System;

namespace app_desktop.Services;

internal sealed class DesktopDenuvoActiveRealtimeRequestCancelled
{
	public Guid ActiveRequestId { get; set; }

	public Guid AssignmentId { get; set; }

	public Guid LibraryId { get; set; }

	public string GameName { get; set; } = string.Empty;

	public string Reason { get; set; } = string.Empty;

	public DateTime CancelledAt { get; set; }
}
