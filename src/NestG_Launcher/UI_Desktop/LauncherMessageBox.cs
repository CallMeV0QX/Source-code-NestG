using System.Threading.Tasks;
using System.Windows.Forms;
using app_desktop.Services;

namespace UI_Desktop;

internal static class LauncherMessageBox
{
	internal static Task<DialogResult> ShowAsync(Form owner, string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon)
	{
		return DeferredModalDialog.ShowAsync(owner, () => Show(owner, text, caption, buttons, icon));
	}

	internal static DialogResult Show(string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon)
	{
		using LauncherMessageDialog launcherMessageDialog = new LauncherMessageDialog(text, caption, buttons, icon);
		return launcherMessageDialog.ShowDialog();
	}

	internal static DialogResult Show(IWin32Window owner, string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon)
	{
		using LauncherMessageDialog launcherMessageDialog = new LauncherMessageDialog(text, caption, buttons, icon);
		return launcherMessageDialog.ShowDialog(owner);
	}
}
