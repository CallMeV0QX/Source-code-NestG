using System;
using System.Collections.Generic;

namespace app_desktop.Models;

internal sealed class AuthSession
{
	public Guid UserId { get; set; }

	public string Email { get; set; } = string.Empty;

	public string DisplayName { get; set; } = string.Empty;

	public string Status { get; set; } = string.Empty;

	public bool EmailVerified { get; set; }

	public List<string> Roles { get; set; } = new List<string>();

	public string AccessToken { get; set; } = string.Empty;

	public DateTime AccessTokenExpiresAt { get; set; }

	public string RefreshToken { get; set; } = string.Empty;

	public DateTime RefreshTokenExpiresAt { get; set; }
}
