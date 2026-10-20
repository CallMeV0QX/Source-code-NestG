using System;
using System.Threading;
using System.Threading.Tasks;

namespace app_desktop.Services.SteamConnection;

internal interface ISteamDpiRuntime : IAsyncDisposable
{
	bool IsAvailable { get; }

	Task<SteamDpiRuntimeResult> StartAsync(string profile, CancellationToken cancellationToken);

	Task<SteamDpiRuntimeResult> ReadAsync(CancellationToken cancellationToken);

	Task<SteamDpiRuntimeResult> StopAsync(CancellationToken cancellationToken);
}
