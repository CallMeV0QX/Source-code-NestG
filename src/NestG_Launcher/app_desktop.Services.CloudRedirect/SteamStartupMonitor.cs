using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace app_desktop.Services.CloudRedirect;

internal sealed class SteamStartupMonitor
{
	private static readonly TimeSpan StablePeriod = TimeSpan.FromSeconds(5.0);

	private SteamProcessObservation? previous;

	private TimeSpan stateSince;

	internal SteamStartupResult? Observe(SteamProcessObservation process, CloudRedirectHealthResult? health, TimeSpan elapsed, bool deadlineReached, bool requireCloudHealth, BetterSteamToolsHealth? betterHealth = null, bool requireBetterHealth = false)
	{
		if (previous != process)
		{
			stateSince = elapsed;
			previous = process;
		}
		bool flag = process.State == SteamProcessState.Running && (object)process.Session != null;
		bool flag2 = elapsed - stateSince >= StablePeriod;
		if (flag && flag2 && (!requireCloudHealth || ((object)health != null && health.IsReady)) && (!requireBetterHealth || ((object)betterHealth != null && betterHealth.IsReady)) && ((object)betterHealth == null || !betterHealth.CoreFailed))
		{
			return new SteamStartupResult(IsRunning: true, StartupFailed: false, health, process.Session, "Đã xác nhận Steam đang chạy sau khi khởi động.")
			{
				BetterRuntimeReady = (betterHealth?.IsReady ?? false)
			};
		}
		if ((object)betterHealth != null && betterHealth.CoreFailed && process.State != SteamProcessState.Unknown)
		{
			return new SteamStartupResult(flag, StartupFailed: false, health, process.Session, "BetterSteamTools không thể khởi tạo.")
			{
				CoreInitializationFailed = true
			};
		}
		if ((object)health != null && health.IsDefinitiveFailure && process.State != SteamProcessState.Unknown)
		{
			return new SteamStartupResult(flag, StartupFailed: false, health, process.Session, health.Message)
			{
				BetterRuntimeReady = (betterHealth?.IsReady ?? false),
				CoreInitializationFailed = (betterHealth?.CoreFailed ?? false)
			};
		}
		if (!deadlineReached)
		{
			return null;
		}
		bool flag3 = process.State == SteamProcessState.Stopped && flag2;
		return new SteamStartupResult(flag && flag2, flag3, health, process.Session, flag3 ? "Steam không chạy sau khi mở lại; có thể đã thoát hoặc gặp lỗi trước khi tạo log Cloud Save." : "Chưa xác nhận được Steam và Cloud Save đã sẵn sàng; launcher giữ nguyên dữ liệu khi chưa đủ bằng chứng lỗi.")
		{
			ProcessStateUnknown = (process.State == SteamProcessState.Unknown || (process.State == SteamProcessState.Running && (object)process.Session == null)),
			BetterRuntimeReady = (betterHealth?.IsReady ?? false),
			CoreInitializationFailed = (betterHealth?.CoreFailed ?? false)
		};
	}

	internal static async Task<SteamStartupResult> WaitAsync(string steamDirectory, CloudRedirectHealthMarker? marker, CancellationToken cancellationToken, TimeSpan? timeout = null, Action<SteamProcessObservation, TimeSpan>? observation = null, BetterSteamToolsLogMarker? betterMarker = null, bool requireBetterHealth = false)
	{
		SteamStartupMonitor monitor = new SteamStartupMonitor();
		Stopwatch stopwatch = Stopwatch.StartNew();
		CloudRedirectSteamSession previousSession = null;
		SteamStartupResult steamStartupResult;
		while (true)
		{
			cancellationToken.ThrowIfCancellationRequested();
			SteamProcessObservation steamProcessObservation = InspectProcess(steamDirectory);
			CloudRedirectSteamSession session = steamProcessObservation.Session;
			if ((object)session != null)
			{
				if ((object)previousSession != null && previousSession != session && (object)marker != null)
				{
					marker = CloudRedirectRuntimeService.CaptureHealthMarker(steamDirectory);
				}
				previousSession = session;
			}
			CloudRedirectHealthResult cloudRedirectHealthResult = null;
			object obj;
			if ((object)betterMarker != null)
			{
				CloudRedirectSteamSession session2 = steamProcessObservation.Session;
				if ((object)session2 != null)
				{
					obj = BetterSteamToolsHealthReader.Inspect(steamDirectory, betterMarker, session2);
					goto IL_00ae;
				}
			}
			obj = null;
			goto IL_00ae;
			IL_00ae:
			BetterSteamToolsHealth betterSteamToolsHealth = (BetterSteamToolsHealth)obj;
			steamProcessObservation = steamProcessObservation with
			{
				RuntimeStartedAtUtc = betterSteamToolsHealth?.InitializationStartedAtUtc
			};
			try
			{
				observation?.Invoke(steamProcessObservation, stopwatch.Elapsed);
			}
			catch
			{
			}
			if ((object)marker != null)
			{
				try
				{
					cloudRedirectHealthResult = CloudRedirectRuntimeService.InspectHealthLog(marker);
				}
				catch (Exception ex) when (((ex is IOException || ex is UnauthorizedAccessException) ? 1 : 0) != 0)
				{
					cloudRedirectHealthResult = new CloudRedirectHealthResult(IsReady: false, IsDefinitiveFailure: false, marker.LogPath, "Không đọc được log Cloud Save; chưa thể xác nhận hoạt động.");
				}
				if (requireBetterHealth && ((object)betterSteamToolsHealth == null || !betterSteamToolsHealth.CloudReady) && cloudRedirectHealthResult.IsReady)
				{
					cloudRedirectHealthResult = new CloudRedirectHealthResult(IsReady: false, IsDefinitiveFailure: false, marker.LogPath, "Chưa xác nhận CloudRedirect trong phiên Steam hiện tại.");
				}
				if ((object)betterSteamToolsHealth != null && betterSteamToolsHealth.CloudFailed)
				{
					cloudRedirectHealthResult = new CloudRedirectHealthResult(IsReady: false, IsDefinitiveFailure: true, marker.LogPath, "CloudRedirect initialization failed.");
				}
			}
			steamStartupResult = monitor.Observe(steamProcessObservation, cloudRedirectHealthResult, stopwatch.Elapsed, stopwatch.Elapsed >= (timeout ?? TimeSpan.FromSeconds(60.0)), (object)marker != null, betterSteamToolsHealth, requireBetterHealth);
			if ((object)steamStartupResult != null)
			{
				break;
			}
			await Task.Delay(500, cancellationToken);
		}
		return steamStartupResult;
	}

	internal static SteamProcessObservation InspectProcess(string steamDirectory)
	{
		return SteamProcessInspector.Capture(steamDirectory).Observation;
	}
}
