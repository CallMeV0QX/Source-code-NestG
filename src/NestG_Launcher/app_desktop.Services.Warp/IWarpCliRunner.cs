using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace app_desktop.Services.Warp;

internal interface IWarpCliRunner
{
	bool IsInstalled { get; }

	Task<WarpCliResult> RunAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken);
}
