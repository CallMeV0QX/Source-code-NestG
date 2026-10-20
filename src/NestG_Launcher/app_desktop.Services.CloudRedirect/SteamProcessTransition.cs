using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace app_desktop.Services.CloudRedirect;

internal sealed class SteamProcessTransition
{
	internal static readonly TimeSpan StablePeriod = TimeSpan.FromSeconds(1.0);

	private SteamProcessSnapshot? previous;

	private TimeSpan stateSince;

	internal SteamProcessSnapshot? Observe(SteamProcessSnapshot snapshot, TimeSpan elapsed, bool deadlineReached)
	{
		if ((object)previous == null || previous.State != snapshot.State || !previous.Sessions.OrderBy((CloudRedirectSteamSession s) => s.ProcessId).SequenceEqual(snapshot.Sessions.OrderBy((CloudRedirectSteamSession s) => s.ProcessId)))
		{
			stateSince = elapsed;
		}
		previous = snapshot;
		if (snapshot.State != SteamProcessState.Unknown && elapsed - stateSince >= StablePeriod)
		{
			return snapshot;
		}
		if (!deadlineReached)
		{
			return null;
		}
		return new SteamProcessSnapshot(SteamProcessState.Unknown, Array.Empty<CloudRedirectSteamSession>());
	}

	internal static async Task<SteamProcessSnapshot> WaitAsync(string steamDirectory, CancellationToken cancellationToken, string operationId, string stage)
	{
		SteamProcessTransition monitor = new SteamProcessTransition();
		Stopwatch clock = Stopwatch.StartNew();
		TimeSpan timeout = TimeSpan.FromSeconds(30.0);
		DesktopDiagnosticLog.Write("SteamProcess", "TransitionWait", $"Operation={operationId}; Stage={stage}; TimeoutMs={timeout.TotalMilliseconds}");
		SteamProcessSnapshot steamProcessSnapshot;
		while (true)
		{
			cancellationToken.ThrowIfCancellationRequested();
			SteamProcessSnapshot snapshot = SteamProcessInspector.Capture(steamDirectory);
			steamProcessSnapshot = monitor.Observe(snapshot, clock.Elapsed, clock.Elapsed >= timeout);
			if ((object)steamProcessSnapshot != null)
			{
				break;
			}
			await Task.Delay(TimeSpan.FromMilliseconds(250.0), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
		DesktopDiagnosticLog.Write("SteamProcess", "TransitionObserved", $"Operation={operationId}; Stage={stage}; State={steamProcessSnapshot.State}; ElapsedMs={clock.ElapsedMilliseconds}; Pids={string.Join(',', steamProcessSnapshot.Sessions.Select((CloudRedirectSteamSession s) => s.ProcessId))}");
		return steamProcessSnapshot;
	}
}
