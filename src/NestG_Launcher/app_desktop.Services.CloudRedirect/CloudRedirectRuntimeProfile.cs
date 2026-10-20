using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using UI_Desktop.Store;

namespace app_desktop.Services.CloudRedirect;

internal sealed class CloudRedirectRuntimeProfile
{
	internal const string LegacyBundleId = "opensteamtool-cloud";

	internal const string BetterBundleId = "bettersteamtools-cloud";

	private readonly DesktopRuntimeBundleResponse manifest;

	internal static CloudRedirectRuntimeProfile Legacy { get; } = Load("opensteamtool-cloud");

	internal static CloudRedirectRuntimeProfile Better { get; } = Load("bettersteamtools-cloud");

	internal static CloudRedirectRuntimeProfile Better100 { get; } = Load("bettersteamtools-cloud", "1.0.0");

	internal static CloudRedirectRuntimeProfile Better101 { get; } = Load("bettersteamtools-cloud", "1.0.1");

	internal static CloudRedirectRuntimeProfile Better102 { get; } = Load("bettersteamtools-cloud", "1.0.2");

	internal static CloudRedirectRuntimeProfile Better103 { get; } = Load("bettersteamtools-cloud", "1.0.3");

	internal static CloudRedirectRuntimeProfile Better104 { get; } = Load("bettersteamtools-cloud", "1.0.4");

	internal static CloudRedirectRuntimeProfile Better105 { get; } = Load("bettersteamtools-cloud", "1.0.5");

	internal static CloudRedirectRuntimeProfile Better106 { get; } = Load("bettersteamtools-cloud", "1.0.6");

	internal static IReadOnlyList<CloudRedirectRuntimeProfile> Known { get; } = new _003C_003Ez__ReadOnlyArray<CloudRedirectRuntimeProfile>(new CloudRedirectRuntimeProfile[9] { Better, Better106, Better105, Better104, Better103, Better102, Better101, Better100, Legacy });

	internal string BundleId => manifest.BundleId;

	internal string Version => manifest.Version;

	internal string Commit => manifest.OpenSteamToolCommit;

	internal string CloudRedirectVersion => manifest.CloudRedirectVersion;

	internal string DisplayName
	{
		get
		{
			if (!(BundleId == "bettersteamtools-cloud"))
			{
				return "OpenSteamTool";
			}
			return "BetterSteamTools";
		}
	}

	internal bool IsBetterSteamTools => BundleId == "bettersteamtools-cloud";

	internal bool ReplacesExistingMainDll
	{
		get
		{
			bool flag = IsBetterSteamTools;
			if (flag)
			{
				bool flag2;
				switch (Version)
				{
				case "1.0.5":
				case "1.0.6":
				case "1.0.7":
					flag2 = true;
					break;
				default:
					flag2 = false;
					break;
				}
				flag = flag2;
			}
			return flag;
		}
	}

	internal string MainDllName
	{
		get
		{
			if (!Files.ContainsKey("NestG.dll"))
			{
				if (!Files.ContainsKey("NestGSteamTools.dll"))
				{
					return "OpenSteamTool.dll";
				}
				return "NestGSteamTools.dll";
			}
			return "NestG.dll";
		}
	}

	internal IReadOnlyList<string> RetiredMainDllNames
	{
		get
		{
			if (ReplacesExistingMainDll)
			{
				return new _003C_003Ez__ReadOnlyArray<string>(new string[2] { "NestG.dll", "NestGSteamTools.dll" });
			}
			string mainDllName = MainDllName;
			return (mainDllName == "NestG.dll") ? new _003C_003Ez__ReadOnlyArray<string>(new string[2] { "OpenSteamTool.dll", "NestGSteamTools.dll" }) : ((!(mainDllName == "NestGSteamTools.dll")) ? ((IReadOnlyList<string>)Array.Empty<string>()) : ((IReadOnlyList<string>)new _003C_003Ez__ReadOnlySingleElementList<string>("OpenSteamTool.dll")));
		}
	}

	internal IEnumerable<string> RootDllNames => Files.Keys.Where((string path) => !path.Contains('/') && path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase));

	internal IReadOnlyDictionary<string, DesktopRuntimeBundleFileResponse> Files { get; }

	internal IReadOnlyDictionary<string, string> Hashes { get; }

	internal static bool IsCloudFile(string installPath)
	{
		return string.Equals(installPath, "cloud_redirect.dll", StringComparison.OrdinalIgnoreCase);
	}

	internal IReadOnlyDictionary<string, DesktopRuntimeBundleFileResponse> RequiredFiles(bool betterOnly)
	{
		if (betterOnly && !IsBetterSteamTools)
		{
			throw new InvalidDataException("Better-only requires a BetterSteamTools profile.");
		}
		if (!betterOnly)
		{
			return Files;
		}
		return Files.Where<KeyValuePair<string, DesktopRuntimeBundleFileResponse>>((KeyValuePair<string, DesktopRuntimeBundleFileResponse> file) => !IsCloudFile(file.Key)).ToDictionary<KeyValuePair<string, DesktopRuntimeBundleFileResponse>, string, DesktopRuntimeBundleFileResponse>((KeyValuePair<string, DesktopRuntimeBundleFileResponse> file) => file.Key, (KeyValuePair<string, DesktopRuntimeBundleFileResponse> file) => file.Value, StringComparer.OrdinalIgnoreCase);
	}

	private CloudRedirectRuntimeProfile(DesktopRuntimeBundleResponse manifest)
	{
		this.manifest = manifest;
		Files = manifest.Files.ToDictionary<DesktopRuntimeBundleFileResponse, string>((DesktopRuntimeBundleFileResponse file) => file.InstallPath, StringComparer.OrdinalIgnoreCase);
		Hashes = manifest.Files.ToDictionary<DesktopRuntimeBundleFileResponse, string, string>((DesktopRuntimeBundleFileResponse file) => file.InstallPath, (DesktopRuntimeBundleFileResponse file) => file.Sha256, StringComparer.OrdinalIgnoreCase);
	}

	internal static CloudRedirectRuntimeProfile Resolve(string bundleId, string version)
	{
		return Known.FirstOrDefault((CloudRedirectRuntimeProfile profile) => profile.BundleId == bundleId && profile.Version == version) ?? throw new InvalidDataException("Runtime bundle/version chưa được launcher tin cậy.");
	}

	internal static CloudRedirectRuntimeProfile? FindInstalled(string steamDirectory)
	{
		return Known.FirstOrDefault((CloudRedirectRuntimeProfile profile) => profile.IsInstalled(steamDirectory));
	}

	internal static bool IsKnownFileHash(string installPath, string hash)
	{
		string value;
		return Known.Any((CloudRedirectRuntimeProfile profile) => profile.Hashes.TryGetValue(installPath, out value) && string.Equals(value, hash, StringComparison.OrdinalIgnoreCase));
	}

	internal void Validate(DesktopRuntimeBundleResponse bundle, string desktopVersion, bool betterOnly = false)
	{
		IReadOnlyDictionary<string, DesktopRuntimeBundleFileResponse> readOnlyDictionary = RequiredFiles(betterOnly);
		if (bundle.BundleId != BundleId || bundle.Version != Version || bundle.RuntimeMode != manifest.RuntimeMode || bundle.Architecture != "x64" || bundle.OpenSteamToolCommit != Commit || bundle.CloudRedirectVersion != CloudRedirectVersion || bundle.MinimumDesktopVersion != manifest.MinimumDesktopVersion || bundle.Files.Count != readOnlyDictionary.Count)
		{
			throw new InvalidDataException("Runtime bundle không khớp bản phát hành đã pin.");
		}
		if (!System.Version.TryParse(desktopVersion, out Version result) || !System.Version.TryParse(manifest.MinimumDesktopVersion, out Version result2) || result < result2)
		{
			throw new InvalidOperationException("Launcher cần cập nhật lên version " + manifest.MinimumDesktopVersion + ".");
		}
		HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (DesktopRuntimeBundleFileResponse file in bundle.Files)
		{
			if (!hashSet.Add(file.InstallPath) || !readOnlyDictionary.TryGetValue(file.InstallPath, out var value) || file.RelativePath != value.RelativePath || file.Size != value.Size || !string.Equals(file.Sha256, value.Sha256, StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(file.DownloadUrl))
			{
				throw new InvalidDataException("Runtime file không khớp bản phát hành đã pin: " + file.InstallPath);
			}
		}
	}

	internal bool IsInstalled(string steamDirectory, bool betterOnly = false)
	{
		return RequiredFiles(betterOnly).Values.All(delegate(DesktopRuntimeBundleFileResponse file)
		{
			string text = Path.Combine(steamDirectory, file.InstallPath.Replace('/', Path.DirectorySeparatorChar));
			try
			{
				for (DirectoryInfo directoryInfo = new DirectoryInfo(Path.GetDirectoryName(text)); directoryInfo != null; directoryInfo = directoryInfo.Parent)
				{
					if (directoryInfo.Exists && (directoryInfo.Attributes & FileAttributes.ReparsePoint) != 0)
					{
						return false;
					}
				}
				FileInfo fileInfo = new FileInfo(text);
				if (fileInfo.Exists && fileInfo.Length == file.Size && (fileInfo.Attributes & FileAttributes.ReparsePoint) == 0)
				{
					using FileStream source = File.OpenRead(text);
					return string.Equals(Convert.ToHexString(SHA256.HashData(source)), file.Sha256, StringComparison.OrdinalIgnoreCase);
				}
				return false;
			}
			catch (Exception ex) when (((ex is IOException || ex is UnauthorizedAccessException) ? 1 : 0) != 0)
			{
				return false;
			}
		});
	}

	internal void ValidateSteamBuild(string steamDirectory)
	{
		if (!IsBetterSteamTools)
		{
			return;
		}
		(string, string)[] array = new(string, string)[2]
		{
			("steamclient", "steamclient64.dll"),
			("steamui", "steamui.dll")
		};
		for (int i = 0; i < array.Length; i++)
		{
			(string, string) tuple = array[i];
			string item = tuple.Item1;
			string item2 = tuple.Item2;
			string path = Path.Combine(steamDirectory, item2);
			if (!File.Exists(path))
			{
				throw new InvalidOperationException("Thiếu Steam module " + item2 + "; hãy sửa Steam trước khi ACTIVE.");
			}
			using FileStream source = File.OpenRead(path);
			string value = Convert.ToHexString(SHA256.HashData(source)).ToLowerInvariant();
			if (!Files.ContainsKey($"opensteamtool/pattern/{item}/{value}.toml") || (item == "steamclient" && !Files.ContainsKey($"opensteamtool/ipc/{item}/{value}.toml")))
			{
				throw new UnsupportedSteamBuildException();
			}
		}
	}

	private static CloudRedirectRuntimeProfile Load(string id, string? version = null)
	{
		using Stream utf8Json = typeof(CloudRedirectRuntimeProfile).Assembly.GetManifestResourceStream("NestG.RuntimeBundles." + id + ((version == null) ? "" : ("-" + version)) + ".json") ?? throw new InvalidOperationException("Thiếu client pin: " + id);
		DesktopRuntimeBundleResponse desktopRuntimeBundleResponse = JsonSerializer.Deserialize<DesktopRuntimeBundleResponse>(utf8Json, new JsonSerializerOptions
		{
			PropertyNameCaseInsensitive = true
		}) ?? throw new InvalidDataException("Client pin không hợp lệ.");
		if (desktopRuntimeBundleResponse.BundleId != id || (version != null && desktopRuntimeBundleResponse.Version != version))
		{
			throw new InvalidDataException("Client pin không đúng bundle/version.");
		}
		return new CloudRedirectRuntimeProfile(desktopRuntimeBundleResponse);
	}
}
