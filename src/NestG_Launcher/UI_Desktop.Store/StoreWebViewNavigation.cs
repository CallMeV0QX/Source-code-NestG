using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.Core;

namespace UI_Desktop.Store;

internal static class StoreWebViewNavigation
{
	internal static async Task NavigateAsync(CoreWebView2 webView, Action navigate, CancellationToken cancellationToken, TimeSpan? timeout = null)
	{
		cancellationToken.ThrowIfCancellationRequested();
		TaskCompletionSource completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		ulong? navigationId = null;
		webView.NavigationStarting += Started;
		webView.DOMContentLoaded += Ready;
		webView.NavigationCompleted += Completed;
		try
		{
			navigate();
			await completion.Task.WaitAsync(timeout ?? TimeSpan.FromSeconds(15.0), cancellationToken);
		}
		catch (TimeoutException innerException)
		{
			webView.Stop();
			throw new TimeoutException("Trang game phản hồi quá chậm. Vui lòng thử lại.", innerException);
		}
		finally
		{
			webView.NavigationStarting -= Started;
			webView.DOMContentLoaded -= Ready;
			webView.NavigationCompleted -= Completed;
		}
		void Completed(object? sender, CoreWebView2NavigationCompletedEventArgs args)
		{
			if (navigationId == args.NavigationId)
			{
				if (args.IsSuccess)
				{
					completion.TrySetResult();
				}
				else
				{
					completion.TrySetException(new InvalidOperationException("Không thể hiển thị trang game. Vui lòng thử lại."));
				}
			}
		}
		void Ready(object? sender, CoreWebView2DOMContentLoadedEventArgs args)
		{
			if (navigationId == args.NavigationId)
			{
				completion.TrySetResult();
			}
		}
		void Started(object? sender, CoreWebView2NavigationStartingEventArgs args)
		{
			ulong valueOrDefault = navigationId.GetValueOrDefault();
			if (!navigationId.HasValue)
			{
				valueOrDefault = args.NavigationId;
				navigationId = valueOrDefault;
			}
		}
	}
}
