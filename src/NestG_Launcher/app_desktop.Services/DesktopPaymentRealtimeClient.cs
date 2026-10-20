using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http.Connections.Client;
using Microsoft.AspNetCore.SignalR.Client;
using UI_Desktop.Store;

namespace app_desktop.Services;

internal sealed class DesktopPaymentRealtimeClient : IAsyncDisposable
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

	public event EventHandler<DesktopCheckoutStatusResponse>? PaymentStatusUpdated;

	public DesktopPaymentRealtimeClient(string apiBaseUrl, AuthSessionManager authSessionManager)
	{
		this.authSessionManager = authSessionManager;
		hubUrl = new Uri(new Uri(NormalizeBaseUrl(apiBaseUrl)), "hubs/payments").ToString();
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
		HubConnection result = ((IHubConnectionBuilder)new HubConnectionBuilder()).WithUrl(hubUrl, (Action<HttpConnectionOptions>)delegate(HttpConnectionOptions options)
		{
			options.AccessTokenProvider = () => authSessionManager.GetAccessTokenAsync();
		}).WithAutomaticReconnect(ReconnectDelays).Build();
		result.On("PaymentStatusUpdated", delegate(DesktopCheckoutStatusResponse status)
		{
			this.PaymentStatusUpdated?.Invoke(this, status);
		});
		return result;
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
