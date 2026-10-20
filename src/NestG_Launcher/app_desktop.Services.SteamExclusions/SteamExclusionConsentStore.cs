using System;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace app_desktop.Services.SteamExclusions;

internal sealed class SteamExclusionConsentStore(string directory) : ISteamExclusionConsentStore
{
	public SteamExclusionConsent Load()
	{
		string path2 = Path.Combine(directory, "consent-v1.json");
		if (!File.Exists(path2))
		{
			return SteamExclusionConsent.Empty;
		}
		using StreamReader streamReader = new StreamReader(path2);
		char[] array = new char[65537];
		int num = streamReader.ReadBlock(array, 0, array.Length);
		if (num > 65536)
		{
			throw new InvalidDataException("Invalid exclusion consent.");
		}
		SteamExclusionConsent steamExclusionConsent = JsonSerializer.Deserialize<SteamExclusionConsent>(new string(array, 0, num));
		if (steamExclusionConsent?.ApprovedPaths == null || steamExclusionConsent.ApprovedPaths.Length > 256 || steamExclusionConsent.ApprovedPaths.Any((string path) => SteamExclusionPaths.NormalizeLiteralLocalPath(path) == null))
		{
			throw new InvalidDataException("Invalid exclusion consent.");
		}
		return steamExclusionConsent;
	}

	public void Save(SteamExclusionConsent consent)
	{
		Directory.CreateDirectory(directory);
		string text = Path.Combine(directory, "consent-" + Guid.NewGuid().ToString("N") + ".tmp");
		try
		{
			File.WriteAllText(text, JsonSerializer.Serialize(consent));
			File.Move(text, Path.Combine(directory, "consent-v1.json"), overwrite: true);
		}
		finally
		{
			if (File.Exists(text))
			{
				File.Delete(text);
			}
		}
	}
}
