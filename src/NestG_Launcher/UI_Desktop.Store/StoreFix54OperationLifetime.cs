using System;
using System.Threading;
using System.Threading.Tasks;

namespace UI_Desktop.Store;

internal sealed class StoreFix54OperationLifetime
{
	private sealed class Operation(CancellationTokenSource cancellation)
	{
		internal CancellationTokenSource Cancellation { get; } = cancellation;

		internal TaskCompletionSource Completion { get; } = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
	}

	private readonly object stateLock = new object();

	private Operation? pendingOperation;

	private bool stopping;

	internal bool IsRunning
	{
		get
		{
			lock (stateLock)
			{
				return pendingOperation != null;
			}
		}
	}

	internal bool IsStopping
	{
		get
		{
			lock (stateLock)
			{
				return stopping;
			}
		}
	}

	internal Task? TryStart(Func<CancellationToken, Task> work, CancellationToken ownerToken = default(CancellationToken))
	{
		ArgumentNullException.ThrowIfNull(work, "work");
		Operation operation;
		lock (stateLock)
		{
			if (stopping || pendingOperation != null)
			{
				return null;
			}
			operation = (pendingOperation = new Operation(CancellationTokenSource.CreateLinkedTokenSource(ownerToken)));
		}
		RunAsync(operation, work);
		return operation.Completion.Task;
	}

	internal void Cancel()
	{
		CancellationTokenSource cancellation;
		lock (stateLock)
		{
			cancellation = pendingOperation?.Cancellation;
		}
		RequestCancellation(cancellation);
	}

	internal async Task StopAsync()
	{
		Operation operation;
		lock (stateLock)
		{
			stopping = true;
			operation = pendingOperation;
		}
		try
		{
			RequestCancellation(operation?.Cancellation);
		}
		finally
		{
			if (operation != null)
			{
				await operation.Completion.Task.ConfigureAwait(continueOnCapturedContext: false);
			}
		}
	}

	internal void AllowNewOperations()
	{
		lock (stateLock)
		{
			stopping = false;
		}
	}

	private async Task RunAsync(Operation operation, Func<CancellationToken, Task> work)
	{
		Exception failure = null;
		try
		{
			await work(operation.Cancellation.Token).ConfigureAwait(continueOnCapturedContext: false);
		}
		catch (Exception ex)
		{
			failure = ex;
		}
		finally
		{
			lock (stateLock)
			{
				if (pendingOperation == operation)
				{
					pendingOperation = null;
				}
				if (failure is OperationCanceledException ex2)
				{
					operation.Completion.TrySetCanceled(ex2.CancellationToken);
				}
				else if (failure != null)
				{
					operation.Completion.TrySetException(failure);
				}
				else
				{
					operation.Completion.TrySetResult();
				}
			}
			operation.Cancellation.Dispose();
		}
	}

	private static void RequestCancellation(CancellationTokenSource? cancellation)
	{
		try
		{
			cancellation?.Cancel();
		}
		catch (ObjectDisposedException)
		{
		}
	}
}
