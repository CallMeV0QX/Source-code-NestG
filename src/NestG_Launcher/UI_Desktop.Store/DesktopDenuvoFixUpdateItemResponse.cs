using System;

namespace UI_Desktop.Store;

internal sealed class DesktopDenuvoFixUpdateItemResponse
{
	public Guid LibraryId { get; set; }

	public Guid AssignmentId { get; set; }

	public Guid GameId { get; set; }

	public string GameName { get; set; } = string.Empty;

	public string VersionName { get; set; } = string.Empty;

	public string AssignmentStatus { get; set; } = string.Empty;

	public int CurrentRevisionNumber { get; set; }

	public int ActiveRevisionNumber
	{
		get
		{
			return CurrentRevisionNumber;
		}
		set
		{
			CurrentRevisionNumber = value;
		}
	}

	public int? LastAppliedRevisionNumber { get; set; }

	public string ConfigRelativePath { get; set; } = "steam_settings/configs.user.ini";

	public string ConfigDownloadUrl { get; set; } = string.Empty;

	public string Message { get; set; } = string.Empty;
}
