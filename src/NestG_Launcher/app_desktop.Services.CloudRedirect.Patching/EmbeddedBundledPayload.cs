using System;
using System.IO;
using System.Reflection;

namespace app_desktop.Services.CloudRedirect.Patching;

internal static class EmbeddedBundledPayload
{
	public static bool IsAvailableForBuild(long steamBuild)
	{
		return GetResourceStream(steamBuild) != null;
	}

	public static bool TryInstall(string steamPath, long steamBuild, Action<string>? log)
	{
		try
		{
			using Stream stream = GetResourceStream(steamBuild);
			if (stream == null)
			{
				log?.Invoke($"Embedded payload: not present for build {steamBuild}");
				return false;
			}
			string expectedCachePath = Fingerprint.GetExpectedCachePath(steamPath);
			string directoryName = Path.GetDirectoryName(expectedCachePath);
			if (!string.IsNullOrEmpty(directoryName))
			{
				Directory.CreateDirectory(directoryName);
			}
			using MemoryStream memoryStream = new MemoryStream(checked((int)stream.Length));
			stream.CopyTo(memoryStream);
			FileUtils.AtomicWriteAllBytes(expectedCachePath, memoryStream.ToArray());
			if (!Fingerprint.ValidatePayloadFile(expectedCachePath))
			{
				log?.Invoke($"Embedded payload for build {steamBuild} failed validation after install");
				try
				{
					File.Delete(expectedCachePath);
				}
				catch
				{
				}
				return false;
			}
			log?.Invoke("Embedded payload installed to " + expectedCachePath);
			return true;
		}
		catch (Exception ex)
		{
			log?.Invoke("Embedded payload install failed: " + ex.Message);
			return false;
		}
	}

	private static Stream? GetResourceStream(long steamBuild)
	{
		Assembly executingAssembly = Assembly.GetExecutingAssembly();
		string value = Fingerprint.ComputeFingerprint();
		string name = $"payloads/{steamBuild}/{value}";
		Stream manifestResourceStream = executingAssembly.GetManifestResourceStream(name);
		if (manifestResourceStream != null)
		{
			return manifestResourceStream;
		}
		string name2 = $"payloads/{steamBuild}/payload";
		manifestResourceStream = executingAssembly.GetManifestResourceStream(name2);
		if (manifestResourceStream != null)
		{
			return manifestResourceStream;
		}
		string value2 = $"payloads/{steamBuild}/";
		string[] manifestResourceNames = executingAssembly.GetManifestResourceNames();
		foreach (string text in manifestResourceNames)
		{
			if (text.Replace('\\', '/').StartsWith(value2))
			{
				return executingAssembly.GetManifestResourceStream(text);
			}
		}
		return null;
	}
}
