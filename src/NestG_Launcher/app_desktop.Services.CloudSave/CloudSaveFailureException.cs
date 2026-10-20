using System;

namespace app_desktop.Services.CloudSave;

internal sealed class CloudSaveFailureException : InvalidOperationException
{
	internal CloudSaveProgressStage Stage { get; }

	internal bool IsRestore { get; }

	public CloudSaveFailureException(CloudSaveProgressStage stage, Exception cause, bool isRestore = false)
		: base("Cloud Save failed.", cause)
	{
		Stage = stage;
		IsRestore = isRestore;
	}
}
