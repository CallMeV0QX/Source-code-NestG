using System.Threading;
using System.Threading.Tasks;

namespace app_desktop.Services.Fix54;

internal interface IFix54GameLocator
{
	Task<string?> ResolveAsync(int steamAppId, CancellationToken cancellationToken);
}
