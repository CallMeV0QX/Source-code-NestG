using System;
using System.Threading;
using System.Threading.Tasks;

namespace app_desktop.Services.SteamExclusions;

internal interface IDefenderPowerShellRunner
{
	Task<DefenderProcessResult> RunAsync(string script, bool elevated, TimeSpan timeout, CancellationToken cancellationToken);
}
