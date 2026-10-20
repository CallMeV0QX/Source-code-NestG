using System;
using System.Threading;
using System.Threading.Tasks;

namespace app_desktop.Services.WindowsUpdate;

internal interface IWubPackageDownloader
{
	Task DownloadAsync(string destinationPath, IProgress<WubPackageProgress>? progress, CancellationToken cancellationToken);
}
