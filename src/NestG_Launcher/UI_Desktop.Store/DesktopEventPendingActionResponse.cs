using System;

namespace UI_Desktop.Store;

internal sealed class DesktopEventPendingActionResponse
{
	public string Type { get; set; } = string.Empty;

	public Guid ReferenceId { get; set; }

	public string Title { get; set; } = string.Empty;

	public string Status { get; set; } = string.Empty;

	public string OrderCode { get; set; } = string.Empty;
}
