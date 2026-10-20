using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace app_desktop.Services.SteamExclusions;

internal interface IDefenderExclusionClient
{
	Task<DefenderSnapshot> ReadAsync(CancellationToken cancellationToken);

	Task<ExclusionApplyResult> AddAndVerifyAsync(IReadOnlyList<string> paths, CancellationToken cancellationToken);
}
