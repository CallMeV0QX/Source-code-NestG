using System;
using System.Threading;
using System.Threading.Tasks;

namespace app_desktop.Services;

internal sealed class GameActivationProgressQueue(Func<GameActivationProgress, Task> render, CancellationToken token = default(CancellationToken)) : IProgress<GameActivationProgress>
{
	private readonly SynchronizationContext? context = SynchronizationContext.Current;

	private readonly object gate = new object();

	private Task pending = Task.CompletedTask;

	private bool completed;

	public void Report(GameActivationProgress value)
	{
		lock (gate)
		{
			if (!completed)
			{
				pending = DeliverAsync(pending, value);
			}
		}
	}

	private async Task DeliverAsync(Task previous, GameActivationProgress value)
	{
		await previous.ConfigureAwait(continueOnCapturedContext: false);
		token.ThrowIfCancellationRequested();
		if (context == null)
		{
			await render(value).ConfigureAwait(continueOnCapturedContext: false);
			return;
		}
		TaskCompletionSource completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		using (token.Register(delegate
		{
			completion.TrySetCanceled(token);
		}))
		{
			context.Post(async delegate
			{
				try
				{
					token.ThrowIfCancellationRequested();
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
			completed = true;
			task = pending;
		}
		try
		{
			await task.ConfigureAwait(continueOnCapturedContext: false);
		}
		catch (Exception ex)
		{
			DesktopDiagnosticLog.Write("GameActivationProgress", "RenderStopped", "ErrorType=" + ex.GetType().Name);
		}
	}
}
