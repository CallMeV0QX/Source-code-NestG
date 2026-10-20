using System.Threading;
using System.Threading.Tasks;

namespace app_desktop.Services.WindowsUpdate;

internal interface IWubMachineStateReader
{
	Task<WubMachineState> ReadAsync(CancellationToken cancellationToken);
}
