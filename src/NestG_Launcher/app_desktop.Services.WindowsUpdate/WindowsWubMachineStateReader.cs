using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace app_desktop.Services.WindowsUpdate;

internal sealed class WindowsWubMachineStateReader : IWubMachineStateReader
{
	private struct ServiceStatus
	{
		internal uint ServiceType;

		internal uint CurrentState;

		internal uint ControlsAccepted;

		internal uint Win32ExitCode;

		internal uint ServiceSpecificExitCode;

		internal uint CheckPoint;

		internal uint WaitHint;
	}

	public Task<WubMachineState> ReadAsync(CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		try
		{
			RegistryKey machine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
			try
			{
				int? num = ReadStart(machine, "wuauserv", required: true);
				int?[] source = new string[3] { "dosvc", "WaaSMedicSvc", "InstallService" }.Select((string name) => ReadStart(machine, name, required: false)).ToArray();
				bool value = source.All((int? start) => (!start.HasValue || start == 4) ? true : false);
				bool value2 = source.All((int? start) => !start.HasValue || start != 4);
				using RegistryKey registryKey = machine.OpenSubKey("SOFTWARE\\Policies\\Microsoft\\Windows\\WindowsUpdate\\AU");
				bool value3 = registryKey?.GetValue("NoAutoUpdate") is int num2 && num2 == 1;
				return Task.FromResult(new WubMachineState(num == 4, QueryCoreServiceStopped(), value3, value, value2));
			}
			finally
			{
				if (machine != null)
				{
					((IDisposable)machine).Dispose();
				}
			}
		}
		catch (Exception ex) when (((ex is IOException || ex is InvalidDataException || ex is UnauthorizedAccessException || ex is SecurityException || ex is InvalidCastException) ? 1 : 0) != 0)
		{
			return Task.FromResult(new WubMachineState(null, null, null, null, null));
		}
	}

	private static int? ReadStart(RegistryKey machine, string name, bool required)
	{
		using RegistryKey registryKey = machine.OpenSubKey("SYSTEM\\CurrentControlSet\\Services\\" + name);
		if (registryKey == null)
		{
			if (required)
			{
				throw new InvalidDataException("Không tìm thấy dịch vụ Windows Update.");
			}
			return null;
		}
		if (!(registryKey.GetValue("Start") is int value))
		{
			throw new InvalidDataException("Không đọc được trạng thái dịch vụ Windows Update.");
		}
		return value;
	}

	private static bool? QueryCoreServiceStopped()
	{
		nint num = OpenSCManager(null, null, 1u);
		if (num == IntPtr.Zero)
		{
			return null;
		}
		try
		{
			nint num2 = OpenService(num, "wuauserv", 4u);
			if (num2 == IntPtr.Zero)
			{
				return null;
			}
			try
			{
				ServiceStatus status;
				return QueryServiceStatus(num2, out status) ? new bool?(status.CurrentState == 1) : ((bool?)null);
			}
			finally
			{
				CloseServiceHandle(num2);
			}
		}
		finally
		{
			CloseServiceHandle(num);
		}
	}

	[DllImport("advapi32.dll", CharSet = CharSet.Unicode, EntryPoint = "OpenSCManagerW", SetLastError = true)]
	private static extern nint OpenSCManager(string? machineName, string? databaseName, uint desiredAccess);

	[DllImport("advapi32.dll", CharSet = CharSet.Unicode, EntryPoint = "OpenServiceW", SetLastError = true)]
	private static extern nint OpenService(nint manager, string serviceName, uint desiredAccess);

	[DllImport("advapi32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool QueryServiceStatus(nint service, out ServiceStatus status);

	[DllImport("advapi32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool CloseServiceHandle(nint handle);
}
