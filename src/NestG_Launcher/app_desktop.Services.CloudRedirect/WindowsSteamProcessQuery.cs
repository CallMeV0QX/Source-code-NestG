using System;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace app_desktop.Services.CloudRedirect;

internal static class WindowsSteamProcessQuery
{
	private const uint QueryLimitedInformation = 4096u;

	private const uint StillActive = 259u;

	internal static SteamProcessDetails Read(int processId)
	{
		using SafeProcessHandle safeProcessHandle = OpenProcess(4096u, inherit: false, processId);
		if (safeProcessHandle.IsInvalid)
		{
			return Unknown("OpenProcess");
		}
		return Read(safeProcessHandle);
	}

	internal static SteamProcessDetails Read(SafeProcessHandle handle)
	{
		if (!GetExitCodeProcess(handle, out var exitCode))
		{
			return Unknown("GetExitCodeProcess");
		}
		if (exitCode != 259)
		{
			return new SteamProcessDetails(SteamProcessState.Stopped);
		}
		StringBuilder stringBuilder = new StringBuilder(32768);
		int length = stringBuilder.Capacity;
		if (!QueryFullProcessImageName(handle, 0u, stringBuilder, ref length))
		{
			return Unknown("QueryFullProcessImageName");
		}
		if (!GetProcessTimes(handle, out var created, out var _, out var _, out var _))
		{
			return Unknown("GetProcessTimes");
		}
		if (!GetExitCodeProcess(handle, out exitCode))
		{
			return Unknown("GetExitCodeProcess");
		}
		if (exitCode != 259)
		{
			return new SteamProcessDetails(SteamProcessState.Stopped);
		}
		return new SteamProcessDetails(SteamProcessState.Running, stringBuilder.ToString(), DateTime.FromFileTimeUtc(created));
	}

	private static SteamProcessDetails Unknown(string stage)
	{
		int lastWin32Error = Marshal.GetLastWin32Error();
		return new SteamProcessDetails(SteamProcessState.Unknown, null, default(DateTime), stage, lastWin32Error);
	}

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern SafeProcessHandle OpenProcess(uint access, [MarshalAs(UnmanagedType.Bool)] bool inherit, int processId);

	[DllImport("kernel32.dll", CharSet = CharSet.Unicode, EntryPoint = "QueryFullProcessImageNameW", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool QueryFullProcessImageName(SafeProcessHandle process, uint flags, StringBuilder path, ref int length);

	[DllImport("kernel32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool GetProcessTimes(SafeProcessHandle process, out long created, out long exited, out long kernel, out long user);

	[DllImport("kernel32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool GetExitCodeProcess(SafeProcessHandle process, out uint exitCode);
}
