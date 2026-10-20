using System.Threading;
using System.Threading.Tasks;

namespace app_desktop.Services.Fix54;

internal interface IFix54ProcessRunner
{
	Task<Fix54ProcessResult> RunAsync(Fix54ProcessRequest request, CancellationToken cancellationToken);
}
