using System.Threading.Tasks;

namespace app_desktop.Services.Warp;

internal interface IWarpPackageRunner
{
	Task<int> RunAsync(string installerPath);
}
