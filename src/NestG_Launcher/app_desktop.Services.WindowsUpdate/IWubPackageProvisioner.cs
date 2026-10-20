using System;
using System.Threading;
using System.Threading.Tasks;

namespace app_desktop.Services.WindowsUpdate;

internal interface IWubPackageProvisioner
{
	Task<WubPackageResult> DownloadAndImportAsync(IProgress<WubPackageProgress>? progress, CancellationToken cancellationToken);
}
