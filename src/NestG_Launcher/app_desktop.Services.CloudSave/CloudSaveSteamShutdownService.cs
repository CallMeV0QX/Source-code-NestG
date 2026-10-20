using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using app_desktop.Services.SteamExclusions;

namespace app_desktop.Services.CloudSave;

internal sealed class CloudSaveSteamShutdownService
{
	private static readonly TimeSpan GameExitTimeout = TimeSpan.FromSeconds(45.0);

	private static readonly TimeSpan SyncTimeout = TimeSpan.FromSeconds(90.0);

	private static readonly TimeSpan SteamExitTimeout = TimeSpan.FromSeconds(30.0);

	internal async Task PrepareForSnapshotAsync(CloudSaveBackupRequest request, IProgress<CloudSaveProgress>? progress, CancellationToken cancellationToken, bool forRestore = false)
	{
		string steamDirectory = SteamInstallLocator.FindSteamDirectory(allowDriveScan: false) ?? throw new InvalidOperationException("Không tìm thấy Steam. Hãy mở Steam ít nhất một lần rồi thử CLOUD SAVE lại.");
		IReadOnlyList<string> libraries = ReadSteamLibraries(steamDirectory);
		string gameRoot = ResolveInstalledGameRoot(libraries, request.SteamAppId);
		bool flag = IsSteamRunning();
		if (gameRoot == null && flag)
		{
			if (!HasCloudRedirectCache(steamDirectory, request.SteamAppId))
			{
				throw new CloudSaveNoDataException();
			}
			throw new InvalidOperationException("Không xác định được thư mục cài đặt của game theo Steam App ID. Launcher không thể tự đóng đúng game; hãy kiểm tra bản cài Steam rồi thử lại.");
		}
		progress?.Report(new CloudSaveProgress(CloudSaveProgressStage.ClosingGame, forRestore ? "Đang thoát game trước khi khôi phục bản lưu..." : "Đang thoát game để chuẩn bị bản sao lưu...", 14));
		if (gameRoot != null)
		{
			await CloseGameAsync(gameRoot, cancellationToken);
		}
		progress?.Report(new CloudSaveProgress(CloudSaveProgressStage.WaitingForSync, "Đang chờ CloudRedirect hoàn tất đồng bộ save...", 22));
		await WaitForCloudRedirectAsync(steamDirectory, request.SteamAppId, gameRoot, cancellationToken);
		progress?.Report(new CloudSaveProgress(CloudSaveProgressStage.ClosingSteam, forRestore ? "Đang thoát Steam trước khi thay bản lưu..." : "Đang thoát Steam trước khi tạo snapshot...", 30));
		if (gameRoot != null)
		{
			EnsureNoOtherSteamGameRunning(libraries, gameRoot);
		}
		await CloseSteamAsync(steamDirectory, cancellationToken);
		if (InspectAllSessions(steamDirectory, request.SteamAppId) != 0)
		{
			throw new InvalidOperationException("CloudRedirect còn phiên hoạt động hoặc đồng bộ chờ sau khi Steam thoát; chưa thể tạo bản sao lưu an toàn.");
		}
	}

	internal static string? ResolveInstalledGameRoot(IReadOnlyList<string> libraries, int steamAppId)
	{
		if (steamAppId <= 0)
		{
			return null;
		}
		HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (string library in libraries)
		{
			string text = Path.Combine(library, "steamapps", "appmanifest_" + steamAppId.ToString(CultureInfo.InvariantCulture) + ".acf");
			if (File.Exists(text))
			{
				long length = new FileInfo(text).Length;
				if ((length <= 0 || length > 262144) ? true : false)
				{
					throw new InvalidDataException("Steam app manifest có kích thước không hợp lệ.");
				}
				string input = File.ReadAllText(text);
				Match match = Regex.Match(input, "\"appid\"\\s+\"(?<id>[0-9]+)\"", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(250.0));
				Match match2 = Regex.Match(input, "\"installdir\"\\s+\"(?<dir>[^\"]+)\"", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(250.0));
				if (!match.Success || match.Groups["id"].Value != steamAppId.ToString(CultureInfo.InvariantCulture) || !match2.Success)
				{
					throw new InvalidDataException("Steam app manifest không khớp App ID hoặc thiếu thư mục game.");
				}
				string text2 = match2.Groups["dir"].Value.Trim();
				int length2 = text2.Length;
				bool flag = ((length2 > 180 || length2 == 0) ? true : false);
				bool flag2 = flag;
				if (!flag2)
				{
					bool flag3 = ((text2 == "." || text2 == "..") ? true : false);
					flag2 = flag3;
				}
				if (flag2 || text2.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || text2.IndexOfAny(new char[3] { '/', '\\', ':' }) >= 0 || text2.Any(char.IsControl))
				{
					throw new InvalidDataException("Steam app manifest chứa thư mục game không an toàn.");
				}
				string fullPath = Path.GetFullPath(Path.Combine(library, "steamapps", "common"));
				string fullPath2 = Path.GetFullPath(Path.Combine(fullPath, text2));
				if (!IsChildOf(fullPath2, fullPath) || !Directory.Exists(fullPath2))
				{
					throw new InvalidDataException("Không tìm thấy thư mục game đã khai báo trong Steam app manifest.");
				}
				EnsureNoReparsePoints(fullPath2, fullPath);
				hashSet.Add(fullPath2);
			}
		}
		if (hashSet.Count > 1)
		{
			throw new InvalidOperationException("Có nhiều bản cài Steam cho cùng App ID; launcher không thể xác định đúng game cần đóng.");
		}
		return hashSet.SingleOrDefault();
	}

	internal static string RequireInstalledGameRoot(string steamDirectory, int steamAppId)
	{
		return FindInstalledGameRoot(steamDirectory, steamAppId) ?? throw new InvalidOperationException("Không tìm thấy bản cài game theo đúng Steam App ID; chưa thể restore save an toàn.");
	}

	internal static string? FindInstalledGameRoot(string steamDirectory, int steamAppId)
	{
		return ResolveInstalledGameRoot(ReadSteamLibraries(steamDirectory), steamAppId);
	}

	private static IReadOnlyList<string> ReadSteamLibraries(string steamDirectory)
	{
		string text = Path.Combine(steamDirectory, "steamapps", "libraryfolders.vdf");
		string vdf = null;
		if (File.Exists(text))
		{
			if (new FileInfo(text).Length > 262144)
			{
				throw new InvalidDataException("Steam library metadata vượt giới hạn an toàn.");
			}
			vdf = File.ReadAllText(text);
		}
		return SteamGameFolderDiscovery.ParseLibraryRoots(steamDirectory, vdf);
	}

	private static bool HasCloudRedirectCache(string steamDirectory, int steamAppId)
	{
		string path = Path.Combine(steamDirectory, "cloud_redirect", "storage");
		if (!Directory.Exists(path))
		{
			return false;
		}
		foreach (string item in Directory.EnumerateDirectories(path, "*", SearchOption.TopDirectoryOnly).Take(129))
		{
			if (uint.TryParse(Path.GetFileName(item), NumberStyles.None, CultureInfo.InvariantCulture, out var _))
			{
				string path2 = Path.Combine(item, steamAppId.ToString(CultureInfo.InvariantCulture));
				if (File.Exists(Path.Combine(path2, "manifest.cloudredirect")) || File.Exists(Path.Combine(path2, "manifest.dat")))
				{
					return true;
				}
			}
		}
		return false;
	}

	private static async Task CloseGameAsync(string gameRoot, CancellationToken cancellationToken)
	{
		List<Process> list = FindProcessesUnder(gameRoot);
		try
		{
			if (list.Count == 0)
			{
				return;
			}
			bool flag = false;
			foreach (Process item in list)
			{
				cancellationToken.ThrowIfCancellationRequested();
				try
				{
					if (!item.HasExited && item.MainWindowHandle != IntPtr.Zero)
					{
						flag |= item.CloseMainWindow();
					}
				}
				catch (InvalidOperationException)
				{
				}
			}
			if (!flag)
			{
				throw new InvalidOperationException("Game đang chạy nhưng không có cửa sổ có thể yêu cầu thoát. Launcher chưa thể đóng game an toàn.");
			}
		}
		finally
		{
			foreach (Process item2 in list)
			{
				item2.Dispose();
			}
		}
		DateTimeOffset deadline = DateTimeOffset.UtcNow + GameExitTimeout;
		while (DateTimeOffset.UtcNow < deadline)
		{
			cancellationToken.ThrowIfCancellationRequested();
			List<Process> list2 = FindProcessesUnder(gameRoot);
			try
			{
				if (list2.Count == 0)
				{
					return;
				}
			}
			finally
			{
				foreach (Process item3 in list2)
				{
					item3.Dispose();
				}
			}
			await Task.Delay(500, cancellationToken);
		}
		throw new InvalidOperationException("Game không thoát trong thời gian chờ. Launcher chưa tạo snapshot để tránh mất save chưa được ghi.");
	}

	private static async Task WaitForCloudRedirectAsync(string steamDirectory, int steamAppId, string? gameRoot, CancellationToken cancellationToken)
	{
		DateTimeOffset deadline = DateTimeOffset.UtcNow + SyncTimeout;
		int idleObservations = 0;
		while (DateTimeOffset.UtcNow < deadline)
		{
			cancellationToken.ThrowIfCancellationRequested();
			if (gameRoot != null)
			{
				List<Process> list = FindProcessesUnder(gameRoot);
				try
				{
					if (list.Count > 0)
					{
						throw new InvalidOperationException("Game đã chạy lại khi Cloud Save đang chờ đồng bộ; chưa thể tạo snapshot.");
					}
				}
				finally
				{
					foreach (Process item in list)
					{
						item.Dispose();
					}
				}
			}
			if (InspectAllSessions(steamDirectory, steamAppId) == CloudSaveSessionStatus.Idle)
			{
				int num = idleObservations + 1;
				idleObservations = num;
				if (num >= 2)
				{
					return;
				}
			}
			else
			{
				idleObservations = 0;
				if (!IsSteamRunning())
				{
					throw new InvalidOperationException("CloudRedirect còn đồng bộ chờ nhưng Steam đã thoát. Hãy mở lại Steam để hoàn tất đồng bộ rồi thử lại.");
				}
			}
			await Task.Delay(1000, cancellationToken);
		}
		throw new InvalidOperationException("CloudRedirect chưa hoàn tất phiên game hoặc đồng bộ save trong thời gian chờ; chưa thể tạo bản backup.");
	}

	internal static CloudSaveSessionStatus InspectAllSessions(string steamDirectory, int steamAppId)
	{
		string path = Path.Combine(steamDirectory, "cloud_redirect", "storage");
		if (!Directory.Exists(path))
		{
			return CloudSaveSessionStatus.Idle;
		}
		string[] array = Directory.EnumerateDirectories(path, "*", SearchOption.TopDirectoryOnly).Take(129).ToArray();
		if (array.Length > 128)
		{
			throw new InvalidDataException("Có quá nhiều Steam account trong CloudRedirect storage.");
		}
		CloudSaveSessionStatus result = CloudSaveSessionStatus.Idle;
		string[] array2 = array;
		foreach (string text in array2)
		{
			if (!uint.TryParse(Path.GetFileName(text), NumberStyles.None, CultureInfo.InvariantCulture, out var _))
			{
				continue;
			}
			string text2 = Path.Combine(text, steamAppId.ToString(CultureInfo.InvariantCulture));
			if (Directory.Exists(text2))
			{
				CloudSaveSessionStatus cloudSaveSessionStatus = CloudRedirectSnapshotService.InspectSessionStatus(text2);
				switch (cloudSaveSessionStatus)
				{
				case CloudSaveSessionStatus.Active:
					return cloudSaveSessionStatus;
				case CloudSaveSessionStatus.Pending:
					result = cloudSaveSessionStatus;
					break;
				}
			}
		}
		return result;
	}

	private static void EnsureNoOtherSteamGameRunning(IReadOnlyList<string> libraries, string selectedGameRoot)
	{
		foreach (string library in libraries)
		{
			List<Process> list = FindProcessesUnder(Path.Combine(library, "steamapps", "common"));
			try
			{
				if (list.Any(delegate(Process process)
				{
					string text = TryGetExecutablePath(process);
					return text != null && !IsChildOf(text, selectedGameRoot);
				}))
				{
					throw new InvalidOperationException("Một game Steam khác vẫn đang chạy. Launcher sẽ không tắt Steam khi game đó có thể còn save chưa ghi.");
				}
			}
			finally
			{
				foreach (Process item in list)
				{
					item.Dispose();
				}
			}
		}
	}

	private static async Task CloseSteamAsync(string steamDirectory, CancellationToken cancellationToken)
	{
		if (!IsSteamRunning())
		{
			return;
		}
		string text = Path.Combine(steamDirectory, "steam.exe");
		if (!File.Exists(text))
		{
			throw new InvalidOperationException("Không tìm thấy steam.exe để yêu cầu Steam thoát.");
		}
		try
		{
			Process.Start(new ProcessStartInfo
			{
				FileName = text,
				Arguments = "-shutdown",
				WorkingDirectory = steamDirectory,
				UseShellExecute = true
			})?.Dispose();
		}
		catch (Exception ex) when (((ex is Win32Exception || ex is InvalidOperationException) ? 1 : 0) != 0)
		{
			throw new InvalidOperationException("Không gửi được lệnh thoát Steam.", ex);
		}
		Process[] processesByName = Process.GetProcessesByName("steam");
		try
		{
			Process[] array = processesByName;
			foreach (Process process in array)
			{
				try
				{
					if (!process.HasExited && process.MainWindowHandle != IntPtr.Zero)
					{
						process.CloseMainWindow();
					}
				}
				catch (InvalidOperationException)
				{
				}
			}
		}
		finally
		{
			Process[] array = processesByName;
			for (int i = 0; i < array.Length; i++)
			{
				array[i].Dispose();
			}
		}
		DateTimeOffset deadline = DateTimeOffset.UtcNow + SteamExitTimeout;
		while (DateTimeOffset.UtcNow < deadline)
		{
			cancellationToken.ThrowIfCancellationRequested();
			if (!IsSteamRunning())
			{
				return;
			}
			await Task.Delay(500, cancellationToken);
		}
		throw new InvalidOperationException("Steam không thoát trong thời gian chờ. Launcher chưa tạo snapshot để tránh backup khi Steam còn ghi save.");
	}

	private static bool IsSteamRunning()
	{
		Process[] processesByName = Process.GetProcessesByName("steam");
		try
		{
			return processesByName.Any((Process process) => !process.HasExited);
		}
		finally
		{
			Process[] array = processesByName;
			for (int i = 0; i < array.Length; i++)
			{
				array[i].Dispose();
			}
		}
	}

	private static List<Process> FindProcessesUnder(string root)
	{
		List<Process> list = new List<Process>();
		Process[] processes = Process.GetProcesses();
		foreach (Process process in processes)
		{
			string text = TryGetExecutablePath(process);
			if (text != null && IsChildOf(text, root))
			{
				list.Add(process);
			}
			else
			{
				process.Dispose();
			}
		}
		return list;
	}

	private static string? TryGetExecutablePath(Process process)
	{
		try
		{
			return process.HasExited ? null : process.MainModule?.FileName;
		}
		catch (Exception ex) when (((ex is Win32Exception || ex is InvalidOperationException || ex is FileNotFoundException) ? 1 : 0) != 0)
		{
			return null;
		}
	}

	private static bool IsChildOf(string path, string root)
	{
		try
		{
			return Path.GetFullPath(path).StartsWith(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
		}
		catch
		{
			return false;
		}
	}

	private static void EnsureNoReparsePoints(string gameRoot, string commonRoot)
	{
		DirectoryInfo directoryInfo = new DirectoryInfo(gameRoot);
		while (directoryInfo != null && IsChildOf(directoryInfo.FullName, commonRoot))
		{
			if (directoryInfo.Attributes.HasFlag(FileAttributes.ReparsePoint))
			{
				throw new InvalidDataException("Thư mục game đi qua symbolic link hoặc junction.");
			}
			directoryInfo = directoryInfo.Parent;
		}
	}
}
