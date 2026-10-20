using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace app_desktop.Services.SteamExclusions;

internal static class SteamExclusionPaths
{
	internal static string? NormalizeLiteralLocalPath(string? value)
	{
		if (string.IsNullOrWhiteSpace(value) || value.Any(char.IsControl) || value.IndexOfAny(new char[9] { '*', '?', '[', ']', '%', '"', '|', '<', '>' }) >= 0)
		{
			return null;
		}
		string text = value.Replace('/', '\\');
		if (!Regex.IsMatch(text, "^[a-zA-Z]:\\\\") || text.Length > 1024 || text.AsSpan(2).Contains(':') || text.Split('\\').Any(delegate(string part)
		{
			bool flag = ((part == "." || part == "..") ? true : false);
			return flag || part.EndsWith(' ') || part.EndsWith('.');
		}))
		{
			return null;
		}
		try
		{
			return Path.TrimEndingDirectorySeparator(Path.GetFullPath(text));
		}
		catch (Exception ex) when (((ex is ArgumentException || ex is NotSupportedException || ex is IOException) ? 1 : 0) != 0)
		{
			return null;
		}
	}

	internal static bool IsSafeExistingGameFolder(string raw)
	{
		string text = NormalizeLiteralLocalPath(raw);
		if (text == null || !text.EndsWith("\\steamapps\\common", StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}
		string folderPath = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
		if (IsSameOrChild(text, folderPath))
		{
			return false;
		}
		try
		{
			DirectoryInfo directoryInfo = new DirectoryInfo(text);
			if (!directoryInfo.Exists)
			{
				return false;
			}
			for (DirectoryInfo directoryInfo2 = directoryInfo; directoryInfo2 != null; directoryInfo2 = directoryInfo2.Parent)
			{
				if ((directoryInfo2.Attributes & FileAttributes.ReparsePoint) != 0)
				{
					return false;
				}
			}
			return true;
		}
		catch (Exception ex) when (((ex is IOException || ex is UnauthorizedAccessException) ? 1 : 0) != 0)
		{
			return false;
		}
	}

	internal static bool IsSameOrChild(string path, string parent)
	{
		if (!string.IsNullOrEmpty(parent))
		{
			if (!string.Equals(path, parent, StringComparison.OrdinalIgnoreCase))
			{
				return path.StartsWith(parent.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase);
			}
			return true;
		}
		return false;
	}

	internal static bool IsCovered(string path, IEnumerable<string> exclusions)
	{
		return exclusions.Any(delegate(string exclusion)
		{
			string text = NormalizeLiteralLocalPath(exclusion);
			return text != null && IsSameOrChild(path, text);
		});
	}
}
