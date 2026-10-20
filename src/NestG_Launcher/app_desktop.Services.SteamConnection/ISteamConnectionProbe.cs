using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace app_desktop.Services.SteamConnection;

internal interface ISteamConnectionProbe
{
	Task<IReadOnlyDictionary<string, SteamConnectionProbeResult>> CheckAsync(CancellationToken cancellationToken);
}
