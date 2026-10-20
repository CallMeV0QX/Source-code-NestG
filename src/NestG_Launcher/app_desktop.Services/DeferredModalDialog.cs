using System;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace app_desktop.Services;

internal static class DeferredModalDialog
{
	internal static Task<DialogResult> ShowAsync(Form owner, Func<DialogResult> showDialog)
	{
		ArgumentNullException.ThrowIfNull(owner, "owner");
		ArgumentNullException.ThrowIfNull(showDialog, "showDialog");
		if (CannotShow(owner))
		{
			return Task.FromResult(DialogResult.Cancel);
		}
		TaskCompletionSource<DialogResult> completion = new TaskCompletionSource<DialogResult>(TaskCreationOptions.RunContinuationsAsynchronously);
		owner.FormClosed += OwnerClosed;
		owner.Disposed += OwnerDisposed;
		try
		{
			owner.BeginInvoke(delegate
			{
				Detach();
				if (completion.Task.IsCompleted || CannotShow(owner))
				{
					completion.TrySetResult(DialogResult.Cancel);
					return;
				}
				try
				{
					completion.TrySetResult(showDialog());
				}
				catch (Exception exception)
				{
					completion.TrySetException(exception);
				}
			});
		}
		catch (InvalidOperationException) when (owner.IsDisposed || owner.Disposing || !owner.IsHandleCreated)
		{
			CancelPending();
		}
		catch (Exception exception2)
		{
			Detach();
			completion.TrySetException(exception2);
		}
		return completion.Task;
		void CancelPending()
		{
			Detach();
			completion.TrySetResult(DialogResult.Cancel);
		}
		void Detach()
		{
			owner.FormClosed -= OwnerClosed;
			owner.Disposed -= OwnerDisposed;
		}
		void OwnerClosed(object? sender, FormClosedEventArgs args)
		{
			CancelPending();
		}
		void OwnerDisposed(object? sender, EventArgs args)
		{
			CancelPending();
		}
	}

	private static bool CannotShow(Form owner)
	{
		if (!owner.IsDisposed && !owner.Disposing && owner.IsHandleCreated)
		{
			if (owner.Modal)
			{
				return owner.DialogResult != DialogResult.None;
			}
			return false;
		}
		return true;
	}
}
