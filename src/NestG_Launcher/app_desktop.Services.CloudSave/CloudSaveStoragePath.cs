using System;
using System.IO;

namespace app_desktop.Services.CloudSave;

internal static class CloudSaveStoragePath
{
	internal static string GetRoot(string storageFolderName, string? localAppDataRoot = null)
	{
		string fullPath = Path.GetFullPath(localAppDataRoot ?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
		string text = (string.IsNullOrWhiteSpace(storageFolderName) ? string.Empty : Path.GetFileName(storageFolderName.Trim().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)));
		bool flag = string.IsNullOrWhiteSpace(text);
		if (!flag)
		{
			bool flag2 = ((text == "." || text == "..") ? true : false);
			flag = flag2;
		}
		string path = ((flag || text.Length > 80 || text.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) ? "NestGLauncher" : text);
		return Path.Combine(fullPath, path);
	}
}
