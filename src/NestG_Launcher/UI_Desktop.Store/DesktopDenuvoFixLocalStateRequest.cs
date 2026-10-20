using System;

namespace UI_Desktop.Store;

internal sealed class DesktopDenuvoFixLocalStateRequest
{
	public Guid LibraryId { get; set; }

	public Guid AssignmentId { get; set; }

	public int? LastAppliedRevisionNumber { get; set; }

	public string? GameInstallPath { get; set; }

	public string? ConfigRelativePath { get; set; }

	public string? ClientVersion { get; set; }
}
