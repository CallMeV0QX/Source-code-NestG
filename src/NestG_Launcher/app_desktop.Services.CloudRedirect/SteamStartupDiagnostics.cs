using System;
using System.Diagnostics;

namespace app_desktop.Services.CloudRedirect;

internal sealed class SteamStartupDiagnostics(string operationId, Action<string, string>? write = null) : IDisposable
{
	private SteamProcessObservation? previous;

	private int remaining = 64;

	internal void TrackLaunch(Process process)
	{
		try
		{
			Emit("StartRequested", $"PID={process.Id}");
		}
		finally
		{
			process.Dispose();
		}
	}

	internal void Observe(SteamProcessObservation observation, TimeSpan elapsed)
	{
		if (!(observation == previous))
		{
			previous = observation;
			Emit("ProcessState", $"ElapsedMs={(long)elapsed.TotalMilliseconds}; State={observation.State}; PID={observation.Session?.ProcessId}; RuntimeInitUtc={observation.RuntimeStartedAtUtc:O}");
		}
	}

	private void Emit(string name, string details)
	{
		if (remaining-- <= 0)
		{
			return;
		}
		try
		{
			if (write != null)
			{
				write(name, "Operation=" + operationId + "; " + details);
			}
			else
			{
				DesktopDiagnosticLog.Write("SteamStartup", name, "Operation=" + operationId + "; " + details);
			}
		}
		catch
		{
		}
	}

	public void Dispose()
	{
	}
}
