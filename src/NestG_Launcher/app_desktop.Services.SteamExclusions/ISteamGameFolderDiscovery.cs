using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace app_desktop.Services.SteamExclusions;

internal interface ISteamGameFolderDiscovery
{
	Task<IReadOnlyList<string>> FindAsync(CancellationToken cancellationToken);
}
