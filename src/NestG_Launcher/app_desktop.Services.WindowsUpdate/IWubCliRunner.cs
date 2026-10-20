using System.Threading;
using System.Threading.Tasks;

namespace app_desktop.Services.WindowsUpdate;

internal interface IWubCliRunner
{
	bool IsReady { get; }

	Task<WubCommandResult> SetBlockedAsync(bool blocked, CancellationToken cancellationToken);
}
