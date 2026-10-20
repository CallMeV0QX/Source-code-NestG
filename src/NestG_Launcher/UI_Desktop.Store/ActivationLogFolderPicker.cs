using System.Windows.Forms;

namespace UI_Desktop.Store;

internal static class ActivationLogFolderPicker
{
	internal static string? Choose(IWin32Window owner)
	{
		using FolderBrowserDialog folderBrowserDialog = new FolderBrowserDialog
		{
			Description = "Chọn thư mục để tải file log ACTIVE",
			UseDescriptionForTitle = true,
			ShowNewFolderButton = true
		};
		return (folderBrowserDialog.ShowDialog(owner) == DialogResult.OK) ? folderBrowserDialog.SelectedPath : null;
	}
}
