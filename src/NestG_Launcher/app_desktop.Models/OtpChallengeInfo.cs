using System;

namespace app_desktop.Models;

internal sealed class OtpChallengeInfo
{
	public string Email { get; set; } = string.Empty;

	public string Purpose { get; set; } = string.Empty;

	public string Message { get; set; } = string.Empty;

	public DateTime ExpiresAtUtc { get; set; }

	public bool EmailDispatched { get; set; }
}
