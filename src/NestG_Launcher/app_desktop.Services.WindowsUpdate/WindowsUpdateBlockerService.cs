using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace app_desktop.Services.WindowsUpdate;

internal sealed class WindowsUpdateBlockerService
{
	private readonly IWubMachineStateReader reader;

	private readonly IWubCliRunner runner;

	internal WindowsUpdateBlockerService(IWubMachineStateReader? reader = null, IWubCliRunner? runner = null, WubPackageStore? package = null)
	{
		this.reader = reader ?? new WindowsWubMachineStateReader();
		this.runner = runner ?? new WubCliRunner(package ?? new WubPackageStore());
	}

	internal async Task<WindowsUpdateBlockStatus> GetStatusAsync(CancellationToken cancellationToken = default(CancellationToken))
	{
		WubMachineState wubMachineState = await reader.ReadAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		if (!runner.IsReady)
		{
			bool? isBlocked = wubMachineState.Kind == WindowsUpdateBlockKind.Blocked;
			WindowsUpdateBlockKind? machineKind = wubMachineState.Kind;
			return new WindowsUpdateBlockStatus(WindowsUpdateBlockKind.MissingWub, isBlocked, null, null, machineKind);
		}
		return new WindowsUpdateBlockStatus(wubMachineState.Kind, wubMachineState.Kind == WindowsUpdateBlockKind.Blocked, null, null, null);
	}

	internal async Task<WindowsUpdateBlockStatus> SetBlockedAsync(bool blocked, CancellationToken cancellationToken = default(CancellationToken))
	{
		WindowsUpdateBlockStatus initial = await GetStatusAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		if (!initial.CanToggle || initial.IsBlocked == blocked)
		{
			return initial;
		}
		WubCommandResult command = await runner.SetBlockedAsync(blocked, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		if (command.Outcome == WubCommandOutcome.Missing)
		{
			return new WindowsUpdateBlockStatus(WindowsUpdateBlockKind.MissingWub, null, null, null, null);
		}
		if (command.Outcome == WubCommandOutcome.Cancelled)
		{
			ConfiguredTaskAwaitable<WindowsUpdateBlockStatus> configuredTaskAwaitable = GetStatusAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			return await configuredTaskAwaitable with
			{
				LastOutcome = WubCommandOutcome.Cancelled
			};
		}
		WindowsUpdateBlockStatus current = initial;
		for (int attempt = 0; attempt < 5; attempt++)
		{
			current = await GetStatusAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			WindowsUpdateBlockKind kind = current.Kind;
			bool flag = (uint)(kind - 1) <= 1u;
			if (flag && current.IsBlocked == blocked)
			{
				return current;
			}
			flag = command.Outcome != WubCommandOutcome.Completed;
			if (!flag)
			{
				kind = current.Kind;
				bool flag2 = ((kind == WindowsUpdateBlockKind.MissingWub || kind == WindowsUpdateBlockKind.Unavailable) ? true : false);
				flag = flag2;
			}
			if (flag)
			{
				break;
			}
			if (attempt < 4)
			{
				await Task.Delay(1000, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			}
		}
		return current with
		{
			LastOutcome = command.Outcome,
			UnconfirmedRequest = blocked
		};
	}
}
