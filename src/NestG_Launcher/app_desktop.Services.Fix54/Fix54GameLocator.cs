using System.Threading;
using System.Threading.Tasks;
using app_desktop.Services.CloudSave;

namespace app_desktop.Services.Fix54;

internal sealed class Fix54GameLocator : IFix54GameLocator
{
	public Task<string?> ResolveAsync(int steamAppId, CancellationToken cancellationToken)
	{
		return Task.Run(delegate
		{
			cancellationToken.ThrowIfCancellationRequested();
			string text = CloudSaveSteamShutdownService.FindInstalledGameRoot(SteamInstallLocator.FindSteamDirectory(allowDriveScan: false) ?? throw new Fix54Exception("Không tìm thấy Steam đã cài trên máy."), steamAppId);
			cancellationToken.ThrowIfCancellationRequested();
			if (text != null)
			{
				Fix54Paths.EnsureNoReparse(text);
			}
			return text;
		}, cancellationToken);
	}
}
