using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace app_desktop.Services;

internal sealed class SteamShortcutService
{
	private sealed record ShortcutVdfEntry(byte Type, string Name, object? Value);

	private sealed class ShortcutVdfObject(string name)
	{
		public string Name { get; } = name;

		public List<ShortcutVdfEntry> Entries { get; } = new List<ShortcutVdfEntry>();
	}

	private const string LaunchModeNonSteamShortcut = "non_steam_shortcut";

	private const string ExeLaunchArgumentPlaceholder = "{exe}";

	private const byte VdfObject = 0;

	private const byte VdfString = 1;

	private const byte VdfInt32 = 2;

	private const byte VdfFloat32 = 3;

	private const byte VdfUInt64 = 7;

	private const byte VdfEnd = 8;

	private const ulong SteamId64Base = 76561197960265728uL;

	public SteamShortcutApplyResult EnsureShortcut(SteamShortcutApplyRequest request)
	{
		List<string> list = new List<string>();
		if (!string.Equals(request.LaunchMode, "non_steam_shortcut", StringComparison.OrdinalIgnoreCase))
		{
			return SteamShortcutApplyResult.CreateSkipped("Launch mode không phải non_steam_shortcut.");
		}
		string text = ResolveExePath(request.GameInstallPath, request.MainExeRelativePath);
		if (text == null)
		{
			return SteamShortcutApplyResult.CreateFailed("Chưa cấu hình MainExeRelativePath hợp lệ cho game.", list);
		}
		if (!File.Exists(text))
		{
			return SteamShortcutApplyResult.CreateFailed("Không tìm thấy file exe để tạo Steam shortcut: " + text, list);
		}
		string text2 = SteamInstallLocator.FindSteamDirectory();
		if (string.IsNullOrWhiteSpace(text2))
		{
			return SteamShortcutApplyResult.CreateFailed("Không tìm thấy thư mục Steam trên máy.", list);
		}
		IReadOnlyList<string> readOnlyList = ResolveSteamProfileDirectories(text2, list);
		if (readOnlyList.Count == 0)
		{
			return SteamShortcutApplyResult.CreateFailed("Không tìm thấy Steam profile trong thư mục userdata.", list);
		}
		bool flag = StopSteamIfRunning(list);
		List<string> list2 = new List<string>();
		List<string> list3 = new List<string>();
		string appName = CleanShortcutName(request.SteamShortcutName, request.GameName);
		string startDirectory = Path.GetDirectoryName(text) ?? request.GameInstallPath;
		string launchOptions = RenderLaunchArguments(request.LaunchArguments, text);
		foreach (string item in readOnlyList)
		{
			string text3 = Path.Combine(item, "config");
			Directory.CreateDirectory(text3);
			string text4 = Path.Combine(text3, "shortcuts.vdf");
			try
			{
				ShortcutVdfObject shortcutVdfObject = (File.Exists(text4) ? ReadShortcutRoot(text4, list) : CreateShortcutRoot());
				if (shortcutVdfObject != null)
				{
					UpsertShortcut(shortcutVdfObject, appName, text, startDirectory, launchOptions);
					string text5 = BackupShortcutFile(text4);
					if (text5 != null)
					{
						list3.Add(text5);
					}
					WriteShortcutRoot(text4, shortcutVdfObject);
					list2.Add(text4);
				}
			}
			catch (Exception ex)
			{
				list.Add("Không thể cập nhật Steam shortcut profile " + Path.GetFileName(item) + ": " + ex.Message);
			}
		}
		if (flag)
		{
			StartSteam(text2, list);
		}
		else
		{
			list.Add("Nếu shortcut chưa hiện trong Steam Library, hãy tắt/mở lại Steam.");
		}
		list.Add("Steam shortcut trỏ tới: " + text);
		if (list2.Count == 0)
		{
			return SteamShortcutApplyResult.CreateFailed("Không ghi được Steam shortcut vào profile local nào.", list);
		}
		return SteamShortcutApplyResult.CreateSuccess(text, list2, list3, list);
	}

	public static string? ResolveExePath(string? gameInstallPath, string? mainExeRelativePath)
	{
		if (string.IsNullOrWhiteSpace(gameInstallPath) || string.IsNullOrWhiteSpace(mainExeRelativePath) || !Directory.Exists(gameInstallPath))
		{
			return null;
		}
		string text = mainExeRelativePath.Trim().Replace('\\', '/').Trim('/');
		if (string.IsNullOrWhiteSpace(text) || Path.IsPathRooted(text) || text.Contains(':', StringComparison.Ordinal) || text.Split('/').Any((string part) => (part == "." || part == "..") ? true : false) || !text.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
		{
			return null;
		}
		string text2 = Path.GetFullPath(gameInstallPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
		string fullPath = Path.GetFullPath(Path.Combine(text2, text.Replace('/', Path.DirectorySeparatorChar)));
		if (!IsPathInsideDirectory(fullPath, text2))
		{
			return null;
		}
		return NormalizeDriveLetterCasing(fullPath);
	}

	public static string RenderLaunchArguments(string? launchArguments, string exePath)
	{
		string text = launchArguments?.Trim() ?? string.Empty;
		if (text.Length == 0)
		{
			return string.Empty;
		}
		return Regex.Replace(text, Regex.Escape("{exe}"), (Match _) => NormalizeDriveLetterCasing(exePath), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
	}

	private static string NormalizeDriveLetterCasing(string path)
	{
		if (path.Length < 2 || path[1] != ':' || !char.IsLetter(path[0]))
		{
			return path;
		}
		return char.ToUpperInvariant(path[0]) + path.Substring(1);
	}

	private static IReadOnlyList<string> ResolveSteamProfileDirectories(string steamDirectory, ICollection<string> messages)
	{
		string path = Path.Combine(steamDirectory, "userdata");
		if (!Directory.Exists(path))
		{
			return Array.Empty<string>();
		}
		string[] array = (from directory in Directory.GetDirectories(path)
			where Regex.IsMatch(Path.GetFileName(directory), "^\\d+$", RegexOptions.CultureInvariant)
			select directory).OrderBy<string, string>((string directory) => directory, StringComparer.OrdinalIgnoreCase).ToArray();
		if (array.Length == 0)
		{
			return Array.Empty<string>();
		}
		HashSet<string> preferredIds = ResolveMostRecentSteamUserIds(steamDirectory);
		string[] array2 = array.Where((string directory) => preferredIds.Contains(Path.GetFileName(directory))).ToArray();
		if (array2.Length != 0)
		{
			return array2;
		}
		messages.Add("Không xác định được Steam user gần nhất, launcher sẽ cập nhật shortcut cho tất cả profile local.");
		return array;
	}

	private static HashSet<string> ResolveMostRecentSteamUserIds(string steamDirectory)
	{
		HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		string path = Path.Combine(steamDirectory, "config", "loginusers.vdf");
		if (!File.Exists(path))
		{
			return hashSet;
		}
		try
		{
			foreach (Match item in Regex.Matches(File.ReadAllText(path), "\"(?<steamid>\\d+)\"\\s*\\{(?<body>.*?)\\}", RegexOptions.Singleline | RegexOptions.CultureInvariant))
			{
				if (Regex.IsMatch(item.Groups["body"].Value, "\"MostRecent\"\\s+\"1\"", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
				{
					string value = item.Groups["steamid"].Value;
					hashSet.Add(value);
					if (ulong.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var result) && result >= 76561197960265728L)
					{
						hashSet.Add((result - 76561197960265728L).ToString(CultureInfo.InvariantCulture));
					}
				}
			}
		}
		catch
		{
		}
		return hashSet;
	}

	private static bool StopSteamIfRunning(ICollection<string> messages)
	{
		List<Process> runningSteamProcesses = GetRunningSteamProcesses();
		if (runningSteamProcesses.Count == 0)
		{
			return false;
		}
		foreach (Process item in runningSteamProcesses)
		{
			try
			{
				if (!item.HasExited && item.MainWindowHandle != IntPtr.Zero)
				{
					item.CloseMainWindow();
				}
			}
			catch
			{
			}
		}
		WaitForSteamExit(TimeSpan.FromSeconds(8.0));
		foreach (Process runningSteamProcess in GetRunningSteamProcesses())
		{
			try
			{
				if (!runningSteamProcess.HasExited)
				{
					runningSteamProcess.Kill(entireProcessTree: true);
					runningSteamProcess.WaitForExit(5000);
				}
			}
			catch (Exception ex)
			{
				messages.Add($"Không thể đóng Steam process {runningSteamProcess.Id}: {ex.Message}");
			}
		}
		messages.Add("Launcher đã đóng Steam để cập nhật Non-Steam Shortcut.");
		return true;
	}

	private static void StartSteam(string steamDirectory, ICollection<string> messages)
	{
		string text = Path.Combine(steamDirectory, "steam.exe");
		if (!File.Exists(text))
		{
			messages.Add("Đã ghi shortcut nhưng không tìm thấy steam.exe để mở lại Steam.");
			return;
		}
		try
		{
			Process.Start(new ProcessStartInfo
			{
				FileName = text,
				WorkingDirectory = steamDirectory,
				UseShellExecute = true
			});
			messages.Add("Launcher đã mở lại Steam để nạp shortcut mới.");
		}
		catch (Exception ex)
		{
			messages.Add("Đã ghi shortcut nhưng không mở lại được Steam: " + ex.Message);
		}
	}

	private static List<Process> GetRunningSteamProcesses()
	{
		return (from process in Process.GetProcessesByName("steam")
			where !process.HasExited
			select process).ToList();
	}

	private static void WaitForSteamExit(TimeSpan timeout)
	{
		Stopwatch stopwatch = Stopwatch.StartNew();
		while (stopwatch.Elapsed < timeout && GetRunningSteamProcesses().Count != 0)
		{
			Thread.Sleep(250);
		}
	}

	private static ShortcutVdfObject CreateShortcutRoot()
	{
		return new ShortcutVdfObject("shortcuts");
	}

	private static ShortcutVdfObject? ReadShortcutRoot(string shortcutsPath, ICollection<string> messages)
	{
		try
		{
			using FileStream input = File.OpenRead(shortcutsPath);
			using BinaryReader binaryReader = new BinaryReader(input, Encoding.UTF8, leaveOpen: false);
			byte num = binaryReader.ReadByte();
			string text = ReadNullTerminatedString(binaryReader);
			if (num != 0 || !string.Equals(text, "shortcuts", StringComparison.OrdinalIgnoreCase))
			{
				throw new InvalidDataException("File shortcuts.vdf không đúng root shortcuts.");
			}
			return ReadObject(binaryReader, text);
		}
		catch (Exception ex)
		{
			messages.Add("Không thể đọc shortcuts.vdf hiện có (" + shortcutsPath + "): " + ex.Message);
			return null;
		}
	}

	private static ShortcutVdfObject ReadObject(BinaryReader reader, string name)
	{
		ShortcutVdfObject shortcutVdfObject = new ShortcutVdfObject(name);
		while (reader.BaseStream.Position < reader.BaseStream.Length)
		{
			byte b = reader.ReadByte();
			if (b == 8)
			{
				break;
			}
			string name2 = ReadNullTerminatedString(reader);
			switch (b)
			{
			case 0:
				shortcutVdfObject.Entries.Add(new ShortcutVdfEntry(b, name2, ReadObject(reader, name2)));
				continue;
			case 1:
				shortcutVdfObject.Entries.Add(new ShortcutVdfEntry(b, name2, ReadNullTerminatedString(reader)));
				continue;
			case 2:
				shortcutVdfObject.Entries.Add(new ShortcutVdfEntry(b, name2, reader.ReadInt32()));
				continue;
			case 3:
				shortcutVdfObject.Entries.Add(new ShortcutVdfEntry(b, name2, reader.ReadSingle()));
				continue;
			case 7:
				shortcutVdfObject.Entries.Add(new ShortcutVdfEntry(b, name2, reader.ReadUInt64()));
				continue;
			}
			throw new InvalidDataException($"Unsupported shortcuts.vdf value type {b}.");
		}
		return shortcutVdfObject;
	}

	private static void WriteShortcutRoot(string shortcutsPath, ShortcutVdfObject root)
	{
		Directory.CreateDirectory(Path.GetDirectoryName(shortcutsPath));
		string text = $"{shortcutsPath}.{Guid.NewGuid():N}.tmp";
		using (FileStream output = File.Create(text))
		{
			using BinaryWriter writer = new BinaryWriter(output, Encoding.UTF8, leaveOpen: false);
			WriteObjectEntry(writer, root);
		}
		File.Move(text, shortcutsPath, overwrite: true);
	}

	private static void WriteObjectEntry(BinaryWriter writer, ShortcutVdfObject obj)
	{
		writer.Write((byte)0);
		WriteNullTerminatedString(writer, obj.Name);
		WriteObjectBody(writer, obj);
	}

	private static void WriteObjectBody(BinaryWriter writer, ShortcutVdfObject obj)
	{
		foreach (ShortcutVdfEntry entry in obj.Entries)
		{
			writer.Write(entry.Type);
			WriteNullTerminatedString(writer, entry.Name);
			switch (entry.Type)
			{
			case 0:
				WriteObjectBody(writer, (ShortcutVdfObject)entry.Value);
				break;
			case 1:
				WriteNullTerminatedString(writer, (entry.Value as string) ?? string.Empty);
				break;
			case 2:
				writer.Write(Convert.ToInt32(entry.Value, CultureInfo.InvariantCulture));
				break;
			case 3:
				writer.Write(Convert.ToSingle(entry.Value, CultureInfo.InvariantCulture));
				break;
			case 7:
				writer.Write(Convert.ToUInt64(entry.Value, CultureInfo.InvariantCulture));
				break;
			}
		}
		writer.Write((byte)8);
	}

	private static void UpsertShortcut(ShortcutVdfObject root, string appName, string exePath, string startDirectory, string launchOptions)
	{
		ShortcutVdfObject shortcutVdfObject = (from entry in root.Entries
			where entry.Type == 0 && entry.Value is ShortcutVdfObject
			select (ShortcutVdfObject)entry.Value).FirstOrDefault((ShortcutVdfObject item) => string.Equals(GetString(item, "appname"), appName, StringComparison.OrdinalIgnoreCase) || string.Equals(NormalizeExePath(GetString(item, "exe")), exePath, StringComparison.OrdinalIgnoreCase));
		if (shortcutVdfObject == null)
		{
			shortcutVdfObject = new ShortcutVdfObject(ResolveNextShortcutIndex(root));
			root.Entries.Add(new ShortcutVdfEntry(0, shortcutVdfObject.Name, shortcutVdfObject));
		}
		string text = QuotePath(exePath);
		SetInt32(shortcutVdfObject, "appid", (int)ComputeShortcutAppId(text, appName));
		SetString(shortcutVdfObject, "appname", appName);
		SetString(shortcutVdfObject, "exe", text);
		SetString(shortcutVdfObject, "StartDir", QuotePath(startDirectory));
		SetString(shortcutVdfObject, "icon", string.Empty);
		SetString(shortcutVdfObject, "ShortcutPath", string.Empty);
		SetString(shortcutVdfObject, "LaunchOptions", launchOptions);
		SetInt32(shortcutVdfObject, "IsHidden", 0);
		SetInt32(shortcutVdfObject, "AllowDesktopConfig", 1);
		SetInt32(shortcutVdfObject, "AllowOverlay", 1);
		SetInt32(shortcutVdfObject, "OpenVR", 0);
		SetString(shortcutVdfObject, "Devkit", string.Empty);
		SetString(shortcutVdfObject, "DevkitGameID", string.Empty);
		SetString(shortcutVdfObject, "DevkitOverrideAppID", string.Empty);
		SetInt32(shortcutVdfObject, "LastPlayTime", 0);
		SetString(shortcutVdfObject, "FlatpakAppID", string.Empty);
		EnsureTagsObject(shortcutVdfObject);
	}

	private static void EnsureTagsObject(ShortcutVdfObject shortcut)
	{
		if (!shortcut.Entries.Any((ShortcutVdfEntry entry) => entry.Type == 0 && string.Equals(entry.Name, "tags", StringComparison.OrdinalIgnoreCase)))
		{
			shortcut.Entries.Add(new ShortcutVdfEntry(0, "tags", new ShortcutVdfObject("tags")));
		}
	}

	private static string ResolveNextShortcutIndex(ShortcutVdfObject root)
	{
		int result;
		return ((from entry in root.Entries
			where entry.Type == 0
			select (!int.TryParse(entry.Name, NumberStyles.None, CultureInfo.InvariantCulture, out result)) ? (-1) : result).DefaultIfEmpty(-1).Max() + 1).ToString(CultureInfo.InvariantCulture);
	}

	private static string? BackupShortcutFile(string shortcutsPath)
	{
		if (!File.Exists(shortcutsPath))
		{
			return null;
		}
		string text = Path.Combine(Path.GetDirectoryName(shortcutsPath), $"shortcuts.vdf.bak-{DateTime.UtcNow:yyyyMMddHHmmss}");
		File.Copy(shortcutsPath, text, overwrite: true);
		return text;
	}

	private static string? GetString(ShortcutVdfObject obj, string key)
	{
		return obj.Entries.FirstOrDefault((ShortcutVdfEntry entry) => entry.Type == 1 && string.Equals(entry.Name, key, StringComparison.OrdinalIgnoreCase))?.Value as string;
	}

	private static void SetString(ShortcutVdfObject obj, string key, string value)
	{
		SetEntry(obj, 1, key, value);
	}

	private static void SetInt32(ShortcutVdfObject obj, string key, int value)
	{
		SetEntry(obj, 2, key, value);
	}

	private static void SetEntry(ShortcutVdfObject obj, byte type, string key, object value)
	{
		int num = obj.Entries.FindIndex((ShortcutVdfEntry entry) => string.Equals(entry.Name, key, StringComparison.OrdinalIgnoreCase));
		ShortcutVdfEntry shortcutVdfEntry = new ShortcutVdfEntry(type, key, value);
		if (num >= 0)
		{
			obj.Entries[num] = shortcutVdfEntry;
		}
		else
		{
			obj.Entries.Add(shortcutVdfEntry);
		}
	}

	private static string ReadNullTerminatedString(BinaryReader reader)
	{
		List<byte> list = new List<byte>();
		while (reader.BaseStream.Position < reader.BaseStream.Length)
		{
			byte b = reader.ReadByte();
			if (b == 0)
			{
				break;
			}
			list.Add(b);
		}
		return Encoding.UTF8.GetString(list.ToArray());
	}

	private static void WriteNullTerminatedString(BinaryWriter writer, string value)
	{
		writer.Write(Encoding.UTF8.GetBytes(value));
		writer.Write((byte)0);
	}

	private static string CleanShortcutName(string? shortcutName, string gameName)
	{
		if (!string.IsNullOrWhiteSpace(shortcutName))
		{
			return shortcutName.Trim();
		}
		return gameName.Trim();
	}

	private static string QuotePath(string path)
	{
		string text = path.Trim().Trim('"');
		return "\"" + text + "\"";
	}

	private static string? NormalizeExePath(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return null;
		}
		return value.Trim().Trim('"');
	}

	private static uint ComputeShortcutAppId(string exePath, string appName)
	{
		return Crc32(Encoding.UTF8.GetBytes(exePath + appName)) | 0x80000000u;
	}

	private static uint Crc32(byte[] bytes)
	{
		uint num = uint.MaxValue;
		foreach (byte b in bytes)
		{
			num ^= b;
			for (int j = 0; j < 8; j++)
			{
				num = (((num & 1) == 1) ? ((num >> 1) ^ 0xEDB88320u) : (num >> 1));
			}
		}
		return ~num;
	}

	private static bool IsPathInsideDirectory(string candidatePath, string directoryPath)
	{
		string text = Path.GetFullPath(directoryPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
		string fullPath = Path.GetFullPath(candidatePath);
		if (!string.Equals(fullPath, text, StringComparison.OrdinalIgnoreCase))
		{
			return fullPath.StartsWith(text + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
		}
		return true;
	}
}
