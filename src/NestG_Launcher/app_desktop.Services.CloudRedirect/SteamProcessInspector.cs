using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;

namespace app_desktop.Services.CloudRedirect;

internal static class SteamProcessInspector
{
	private sealed class Handle(Process process) : ISteamProcessHandle, IDisposable
	{
		public int Id => process.Id;

		public void Dispose()
		{
			process.Dispose();
		}
	}

	internal const string PendingMessage = "Chưa xác minh được trạng thái Steam. Launcher giữ nguyên file; hãy kiểm tra Steam rồi thử lại.";

	internal static SteamProcessSnapshot Capture(string steamDirectory, Func<IReadOnlyList<ISteamProcessHandle>>? enumerate = null)
	{
		IReadOnlyList<ISteamProcessHandle> readOnlyList = Array.Empty<ISteamProcessHandle>();
		try
		{
			readOnlyList = (enumerate ?? new Func<IReadOnlyList<ISteamProcessHandle>>(Enumerate))();
			CloudRedirectSteamSession[] array = (from pid in readOnlyList.Select((ISteamProcessHandle p) => p.Id).Distinct().Order()
				select new CloudRedirectSteamSession(pid, default(DateTime))).ToArray();
			return new SteamProcessSnapshot((array.Length != 0) ? SteamProcessState.Running : SteamProcessState.Stopped, array);
		}
		catch (Exception ex) when (((ex is Win32Exception || ex is InvalidOperationException || ex is UnauthorizedAccessException || ex is ArgumentException) ? 1 : 0) != 0)
		{
			DesktopDiagnosticLog.Write("SteamProcess", "EnumerationUnavailable", $"Error={ex.GetType().Name}; NativeError={(ex as Win32Exception)?.NativeErrorCode}");
			return new SteamProcessSnapshot(SteamProcessState.Unknown, Array.Empty<CloudRedirectSteamSession>());
		}
		finally
		{
			foreach (ISteamProcessHandle item in readOnlyList)
			{
				item.Dispose();
			}
		}
	}

	private static IReadOnlyList<ISteamProcessHandle> Enumerate()
	{
		return ((IEnumerable<Process>)Process.GetProcessesByName("steam")).Select((Func<Process, ISteamProcessHandle>)((Process process) => new Handle(process))).ToArray();
	}
}
