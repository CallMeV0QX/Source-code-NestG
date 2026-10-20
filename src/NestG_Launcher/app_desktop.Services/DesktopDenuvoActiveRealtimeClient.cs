using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.AspNetCore.Http.Connections.Client;
using Microsoft.AspNetCore.SignalR.Client;
using UI_Desktop.Store;
using app_desktop.Models;

namespace app_desktop.Services;

internal sealed class DesktopDenuvoActiveRealtimeClient : IAsyncDisposable
{
	private readonly record struct RealtimeLifetime(long Generation, Guid UserId, CancellationToken Token);

	private static readonly TimeSpan[] ReconnectDelays = new TimeSpan[4]
	{
		TimeSpan.Zero,
		TimeSpan.FromSeconds(2.0),
		TimeSpan.FromSeconds(5.0),
		TimeSpan.FromSeconds(10.0)
	};

	private readonly AuthSessionManager authSessionManager;

	private readonly StorefrontApiClient storefrontApiClient;

	private readonly DenuvoFixInstallService installService;

	private readonly string hubUrl;

	private readonly SemaphoreSlim gate = new SemaphoreSlim(1, 1);

	private readonly SemaphoreSlim processGate = new SemaphoreSlim(1, 1);

	private readonly SemaphoreSlim watchGate = new SemaphoreSlim(1, 1);

	private HubConnection? hubConnection;

	private CancellationTokenSource? pendingLoopCts;

	private readonly HashSet<Guid> notifiedPendingRequests = new HashSet<Guid>();

	private readonly HashSet<Guid> notifiedLoadKeyRequests = new HashSet<Guid>();

	private readonly HashSet<Guid> notifiedCancelledRequests = new HashSet<Guid>();

	private readonly HashSet<Guid> watchedActiveRequests = new HashSet<Guid>();

	private readonly HashSet<Guid> watchedLibraryDetails = new HashSet<Guid>();

	private int pendingLoopStarted;

	private readonly object tokenSessionGate = new object();

	private CancellationTokenSource realtimeSessionCancellation = new CancellationTokenSource();

	private CancellationTokenSource? tokenApplicationCancellation;

	private Guid? applyingRequestId;

	private readonly object lifecycleStateGate = new object();

	private readonly HashSet<Guid> registeredLibraryDetails = new HashSet<Guid>();

	private readonly HashSet<Guid> pendingLibraryRemovals = new HashSet<Guid>();

	private CancellationTokenSource? lifecycleCancellation;

	private Task? supervisorTask;

	private Task? pendingLoopTask;

	private Task stopTask = Task.CompletedTask;

	private long lifecycleGeneration;

	private Guid? lifecycleUser;

	private bool stopping;

	private bool disposed;

	private string? presenceConnectionId;

	private string? restoredConnectionId;

	private long stateRevision;

	private string? rejectedCredentialFingerprint;

	private Guid? rejectedCredentialUser;

	private DesktopDenuvoRealtimeState currentState = new DesktopDenuvoRealtimeState(0L, "stopped", isConnected: false, null, isLibraryTracked: false, null);

	public bool IsConnected
	{
		get
		{
			HubConnection? obj = hubConnection;
			if (obj == null)
			{
				return false;
			}
			return obj.State == HubConnectionState.Connected;
		}
	}

	public DesktopDenuvoRealtimeState CurrentState
	{
		get
		{
			lock (lifecycleStateGate)
			{
				return currentState;
			}
		}
	}

	public event EventHandler<DesktopDenuvoActiveTokenAppliedEventArgs>? TokenApplied;

	public event EventHandler<DesktopDenuvoActiveRequestCancelledEventArgs>? RequestCancelled;

	public event EventHandler<DesktopDenuvoActiveCooldownUnlockedEventArgs>? CooldownUnlocked;

	public event EventHandler<DesktopDenuvoRealtimeState>? ConnectionStateChanged;

	public async Task<string> GetConnectionIdForRequestAsync(CancellationToken cancellationToken = default(CancellationToken))
	{
		await StartAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		if (TryGetLifecycle(out var lifetime) && IsCurrent(lifetime))
		{
			HubConnection? obj = hubConnection;
			if (obj != null && obj.State == HubConnectionState.Connected && !string.IsNullOrWhiteSpace(hubConnection.ConnectionId))
			{
				return hubConnection.ConnectionId;
			}
		}
		throw new InvalidOperationException("Realtime chưa kết nối. Hãy mở lại launcher rồi chủ động yêu cầu token.");
	}

	private void CancelTokenSession()
	{
		lock (tokenSessionGate)
		{
			realtimeSessionCancellation.Cancel();
		}
	}

	private void ResetTokenSession()
	{
		lock (tokenSessionGate)
		{
			realtimeSessionCancellation.Cancel();
			realtimeSessionCancellation.Dispose();
			realtimeSessionCancellation = new CancellationTokenSource();
		}
	}

	public DesktopDenuvoActiveRealtimeClient(string apiBaseUrl, AuthSessionManager authSessionManager)
	{
		this.authSessionManager = authSessionManager;
		storefrontApiClient = new StorefrontApiClient(apiBaseUrl);
		installService = new DenuvoFixInstallService(storefrontApiClient);
		hubUrl = new Uri(new Uri(NormalizeBaseUrl(apiBaseUrl)), "hubs/denuvo-active").ToString();
	}

	public Task StartAsync(CancellationToken cancellationToken = default(CancellationToken))
	{
		return StartSupervisedAsync(cancellationToken);
	}

	public Task StopAsync(CancellationToken cancellationToken = default(CancellationToken))
	{
		return StopSupervisedAsync(cancellationToken);
	}

	public async Task WatchActiveRequestAsync(Guid activeRequestId, CancellationToken cancellationToken = default(CancellationToken))
	{
		if (!(activeRequestId == Guid.Empty))
		{
			await StartAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			await WatchActiveRequestCoreAsync(activeRequestId, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
	}

	public async Task UnwatchActiveRequestAsync(Guid activeRequestId, CancellationToken cancellationToken = default(CancellationToken))
	{
		await UnwatchActiveRequestCoreAsync(activeRequestId, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
	}

	public async Task WatchLibraryDetailAsync(Guid libraryId, CancellationToken cancellationToken = default(CancellationToken))
	{
		if (libraryId == Guid.Empty)
		{
			return;
		}
		long stamp;
		lock (lifecycleStateGate)
		{
			if (stopping || disposed || authSessionManager.CurrentSession == null)
			{
				return;
			}
			stamp = lifecycleGeneration;
			watchedLibraryDetails.Add(libraryId);
		}
		await StartAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		if (TryGetLifecycle(out var lifetime) && lifetime.Generation == stamp)
		{
			await ReconcileLibraryPresenceAsync(lifetime, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
	}

	public async Task UnwatchLibraryDetailAsync(Guid libraryId, CancellationToken cancellationToken = default(CancellationToken))
	{
		lock (lifecycleStateGate)
		{
			watchedLibraryDetails.Remove(libraryId);
			if (registeredLibraryDetails.Contains(libraryId))
			{
				pendingLibraryRemovals.Add(libraryId);
			}
		}
		if (TryGetLifecycle(out var lifetime))
		{
			await ReconcileLibraryPresenceAsync(lifetime, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
	}

	public async Task ProcessPendingAsync(CancellationToken cancellationToken = default(CancellationToken))
	{
		if (!TryGetLifecycle(out var lifetime) || !IsCurrent(lifetime))
		{
			return;
		}
		using CancellationTokenSource sessionCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetime.Token);
		cancellationToken = sessionCancellation.Token;
		string accessToken = await authSessionManager.GetAccessTokenAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		if (string.IsNullOrWhiteSpace(accessToken) || !IsCurrent(lifetime) || !(await processGate.WaitAsync(0, cancellationToken).ConfigureAwait(continueOnCapturedContext: false)))
		{
			return;
		}
		try
		{
			await RefreshWatchedRequestStatesAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			foreach (DesktopDenuvoActivePendingItemResponse item in (await storefrontApiClient.GetDenuvoActivePendingAsync(accessToken, cancellationToken).ConfigureAwait(continueOnCapturedContext: false)).Items)
			{
				cancellationToken.ThrowIfCancellationRequested();
				if (!IsCurrent(lifetime))
				{
					return;
				}
				string status = item.Status.Trim().ToLowerInvariant();
				if (ShouldKeepAdminPresence(status))
				{
					await WatchActiveRequestCoreAsync(item.ActiveRequestId, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				}
				bool flag;
				switch (status)
				{
				case "waiting_user_active_game":
				case "waiting_steam_download":
				case "waiting_user_load_key":
					flag = true;
					break;
				default:
					flag = false;
					break;
				}
				if (flag)
				{
					await TrySyncSteamGameStateAsync(item, status, accessToken, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
					continue;
				}
				flag = IsConnected;
				if (flag)
				{
					string text = status;
					bool flag2 = ((text == "token_issued" || text == "token_delivered") ? true : false);
					flag = flag2;
				}
				if (flag)
				{
					await TryApplyTokenAsync(item, accessToken, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				}
			}
		}
		catch (Exception ex) when (!(ex is OperationCanceledException))
		{
			WriteLog("pending-load", ex);
		}
		finally
		{
			processGate.Release();
		}
	}

	public async ValueTask DisposeAsync()
	{
		lock (lifecycleStateGate)
		{
			disposed = true;
		}
		try
		{
			await StopAsync().ConfigureAwait(continueOnCapturedContext: false);
		}
		catch
		{
		}
		if (hubConnection != null)
		{
			await hubConnection.DisposeAsync().ConfigureAwait(continueOnCapturedContext: false);
		}
		gate.Dispose();
		processGate.Dispose();
		watchGate.Dispose();
		pendingLoopCts?.Dispose();
		realtimeSessionCancellation.Dispose();
	}

	private void StartPendingLoop()
	{
		lock (lifecycleStateGate)
		{
			if (!TryGetLifecycle(out var lifetime) || !IsCurrent(lifetime) || pendingLoopStarted != 0)
			{
				return;
			}
			Interlocked.Exchange(ref pendingLoopStarted, 1);
			pendingLoopCts = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
			CancellationToken token = pendingLoopCts.Token;
			pendingLoopTask = Task.Run(async delegate
			{
				while (!token.IsCancellationRequested)
				{
					try
					{
						await ProcessPendingAsync(token).ConfigureAwait(continueOnCapturedContext: false);
					}
					catch (OperationCanceledException) when (token.IsCancellationRequested)
					{
						break;
					}
					catch (Exception exception)
					{
						WriteLog("pending-loop", exception);
					}
					try
					{
						await Task.Delay(TimeSpan.FromSeconds(60.0), token).ConfigureAwait(continueOnCapturedContext: false);
					}
					catch (OperationCanceledException) when (token.IsCancellationRequested)
					{
						break;
					}
				}
			});
		}
	}

	private HubConnection CreateConnection()
	{
		HubConnection obj = ((IHubConnectionBuilder)new HubConnectionBuilder()).WithUrl(hubUrl, (Action<HttpConnectionOptions>)delegate(HttpConnectionOptions options)
		{
			options.AccessTokenProvider = () => authSessionManager.GetAccessTokenAsync();
		}).WithAutomaticReconnect(ReconnectDelays).Build();
		obj.On<DesktopDenuvoActiveRealtimeTokenReady>("DenuvoActiveTokenReady", delegate
		{
			QueueRealtimeCallback(ProcessPendingAsync);
		});
		obj.On("DenuvoActiveUserRequestPending", delegate(DesktopDenuvoActivePendingItemResponse request)
		{
			ShowUserNotification("Denuvo Active", request.GameName + " đã sẵn sàng trong thư viện. Hãy bấm Active, tải game trên Steam; launcher sẽ tự nhận token khi admin cấp.");
			QueueRealtimeCallback(ProcessPendingAsync);
		});
		obj.On("DenuvoActiveRequestCancelled", delegate(DesktopDenuvoActiveRealtimeRequestCancelled request)
		{
			QueueRealtimeCallback((CancellationToken token) => ProcessRequestCancelledAsync(request, token));
		});
		obj.On("DenuvoActiveCooldownUnlocked", delegate(DesktopDenuvoActiveRealtimeCooldownUnlocked unlock)
		{
			QueueRealtimeCallback((CancellationToken token) => ProcessCooldownUnlockedAsync(unlock, token));
		});
		obj.Reconnected += delegate
		{
			if (TryGetLifecycle(out var lifetime) && IsCurrent(lifetime))
			{
				ResetTokenSession();
				PublishState("connected");
			}
			return Task.CompletedTask;
		};
		obj.Reconnecting += delegate
		{
			CancelTokenSession();
			if (TryGetLifecycle(out var lifetime2) && IsCurrent(lifetime2))
			{
				PublishState("retrying", "transport_reconnecting");
			}
			return Task.CompletedTask;
		};
		obj.Closed += delegate
		{
			CancelTokenSession();
			if (TryGetLifecycle(out var lifetime3) && IsCurrent(lifetime3))
			{
				PublishState("retrying", "transport_closed");
			}
			return Task.CompletedTask;
		};
		return obj;
	}

	private async Task WatchActiveRequestCoreAsync(Guid activeRequestId, CancellationToken cancellationToken)
	{
		if (activeRequestId == Guid.Empty)
		{
			return;
		}
		await watchGate.WaitAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		try
		{
			if (!watchedActiveRequests.Contains(activeRequestId))
			{
				HubConnection? obj = hubConnection;
				if (obj != null && obj.State == HubConnectionState.Connected)
				{
					await hubConnection.InvokeAsync("WatchActiveRequest", activeRequestId, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
					watchedActiveRequests.Add(activeRequestId);
				}
			}
		}
		catch (Exception ex) when (!(ex is OperationCanceledException))
		{
			WriteLog("watch-request", ex);
		}
		finally
		{
			watchGate.Release();
		}
	}

	private async Task UnwatchActiveRequestCoreAsync(Guid activeRequestId, CancellationToken cancellationToken)
	{
		if (activeRequestId == Guid.Empty)
		{
			return;
		}
		await watchGate.WaitAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		try
		{
			watchedActiveRequests.Remove(activeRequestId);
			HubConnection? obj = hubConnection;
			if (obj != null && obj.State == HubConnectionState.Connected)
			{
				await hubConnection.InvokeAsync("UnwatchActiveRequest", activeRequestId, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			}
		}
		catch (Exception ex) when (!(ex is OperationCanceledException))
		{
			WriteLog("unwatch-request", ex);
		}
		finally
		{
			watchGate.Release();
		}
	}

	private async Task RewatchActiveRequestsAsync(CancellationToken cancellationToken)
	{
		await RefreshWatchedRequestStatesAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		await watchGate.WaitAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		Guid[] array;
		try
		{
			HubConnection? obj = hubConnection;
			if (obj == null || obj.State != HubConnectionState.Connected || watchedActiveRequests.Count == 0)
			{
				return;
			}
			array = watchedActiveRequests.ToArray();
			watchedActiveRequests.Clear();
		}
		finally
		{
			watchGate.Release();
		}
		Guid[] array2 = array;
		foreach (Guid activeRequestId in array2)
		{
			await WatchActiveRequestCoreAsync(activeRequestId, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
	}

	private static bool ShouldKeepAdminPresence(string status)
	{
		switch (status)
		{
		case "pending_admin_token":
		case "admin_editing":
		case "token_issued":
		case "token_delivered":
		case "token_expired":
		case "error":
			return true;
		default:
			return false;
		}
	}

	private async Task RefreshWatchedRequestStatesAsync(CancellationToken cancellationToken)
	{
		if (!IsConnected)
		{
			return;
		}
		await watchGate.WaitAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		Guid[] requestIds;
		try
		{
			requestIds = watchedActiveRequests.ToArray();
		}
		finally
		{
			watchGate.Release();
		}
		if (requestIds.Length == 0)
		{
			return;
		}
		string accessToken = await authSessionManager.GetAccessTokenAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		if (string.IsNullOrWhiteSpace(accessToken))
		{
			return;
		}
		Guid[] array = requestIds;
		foreach (Guid requestId in array)
		{
			try
			{
				DesktopDenuvoActivePendingItemResponse desktopDenuvoActivePendingItemResponse = await storefrontApiClient.GetDenuvoActiveRequestStateAsync(requestId, accessToken, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				if (desktopDenuvoActivePendingItemResponse.Status == "cancelled")
				{
					await ProcessRequestCancelledAsync(new DesktopDenuvoActiveRealtimeRequestCancelled
					{
						ActiveRequestId = desktopDenuvoActivePendingItemResponse.ActiveRequestId,
						AssignmentId = desktopDenuvoActivePendingItemResponse.AssignmentId,
						LibraryId = desktopDenuvoActivePendingItemResponse.LibraryId,
						GameName = desktopDenuvoActivePendingItemResponse.GameName,
						Reason = (desktopDenuvoActivePendingItemResponse.CancelReason ?? "Yêu cầu đã bị hủy. Hãy chủ động gửi yêu cầu mới."),
						CancelledAt = (desktopDenuvoActivePendingItemResponse.CancelledAt ?? DateTime.UtcNow)
					}, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				}
				else if (desktopDenuvoActivePendingItemResponse.Status == "token_applied")
				{
					await UnwatchActiveRequestCoreAsync(requestId, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				}
			}
			catch (Exception ex) when (!(ex is OperationCanceledException))
			{
				WriteLog("request-state", ex);
			}
		}
	}

	private async Task ProcessRequestCancelledAsync(DesktopDenuvoActiveRealtimeRequestCancelled request, CancellationToken cancellationToken)
	{
		bool notify;
		lock (tokenSessionGate)
		{
			if (applyingRequestId == request.ActiveRequestId)
			{
				tokenApplicationCancellation?.Cancel();
			}
			notify = notifiedCancelledRequests.Add(request.ActiveRequestId);
		}
		await UnwatchActiveRequestCoreAsync(request.ActiveRequestId, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		cancellationToken.ThrowIfCancellationRequested();
		if (notify && TryGetLifecycle(out var lifetime) && IsCurrent(lifetime))
		{
			RaiseRequestCancelled(request);
		}
	}

	private Task ProcessCooldownUnlockedAsync(DesktopDenuvoActiveRealtimeCooldownUnlocked unlock, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		DenuvoFixInstallService.ClearAdminRequestCooldown(unlock.LibraryId);
		RaiseCooldownUnlocked(unlock);
		return Task.CompletedTask;
	}

	private async Task TrySyncSteamGameStateAsync(DesktopDenuvoActivePendingItemResponse item, string status, string accessToken, CancellationToken cancellationToken)
	{
		DenuvoSteamInstallMonitor denuvoSteamInstallMonitor = new DenuvoSteamInstallMonitor(storefrontApiClient);
		bool flag;
		try
		{
			flag = await denuvoSteamInstallMonitor.SyncAsync(item, accessToken, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
		catch (Exception ex) when (!(ex is OperationCanceledException))
		{
			WriteLog("steam-installed", ex);
			return;
		}
		if (!flag)
		{
			if (status == "waiting_user_active_game" && notifiedPendingRequests.Add(item.ActiveRequestId))
			{
				ShowUserNotification("Denuvo Active", item.GameName + " đã sẵn sàng trong thư viện. Hãy bấm Active và tải game trên Steam.");
			}
		}
		else if (notifiedLoadKeyRequests.Add(item.ActiveRequestId))
		{
			ShowUserNotification("Denuvo Active", item.GameName + " đã được phát hiện trong Steam. Hãy mở Library và bấm Yêu cầu admin nạp game trước khi chờ admin cấp token.");
		}
	}

	private async Task TryApplyTokenAsync(DesktopDenuvoActivePendingItemResponse item, string accessToken, CancellationToken cancellationToken)
	{
		using CancellationTokenSource applicationCancellation = CreateTokenApplicationCancellation(item.ActiveRequestId, cancellationToken);
		cancellationToken = applicationCancellation.Token;
		try
		{
			await storefrontApiClient.PostDenuvoActiveDeliveredAsync(item.ActiveRequestId, BuildState(item.GameInstallPath, item.TokenRevisionNumber, null, "Launcher đã nhận token sẵn sàng."), accessToken, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			DenuvoFixInstallResult result = await installService.InstallActiveTokenPackageAsync(item, accessToken, hubConnection.ConnectionId, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			string message = BuildTokenAppliedMessage(result);
			await storefrontApiClient.PostDenuvoActiveAppliedAsync(item.ActiveRequestId, BuildState(result.TargetDirectory, result.ConfigRevisionNumber, result.PackageSha256, message), accessToken, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			DateTime appliedAtUtc = DateTime.UtcNow;
			DateTime cooldownUntilUtc = DenuvoFixInstallService.MarkAdminRequestCooldown(item.LibraryId, item.AssignmentId, item.GameId, item.GameName, appliedAtUtc);
			await UnwatchActiveRequestCoreAsync(item.ActiveRequestId, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			cancellationToken.ThrowIfCancellationRequested();
			if (!TryGetLifecycle(out var lifetime) || !IsCurrent(lifetime))
			{
				return;
			}
			if (!RaiseTokenApplied(item, result, appliedAtUtc, cooldownUntilUtc))
			{
				ShowUserNotification("Nạp game thành công", BuildTokenAppliedNotification(item, result));
			}
		}
		catch (Exception ex) when (!(ex is OperationCanceledException))
		{
			WriteLog("apply-token", ex);
			try
			{
				await storefrontApiClient.PostDenuvoActiveErrorAsync(item.ActiveRequestId, BuildState(item.GameInstallPath, item.TokenRevisionNumber, null, "Launcher không thể apply token. ErrorType=" + ex.GetType().Name), accessToken, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			}
			catch (Exception ex2) when (!(ex2 is OperationCanceledException))
			{
				WriteLog("apply-token-report", ex2);
			}
		}
		finally
		{
			lock (tokenSessionGate)
			{
				applyingRequestId = null;
				tokenApplicationCancellation = null;
			}
		}
	}

	private CancellationTokenSource CreateTokenApplicationCancellation(Guid requestId, CancellationToken caller)
	{
		lock (tokenSessionGate)
		{
			applyingRequestId = requestId;
			return tokenApplicationCancellation = CancellationTokenSource.CreateLinkedTokenSource(caller, realtimeSessionCancellation.Token);
		}
	}

	private bool RaiseTokenApplied(DesktopDenuvoActivePendingItemResponse item, DenuvoFixInstallResult result, DateTime appliedAtUtc, DateTime cooldownUntilUtc)
	{
		EventHandler<DesktopDenuvoActiveTokenAppliedEventArgs> eventHandler = this.TokenApplied;
		if (eventHandler == null)
		{
			return false;
		}
		DesktopDenuvoActiveTokenAppliedEventArgs desktopDenuvoActiveTokenAppliedEventArgs = new DesktopDenuvoActiveTokenAppliedEventArgs(item.ActiveRequestId, item.AssignmentId, item.LibraryId, item.GameId, item.GameName, result.TargetDirectory, result.ConfigFullPath, result.PackageVersion, result.ConfigRevisionNumber, result.WrittenFiles.Count, result.OverwrittenFiles.Count, result.MainExeRelativePath, result.GameExePath, result.Messages, appliedAtUtc, cooldownUntilUtc);
		Delegate[] invocationList = eventHandler.GetInvocationList();
		for (int i = 0; i < invocationList.Length; i++)
		{
			EventHandler<DesktopDenuvoActiveTokenAppliedEventArgs> eventHandler2 = (EventHandler<DesktopDenuvoActiveTokenAppliedEventArgs>)invocationList[i];
			try
			{
				eventHandler2(this, desktopDenuvoActiveTokenAppliedEventArgs);
			}
			catch (Exception exception)
			{
				WriteLog("token-applied-ui", exception);
			}
		}
		return desktopDenuvoActiveTokenAppliedEventArgs.Handled;
	}

	private void RaiseRequestCancelled(DesktopDenuvoActiveRealtimeRequestCancelled request)
	{
		EventHandler<DesktopDenuvoActiveRequestCancelledEventArgs> eventHandler = this.RequestCancelled;
		if (eventHandler == null)
		{
			ShowUserNotification("Yêu cầu đã bị hủy", BuildRequestCancelledNotification(request));
			return;
		}
		DesktopDenuvoActiveRequestCancelledEventArgs desktopDenuvoActiveRequestCancelledEventArgs = new DesktopDenuvoActiveRequestCancelledEventArgs(request.ActiveRequestId, request.AssignmentId, request.LibraryId, request.GameName, request.Reason, request.CancelledAt);
		Delegate[] invocationList = eventHandler.GetInvocationList();
		for (int i = 0; i < invocationList.Length; i++)
		{
			EventHandler<DesktopDenuvoActiveRequestCancelledEventArgs> eventHandler2 = (EventHandler<DesktopDenuvoActiveRequestCancelledEventArgs>)invocationList[i];
			try
			{
				eventHandler2(this, desktopDenuvoActiveRequestCancelledEventArgs);
			}
			catch (Exception exception)
			{
				WriteLog("request-cancelled-ui", exception);
			}
		}
		if (!desktopDenuvoActiveRequestCancelledEventArgs.Handled)
		{
			ShowUserNotification("Yêu cầu đã bị hủy", BuildRequestCancelledNotification(request));
		}
	}

	private void RaiseCooldownUnlocked(DesktopDenuvoActiveRealtimeCooldownUnlocked unlock)
	{
		EventHandler<DesktopDenuvoActiveCooldownUnlockedEventArgs> eventHandler = this.CooldownUnlocked;
		if (eventHandler == null)
		{
			ShowUserNotification("Denuvo Active", unlock.Message);
			return;
		}
		DesktopDenuvoActiveCooldownUnlockedEventArgs desktopDenuvoActiveCooldownUnlockedEventArgs = new DesktopDenuvoActiveCooldownUnlockedEventArgs(unlock.LibraryId, unlock.GameId, unlock.GameName, unlock.VersionId, unlock.VersionName, unlock.UnlockedAt, unlock.Message);
		Delegate[] invocationList = eventHandler.GetInvocationList();
		for (int i = 0; i < invocationList.Length; i++)
		{
			EventHandler<DesktopDenuvoActiveCooldownUnlockedEventArgs> eventHandler2 = (EventHandler<DesktopDenuvoActiveCooldownUnlockedEventArgs>)invocationList[i];
			try
			{
				eventHandler2(this, desktopDenuvoActiveCooldownUnlockedEventArgs);
			}
			catch (Exception exception)
			{
				WriteLog("cooldown-unlocked-ui", exception);
			}
		}
		if (!desktopDenuvoActiveCooldownUnlockedEventArgs.Handled)
		{
			ShowUserNotification("Denuvo Active", unlock.Message);
		}
	}

	private DesktopDenuvoActiveClientStateRequest BuildState(string? gameInstallPath, int? appliedRevisionNumber, string? packageSha256, string message)
	{
		return new DesktopDenuvoActiveClientStateRequest
		{
			RealtimeConnectionId = hubConnection?.ConnectionId,
			GameInstallPath = gameInstallPath,
			ClientMachineIdHash = ResolveClientMachineIdHash(),
			ClientVersion = ResolveClientVersion(),
			AppliedRevisionNumber = appliedRevisionNumber,
			PackageSha256 = packageSha256,
			Message = message
		};
	}

	private static string BuildTokenAppliedMessage(DenuvoFixInstallResult result)
	{
		string text = (string.IsNullOrWhiteSpace(result.GameExePath) ? "Chưa có link file chạy game vì admin chưa cấu hình exe hợp lệ." : ("File chạy game: " + result.GameExePath));
		return "Launcher đã apply token Denuvo Active do admin cấp. " + text;
	}

	private static string BuildTokenAppliedNotification(DesktopDenuvoActivePendingItemResponse item, DenuvoFixInstallResult result)
	{
		if (string.IsNullOrWhiteSpace(result.GameExePath))
		{
			return item.GameName + " đã nhận token từ admin và apply xong. Chưa có link file chạy game, hãy kiểm tra cấu hình exe trong admin.";
		}
		return item.GameName + " đã nhận token từ admin và apply xong. File chạy game: " + result.GameExePath;
	}

	private static string BuildRequestCancelledNotification(DesktopDenuvoActiveRealtimeRequestCancelled request)
	{
		string text = (string.IsNullOrWhiteSpace(request.Reason) ? "Admin đã hủy yêu cầu." : request.Reason.Trim());
		return request.GameName + " đã bị admin hủy yêu cầu nạp game. Lý do: " + text;
	}

	private static string ResolveClientVersion()
	{
		DesktopAppSettings settings = DesktopAppContext.Settings;
		return $"{settings.CurrentVersion}+{settings.CurrentBuildNumber}";
	}

	private static string ResolveClientMachineIdHash()
	{
		string s = $"{Environment.MachineName}|{Environment.UserName}|{Environment.OSVersion.VersionString}";
		return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(s)));
	}

	private static void ShowUserNotification(string title, string message)
	{
	}

	private static void ShowDenuvoActiveNotification(IWin32Window? owner, string title, string message)
	{
		using DenuvoActiveNotificationForm denuvoActiveNotificationForm = new DenuvoActiveNotificationForm(title, message);
		if (owner == null)
		{
			denuvoActiveNotificationForm.StartPosition = FormStartPosition.CenterScreen;
			denuvoActiveNotificationForm.ShowDialog();
		}
		else
		{
			denuvoActiveNotificationForm.ShowDialog(owner);
		}
	}

	private static void WriteLog(string scope, Exception exception)
	{
		try
		{
			string text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NestGLauncher", "DenuvoFixLogs");
			Directory.CreateDirectory(text);
			File.WriteAllLines(Path.Combine(text, $"denuvo-active-{scope}-{DateTime.UtcNow:yyyyMMddHHmmss}.log"), new string[4]
			{
				$"Time UTC: {DateTime.UtcNow:O}",
				"Scope: " + scope,
				$"ErrorType: {exception.GetType().Name}; HResult: {exception.HResult}",
				$"HttpStatus: {(exception as HttpRequestException)?.StatusCode}"
			});
		}
		catch
		{
		}
	}

	private static string NormalizeBaseUrl(string apiBaseUrl)
	{
		if (string.IsNullOrWhiteSpace(apiBaseUrl))
		{
			return "https://localhost:7023/";
		}
		if (!apiBaseUrl.EndsWith("/", StringComparison.Ordinal))
		{
			return apiBaseUrl + "/";
		}
		return apiBaseUrl;
	}

	private bool TryGetLifecycle(out RealtimeLifetime lifetime)
	{
		lock (lifecycleStateGate)
		{
			if (!stopping && !disposed)
			{
				Guid? guid = lifecycleUser;
				if (guid.HasValue)
				{
					Guid valueOrDefault = guid.GetValueOrDefault();
					CancellationTokenSource cancellationTokenSource = lifecycleCancellation;
					if (cancellationTokenSource != null && !cancellationTokenSource.IsCancellationRequested)
					{
						lifetime = new RealtimeLifetime(lifecycleGeneration, valueOrDefault, cancellationTokenSource.Token);
						return true;
					}
				}
			}
		}
		lifetime = default(RealtimeLifetime);
		return false;
	}

	private bool IsCurrent(RealtimeLifetime lifetime)
	{
		lock (lifecycleStateGate)
		{
			int result;
			if (!stopping && !disposed && lifecycleGeneration == lifetime.Generation)
			{
				Guid? guid = lifecycleUser;
				Guid userId = lifetime.UserId;
				if (guid.HasValue && guid.GetValueOrDefault() == userId && !lifetime.Token.IsCancellationRequested)
				{
					result = ((authSessionManager.CurrentSession?.UserId == lifetime.UserId) ? 1 : 0);
					goto IL_00bf;
				}
			}
			result = 0;
			goto IL_00bf;
			IL_00bf:
			return (byte)result != 0;
		}
	}

	private async Task StartSupervisedAsync(CancellationToken cancellationToken)
	{
		RealtimeLifetime lifetime;
		lock (lifecycleStateGate)
		{
			if (stopping || disposed)
			{
				return;
			}
			AuthSession currentSession = authSessionManager.CurrentSession;
			if (currentSession == null)
			{
				return;
			}
			Guid? guid = lifecycleUser;
			if (guid.HasValue)
			{
				Guid valueOrDefault = guid.GetValueOrDefault();
				if (valueOrDefault != currentSession.UserId)
				{
					return;
				}
			}
			if (lifecycleCancellation == null)
			{
				lifecycleUser = currentSession.UserId;
				lifecycleCancellation = new CancellationTokenSource();
				lifetime = new RealtimeLifetime(lifecycleGeneration, currentSession.UserId, lifecycleCancellation.Token);
				supervisorTask = Task.Run(() => SuperviseAsync(lifetime));
			}
			else
			{
				lifetime = new RealtimeLifetime(lifecycleGeneration, currentSession.UserId, lifecycleCancellation.Token);
			}
		}
		await ConnectAndReconcileAsync(lifetime, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
	}

	private async Task SuperviseAsync(RealtimeLifetime lifetime)
	{
		TimeSpan delay = TimeSpan.FromSeconds(2.0);
		try
		{
			while (!lifetime.Token.IsCancellationRequested)
			{
				await Task.Delay(delay, lifetime.Token).ConfigureAwait(continueOnCapturedContext: false);
				if (!IsCurrent(lifetime))
				{
					CancelTokenSession();
					PublishState("auth_required", "session_changed");
					await gate.WaitAsync(lifetime.Token).ConfigureAwait(continueOnCapturedContext: false);
					try
					{
						if (hubConnection != null)
						{
							await hubConnection.StopAsync(lifetime.Token).ConfigureAwait(continueOnCapturedContext: false);
						}
						break;
					}
					finally
					{
						gate.Release();
					}
				}
				try
				{
					await ConnectAndReconcileAsync(lifetime, lifetime.Token).ConfigureAwait(continueOnCapturedContext: false);
					delay = TimeSpan.FromSeconds(2.0);
				}
				catch (OperationCanceledException) when (lifetime.Token.IsCancellationRequested)
				{
					break;
				}
				catch (Exception ex2)
				{
					WriteLog("supervisor", ex2);
					PublishState(IsAuthenticationRejection(ex2) ? "auth_required" : "retrying", SafeConnectionError(ex2));
					delay = TimeSpan.FromSeconds(Math.Min(10.0, delay.TotalSeconds * 2.0));
				}
			}
		}
		catch (OperationCanceledException) when (lifetime.Token.IsCancellationRequested)
		{
		}
	}

	private async Task ConnectAndReconcileAsync(RealtimeLifetime lifetime, CancellationToken caller)
	{
		using CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(caller, lifetime.Token);
		CancellationToken token = cancellation.Token;
		await gate.WaitAsync(token).ConfigureAwait(continueOnCapturedContext: false);
		try
		{
			if (!IsCurrent(lifetime))
			{
				return;
			}
			string text = await authSessionManager.GetAccessTokenAsync(token).ConfigureAwait(continueOnCapturedContext: false);
			if (string.IsNullOrWhiteSpace(text) || !IsCurrent(lifetime))
			{
				CancelTokenSession();
				PublishState("auth_required", "session_expired");
				if (hubConnection != null)
				{
					await hubConnection.StopAsync(token).ConfigureAwait(continueOnCapturedContext: false);
				}
				return;
			}
			if (hubConnection == null)
			{
				hubConnection = CreateConnection();
			}
			if (hubConnection.State == HubConnectionState.Disconnected)
			{
				string fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
				if (rejectedCredentialUser == lifetime.UserId && rejectedCredentialFingerprint == fingerprint)
				{
					PublishState("auth_required", "authentication_failed");
					return;
				}
				PublishState("connecting");
				using CancellationTokenSource attempt = CancellationTokenSource.CreateLinkedTokenSource(token);
				attempt.CancelAfter(TimeSpan.FromSeconds(20.0));
				try
				{
					await hubConnection.StartAsync(attempt.Token).ConfigureAwait(continueOnCapturedContext: false);
				}
				catch (Exception ex)
				{
					WriteLog("connect", ex);
					if (IsAuthenticationRejection(ex))
					{
						rejectedCredentialUser = lifetime.UserId;
						rejectedCredentialFingerprint = fingerprint;
					}
					PublishState(IsAuthenticationRejection(ex) ? "auth_required" : "retrying", SafeConnectionError(ex));
					throw;
				}
				if (!IsCurrent(lifetime))
				{
					return;
				}
				rejectedCredentialFingerprint = null;
				rejectedCredentialUser = null;
				ResetTokenSession();
			}
		}
		finally
		{
			gate.Release();
		}
		if (!IsCurrent(lifetime) || !IsConnected)
		{
			return;
		}
		await ReconcileLibraryPresenceAsync(lifetime, token).ConfigureAwait(continueOnCapturedContext: false);
		bool restore = false;
		lock (lifecycleStateGate)
		{
			if (IsCurrent(lifetime) && restoredConnectionId != hubConnection.ConnectionId)
			{
				restoredConnectionId = hubConnection.ConnectionId;
				restore = true;
			}
		}
		if (restore)
		{
			await RewatchActiveRequestsAsync(token).ConfigureAwait(continueOnCapturedContext: false);
		}
		StartPendingLoop();
		if (restore)
		{
			await ProcessPendingAsync(token).ConfigureAwait(continueOnCapturedContext: false);
		}
	}

	private Task StopSupervisedAsync(CancellationToken caller)
	{
		Task task;
		lock (lifecycleStateGate)
		{
			if (!stopping)
			{
				stopping = true;
				lifecycleGeneration++;
				lifecycleCancellation?.Cancel();
				pendingLoopCts?.Cancel();
				watchedLibraryDetails.Clear();
				task = (stopTask = Task.Run((Func<Task?>)StopCoreAsync));
			}
			else
			{
				task = stopTask;
			}
		}
		CancelTokenSession();
		if (!caller.CanBeCanceled)
		{
			return task;
		}
		return task.WaitAsync(caller);
	}

	private async Task StopCoreAsync()
	{
		await gate.WaitAsync().ConfigureAwait(continueOnCapturedContext: false);
		try
		{
			if (hubConnection != null)
			{
				await hubConnection.StopAsync().ConfigureAwait(continueOnCapturedContext: false);
			}
		}
		finally
		{
			gate.Release();
		}
		Task task = supervisorTask;
		if (task != null)
		{
			await task.ConfigureAwait(continueOnCapturedContext: false);
		}
		Task task2 = pendingLoopTask;
		if (task2 != null)
		{
			await task2.ConfigureAwait(continueOnCapturedContext: false);
		}
		await processGate.WaitAsync().ConfigureAwait(continueOnCapturedContext: false);
		processGate.Release();
		await watchGate.WaitAsync().ConfigureAwait(continueOnCapturedContext: false);
		try
		{
			watchedActiveRequests.Clear();
			lock (lifecycleStateGate)
			{
				watchedLibraryDetails.Clear();
				registeredLibraryDetails.Clear();
				pendingLibraryRemovals.Clear();
				presenceConnectionId = (restoredConnectionId = null);
				pendingLoopCts?.Dispose();
				pendingLoopCts = null;
				lifecycleCancellation?.Dispose();
				lifecycleCancellation = null;
				lifecycleUser = null;
				supervisorTask = (pendingLoopTask = null);
				Interlocked.Exchange(ref pendingLoopStarted, 0);
			}
		}
		finally
		{
			watchGate.Release();
		}
		PublishState("stopped");
		lock (lifecycleStateGate)
		{
			stopping = false;
		}
	}

	private async Task ReconcileLibraryPresenceAsync(RealtimeLifetime lifetime, CancellationToken caller)
	{
		using CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(caller, lifetime.Token);
		CancellationToken token = cancellation.Token;
		await watchGate.WaitAsync(token).ConfigureAwait(continueOnCapturedContext: false);
		try
		{
			if (!IsCurrent(lifetime) || !IsConnected)
			{
				return;
			}
			HubConnection connection = hubConnection;
			string connectionId = connection.ConnectionId;
			Guid[] desired;
			lock (lifecycleStateGate)
			{
				if (presenceConnectionId != connectionId)
				{
					registeredLibraryDetails.Clear();
					pendingLibraryRemovals.Clear();
					presenceConnectionId = connectionId;
				}
				foreach (Guid item in registeredLibraryDetails.Except(watchedLibraryDetails))
				{
					pendingLibraryRemovals.Add(item);
				}
				desired = watchedLibraryDetails.ToArray();
			}
			if (!(await RemoveDepartedLibrariesAsync(lifetime, connection, connectionId, token).ConfigureAwait(continueOnCapturedContext: false)))
			{
				return;
			}
			Guid[] array = desired;
			foreach (Guid id in array)
			{
				lock (lifecycleStateGate)
				{
					if (!watchedLibraryDetails.Contains(id) || registeredLibraryDetails.Contains(id))
					{
						continue;
					}
					goto IL_02c7;
				}
				IL_02c7:
				if (!IsCurrent(lifetime) || connection.ConnectionId != connectionId || !IsConnected)
				{
					return;
				}
				DesktopDenuvoRealtimeState desktopDenuvoRealtimeState = CurrentState;
				bool flag = desktopDenuvoRealtimeState.LibraryId != id;
				if (!flag)
				{
					string stage = desktopDenuvoRealtimeState.Stage;
					bool flag2 = ((stage == "presence_pending" || stage == "presence_rejected") ? true : false);
					flag = !flag2;
				}
				if (flag)
				{
					PublishState("presence_pending");
				}
				try
				{
					using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
					deadline.CancelAfter(TimeSpan.FromSeconds(5.0));
					bool flag3 = await connection.InvokeAsync<bool>("WatchLibraryDetail", id, deadline.Token).ConfigureAwait(continueOnCapturedContext: false);
					if (!IsCurrent(lifetime) || connection.ConnectionId != connectionId)
					{
						return;
					}
					lock (lifecycleStateGate)
					{
						if (flag3)
						{
							registeredLibraryDetails.Add(id);
							if (watchedLibraryDetails.Contains(id))
							{
								pendingLibraryRemovals.Remove(id);
							}
							else
							{
								pendingLibraryRemovals.Add(id);
							}
						}
					}
					if (!flag3)
					{
						PublishState("presence_rejected", "library_not_accepted");
					}
				}
				catch (OperationCanceledException) when (token.IsCancellationRequested)
				{
					throw;
				}
				catch (Exception exception)
				{
					WriteLog("watch-library-detail", exception);
					lock (lifecycleStateGate)
					{
						if (!watchedLibraryDetails.Contains(id))
						{
							pendingLibraryRemovals.Add(id);
						}
					}
					PublishState("presence_pending", "watch_failed");
				}
			}
			if (await RemoveDepartedLibrariesAsync(lifetime, connection, connectionId, token).ConfigureAwait(continueOnCapturedContext: false))
			{
				string text;
				lock (lifecycleStateGate)
				{
					text = ((!watchedLibraryDetails.All(registeredLibraryDetails.Contains)) ? null : ((watchedLibraryDetails.Count == 0) ? "connected" : "tracked"));
				}
				if (text != null)
				{
					PublishState(text);
				}
			}
		}
		finally
		{
			watchGate.Release();
		}
	}

	private async Task<bool> RemoveDepartedLibrariesAsync(RealtimeLifetime lifetime, HubConnection connection, string? connectionId, CancellationToken token)
	{
		Guid[] array;
		lock (lifecycleStateGate)
		{
			array = pendingLibraryRemovals.Where((Guid id) => !watchedLibraryDetails.Contains(id)).ToArray();
		}
		Guid[] array2 = array;
		foreach (Guid id2 in array2)
		{
			if (!IsCurrent(lifetime) || !IsConnected || connection.ConnectionId != connectionId)
			{
				return false;
			}
			lock (lifecycleStateGate)
			{
				registeredLibraryDetails.Remove(id2);
			}
			try
			{
				using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
				deadline.CancelAfter(TimeSpan.FromSeconds(5.0));
				await connection.InvokeAsync("UnwatchLibraryDetail", id2, deadline.Token).ConfigureAwait(continueOnCapturedContext: false);
				lock (lifecycleStateGate)
				{
					registeredLibraryDetails.Remove(id2);
					pendingLibraryRemovals.Remove(id2);
				}
			}
			catch (OperationCanceledException) when (token.IsCancellationRequested)
			{
				throw;
			}
			catch (Exception exception)
			{
				WriteLog("unwatch-library-detail", exception);
				PublishState("presence_pending", "unwatch_failed");
				return false;
			}
		}
		return true;
	}

	private void PublishState(string stage, string? errorCode = null)
	{
		DesktopDenuvoRealtimeState e;
		lock (lifecycleStateGate)
		{
			if (stopping && stage != "stopped")
			{
				return;
			}
			Guid? guid = watchedLibraryDetails.Cast<Guid?>().FirstOrDefault();
			bool isConnected = IsConnected;
			int num;
			if (isConnected && guid.HasValue)
			{
				Guid valueOrDefault = guid.GetValueOrDefault();
				if (presenceConnectionId == hubConnection?.ConnectionId)
				{
					num = (registeredLibraryDetails.Contains(valueOrDefault) ? 1 : 0);
					goto IL_008a;
				}
			}
			num = 0;
			goto IL_008a;
			IL_008a:
			bool flag = (byte)num != 0;
			if (currentState.Stage == stage && currentState.ErrorCode == errorCode && currentState.IsConnected == isConnected)
			{
				Guid? libraryId = currentState.LibraryId;
				Guid? guid2 = guid;
				if (libraryId.HasValue == guid2.HasValue && (!libraryId.HasValue || libraryId.GetValueOrDefault() == guid2.GetValueOrDefault()) && currentState.IsLibraryTracked == flag)
				{
					return;
				}
			}
			e = (currentState = new DesktopDenuvoRealtimeState(++stateRevision, stage, isConnected, guid, flag, errorCode));
		}
		EventHandler<DesktopDenuvoRealtimeState> eventHandler = this.ConnectionStateChanged;
		if (eventHandler == null)
		{
			return;
		}
		Delegate[] invocationList = eventHandler.GetInvocationList();
		for (int i = 0; i < invocationList.Length; i++)
		{
			EventHandler<DesktopDenuvoRealtimeState> eventHandler2 = (EventHandler<DesktopDenuvoRealtimeState>)invocationList[i];
			try
			{
				eventHandler2(this, e);
			}
			catch (Exception exception)
			{
				WriteLog("state-ui", exception);
			}
		}
	}

	private static bool IsAuthenticationRejection(Exception error)
	{
		Stack<Exception> stack = new Stack<Exception>();
		stack.Push(error);
		int num = 0;
		while (stack.Count > 0 && num < 16)
		{
			Exception ex = stack.Pop();
			if ((ex is HttpRequestException { StatusCode: { } statusCode } && (statusCode == HttpStatusCode.Unauthorized || statusCode == HttpStatusCode.Forbidden)) ? true : false)
			{
				return true;
			}
			if (ex is AggregateException ex3)
			{
				foreach (Exception innerException2 in ex3.InnerExceptions)
				{
					stack.Push(innerException2);
				}
			}
			else
			{
				Exception innerException = ex.InnerException;
				if (innerException != null)
				{
					stack.Push(innerException);
				}
			}
			num++;
		}
		return false;
	}

	private static string SafeConnectionError(Exception error)
	{
		if (IsAuthenticationRejection(error))
		{
			return "authentication_failed";
		}
		string result;
		if (error is HttpRequestException { StatusCode: var statusCode })
		{
			switch (statusCode)
			{
			case HttpStatusCode.Unauthorized:
			case HttpStatusCode.Forbidden:
				break;
			case HttpStatusCode.NotFound:
				goto IL_006c;
			default:
				goto IL_007c;
			}
			result = "authentication_failed";
		}
		else
		{
			if (!(error is OperationCanceledException) && !(error is TimeoutException))
			{
				goto IL_007c;
			}
			result = "connection_timeout";
		}
		goto IL_0082;
		IL_006c:
		result = "hub_not_found";
		goto IL_0082;
		IL_007c:
		result = "connection_failed";
		goto IL_0082;
		IL_0082:
		return result;
	}

	private void QueueRealtimeCallback(Func<CancellationToken, Task> action)
	{
		if (!TryGetLifecycle(out var lifetime))
		{
			return;
		}
		Task.Run(async delegate
		{
			try
			{
				if (IsCurrent(lifetime))
				{
					await action(lifetime.Token).ConfigureAwait(continueOnCapturedContext: false);
				}
			}
			catch (OperationCanceledException) when (lifetime.Token.IsCancellationRequested)
			{
			}
			catch (Exception exception)
			{
				WriteLog("callback", exception);
			}
		});
	}
}
