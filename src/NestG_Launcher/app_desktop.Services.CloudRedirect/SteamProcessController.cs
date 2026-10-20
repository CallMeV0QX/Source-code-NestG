using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace app_desktop.Services.CloudRedirect;

internal static class SteamProcessController
{
	internal static async Task<SteamCloseResult> CloseAsync(string steamDirectory, CancellationToken cancellationToken, string operationId, Func<SteamProcessSnapshot>? capture = null, Func<bool>? requestShutdown = null, Func<TimeSpan>? elapsed = null, Func<CancellationToken, Task>? delay = null)
	{
		Stopwatch watch = Stopwatch.StartNew();
		if (capture == null)
		{
			capture = () => SteamProcessInspector.Capture(steamDirectory);
		}
		if (elapsed == null)
		{
			elapsed = () => watch.Elapsed;
		}
		if (delay == null)
		{
			delay = (CancellationToken token) => Task.Delay(500, token);
		}
		SteamProcessTransition transition = new SteamProcessTransition();
		bool requested = false;
		SteamProcessSnapshot steamProcessSnapshot;
		TimeSpan timeSpan;
		while (true)
		{
			cancellationToken.ThrowIfCancellationRequested();
			steamProcessSnapshot = capture();
			timeSpan = elapsed();
			SteamProcessSnapshot? steamProcessSnapshot2 = transition.Observe(steamProcessSnapshot, timeSpan, deadlineReached: false);
			if ((object)steamProcessSnapshot2 != null && steamProcessSnapshot2.State == SteamProcessState.Stopped)
			{
				DesktopDiagnosticLog.Write("SteamProcess", "Closed", $"Operation={operationId}; ElapsedMs={timeSpan.TotalMilliseconds}");
				return new SteamCloseResult(IsClosed: true, SteamProcessState.Stopped, "Đã xác nhận Steam đóng.");
			}
			if (timeSpan >= TimeSpan.FromSeconds(30.0))
			{
				break;
			}
			if (steamProcessSnapshot.State == SteamProcessState.Running && !requested)
			{
				try
				{
					if (!(requestShutdown ?? ((Func<bool>)(() => RequestShutdown(steamDirectory))))())
					{
						return new SteamCloseResult(IsClosed: false, steamProcessSnapshot.State, "Chưa gửi được yêu cầu đóng Steam. Hãy đóng Steam rồi thử lại.");
					}
					requested = true;
					DesktopDiagnosticLog.Write("SteamProcess", "ShutdownRequested", "Operation=" + operationId);
				}
				catch (Exception ex) when (((ex is Win32Exception || ex is InvalidOperationException || ex is IOException || ex is UnauthorizedAccessException) ? 1 : 0) != 0)
				{
					DesktopDiagnosticLog.Write("SteamProcess", "ShutdownDeferred", "Operation=" + operationId + "; Error=" + ex.GetType().Name);
					return new SteamCloseResult(IsClosed: false, steamProcessSnapshot.State, "Chưa gửi được yêu cầu đóng Steam. Hãy đóng Steam rồi thử lại.");
				}
			}
			await delay(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
		DesktopDiagnosticLog.Write("SteamProcess", "ShutdownTimedOut", $"Operation={operationId}; State={steamProcessSnapshot.State}; ElapsedMs={timeSpan.TotalMilliseconds}; Pids={string.Join(',', steamProcessSnapshot.Sessions.Select((CloudRedirectSteamSession s) => s.ProcessId))}");
		return Deferred(steamProcessSnapshot.State);
	}

	internal static async Task<SteamMutationResult<T>> MutateAsync<T>(string steamDirectory, CancellationToken cancellationToken, string operationId, Func<T> mutation, Func<Task<SteamCloseResult>>? close = null, Func<SteamProcessSnapshot>? capture = null)
	{
		SteamCloseResult closure = await (close?.Invoke() ?? CloseAsync(steamDirectory, cancellationToken, operationId)).ConfigureAwait(continueOnCapturedContext: false);
		if (!closure.IsClosed)
		{
			return new SteamMutationResult<T>(default(T), closure);
		}
		return await Task.Run(delegate
		{
			cancellationToken.ThrowIfCancellationRequested();
			SteamProcessState state = (capture ?? ((Func<SteamProcessSnapshot>)(() => SteamProcessInspector.Capture(steamDirectory))))().State;
			return (state != 0) ? new SteamMutationResult<T>(default(T), Deferred(state)) : new SteamMutationResult<T>(mutation(), closure);
		}, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
	}

	internal static SteamCloseResult Deferred(SteamProcessState state)
	{
		return new SteamCloseResult(IsClosed: false, state, (state == SteamProcessState.Running) ? "Steam vẫn đang chạy; launcher chưa tiếp tục ghi file. Hãy đóng Steam rồi bấm ACTIVE lại." : "Chưa xác minh được trạng thái Steam. Launcher giữ nguyên file; hãy kiểm tra Steam rồi thử lại.");
	}

	private static bool RequestShutdown(string steamDirectory)
	{
		using (Process.Start(new ProcessStartInfo
		{
			FileName = Path.Combine(steamDirectory, "steam.exe"),
			Arguments = "-shutdown",
			WorkingDirectory = steamDirectory,
			UseShellExecute = true
		}))
		{
			return true;
		}
	}
}
