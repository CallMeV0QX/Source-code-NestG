using System;

namespace app_desktop.Services.CloudSave;

internal sealed class GoogleDriveResourceNotFoundException : InvalidOperationException
{
	public GoogleDriveResourceNotFoundException(string message)
		: base(message)
	{
	}
}
