using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Win32;

namespace app_desktop.Services.SteamExclusions;

internal sealed class SteamExclusionOwnership : ISteamExclusionOwnership
{
	internal const string RegistryPath = "SOFTWARE\\NestGLauncher.SteamExclusions.v2";

	public IReadOnlyList<string> ReadPaths()
	{
		using RegistryKey registryKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
		using RegistryKey registryKey2 = registryKey.OpenSubKey("SOFTWARE\\NestGLauncher.SteamExclusions.v2");
		if (registryKey2 == null)
		{
			return Array.Empty<string>();
		}
		object value = registryKey2.GetValue("Paths");
		if (value == null)
		{
			return Array.Empty<string>();
		}
		if (!(value is string[] array) || array.Length > 256 || array.Any((string path) => !IsManagedPath(path)))
		{
			throw new InvalidDataException("Invalid Steam exclusion ledger.");
		}
		return array.Distinct<string>(StringComparer.OrdinalIgnoreCase).ToArray();
	}

	internal static bool IsManagedPath(string path)
	{
		string text = SteamExclusionPaths.NormalizeLiteralLocalPath(path);
		if (text != null && text.EndsWith("\\steamapps\\common", StringComparison.OrdinalIgnoreCase))
		{
			return !SteamExclusionPaths.IsSameOrChild(text, Environment.GetFolderPath(Environment.SpecialFolder.Windows));
		}
		return false;
	}
}
