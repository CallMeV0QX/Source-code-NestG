using System;

namespace app_desktop.Services;

internal sealed class DesktopDenuvoActiveRequestCancelledEventArgs(Guid activeRequestId, Guid assignmentId, Guid libraryId, string gameName, string reason, DateTime cancelledAt) : EventArgs()
{
	public Guid ActiveRequestId { get; } = activeRequestId;

	public Guid AssignmentId { get; } = assignmentId;

	public Guid LibraryId { get; } = libraryId;

	public string GameName { get; } = gameName;

	public string Reason { get; } = reason;

	public DateTime CancelledAt { get; } = cancelledAt;

	public bool Handled { get; set; }
}
