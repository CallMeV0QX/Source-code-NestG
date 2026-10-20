using System;

namespace app_desktop.Services;

internal sealed class InlineProgress<T>(Action<T> callback) : IProgress<T>
{
	public void Report(T value)
	{
		callback(value);
	}
}
