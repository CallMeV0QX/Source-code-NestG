using System;
using System.CodeDom.Compiler;
using System.IO;
using System.Text.RegularExpressions;
using System.Text.RegularExpressions.Generated;

namespace app_desktop.Services.SteamConnection;

internal static class SteamConnectionDiagnostics
{
	private static readonly object sync = new object();

	internal static void Write(string stage, string code)
	{
		if (!SafeValue().IsMatch(stage) || !SafeValue().IsMatch(code))
		{
			return;
		}
		try
		{
			lock (sync)
			{
				string text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NestGLauncher", "SteamConnection", "Diagnostics");
				SteamDpiRuntimeClient.EnsureNoReparseAncestors(text);
				Directory.CreateDirectory(text);
				string text2 = Path.Combine(text, "runtime.log");
				if (File.Exists(text2) && new FileInfo(text2).Length >= 131072)
				{
					File.Move(text2, Path.Combine(text, "runtime.previous.log"), overwrite: true);
				}
				File.AppendAllText(text2, $"{DateTimeOffset.UtcNow:O} {stage} {code}{Environment.NewLine}");
			}
		}
		catch
		{
		}
	}

	// [GeneratedRegex("^[A-Za-z0-9_-]{1,48}$", RegexOptions.CultureInvariant)]
	[GeneratedCode("System.Text.RegularExpressions.Generator", "8.0.14.36720")]
	private static Regex SafeValue()
	{
		return _003CRegexGenerator_g_003EF0EE122E5B50AE7A6E112AFB8BF1EBFE9D7DDEF874BBD113E5DA346C1494B7BE9__SafeValue_6.Instance;
	}
}
