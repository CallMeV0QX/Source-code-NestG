using System;
using System.CodeDom.Compiler;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Text.RegularExpressions.Generated;

namespace app_desktop.Services.CloudRedirect;

internal static class BetterSteamToolsHealthReader
{
	internal static BetterSteamToolsLogMarker Capture()
	{
		return new BetterSteamToolsLogMarker(DateTime.UtcNow);
	}

	internal static BetterSteamToolsLogMarker Capture(string steamDirectory)
	{
		string path = Path.Combine(steamDirectory, "opensteamtool", "nestg_startup.log");
		CloudRedirectRuntimeService.EnsureRecoveryPathIsNotLinked(path);
		byte[] array = Array.Empty<byte>();
		if (File.Exists(path))
		{
			using FileStream fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
			if (fileStream.Length > 1048576)
			{
				throw new IOException("Native startup log exceeds limit.");
			}
			array = new byte[checked((int)fileStream.Length)];
			fileStream.ReadExactly(array);
		}
		DateTime utcNow = DateTime.UtcNow;
		return new BetterSteamToolsLogMarker(new DateTime(utcNow.Ticks - utcNow.Ticks % 10000, DateTimeKind.Utc))
		{
			Length = array.Length,
			PrefixSha256 = Convert.ToHexString(SHA256.HashData(array))
		};
	}

	internal static BetterSteamToolsHealth Inspect(string steamDirectory, BetterSteamToolsLogMarker marker, CloudRedirectSteamSession session)
	{
		string path = Path.Combine(steamDirectory, "opensteamtool", "nestg_startup.log");
		try
		{
			CloudRedirectRuntimeService.EnsureRecoveryPathIsNotLinked(path);
			using FileStream fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
			if (fileStream.Length > 1048576)
			{
				return new BetterSteamToolsHealth(IsReady: false, CoreFailed: false, CloudFailed: false);
			}
			long? length = marker.Length;
			if (length.HasValue)
			{
				long valueOrDefault = length.GetValueOrDefault();
				if (valueOrDefault < 0 || valueOrDefault > fileStream.Length || valueOrDefault > 1048576)
				{
					return new BetterSteamToolsHealth(IsReady: false, CoreFailed: false, CloudFailed: false);
				}
				byte[] array = new byte[checked((int)valueOrDefault)];
				fileStream.ReadExactly(array);
				if (!string.Equals(Convert.ToHexString(SHA256.HashData(array)), marker.PrefixSha256, StringComparison.Ordinal))
				{
					return new BetterSteamToolsHealth(IsReady: false, CoreFailed: false, CloudFailed: false);
				}
			}
			using StreamReader streamReader = new StreamReader(fileStream, Encoding.UTF8);
			return Parse(streamReader.ReadToEnd(), marker, session);
		}
		catch (Exception ex) when (((ex is IOException || ex is UnauthorizedAccessException) ? 1 : 0) != 0)
		{
			return new BetterSteamToolsHealth(IsReady: false, CoreFailed: false, CloudFailed: false);
		}
	}

	internal static BetterSteamToolsHealth Parse(string content, BetterSteamToolsLogMarker marker, CloudRedirectSteamSession session)
	{
		bool flag = false;
		DateTime? initializationStartedAtUtc = null;
		bool flag2 = false;
		bool flag3 = false;
		bool flag4 = false;
		string[] array = content.Split('\n');
		foreach (string text in array)
		{
			Match match = StageLine().Match(text.Trim());
			if (match.Success && int.TryParse(match.Groups["pid"].Value, out var result) && result == session.ProcessId && DateTime.TryParseExact(match.Groups["time"].Value, "yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var result2) && !(result2 < marker.CapturedAtUtc) && !(result2 < session.StartedAtUtc))
			{
				string value = match.Groups["stage"].Value;
				if (value == "runtime.init.begin")
				{
					initializationStartedAtUtc = result2;
					flag = false;
					flag3 = false;
					flag4 = false;
					flag2 = false;
				}
				if (value == "runtime.init.complete" && initializationStartedAtUtc.HasValue)
				{
					flag = true;
				}
				if (value == "cloud.init.complete" && initializationStartedAtUtc.HasValue)
				{
					flag2 = true;
				}
				if (value.StartsWith("cloud.", StringComparison.Ordinal))
				{
					flag4 |= value.EndsWith(".failed", StringComparison.Ordinal) || value == "cloud.file.missing";
				}
				else
				{
					flag3 |= value.EndsWith(".failed", StringComparison.Ordinal) || value.StartsWith("runtime.exception", StringComparison.Ordinal);
				}
			}
		}
		return new BetterSteamToolsHealth(flag && !flag3, flag3, flag4)
		{
			InitializationStartedAtUtc = initializationStartedAtUtc,
			CloudReady = (flag2 && !flag4)
		};
	}

	// [GeneratedRegex("^(?<time>\\d{4}-\\d{2}-\\d{2}T\\d{2}:\\d{2}:\\d{2}\\.\\d{3}Z) PID=(?<pid>\\d+) TID=\\d+ Stage=(?<stage>[a-z.]+) Error=\\d+$", RegexOptions.CultureInvariant)]
	[GeneratedCode("System.Text.RegularExpressions.Generator", "8.0.14.36720")]
	private static Regex StageLine()
	{
		return _003CRegexGenerator_g_003EF0EE122E5B50AE7A6E112AFB8BF1EBFE9D7DDEF874BBD113E5DA346C1494B7BE9__StageLine_0.Instance;
	}
}
