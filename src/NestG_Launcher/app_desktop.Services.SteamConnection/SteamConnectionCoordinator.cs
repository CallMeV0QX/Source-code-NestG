using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using app_desktop.Services.Warp;

namespace app_desktop.Services.SteamConnection;

internal sealed class SteamConnectionCoordinator
{
	private static readonly Lazy<SteamConnectionCoordinator> shared = new Lazy<SteamConnectionCoordinator>(() => new SteamConnectionCoordinator(new SteamDpiRuntimeClient(), new SteamConnectionProbe(), new SteamConnectionUiService(), SteamConnectionDiagnostics.Write, ReadWarpAsync));

	private readonly object sync = new object();

	private readonly ISteamDpiRuntime runtime;

	private readonly ISteamConnectionProbe probe;

	private readonly Action<string, string> writeDiagnostic;

	private readonly Func<CancellationToken, Task<bool?>> readWarp;

	private readonly CancellationTokenSource lifetime = new CancellationTokenSource();

	private CancellationTokenSource? operationCancellation;

	private Task operation = Task.CompletedTask;

	private SteamConnectionUiState state;

	private long generation;

	private long networkRevision;

	private bool initialized;

	private bool closing;

	private bool requestedEnabled;

	private CancellationTokenSource? statusCancellation;

	private Task? shutdown;

	private Task? monitor;

	internal static SteamConnectionCoordinator Shared => shared.Value;

	internal static bool IsCreated => shared.IsValueCreated;

	internal SteamConnectionUiService Preferences { get; }

	internal event EventHandler? StateChanged;

	internal SteamConnectionCoordinator(ISteamDpiRuntime runtime, ISteamConnectionProbe probe, SteamConnectionUiService preferences, Action<string, string>? writeDiagnostic = null, Func<CancellationToken, Task<bool?>>? readWarp = null)
	{
		this.runtime = runtime;
		this.probe = probe;
		Preferences = preferences;
		this.writeDiagnostic = writeDiagnostic ?? ((Action<string, string>)delegate
		{
		});
		this.readWarp = readWarp ?? ((Func<CancellationToken, Task<bool?>>)((CancellationToken _) => Task.FromResult((bool?)false)));
		state = preferences.GetState()with
		{
			Kind = (runtime.IsAvailable ? "Off" : "Unavailable"),
			CanToggle = runtime.IsAvailable,
			Label = (runtime.IsAvailable ? "Hỗ trợ đang tắt" : "Thiếu bộ hỗ trợ"),
			Detail = (runtime.IsAvailable ? "Bật hỗ trợ để thử cải thiện kết nối Steam." : "Gói launcher chưa có bộ hỗ trợ Steam hợp lệ.")
		};
	}

	internal SteamConnectionUiState GetState(string? feedback = null)
	{
		lock (sync)
		{
			return state with
			{
				AutoEnable = false,
				Feedback = (feedback ?? state.Feedback)
			};
		}
	}

	internal void InitializeShell()
	{
		lock (sync)
		{
			if (!initialized && !closing)
			{
				initialized = true;
				monitor = Task.Run((Func<Task?>)MonitorAsync);
				NetworkChange.NetworkAddressChanged += HandleNetworkAddressChanged;
			}
		}
	}

	internal SteamConnectionUiState SetAutoEnable(bool enabled)
	{
		SteamConnectionUiState steamConnectionUiState = Preferences.SetAutoEnable(enabled);
		lock (sync)
		{
			state = state with
			{
				AutoEnable = steamConnectionUiState.AutoEnable,
				Feedback = steamConnectionUiState.Feedback,
				Revision = state.Revision + 1
			};
		}
		Notify();
		return GetState();
	}

	internal Task SetEnabledAsync(bool enabled)
	{
		lock (sync)
		{
			if (closing || !runtime.IsAvailable)
			{
				return Task.CompletedTask;
			}
			if (enabled && (!operation.IsCompleted || state.Enabled))
			{
				return operation;
			}
			if (!enabled && state.Kind == "Stopping")
			{
				return operation;
			}
			requestedEnabled = enabled;
			statusCancellation?.Cancel();
			Task previous = operation;
			operationCancellation?.Cancel();
			operationCancellation = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
			CancellationToken token = operationCancellation.Token;
			long current = ++generation;
			state = state with
			{
				Kind = (enabled ? "Starting" : "Stopping"),
				Busy = true,
				Label = (enabled ? "Đang bật hỗ trợ" : "Đang tắt hỗ trợ"),
				Detail = (enabled ? "Đang chuẩn bị bộ hỗ trợ. Windows có thể yêu cầu quyền quản trị." : "Đang dừng bộ hỗ trợ của NestG."),
				Feedback = null,
				Checks = new Dictionary<string, string>(),
				Reasons = new Dictionary<string, string>(),
				CheckedAt = null,
				IndependentVerified = false,
				ErrorCode = null,
				ErrorStage = null,
				OperationId = Guid.NewGuid().ToString("N"),
				Revision = state.Revision + 1
			};
			operation = Task.Run(() => (!enabled) ? StopAfterAsync(previous, current, token) : StartAsync(current, token));
		}
		Notify();
		return operation;
	}

	internal Task RefreshAsync()
	{
		lock (sync)
		{
			if (closing || !operation.IsCompleted)
			{
				return operation;
			}
			if (state.CleanupPending || (state.Enabled && !requestedEnabled))
			{
				return SetEnabledAsync(enabled: false);
			}
			statusCancellation?.Cancel();
			operationCancellation?.Cancel();
			operationCancellation = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
			CancellationToken token = operationCancellation.Token;
			long current = ++generation;
			state = state with
			{
				Kind = "Checking",
				Busy = true,
				Label = "Đang kiểm tra Steam",
				Detail = "Đang kiểm tra kết nối trực tiếp từ máy này.",
				Feedback = null,
				Checks = new Dictionary<string, string>(),
				Reasons = new Dictionary<string, string>(),
				CheckedAt = null,
				IndependentVerified = false,
				OperationId = Guid.NewGuid().ToString("N"),
				Revision = state.Revision + 1
			};
			bool enabled = requestedEnabled && state.Enabled;
			operation = Task.Run(() => (!enabled) ? ProbeAsync(current, token) : StartAsync(current, token));
		}
		Notify();
		return operation;
	}

	private async Task StartAsync(long current, CancellationToken token)
	{
		try
		{
			using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
			deadline.CancelAfter(TimeSpan.FromSeconds(150.0));
			string[] array = new string[4] { "dns-only", "z-split", "z-disorder", "z-fake" };
			foreach (string profile in array)
			{
				writeDiagnostic("start", profile);
				deadline.Token.ThrowIfCancellationRequested();
				Update(current, (SteamConnectionUiState item) => item with
				{
					Kind = "Starting",
					Label = "Đang bật hỗ trợ",
					Detail = ((profile == "dns-only") ? "Đang chuẩn bị DNS mã hóa cho Steam." : "Đang thử cấu hình DPI cho Steam."),
					Busy = true
				});
				SteamDpiRuntimeResult result = await runtime.StartAsync(profile, deadline.Token).ConfigureAwait(continueOnCapturedContext: false);
				Update(current, (SteamConnectionUiState item) => item with
				{
					Enabled = result.Running,
					DnsEnabled = result.DnsReady,
					DpiEnabled = result.DpiRunning,
					CleanupPending = result.RecoveryPending
				});
				if (!result.Running || !result.DnsReady || result.Code != "ok")
				{
					UpdateFailure(current, result.Code, result.Running, result.RecoveryPending, result.Stage, result.Cause);
					return;
				}
				Preferences.MarkRuntimeInitialized();
				Update(current, (SteamConnectionUiState item) => item with
				{
					Enabled = true,
					DnsEnabled = result.DnsReady,
					DpiEnabled = result.DpiRunning,
					CleanupPending = false,
					ErrorCode = null,
					ErrorStage = null,
					Kind = "Checking",
					Label = "Đang kiểm tra Steam",
					Detail = "DNS Steam đã bật; đang kiểm tra đường truyền hiện tại."
				});
				long network = Interlocked.Read(in networkRevision);
				bool? warpBefore = await ReadWarpSafelyAsync(deadline.Token).ConfigureAwait(continueOnCapturedContext: false);
				IReadOnlyDictionary<string, SteamConnectionProbeResult> results = await probe.CheckAsync(deadline.Token).ConfigureAwait(continueOnCapturedContext: false);
				bool? flag = await ReadWarpSafelyAsync(deadline.Token).ConfigureAwait(continueOnCapturedContext: false);
				if (network != Interlocked.Read(in networkRevision) || warpBefore != flag)
				{
					InvalidateConnectivity(operationEnded: true, current);
					if (warpBefore != flag)
					{
						NetworkChanged();
					}
					return;
				}
				if (results.Count == 3 && results.Values.All((SteamConnectionProbeResult item) => item.Result == "ok"))
				{
					PublishProbe(current, results, busy: false, flag);
					return;
				}
				PublishProbe(current, results, busy: true, flag);
			}
			SteamDpiRuntimeResult dnsOnly = await runtime.StartAsync("dns-only", deadline.Token).ConfigureAwait(continueOnCapturedContext: false);
			Update(current, (SteamConnectionUiState item) => item with
			{
				Enabled = dnsOnly.Running,
				DnsEnabled = dnsOnly.DnsReady,
				DpiEnabled = dnsOnly.DpiRunning
			});
			if (!dnsOnly.Running || !dnsOnly.DnsReady || dnsOnly.Code != "ok")
			{
				UpdateFailure(current, dnsOnly.Code, dnsOnly.Running, dnsOnly.RecoveryPending, dnsOnly.Stage, dnsOnly.Cause);
				return;
			}
			Update(current, (SteamConnectionUiState item) => item with
			{
				Enabled = true,
				DnsEnabled = true,
				DpiEnabled = dnsOnly.DpiRunning,
				Kind = "Failed",
				Busy = false,
				Label = "Steam chưa truy cập đầy đủ",
				IndependentVerified = false,
				Detail = "DNS Steam vẫn bật; các cấu hình DPI chưa cải thiện được kết nối. Bạn có thể kiểm tra lại hoặc tắt hỗ trợ.",
				CanToggle = true
			});
		}
		catch (OperationCanceledException)
		{
			await StopAfterFailureAsync(current, "cancelled").ConfigureAwait(continueOnCapturedContext: false);
		}
		catch
		{
			await StopAfterFailureAsync(current, "operation-failed").ConfigureAwait(continueOnCapturedContext: false);
		}
	}

	private async Task StopAfterFailureAsync(long current, string code)
	{
		lock (sync)
		{
			if (current != generation || closing)
			{
				return;
			}
		}
		using CancellationTokenSource deadline = new CancellationTokenSource(TimeSpan.FromSeconds(35.0));
		try
		{
			SteamDpiRuntimeResult steamDpiRuntimeResult = await runtime.StopAsync(deadline.Token).ConfigureAwait(continueOnCapturedContext: false);
			UpdateFailure(current, (steamDpiRuntimeResult.Code == "ok") ? code : steamDpiRuntimeResult.Code, steamDpiRuntimeResult.Running, steamDpiRuntimeResult.Running || steamDpiRuntimeResult.RecoveryPending, steamDpiRuntimeResult.Stage, steamDpiRuntimeResult.Cause);
		}
		catch
		{
			UpdateFailure(current, code, running: true, cleanupPending: true);
		}
	}

	private async Task StopAfterAsync(Task previous, long current, CancellationToken token)
	{
		_ = 1;
		try
		{
			using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
			deadline.CancelAfter(TimeSpan.FromSeconds(90.0));
			try
			{
				await previous.WaitAsync(deadline.Token).ConfigureAwait(continueOnCapturedContext: false);
			}
			catch (OperationCanceledException) when (deadline.IsCancellationRequested)
			{
				throw;
			}
			catch
			{
			}
			SteamDpiRuntimeResult result = await runtime.StopAsync(deadline.Token).ConfigureAwait(continueOnCapturedContext: false);
			if (result.Running || result.RecoveryPending || result.Code != "ok")
			{
				writeDiagnostic("stop", (result.Code == "ok") ? "incomplete" : result.Code);
				UpdateFailure(current, (result.Code == "ok") ? "stop-failed" : result.Code, running: true, cleanupPending: true, result.Stage, result.Cause);
				return;
			}
			writeDiagnostic("stop", "completed");
			Update(current, (SteamConnectionUiState item) => item with
			{
				Enabled = result.Running,
				DnsEnabled = result.DnsReady,
				DpiEnabled = result.DpiRunning,
				CleanupPending = false,
				ErrorCode = null,
				ErrorStage = null,
				IndependentVerified = false,
				Busy = false,
				Kind = "Off",
				Label = "Đã tắt hỗ trợ",
				Detail = "Đã gỡ quy tắc DNS của NestG; Steam sử dụng đường truyền hiện tại.",
				Checks = new Dictionary<string, string>(),
				Reasons = new Dictionary<string, string>(),
				CheckedAt = null
			});
		}
		catch
		{
			UpdateFailure(current, "stop-failed", running: true, cleanupPending: true);
		}
	}

	private async Task ProbeAsync(long current, CancellationToken token)
	{
		_ = 3;
		try
		{
			using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
			deadline.CancelAfter(TimeSpan.FromSeconds(15.0));
			SteamDpiRuntimeResult result = await runtime.ReadAsync(deadline.Token).ConfigureAwait(continueOnCapturedContext: false);
			Update(current, (SteamConnectionUiState item) => item with
			{
				Enabled = result.Running,
				DnsEnabled = result.DnsReady,
				DpiEnabled = result.DpiRunning
			});
			long network = Interlocked.Read(in networkRevision);
			bool? warpBefore = await ReadWarpSafelyAsync(deadline.Token).ConfigureAwait(continueOnCapturedContext: false);
			IReadOnlyDictionary<string, SteamConnectionProbeResult> results = await probe.CheckAsync(deadline.Token).ConfigureAwait(continueOnCapturedContext: false);
			bool? flag = await ReadWarpSafelyAsync(deadline.Token).ConfigureAwait(continueOnCapturedContext: false);
			if (network != Interlocked.Read(in networkRevision) || warpBefore != flag)
			{
				InvalidateConnectivity(operationEnded: true, current);
				return;
			}
			PublishProbe(current, results, busy: false, flag);
		}
		catch (OperationCanceledException)
		{
			Update(current, (SteamConnectionUiState item) => item with
			{
				Busy = false,
				Kind = "Unknown",
				Label = "Chưa xác định kết nối",
				Detail = "Kiểm tra kết nối đã dừng."
			});
		}
		catch
		{
			UpdateFailure(current, "probe-failed", GetState().Enabled);
		}
	}

	private void PublishProbe(long current, IReadOnlyDictionary<string, SteamConnectionProbeResult> results, bool busy = false, bool? warpConnected = null)
	{
		foreach (KeyValuePair<string, SteamConnectionProbeResult> result in results)
		{
			writeDiagnostic("probe-" + result.Key, result.Value.Reason);
		}
		bool reachable = results.Count == 3 && results.Values.All((SteamConnectionProbeResult item) => item.Result == "ok");
		Update(current, (SteamConnectionUiState item) => item with
		{
			Busy = busy,
			CleanupPending = false,
			ErrorCode = null,
			ErrorStage = null,
			Kind = (reachable ? "Connected" : "Failed"),
			Label = (reachable ? "Steam có thể truy cập" : "Steam chưa truy cập đầy đủ"),
			WarpConnected = warpConnected,
			IndependentVerified = (reachable && item.Enabled && item.DnsEnabled && warpConnected == false),
			Detail = ((!reachable) ? "Một số địa chỉ kiểm tra chưa phản hồi tốt." : ((warpConnected == true) ? "Steam đang truy cập được khi Cloudflare/WARP bật; chưa xác nhận hỗ trợ hoạt động độc lập." : ((!warpConnected.HasValue) ? "Các địa chỉ Steam đã phản hồi; chưa xác nhận được trạng thái WARP." : ((item.Enabled && item.DnsEnabled) ? "Các địa chỉ Steam đã phản hồi khi WARP tắt. Đăng nhập Steam client cần được kiểm tra riêng." : "Các địa chỉ Steam đã phản hồi từ đường truyền hiện tại.")))),
			Checks = results.ToDictionary<KeyValuePair<string, SteamConnectionProbeResult>, string, string>((KeyValuePair<string, SteamConnectionProbeResult> item) => item.Key, (KeyValuePair<string, SteamConnectionProbeResult> item) => item.Value.Result),
			Reasons = results.ToDictionary<KeyValuePair<string, SteamConnectionProbeResult>, string, string>((KeyValuePair<string, SteamConnectionProbeResult> item) => item.Key, (KeyValuePair<string, SteamConnectionProbeResult> item) => item.Value.Reason),
			CheckedAt = DateTimeOffset.UtcNow
		});
	}

	private void UpdateFailure(long current, string code, bool running = false, bool cleanupPending = false, string? stage = null, string? cause = null)
	{
		writeDiagnostic("failure", code);
		if (!string.IsNullOrEmpty(stage))
		{
			writeDiagnostic("failure-stage", stage);
		}
		if (!string.IsNullOrEmpty(cause))
		{
			writeDiagnostic("failure-cause", cause);
		}
		Update(current, delegate(SteamConnectionUiState item)
		{
			SteamConnectionUiState steamConnectionUiState = item with
			{
				Busy = false,
				Enabled = running,
				DnsEnabled = (running && item.DnsEnabled),
				DpiEnabled = (running && item.DpiEnabled),
				CleanupPending = cleanupPending,
				ErrorCode = (cause ?? code),
				ErrorStage = stage,
				IndependentVerified = false,
				Kind = "Failed",
				Label = (cleanupPending ? "Chưa tắt xong hỗ trợ" : "Hỗ trợ chưa sẵn sàng")
			};
			SteamConnectionUiState steamConnectionUiState2 = steamConnectionUiState;
			string text = (cleanupPending ? "Chưa khôi phục xong DNS/bộ hỗ trợ. " : "");
			string text2;
			switch (cause ?? code)
			{
			case "uac-cancelled":
				text2 = "Yêu cầu quyền quản trị đã được hủy. Bạn có thể bật lại khi cần.";
				break;
			case "driver-in-use":
			case "busy":
				text2 = "Một bộ hỗ trợ mạng khác đang sử dụng tài nguyên cần thiết.";
				break;
			case "engine-blocked":
				text2 = "Windows hoặc phần mềm bảo vệ đã chặn bộ hỗ trợ.";
				break;
			case "hash-mismatch":
				text2 = "File bộ hỗ trợ không khớp phiên bản đã xác minh.";
				break;
			case "dns-listener-busy":
				text2 = "Cổng DNS cục bộ đang được chương trình khác sử dụng. NestG chưa đổi cấu hình DNS.";
				break;
			case "dns-policy-conflict":
				text2 = "Quy tắc DNS hiện tại xung đột hoặc đã bị thay đổi. NestG giữ nguyên cấu hình đó.";
				break;
			case "dns-recovery-pending":
				text2 = "Chưa khôi phục được quy tắc DNS của NestG. Vui lòng thử tắt lại hỗ trợ.";
				break;
			case "dns-journal-invalid":
			case "dns-journal-untrusted":
				text2 = "Thông tin khôi phục DNS chưa hợp lệ. NestG giữ nguyên cấu hình; cần kiểm tra thông tin khôi phục.";
				break;
			case "dns-runtime-untrusted":
				text2 = "Quyền sở hữu thư mục bộ hỗ trợ chưa hợp lệ. NestG giữ nguyên cấu hình; cần kiểm tra thư mục bộ hỗ trợ.";
				break;
			case "dns-journal-write-failed":
			case "dns-journal-delete-failed":
				text2 = "Chưa cập nhật được thông tin khôi phục DNS. Bạn có thể thử tắt lại.";
				break;
			case "helper-timeout":
			case "dns-policy-timeout":
				text2 = "Bộ hỗ trợ phản hồi quá thời gian chờ. Bạn có thể thử tắt lại.";
				break;
			case "dns-policy-access-denied":
				text2 = "Windows từ chối quyền xử lý quy tắc DNS. Vui lòng chấp thuận quyền quản trị khi thử tắt lại.";
				break;
			case "dns-policy-query-failed":
				text2 = "Chưa đọc được danh sách quy tắc DNS từ Windows. Bạn có thể thử tắt lại.";
				break;
			case "dns-policy-remove-failed":
				text2 = "Windows chưa gỡ được quy tắc DNS của NestG. Bạn có thể thử tắt lại.";
				break;
			case "dns-cache-flush-failed":
				text2 = "Chưa xóa được bộ nhớ đệm DNS sau bước gỡ quy tắc. Bạn có thể thử tắt lại để hoàn tất.";
				break;
			case "dns-cleanup-check-failed":
				text2 = "Chưa xác nhận được quy tắc DNS của NestG đã được gỡ. Bạn có thể thử tắt lại.";
				break;
			case "dns-policy-confirmation-required":
				text2 = "Lệnh gỡ DNS đang yêu cầu xác nhận mà bộ hỗ trợ chạy ngầm chưa xử lý được. Vui lòng dùng bản launcher mới nhất rồi thử tắt lại.";
				break;
			case "dns-policy-missing":
				text2 = "Quy tắc DNS của NestG không còn được xác nhận. Bạn có thể tắt hỗ trợ để kiểm tra và dọn phiên.";
				break;
			case "helper-unavailable":
				text2 = "Mất kết nối với bộ hỗ trợ. Thử tắt lại để khôi phục DNS bằng bộ hỗ trợ đã xác minh.";
				break;
			case "stop-failed":
				text2 = "Thao tác tắt chưa hoàn tất. Bạn có thể thử tắt lại.";
				break;
			case "dns-recovery-unavailable":
			case "dns-guardian-unavailable":
				text2 = "Chưa chuẩn bị được cơ chế khôi phục DNS. Hỗ trợ chưa được bật hoàn chỉnh.";
				break;
			case "dns-policy-unavailable":
			case "dns-unavailable":
				text2 = "Không hoàn tất được thao tác DNS Steam. Bạn có thể thử lại; chưa xác định được nguyên nhân từ Windows.";
				break;
			case "dns-upstream-unavailable":
				text2 = "Chưa kết nối được DNS mã hóa. NestG chưa chuyển DNS Steam; vui lòng kiểm tra đường truyền.";
				break;
			case "cancelled":
				text2 = "Thao tác đã dừng hoặc vượt thời gian chờ.";
				break;
			default:
				text2 = "Không hoàn tất được bộ hỗ trợ Steam. Vui lòng thử lại.";
				break;
			}
			steamConnectionUiState2.Detail = text + text2;
			steamConnectionUiState.Feedback = null;
			steamConnectionUiState.Checks = new Dictionary<string, string>();
			steamConnectionUiState.Reasons = new Dictionary<string, string>();
			steamConnectionUiState.CheckedAt = null;
			return steamConnectionUiState;
		});
	}

	private void Update(long current, Func<SteamConnectionUiState, SteamConnectionUiState> change)
	{
		lock (sync)
		{
			if (current != generation || closing)
			{
				return;
			}
			state = change(state)with
			{
				Revision = state.Revision + 1
			};
		}
		Notify();
	}

	private void Notify()
	{
		EventHandler eventHandler = this.StateChanged;
		if (eventHandler == null)
		{
			return;
		}
		Delegate[] invocationList = eventHandler.GetInvocationList();
		for (int i = 0; i < invocationList.Length; i++)
		{
			EventHandler eventHandler2 = (EventHandler)invocationList[i];
			try
			{
				eventHandler2(this, EventArgs.Empty);
			}
			catch
			{
			}
		}
	}

	private async Task MonitorAsync()
	{
		_ = 1;
		try
		{
			while (!lifetime.IsCancellationRequested)
			{
				await Task.Delay(TimeSpan.FromSeconds(20.0), lifetime.Token).ConfigureAwait(continueOnCapturedContext: false);
				await CheckRuntimeStatusAsync(lifetime.Token).ConfigureAwait(continueOnCapturedContext: false);
			}
		}
		catch (OperationCanceledException) when (lifetime.IsCancellationRequested)
		{
		}
	}

	internal async Task CheckRuntimeStatusAsync(CancellationToken cancellationToken)
	{
		using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		deadline.CancelAfter(TimeSpan.FromSeconds(25.0));
		long current;
		DateTimeOffset? checkedAt;
		lock (sync)
		{
			if (!state.Enabled || !operation.IsCompleted || closing)
			{
				return;
			}
			current = generation;
			checkedAt = state.CheckedAt;
			statusCancellation = deadline;
		}
		try
		{
			SteamDpiRuntimeResult result = await runtime.ReadAsync(deadline.Token).ConfigureAwait(continueOnCapturedContext: false);
			lock (sync)
			{
				if (current != generation || closing)
				{
					return;
				}
			}
			Update(current, (SteamConnectionUiState item) => item with
			{
				DnsEnabled = result.DnsReady,
				DpiEnabled = result.DpiRunning
			});
			if (result.Code != "ok" || result.RecoveryPending)
			{
				writeDiagnostic("status", result.Code);
				SteamConnectionUiState steamConnectionUiState = GetState();
				if (steamConnectionUiState.ErrorCode != null && result.Cause == null)
				{
					PublishUnknownStatus(current);
				}
				else
				{
					UpdateFailure(current, result.Code, steamConnectionUiState.Enabled || result.Running, steamConnectionUiState.CleanupPending || result.RecoveryPending, result.Stage, result.Cause);
				}
			}
			else if (!result.Running)
			{
				if (GetState().CleanupPending)
				{
					Update(current, (SteamConnectionUiState item) => item with
					{
						Enabled = false,
						DnsEnabled = false,
						DpiEnabled = false,
						CleanupPending = false,
						ErrorCode = null,
						ErrorStage = null,
						Kind = "Off",
						Busy = false,
						Label = "Đã tắt hỗ trợ",
						Detail = "Đã xác nhận khôi phục DNS và dừng bộ hỗ trợ.",
						IndependentVerified = false
					});
				}
				else
				{
					UpdateFailure(current, "engine-exited");
				}
			}
			else
			{
				if (GetState().CleanupPending)
				{
					return;
				}
				if (!result.DnsReady)
				{
					PublishUnknownStatus(current);
					return;
				}
				bool? flag = await ReadWarpSafelyAsync(deadline.Token).ConfigureAwait(continueOnCapturedContext: false);
				bool? warpConnected;
				lock (sync)
				{
					warpConnected = state.WarpConnected;
				}
				if (checkedAt.HasValue && flag != warpConnected)
				{
					NetworkChanged();
				}
				else
				{
					if (!checkedAt.HasValue)
					{
						return;
					}
					DateTimeOffset utcNow = DateTimeOffset.UtcNow;
					DateTimeOffset? dateTimeOffset = checkedAt;
					if (utcNow - dateTimeOffset > TimeSpan.FromMinutes(2.0))
					{
						Update(current, (SteamConnectionUiState item) => item with
						{
							Kind = "Unknown",
							Label = "Cần kiểm tra lại Steam",
							Detail = "Kết quả kết nối trước đã cũ.",
							IndependentVerified = false,
							Checks = new Dictionary<string, string>(),
							Reasons = new Dictionary<string, string>(),
							CheckedAt = null
						});
					}
				}
			}
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			throw;
		}
		catch
		{
			writeDiagnostic("status", "unavailable");
			PublishUnknownStatus(current);
		}
		finally
		{
			lock (sync)
			{
				if (statusCancellation == deadline)
				{
					statusCancellation = null;
				}
			}
		}
	}

	private async Task<bool?> ReadWarpSafelyAsync(CancellationToken token)
	{
		try
		{
			return await readWarp(token).ConfigureAwait(continueOnCapturedContext: false);
		}
		catch (OperationCanceledException) when (token.IsCancellationRequested)
		{
			throw;
		}
		catch
		{
			return null;
		}
	}

	private static async Task<bool?> ReadWarpAsync(CancellationToken token)
	{
		bool? result;
		switch ((await new WarpClientService().GetStatusAsync(token).ConfigureAwait(continueOnCapturedContext: false)).Kind)
		{
		case WarpConnectionKind.NotInstalled:
		case WarpConnectionKind.Disconnected:
			result = false;
			break;
		case WarpConnectionKind.Connecting:
		case WarpConnectionKind.Connected:
		case WarpConnectionKind.DnsOnly:
			result = true;
			break;
		default:
			result = null;
			break;
		}
		return result;
	}

	private void PublishUnknownStatus(long current)
	{
		Update(current, (SteamConnectionUiState item) => item with
		{
			Kind = ((item.ErrorCode == null) ? "Unknown" : "Failed"),
			Label = ((item.ErrorCode == null) ? "Cần kiểm tra lại hỗ trợ Steam" : item.Label),
			Detail = ((item.ErrorCode == null) ? "Chưa xác nhận được trạng thái bộ hỗ trợ. Bạn có thể thử tắt lại." : item.Detail),
			Checks = new Dictionary<string, string>(),
			Reasons = new Dictionary<string, string>(),
			CheckedAt = null,
			IndependentVerified = false
		});
	}

	private void HandleNetworkAddressChanged(object? sender, EventArgs e)
	{
		NetworkChanged();
	}

	internal void NetworkChanged()
	{
		NetworkChangedAsync();
	}

	internal async Task NetworkChangedAsync()
	{
		long revision = Interlocked.Increment(ref networkRevision);
		InvalidateConnectivity(operationEnded: false, null);
		lock (sync)
		{
			if (closing || !requestedEnabled || !state.Enabled || state.CleanupPending)
			{
				return;
			}
		}
		try
		{
			await Task.Delay(TimeSpan.FromSeconds(2.0), lifetime.Token).ConfigureAwait(continueOnCapturedContext: false);
			Task task;
			lock (sync)
			{
				if (closing)
				{
					return;
				}
				task = operation;
			}
			await task.ConfigureAwait(continueOnCapturedContext: false);
			if (revision != Interlocked.Read(in networkRevision))
			{
				return;
			}
			lock (sync)
			{
				if (closing || !requestedEnabled || !state.Enabled || state.CleanupPending)
				{
					return;
				}
			}
			await RefreshAsync().ConfigureAwait(continueOnCapturedContext: false);
		}
		catch (OperationCanceledException)
		{
		}
		catch
		{
		}
	}

	private void InvalidateConnectivity(bool operationEnded = false, long? current = null)
	{
		lock (sync)
		{
			if (closing || (!requestedEnabled && !operationEnded) || state.CleanupPending || (current.HasValue && current != generation))
			{
				return;
			}
			bool flag = !operationEnded && !operation.IsCompleted;
			state = state with
			{
				Kind = (flag ? state.Kind : "Unknown"),
				Busy = flag,
				Label = "Cần kiểm tra lại Steam",
				Detail = "Đường truyền đã thay đổi; kết quả trước không còn được xác nhận.",
				Checks = new Dictionary<string, string>(),
				Reasons = new Dictionary<string, string>(),
				CheckedAt = null,
				IndependentVerified = false,
				Revision = state.Revision + 1
			};
		}
		Notify();
	}

	internal Task ShutdownAsync()
	{
		lock (sync)
		{
			if (shutdown != null)
			{
				return shutdown;
			}
			closing = true;
			generation++;
			operationCancellation?.Cancel();
			lifetime.Cancel();
			if (initialized)
			{
				NetworkChange.NetworkAddressChanged -= HandleNetworkAddressChanged;
			}
			Task pending = operation;
			shutdown = Task.Run(async delegate
			{
				try
				{
					await pending.ConfigureAwait(continueOnCapturedContext: false);
				}
				catch
				{
				}
				try
				{
					await runtime.DisposeAsync().ConfigureAwait(continueOnCapturedContext: false);
				}
				finally
				{
					if (probe is IDisposable disposable)
					{
						disposable.Dispose();
					}
				}
			});
			return shutdown;
		}
	}
}
