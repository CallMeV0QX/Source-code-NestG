using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http.Connections.Client;
using Microsoft.AspNetCore.SignalR.Client;

namespace app_desktop.Services;

internal sealed class DesktopSupportRealtimeClient : IAsyncDisposable
{
	private static readonly TimeSpan[] ReconnectDelays = new TimeSpan[4]
	{
		TimeSpan.Zero,
		TimeSpan.FromSeconds(2.0),
		TimeSpan.FromSeconds(5.0),
		TimeSpan.FromSeconds(10.0)
	};

	private readonly AuthSessionManager authSessionManager;

	private readonly string hubUrl;

	private readonly SemaphoreSlim gate = new SemaphoreSlim(1, 1);

	private HubConnection? hubConnection;

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

	public event EventHandler<DesktopSupportConversationUpdatedPayload>? ConversationUpdated;

	public event EventHandler<DesktopSupportMessageReceivedPayload>? MessageReceived;

	public event EventHandler<DesktopSupportMessageReadPayload>? MessageRead;

	public event EventHandler? Reconnected;

	public DesktopSupportRealtimeClient(string apiBaseUrl, AuthSessionManager authSessionManager)
	{
		this.authSessionManager = authSessionManager;
		hubUrl = new Uri(new Uri(NormalizeBaseUrl(apiBaseUrl)), "hubs/support-chat").ToString();
	}

	public async Task StartAsync(CancellationToken cancellationToken = default(CancellationToken))
	{
		if (string.IsNullOrWhiteSpace(await authSessionManager.GetAccessTokenAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false)))
		{
			return;
		}
		await gate.WaitAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		try
		{
			if (hubConnection == null)
			{
				hubConnection = CreateConnection();
			}
			HubConnectionState state = hubConnection.State;
			if ((uint)(state - 1) > 2u)
			{
				await hubConnection.StartAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			}
		}
		finally
		{
			gate.Release();
		}
	}

	public async Task JoinConversationAsync(Guid conversationId, CancellationToken cancellationToken = default(CancellationToken))
	{
		if (!(conversationId == Guid.Empty))
		{
			await StartAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			HubConnection? obj = hubConnection;
			if (obj != null && obj.State == HubConnectionState.Connected)
			{
				await hubConnection.InvokeAsync("JoinConversation", conversationId, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			}
		}
	}

	public async Task StopAsync(CancellationToken cancellationToken = default(CancellationToken))
	{
		await gate.WaitAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		try
		{
			if (hubConnection != null && hubConnection.State != 0)
			{
				await hubConnection.StopAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			}
		}
		finally
		{
			gate.Release();
		}
	}

	public async ValueTask DisposeAsync()
	{
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
	}

	private HubConnection CreateConnection()
	{
		HubConnection obj = ((IHubConnectionBuilder)new HubConnectionBuilder()).WithUrl(hubUrl, (Action<HttpConnectionOptions>)delegate(HttpConnectionOptions options)
		{
			options.AccessTokenProvider = () => authSessionManager.GetAccessTokenAsync();
		}).WithAutomaticReconnect(ReconnectDelays).Build();
		obj.On("ConversationUpdated", delegate(DesktopSupportConversationUpdatedPayload payload)
		{
			this.ConversationUpdated?.Invoke(this, payload);
		});
		obj.On("MessageReceived", delegate(DesktopSupportMessageReceivedPayload payload)
		{
			this.MessageReceived?.Invoke(this, payload);
		});
		obj.On("MessageRead", delegate(DesktopSupportMessageReadPayload payload)
		{
			this.MessageRead?.Invoke(this, payload);
		});
		obj.Reconnected += delegate
		{
			this.Reconnected?.Invoke(this, EventArgs.Empty);
			return Task.CompletedTask;
		};
		return obj;
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
}
