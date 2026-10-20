using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace app_desktop.Services.Fix54;

internal sealed class Fix54SuspendedProcess : IDisposable
{
	private struct SecurityAttributes
	{
		internal int Length;

		internal nint Descriptor;

		[MarshalAs(UnmanagedType.Bool)]
		internal bool Inherit;
	}

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
	private struct StartupInfo
	{
		internal int Size;

		internal nint Reserved;

		internal nint Desktop;

		internal nint Title;

		internal uint X;

		internal uint Y;

		internal uint Width;

		internal uint Height;

		internal uint XChars;

		internal uint YChars;

		internal uint Fill;

		internal uint Flags;

		internal ushort ShowWindow;

		internal ushort ReservedSize;

		internal nint ReservedPointer;

		internal nint Input;

		internal nint Output;

		internal nint Error;
	}

	private struct StartupInfoEx
	{
		internal StartupInfo Startup;

		internal nint Attributes;
	}

	private struct ProcessInformation
	{
		internal nint Process;

		internal nint Thread;

		internal uint ProcessId;

		internal uint ThreadId;
	}

	private readonly SafeProcessHandle nativeProcess;

	internal Process Process { get; }

	internal StreamReader StandardOutput { get; }

	internal StreamReader StandardError { get; }

	private Fix54SuspendedProcess(Process process, SafeProcessHandle nativeProcess, SafeFileHandle stdout, SafeFileHandle stderr)
	{
		Process = process;
		this.nativeProcess = nativeProcess;
		StandardOutput = new StreamReader(new FileStream(stdout, FileAccess.Read, 4096, isAsync: false), Encoding.UTF8);
		StandardError = new StreamReader(new FileStream(stderr, FileAccess.Read, 4096, isAsync: false), Encoding.UTF8);
	}

	internal static Fix54SuspendedProcess Start(ProcessStartInfo info, Fix54ProcessJob job)
	{
		SecurityAttributes securityAttributes = default(SecurityAttributes);
		securityAttributes.Length = Marshal.SizeOf<SecurityAttributes>();
		securityAttributes.Inherit = true;
		SecurityAttributes attributes = securityAttributes;
		SafeFileHandle read = null;
		SafeFileHandle write = null;
		SafeFileHandle read2 = null;
		SafeFileHandle write2 = null;
		SafeFileHandle read3 = null;
		SafeFileHandle write3 = null;
		nint num = IntPtr.Zero;
		nint num2 = IntPtr.Zero;
		nint num3 = IntPtr.Zero;
		bool flag = false;
		SafeProcessHandle safeProcessHandle = null;
		SafeFileHandle safeFileHandle = null;
		try
		{
			if (!CreatePipe(out read, out write, ref attributes, 0u) || !CreatePipe(out read2, out write2, ref attributes, 0u) || !CreatePipe(out read3, out write3, ref attributes, 0u) || !SetHandleInformation(read, 1u, 0u) || !SetHandleInformation(read2, 1u, 0u))
			{
				throw new Fix54Exception("Không tạo được kênh tiến trình FIX54.");
			}
			nint size = IntPtr.Zero;
			InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref size);
			num = Marshal.AllocHGlobal(size);
			if (!InitializeProcThreadAttributeList(num, 1, 0, ref size))
			{
				throw new Fix54Exception("Không tạo được giới hạn handle FIX54.");
			}
			flag = true;
			num2 = Marshal.AllocHGlobal(3 * IntPtr.Size);
			Marshal.WriteIntPtr(num2, 0, read3.DangerousGetHandle());
			Marshal.WriteIntPtr(num2, IntPtr.Size, write.DangerousGetHandle());
			Marshal.WriteIntPtr(num2, 2 * IntPtr.Size, write2.DangerousGetHandle());
			if (!UpdateProcThreadAttribute(num, 0u, 131074, num2, 3 * IntPtr.Size, IntPtr.Zero, IntPtr.Zero))
			{
				throw new Fix54Exception("Không giới hạn được handle kế thừa FIX54.");
			}
			StartupInfoEx startupInfoEx = default(StartupInfoEx);
			startupInfoEx.Attributes = num;
			StartupInfoEx startup = startupInfoEx;
			startup.Startup.Size = Marshal.SizeOf<StartupInfoEx>();
			startup.Startup.Flags = 256u;
			startup.Startup.Input = read3.DangerousGetHandle();
			startup.Startup.Output = write.DangerousGetHandle();
			startup.Startup.Error = write2.DangerousGetHandle();
			num3 = Marshal.StringToHGlobalUni(string.Join('\0', from pair in info.Environment.OrderBy<KeyValuePair<string, string>, string>((KeyValuePair<string, string> pair) => pair.Key, StringComparer.OrdinalIgnoreCase)
				select pair.Key + "=" + pair.Value) + "\0\0");
			StringBuilder stringBuilder = new StringBuilder(Quote(info.FileName));
			foreach (string argument in info.ArgumentList)
			{
				stringBuilder.Append(' ').Append(Quote(argument));
			}
			if (!CreateProcess(info.FileName, stringBuilder, IntPtr.Zero, IntPtr.Zero, inherit: true, 134743044u, num3, info.WorkingDirectory, ref startup, out var information))
			{
				throw new Fix54Exception("Không mở được PowerShell riêng cho FIX54.");
			}
			safeProcessHandle = new SafeProcessHandle(information.Process, ownsHandle: true);
			safeFileHandle = new SafeFileHandle(information.Thread, ownsHandle: true);
			Fix54SuspendedProcess fix54SuspendedProcess = null;
			try
			{
				job.Attach(safeProcessHandle.DangerousGetHandle());
				fix54SuspendedProcess = new Fix54SuspendedProcess(System.Diagnostics.Process.GetProcessById(checked((int)information.ProcessId)), safeProcessHandle, read, read2);
				if (ResumeThread(safeFileHandle) == uint.MaxValue)
				{
					throw new Fix54Exception("Không tiếp tục được tiến trình FIX54.");
				}
				safeProcessHandle = null;
				read = null;
				read2 = null;
				return fix54SuspendedProcess;
			}
			catch
			{
				if (safeProcessHandle == null)
				{
					fix54SuspendedProcess?.Dispose();
					throw new Fix54ProcessStillRunningException();
				}
				bool num4 = TerminateProcess(safeProcessHandle, 1u) && WaitForSingleObject(safeProcessHandle, 15000u) == 0;
				fix54SuspendedProcess?.Dispose();
				if (!num4)
				{
					throw new Fix54ProcessStillRunningException();
				}
				throw;
			}
		}
		finally
		{
			read?.Dispose();
			write?.Dispose();
			read2?.Dispose();
			write2?.Dispose();
			read3?.Dispose();
			write3?.Dispose();
			safeProcessHandle?.Dispose();
			safeFileHandle?.Dispose();
			if (flag)
			{
				DeleteProcThreadAttributeList(num);
			}
			if (num != IntPtr.Zero)
			{
				Marshal.FreeHGlobal(num);
			}
			if (num2 != IntPtr.Zero)
			{
				Marshal.FreeHGlobal(num2);
			}
			if (num3 != IntPtr.Zero)
			{
				Marshal.FreeHGlobal(num3);
			}
		}
	}

	internal static string Quote(string value)
	{
		StringBuilder stringBuilder = new StringBuilder("\"");
		int num = 0;
		foreach (char c in value)
		{
			switch (c)
			{
			case '\\':
				num++;
				continue;
			case '"':
				stringBuilder.Append('\\', num * 2 + 1).Append(c);
				break;
			default:
				stringBuilder.Append('\\', num).Append(c);
				break;
			}
			num = 0;
		}
		return stringBuilder.Append('\\', num * 2).Append('"').ToString();
	}

	public void Dispose()
	{
		StandardOutput.Dispose();
		StandardError.Dispose();
		Process.Dispose();
		nativeProcess.Dispose();
	}

	[DllImport("kernel32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool CreatePipe(out SafeFileHandle read, out SafeFileHandle write, ref SecurityAttributes attributes, uint size);

	[DllImport("kernel32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool SetHandleInformation(SafeFileHandle handle, uint mask, uint flags);

	[DllImport("kernel32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool InitializeProcThreadAttributeList(nint list, int count, int flags, ref nint size);

	[DllImport("kernel32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool UpdateProcThreadAttribute(nint list, uint flags, nint attribute, nint value, nint size, nint previous, nint returnSize);

	[DllImport("kernel32.dll")]
	private static extern void DeleteProcThreadAttributeList(nint list);

	[DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool CreateProcess(string application, StringBuilder commandLine, nint processSecurity, nint threadSecurity, [MarshalAs(UnmanagedType.Bool)] bool inherit, uint flags, nint environment, string directory, ref StartupInfoEx startup, out ProcessInformation information);

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern uint ResumeThread(SafeFileHandle thread);

	[DllImport("kernel32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool TerminateProcess(SafeProcessHandle process, uint exitCode);

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern uint WaitForSingleObject(SafeProcessHandle handle, uint milliseconds);
}
