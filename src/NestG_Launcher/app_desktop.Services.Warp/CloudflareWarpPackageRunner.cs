using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace app_desktop.Services.Warp;

internal sealed class CloudflareWarpPackageRunner : IWarpPackageRunner
{
	public async Task<int> RunAsync(string installerPath)
	{
		ProcessStartInfo startInfo = new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "msiexec.exe"))
		{
			UseShellExecute = true,
			Verb = "runas",
			Arguments = "/i \"" + installerPath + "\"",
			WindowStyle = ProcessWindowStyle.Normal
		};
		using Process process = Process.Start(startInfo) ?? throw new InvalidOperationException("Không thể mở bộ cài Cloudflare.");
		await process.WaitForExitAsync().ConfigureAwait(continueOnCapturedContext: false);
		return process.ExitCode;
	}
}
