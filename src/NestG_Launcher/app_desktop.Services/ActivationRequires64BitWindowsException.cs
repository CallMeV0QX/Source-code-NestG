using System;

namespace app_desktop.Services;

internal sealed class ActivationRequires64BitWindowsException : InvalidOperationException
{
	public ActivationRequires64BitWindowsException()
		: base("Bộ kích hoạt yêu cầu Windows 64-bit.")
	{
	}
}
