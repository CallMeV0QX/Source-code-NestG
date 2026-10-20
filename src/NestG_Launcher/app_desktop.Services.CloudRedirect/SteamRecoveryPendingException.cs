using System;

namespace app_desktop.Services.CloudRedirect;

internal sealed class SteamRecoveryPendingException : Exception
{
	public SteamRecoveryPendingException(string message, Exception? inner = null)
		: base(message, inner)
	{
	}
}
