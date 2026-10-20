using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace app_desktop.Services;

internal sealed class DeferredWebViewMessageDispatcher
{
	private readonly Func<Action, bool> tryPost;

	private readonly Func<Exception, Task> handleUnhandledExceptionAsync;

	private readonly CancellationToken cancellationToken;

	internal DeferredWebViewMessageDispatcher(Control owner, Func<Exception, Task> handleUnhandledExceptionAsync, CancellationToken cancellationToken)
		: this(delegate(Action action)
		{
			if (owner.IsDisposed || owner.Disposing || !owner.IsHandleCreated)
			{
				return false;
			}
			owner.BeginInvoke(action);
			return true;
		}, handleUnhandledExceptionAsync, cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(owner, "owner");
	}

	internal DeferredWebViewMessageDispatcher(Func<Action, bool> tryPost, Func<Exception, Task> handleUnhandledExceptionAsync, CancellationToken cancellationToken)
	{
		this.tryPost = tryPost ?? throw new ArgumentNullException("tryPost");
		this.handleUnhandledExceptionAsync = handleUnhandledExceptionAsync ?? throw new ArgumentNullException("handleUnhandledExceptionAsync");
		this.cancellationToken = cancellationToken;
	}

	internal bool TryDispatch(Func<Task> action)
	{
		ArgumentNullException.ThrowIfNull(action, "action");
		if (cancellationToken.IsCancellationRequested)
		{
			return false;
		}
		try
		{
			return tryPost(delegate
			{
				if (!cancellationToken.IsCancellationRequested)
				{
					ExecuteAsync(action);
				}
			});
		}
		catch
		{
			return false;
		}
	}

	private async Task ExecuteAsync(Func<Task> action)
	{
		try
		{
			await action();
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
		}
		catch (Exception exception)
		{
			await ReportUnhandledExceptionAsync(exception);
		}
	}

	private async Task ReportUnhandledExceptionAsync(Exception exception)
	{
		try
		{
			await handleUnhandledExceptionAsync(exception);
		}
		catch
		{
		}
	}
}
