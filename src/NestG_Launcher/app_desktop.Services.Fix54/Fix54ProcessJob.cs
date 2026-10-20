using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

namespace app_desktop.Services.Fix54;

internal sealed class Fix54ProcessJob : IDisposable
{
	private struct BasicLimits
	{
		internal long PerProcessUserTime;

		internal long PerJobUserTime;

		internal uint Flags;

		internal nuint MinimumWorkingSet;

		internal nuint MaximumWorkingSet;

		internal uint ActiveProcessLimit;

		internal nuint Affinity;

		internal uint PriorityClass;

		internal uint SchedulingClass;
	}

	private struct IoCounters
	{
		internal ulong ReadOperations;

		internal ulong WriteOperations;

		internal ulong OtherOperations;

		internal ulong ReadBytes;

		internal ulong WriteBytes;

		internal ulong OtherBytes;
	}

	private struct ExtendedLimits
	{
		internal BasicLimits Basic;

		internal IoCounters Io;

		internal nuint ProcessMemoryLimit;

		internal nuint JobMemoryLimit;

		internal nuint PeakProcessMemory;

		internal nuint PeakJobMemory;
	}

	private struct Accounting
	{
		internal long TotalUserTime;

		internal long TotalKernelTime;

		internal long CurrentUserTime;

		internal long CurrentKernelTime;

		internal uint PageFaults;

		internal uint TotalProcesses;

		internal uint ActiveProcesses;

		internal uint TerminatedProcesses;
	}

	private readonly SafeFileHandle job;

	internal uint ActiveProcesses
	{
		get
		{
			if (!QueryInformationJobObject(job, 1, out var information, (uint)Marshal.SizeOf<Accounting>(), IntPtr.Zero))
			{
				throw new Fix54ProcessStillRunningException();
			}
			return information.ActiveProcesses;
		}
	}

	internal Fix54ProcessJob()
	{
		job = CreateJobObject(IntPtr.Zero, null);
		if (job.IsInvalid)
		{
			throw new Fix54Exception("Không tạo được vùng tiến trình riêng cho FIX54.");
		}
		ExtendedLimits information = new ExtendedLimits
		{
			Basic = new BasicLimits
			{
				Flags = 8192u
			}
		};
		if (!SetInformationJobObject(job, 9, ref information, (uint)Marshal.SizeOf<ExtendedLimits>()))
		{
			job.Dispose();
			throw new Fix54Exception("Không cấu hình được giới hạn tiến trình FIX54.");
		}
	}

	internal void Attach(nint process)
	{
		if (!AssignProcessToJobObject(job, process))
		{
			throw new Fix54Exception("Không gắn được PowerShell vào vùng tiến trình FIX54.");
		}
	}

	internal async Task StopAndConfirmAsync(Process process)
	{
		if (!TerminateJobObject(job, 1u))
		{
			throw new Fix54ProcessStillRunningException();
		}
		using CancellationTokenSource deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15.0));
		_ = 1;
		try
		{
			await process.WaitForExitAsync(deadline.Token);
			while (ActiveProcesses != 0)
			{
				await Task.Delay(50, deadline.Token);
			}
		}
		catch (OperationCanceledException)
		{
			throw new Fix54ProcessStillRunningException();
		}
	}

	public void Dispose()
	{
		job.Dispose();
	}

	[DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
	private static extern SafeFileHandle CreateJobObject(nint attributes, string? name);

	[DllImport("kernel32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool SetInformationJobObject(SafeFileHandle job, int infoClass, ref ExtendedLimits information, uint length);

	[DllImport("kernel32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool AssignProcessToJobObject(SafeFileHandle job, nint process);

	[DllImport("kernel32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool QueryInformationJobObject(SafeFileHandle job, int infoClass, out Accounting information, uint length, nint returnLength);

	[DllImport("kernel32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool TerminateJobObject(SafeFileHandle job, uint exitCode);
}
