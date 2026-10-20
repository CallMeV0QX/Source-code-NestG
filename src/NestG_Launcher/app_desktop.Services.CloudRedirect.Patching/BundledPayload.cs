using System;
using System.IO;

namespace app_desktop.Services.CloudRedirect.Patching;

internal static class BundledPayload
{
	public static string GetBundledPath(long steamBuild)
	{
		return Path.Combine(AppContext.BaseDirectory, path4: Fingerprint.ComputeFingerprint(), path2: "payloads", path3: steamBuild.ToString());
	}

	public static bool IsAvailableForBuild(long steamBuild)
	{
		try
		{
			return File.Exists(GetBundledPath(steamBuild));
		}
		catch
		{
			return false;
		}
	}

	public static bool TryInstall(string steamPath, long steamBuild, Action<string> log)
	{
		if (!SteamDetector.IsSupportedSteamVersion(steamBuild))
		{
			log?.Invoke($"Bundled payload: Steam build {steamBuild} not in supported list, refusing");
			return false;
		}
		try
		{
			string bundledPath = GetBundledPath(steamBuild);
			if (!File.Exists(bundledPath))
			{
				log?.Invoke($"Bundled payload: not present for build {steamBuild} (looked in {bundledPath})");
				return false;
			}
			if (!Fingerprint.ValidatePayloadFile(bundledPath))
			{
				log?.Invoke("Bundled payload at " + bundledPath + " failed validation, refusing to install");
				return false;
			}
			string expectedCachePath = Fingerprint.GetExpectedCachePath(steamPath);
			string directoryName = Path.GetDirectoryName(expectedCachePath);
			if (!string.IsNullOrEmpty(directoryName))
			{
				Directory.CreateDirectory(directoryName);
			}
			File.Copy(bundledPath, expectedCachePath, overwrite: true);
			log?.Invoke("Bundled payload installed: " + bundledPath + " -> " + expectedCachePath);
			return true;
		}
		catch (Exception ex)
		{
			log?.Invoke("Bundled payload copy failed: " + ex.Message);
			return false;
		}
	}
}
