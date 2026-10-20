using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace NestG.Updater;

internal sealed record UpdaterOptions(int LauncherProcessId, string StageDirectory, string TargetDirectory, string RestartPath, string LogPath)
{
	public static UpdaterOptions Parse(IReadOnlyList<string> args)
	{
		Dictionary<string, string> dictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		for (int i = 0; i < args.Count; i++)
		{
			string text = args[i];
			if (text.StartsWith("--", StringComparison.Ordinal) && i + 1 < args.Count)
			{
				dictionary[text.Substring(2)] = args[++i];
			}
		}
		int launcherProcessId = ParseProcessId(ReadRequired(dictionary, "pid"));
		string stageDirectory = NormalizeDirectory(ReadRequired(dictionary, "stage"), "stage");
		string targetDirectory = NormalizeDirectory(ReadRequired(dictionary, "target"), "target");
		string fullPath = Path.GetFullPath(ReadRequired(dictionary, "restart"));
		string fullPath2 = Path.GetFullPath(ReadRequired(dictionary, "log"));
		return new UpdaterOptions(launcherProcessId, stageDirectory, targetDirectory, fullPath, fullPath2);
	}

	private static int ParseProcessId(string value)
	{
		if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) || result <= 0)
		{
			throw new ArgumentException("PID launcher không hợp lệ.");
		}
		return result;
	}

	private static string NormalizeDirectory(string value, string name)
	{
		string fullPath = Path.GetFullPath(value);
		if (!Directory.Exists(fullPath))
		{
			throw new DirectoryNotFoundException("Không tìm thấy thư mục " + name + ": " + fullPath);
		}
		return fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
	}

	private static string ReadRequired(IReadOnlyDictionary<string, string> values, string key)
	{
		if (!values.TryGetValue(key, out string value) || string.IsNullOrWhiteSpace(value))
		{
			throw new ArgumentException("Thieu tham so --" + key + ".");
		}
		return value;
	}
}
