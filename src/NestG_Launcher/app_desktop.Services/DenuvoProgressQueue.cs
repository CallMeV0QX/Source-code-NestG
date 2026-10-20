using System;
using System.Threading;
using System.Threading.Tasks;

namespace app_desktop.Services;

internal sealed class DenuvoProgressQueue(Func<DenuvoInstallProgress, Task> render, CancellationToken cancellationToken = default(CancellationToken)) : IProgress<DenuvoInstallProgress>
{
	private readonly SynchronizationContext? context = SynchronizationContext.Current;

	private readonly object gate = new object();

	private Task pending = Task.CompletedTask;

	public void Report(DenuvoInstallProgress value)
	{
		lock (gate)
		{
			pending = DeliverAsync(pending, value);
		}
	}

	private async Task DeliverAsync(Task previous, DenuvoInstallProgress value)
	{
		await previous.ConfigureAwait(continueOnCapturedContext: false);
		cancellationToken.ThrowIfCancellationRequested();
		if (context == null)
		{
			await render(value).ConfigureAwait(continueOnCapturedContext: false);
			return;
		}
		TaskCompletionSource completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		using (cancellationToken.Register(delegate
		{
			completion.TrySetCanceled(cancellationToken);
		}))
		{
			context.Post(async delegate
			{
				try
				{
					cancellationToken.ThrowIfCancellationRequested();
					await render(value);
					completion.TrySetResult();
				}
				catch (Exception exception)
				{
					completion.TrySetException(exception);
				}
			}, null);
			await completion.Task.ConfigureAwait(continueOnCapturedContext: false);
		}
	}

	internal async Task FlushAsync()
	{
		Task task;
		lock (gate)
		{
			task = pending;
		}
		try
		{
			await task.ConfigureAwait(continueOnCapturedContext: false);
		}
		catch (Exception ex)
		{
			DesktopDiagnosticLog.Write("DenuvoProgress", "RenderStopped", "ErrorType=" + ex.GetType().Name);
		}
	}
}
