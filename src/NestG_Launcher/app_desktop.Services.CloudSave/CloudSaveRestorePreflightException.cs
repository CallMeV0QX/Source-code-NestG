using System;

namespace app_desktop.Services.CloudSave;

internal sealed class CloudSaveRestorePreflightException : InvalidOperationException
{
	public CloudSaveRestorePreflightException(string message, Exception cause)
		: base(message, cause)
	{
	}
}
