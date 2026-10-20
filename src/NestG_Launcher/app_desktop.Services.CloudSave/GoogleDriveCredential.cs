using System;

namespace app_desktop.Services.CloudSave;

internal sealed class GoogleDriveCredential
{
	public Guid NestGUserId { get; set; }

	public string? OAuthClientId { get; set; }

	public string? ConnectionId { get; set; }

	public string AccessToken { get; set; } = string.Empty;

	public string RefreshToken { get; set; } = string.Empty;

	public DateTimeOffset AccessTokenExpiresAtUtc { get; set; }

	public string? AccountEmail { get; set; }
}
