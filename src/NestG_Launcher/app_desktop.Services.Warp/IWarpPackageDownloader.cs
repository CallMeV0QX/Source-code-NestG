using System;
using System.Threading;
using System.Threading.Tasks;

namespace app_desktop.Services.Warp;

internal interface IWarpPackageDownloader
{
	Task DownloadAsync(string destinationPath, IProgress<WarpInstallProgress>? progress, CancellationToken cancellationToken);
}
