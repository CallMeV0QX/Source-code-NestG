using System.Threading;
using System.Threading.Tasks;

namespace app_desktop.Services.Fix54;

internal interface IFix54FilePublisher
{
	Task ReplaceAsync(string source, string destination, string expectedDestinationHash, CancellationToken cancellationToken);
}
