using System;

namespace app_desktop.Services.CloudRedirect;

internal sealed class CloudRedirectOptionalException : InvalidOperationException
{
	internal string Stage { get; }

	public CloudRedirectOptionalException(string stage, Exception inner)
		: base("CloudRedirect component is unavailable.", inner)
	{
		Stage = stage;
	}
}
