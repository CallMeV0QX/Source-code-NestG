using System;
using System.Threading;
using System.Threading.Tasks;

namespace app_desktop.Services.WindowsUpdate;

internal sealed class WindowsUpdateSwitchFlow
{
	private readonly WindowsUpdateBlockerService blocker;

	private readonly Func<CancellationToken, Task<bool>> preparePackage;

	internal WindowsUpdateSwitchFlow(WindowsUpdateBlockerService blocker, Func<CancellationToken, Task<bool>> preparePackage)
	{
		this.blocker = blocker;
		this.preparePackage = preparePackage;
	}

	internal async Task<WindowsUpdateSwitchResult> ApplyAsync(bool blocked, CancellationToken cancellationToken)
	{
		WindowsUpdateBlockStatus windowsUpdateBlockStatus = await blocker.GetStatusAsync(cancellationToken);
		if (!windowsUpdateBlockStatus.CanStartToggle || windowsUpdateBlockStatus.IsBlocked == blocked)
		{
			return new WindowsUpdateSwitchResult(windowsUpdateBlockStatus);
		}
		bool prepared = false;
		if (windowsUpdateBlockStatus.Kind == WindowsUpdateBlockKind.MissingWub)
		{
			prepared = true;
			bool ready = await preparePackage(cancellationToken);
			cancellationToken.ThrowIfCancellationRequested();
			windowsUpdateBlockStatus = await blocker.GetStatusAsync(cancellationToken);
			if (!ready || !windowsUpdateBlockStatus.CanToggle)
			{
				return new WindowsUpdateSwitchResult(windowsUpdateBlockStatus, prepared, ready && windowsUpdateBlockStatus.Kind != WindowsUpdateBlockKind.MissingWub);
			}
		}
		if (windowsUpdateBlockStatus.IsBlocked == blocked)
		{
			return new WindowsUpdateSwitchResult(windowsUpdateBlockStatus, prepared);
		}
		return new WindowsUpdateSwitchResult(await blocker.SetBlockedAsync(blocked, cancellationToken), prepared, PackageReady: true, CommandAttempted: true);
	}
}
