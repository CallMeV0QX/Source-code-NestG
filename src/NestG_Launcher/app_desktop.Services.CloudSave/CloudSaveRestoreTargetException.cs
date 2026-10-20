using System;

namespace app_desktop.Services.CloudSave;

internal sealed class CloudSaveRestoreTargetException : InvalidOperationException
{
	public CloudSaveRestoreTargetException(string message, Exception cause)
		: base(message, cause)
	{
	}
}
