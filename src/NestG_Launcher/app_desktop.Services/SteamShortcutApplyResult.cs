using System;
using System.Collections.Generic;

namespace app_desktop.Services;

internal sealed record SteamShortcutApplyResult(bool Success, bool Skipped, string Message, string? ExePath, IReadOnlyList<string> WrittenFiles, IReadOnlyList<string> BackupFiles, IReadOnlyList<string> Messages)
{
	public static SteamShortcutApplyResult CreateSuccess(string exePath, IReadOnlyList<string> writtenFiles, IReadOnlyList<string> backupFiles, IReadOnlyList<string> messages)
	{
		return new SteamShortcutApplyResult(Success: true, Skipped: false, "Đã tạo/cập nhật Steam Non-Steam Shortcut.", exePath, writtenFiles, backupFiles, messages);
	}

	public static SteamShortcutApplyResult CreateFailed(string message, IReadOnlyList<string> messages)
	{
		return new SteamShortcutApplyResult(Success: false, Skipped: false, message, null, Array.Empty<string>(), Array.Empty<string>(), messages);
	}

	public static SteamShortcutApplyResult CreateSkipped(string message)
	{
		return new SteamShortcutApplyResult(Success: false, Skipped: true, message, null, Array.Empty<string>(), Array.Empty<string>(), new _003C_003Ez__ReadOnlySingleElementList<string>(message));
	}
}
