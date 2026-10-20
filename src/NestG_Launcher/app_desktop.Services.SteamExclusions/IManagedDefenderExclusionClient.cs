using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace app_desktop.Services.SteamExclusions;

internal interface IManagedDefenderExclusionClient
{
	Task<SteamExclusionToggleResult> SetEnabledAsync(bool enabled, IReadOnlyList<string> paths, CancellationToken cancellationToken);
}
