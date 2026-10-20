using System;
using System.Threading;
using System.Threading.Tasks;

namespace app_desktop.Services.CloudRedirect;

internal static class CloudRedirectOperationCoordinator
{
	private sealed class Lease(Semaphore semaphore) : IDisposable
	{
		private Semaphore? semaphore = semaphore;

		public void Dispose()
		{
			Semaphore semaphore = Interlocked.Exchange(ref this.semaphore, null);
			if (semaphore == null)
			{
				return;
			}
			try
			{
				semaphore.Release();
			}
			finally
			{
				semaphore.Dispose();
				ProcessGate.Release();
			}
		}
	}

	private const string SemaphoreName = "Local\\NestGLauncher.CloudRedirect.Operation.v1";

	private static readonly SemaphoreSlim ProcessGate = new SemaphoreSlim(1, 1);

	internal static async Task<IDisposable> AcquireAsync(CancellationToken cancellationToken = default(CancellationToken), string? testSemaphoreName = null)
	{
		await ProcessGate.WaitAsync(cancellationToken);
		Semaphore machineGate = null;
		try
		{
			machineGate = new Semaphore(1, 1, testSemaphoreName ?? "Local\\NestGLauncher.CloudRedirect.Operation.v1");
			await Task.Run(delegate
			{
				Wait(machineGate, cancellationToken);
			}, cancellationToken);
			return new Lease(machineGate);
		}
		catch
		{
			machineGate?.Dispose();
			ProcessGate.Release();
			throw;
		}
	}

	private static void Wait(Semaphore semaphore, CancellationToken cancellationToken)
	{
		switch (WaitHandle.WaitAny(new WaitHandle[2] { semaphore, cancellationToken.WaitHandle }, TimeSpan.FromMinutes(3.0)))
		{
		case 1:
			throw new OperationCanceledException(cancellationToken);
		case 258:
			throw new InvalidOperationException("Một tác vụ CloudRedirect khác đang chạy quá lâu. Hãy chờ tác vụ đó hoàn tất hoặc mở lại launcher rồi thử lại.");
		}
	}
}
