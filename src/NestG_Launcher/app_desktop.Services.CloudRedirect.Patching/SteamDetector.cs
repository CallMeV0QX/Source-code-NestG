using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace app_desktop.Services.CloudRedirect.Patching;

internal static class SteamDetector
{
	public static readonly long[] SupportedSteamVersions = new long[6] { 1781041600L, 1780352834L, 1779918128L, 1779486452L, 1778281814L, 1778003620L };

	public static bool IsSupportedSteamVersion(long version)
	{
		return SupportedSteamVersions.Contains(version);
	}

	public static string? FindSteamPath()
	{
		return SteamInstallLocator.FindSteamDirectory();
	}

	public static long? GetSteamVersion()
	{
		string text = FindSteamPath();
		if (!string.IsNullOrWhiteSpace(text))
		{
			return GetSteamVersion(text);
		}
		return null;
	}

	public static long? GetSteamVersion(string steamPath)
	{
		try
		{
			string path = Path.Combine(steamPath, "package", "steam_client_win64.manifest");
			if (!File.Exists(path))
			{
				return null;
			}
			foreach (string item in File.ReadLines(path))
			{
				string text = item.Trim();
				if (!text.StartsWith("\"version\"", StringComparison.Ordinal))
				{
					continue;
				}
				int num = text.LastIndexOf('"');
				int num2 = text.LastIndexOf('"', num - 1);
				if (num > num2 && num2 >= 0)
				{
					int num3 = num2 + 1;
					if (long.TryParse(text.Substring(num3, num - num3), out var result))
					{
						return result;
					}
				}
			}
		}
		catch
		{
			return null;
		}
		return null;
	}

	public static bool IsSteamRunning()
	{
		try
		{
			Process[] processesByName = Process.GetProcessesByName("steam");
			bool result = processesByName.Any((Process process) => !process.HasExited);
			Process[] array = processesByName;
			for (int i = 0; i < array.Length; i++)
			{
				array[i].Dispose();
			}
			return result;
		}
		catch
		{
			return false;
		}
	}
}
