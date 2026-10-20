using System.Threading;
using System.Threading.Tasks;

namespace app_desktop.Services.Fix54;

internal interface IFix54RuntimePreparer
{
	Task<Fix54PreparedRuntime> PrepareAsync(string operationDirectory, CancellationToken cancellationToken);
}
