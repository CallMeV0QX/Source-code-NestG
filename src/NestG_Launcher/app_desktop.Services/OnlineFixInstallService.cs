using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using ICSharpCode.SharpZipLib.Zip;
using UI_Desktop.Store;
using app_desktop.Models;

namespace app_desktop.Services;

internal sealed class OnlineFixInstallService(StorefrontApiClient storefrontApiClient)
{
	private sealed class OnlineFixLocalInstallState
	{
		public Guid AssignmentId { get; set; }

		public Guid LibraryId { get; set; }

		public Guid GameId { get; set; }

		public string GameName { get; set; } = string.Empty;

		public string VersionName { get; set; } = string.Empty;

		public string? MainExeRelativePath { get; set; }

		public string? SteamShortcutName { get; set; }

		public string? LaunchArguments { get; set; }

		public string? LaunchMode { get; set; }

		public string? GameExePath { get; set; }

		public string? GameInstallPath { get; set; }

		public string? PackageSha256 { get; set; }

		public string? PackageVersion { get; set; }

		public string? ClientVersion { get; set; }

		public DateTime UpdatedAtUtc { get; set; }
	}

	private sealed record OnlinePackageInstallResult(IReadOnlyList<string> WrittenFiles, IReadOnlyList<string> OverwrittenFiles, string BackupDirectory);

	private static readonly JsonSerializerOptions StateJsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
	{
		PropertyNameCaseInsensitive = true,
		WriteIndented = true
	};

	public async Task<OnlineFixInstallResult> InstallAsync(DesktopLibraryDetailResponse detail, string accessToken, IWin32Window? owner = null, CancellationToken cancellationToken = default(CancellationToken))
	{
		DesktopLibraryOnlineFixResponse online = detail.OnlineFix;
		if (string.IsNullOrWhiteSpace(accessToken))
		{
			throw new InvalidOperationException("Bạn cần đăng nhập để cài Online Fix.");
		}
		if (online != null && online.HasOnlineFix && !online.AssignmentId.HasValue)
		{
			online = (detail.OnlineFix = await storefrontApiClient.EnsureLibraryOnlineFixAsync(detail.LibraryId, accessToken, cancellationToken).ConfigureAwait(continueOnCapturedContext: false));
		}
		if (online == null || !online.HasOnlineFix)
		{
			throw new InvalidOperationException("Game này chưa có Online Fix được gán cho tài khoản.");
		}
		if (string.Equals(online.Status, "revoked", StringComparison.OrdinalIgnoreCase))
		{
			throw new InvalidOperationException("Online Fix của game này đã bị revoke.");
		}
		if (string.IsNullOrWhiteSpace(online.PackageDownloadUrl))
		{
			throw new InvalidOperationException("Game này chưa có package Online active.");
		}
		Guid trackingId = online.AssignmentId ?? detail.LibraryId;
		string workingDirectory = CreateWorkingDirectory();
		string text = Path.Combine(workingDirectory, "downloads");
		Directory.CreateDirectory(text);
		List<string> messages = new List<string>();
		try
		{
			string installDirectory = ResolveInstallDirectory(detail, online, owner);
			if (string.IsNullOrWhiteSpace(installDirectory))
			{
				throw new InvalidOperationException("Chưa chọn thư mục cài game Steam để cài Online Fix.");
			}
			DesktopDownloadedFileResponse package = await storefrontApiClient.DownloadLibraryOnlineFixPackageAsync(detail.LibraryId, accessToken, text, ResolvePackageFallbackFileName(detail, online), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			await TryPostEventAsync(detail.LibraryId, accessToken, "install_started", null, installDirectory, "Launcher bắt đầu cài Online Fix.", cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			OnlinePackageInstallResult installResult = await Task.Run(() => InstallPackage(package.FullPath, installDirectory, trackingId, package.ZipPassword, messages), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			string gameExePath = ResolveGameExePath(installDirectory, online.MainExeRelativePath, messages);
			OnlineFixLocalInstallState state = new OnlineFixLocalInstallState
			{
				AssignmentId = trackingId,
				LibraryId = detail.LibraryId,
				GameId = detail.GameId,
				GameName = detail.GameName,
				VersionName = detail.VersionName,
				MainExeRelativePath = online.MainExeRelativePath,
				SteamShortcutName = online.SteamShortcutName,
				LaunchArguments = online.LaunchArguments,
				LaunchMode = online.LaunchMode,
				GameExePath = gameExePath,
				GameInstallPath = installDirectory,
				PackageSha256 = null,
				PackageVersion = online.PackageVersion,
				ClientVersion = ResolveClientVersion(),
				UpdatedAtUtc = DateTime.UtcNow
			};
			SaveLocalState(state);
			await TryPostEventAsync(detail.LibraryId, accessToken, "install_completed", null, installDirectory, $"Đã cài {installResult.WrittenFiles.Count} file.", cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			online.Status = "installed";
			online.StatusText = "Đã cài Online";
			online.GameInstallPath = installDirectory;
			online.LastInstallAt = DateTime.UtcNow;
			online.PackageSha256 = null;
			detail.OnlineFix = online;
			string logFilePath = WriteInstallLog(detail, online, installDirectory, installResult, messages);
			return new OnlineFixInstallResult(installDirectory, installResult.WrittenFiles, installResult.OverwrittenFiles, installResult.BackupDirectory, logFilePath, null, online.PackageVersion, state.GameExePath, messages);
		}
		catch (OperationCanceledException)
		{
			throw;
		}
		catch (Exception ex2)
		{
			Exception ex3 = ex2;
			string logPath = WriteErrorLog(detail, online, ex3);
			await TryPostEventAsync(detail.LibraryId, accessToken, "error", null, online?.GameInstallPath, ex3.Message, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			messages.Add("Log: " + logPath);
			ExceptionDispatchInfo.Capture((ex2 as Exception) ?? throw ex2).Throw();
		}
		finally
		{
			TryDeleteDirectory(workingDirectory);
		}
		throw null;
	}

	private static OnlinePackageInstallResult InstallPackage(string packagePath, string installDirectory, Guid assignmentId, string? zipPassword, ICollection<string> messages)
	{
		if (!IsZipFile(packagePath))
		{
			throw new InvalidOperationException("Package Online Fix phải là file .zip.");
		}
		string fullPath = Path.GetFullPath(installDirectory);
		if (!Directory.Exists(fullPath))
		{
			throw new DirectoryNotFoundException("Không tìm thấy thư mục game: " + fullPath);
		}
		string text = Path.Combine(ResolveBackupDirectory(), assignmentId.ToString("N"), DateTime.UtcNow.ToString("yyyyMMddHHmmss"));
		List<string> list = new List<string>();
		List<string> list2 = new List<string>();
		bool flag = HasZipPassword(zipPassword);
		try
		{
			using FileStream file = File.OpenRead(packagePath);
			using ZipFile zipFile = new ZipFile(file);
			if (flag)
			{
				zipFile.Password = zipPassword;
			}
			for (int i = 0; i < zipFile.Count; i++)
			{
				ZipEntry zipEntry = zipFile[i];
				if (zipEntry == null || zipEntry.IsDirectory)
				{
					continue;
				}
				if (zipEntry.IsCrypted && !flag)
				{
					throw new InvalidOperationException("Package Online Fix đã đặt mật khẩu nhưng launcher không nhận được password.");
				}
				string relativePath = NormalizeRelativePath(zipEntry.Name);
				string fullPath2 = Path.GetFullPath(Path.Combine(fullPath, ToPlatformRelativePath(relativePath)));
				if (!IsPathInsideDirectory(fullPath2, fullPath))
				{
					throw new InvalidOperationException("Package Online Fix có đường dẫn không hợp lệ.");
				}
				Directory.CreateDirectory(Path.GetDirectoryName(fullPath2));
				if (File.Exists(fullPath2))
				{
					BackupFileIfExists(fullPath2, text, relativePath);
					list2.Add(fullPath2);
				}
				using Stream stream = zipFile.GetInputStream(zipEntry);
				using FileStream destination = File.Create(fullPath2);
				stream.CopyTo(destination);
				list.Add(fullPath2);
			}
		}
		catch (ZipException innerException)
		{
			throw new InvalidOperationException(BuildZipReadErrorMessage(flag), innerException);
		}
		if (list.Count == 0)
		{
			throw new InvalidOperationException("Package Online Fix không có file nào để cài.");
		}
		messages.Add("Package ZIP có password: " + (flag ? "true" : "false"));
		messages.Add($"File đã cài: {list.Count}");
		messages.Add($"File đã ghi đè: {list2.Count}");
		return new OnlinePackageInstallResult(list.OrderBy<string, string>((string item) => item, StringComparer.OrdinalIgnoreCase).ToArray(), list2.OrderBy<string, string>((string item) => item, StringComparer.OrdinalIgnoreCase).ToArray(), text);
	}

	private static string? ResolveInstallDirectory(DesktopLibraryDetailResponse detail, DesktopLibraryOnlineFixResponse online, IWin32Window? owner)
	{
		string text = DenuvoFixInstallService.FindSteamGameInstallDirectory(online.SteamAppId, online.DefaultInstallFolderName, detail.GameName);
		if (!string.IsNullOrWhiteSpace(text))
		{
			return text;
		}
		if (owner == null)
		{
			return null;
		}
		using FolderBrowserDialog folderBrowserDialog = new FolderBrowserDialog
		{
			Description = "Chọn thư mục cài đặt game Steam để cài Online Fix.",
			UseDescriptionForTitle = true,
			ShowNewFolderButton = false
		};
		return (folderBrowserDialog.ShowDialog(owner) == DialogResult.OK && Directory.Exists(folderBrowserDialog.SelectedPath)) ? folderBrowserDialog.SelectedPath : null;
	}

	private static string? ResolveGameExePath(string installDirectory, string? mainExeRelativePath, ICollection<string> messages)
	{
		if (string.IsNullOrWhiteSpace(mainExeRelativePath))
		{
			messages.Add("Chưa có link file chạy game vì admin chưa cấu hình MainExeRelativePath.");
			return null;
		}
		string text = SteamShortcutService.ResolveExePath(installDirectory, mainExeRelativePath);
		if (string.IsNullOrWhiteSpace(text))
		{
			messages.Add("Không tạo được link file chạy game vì MainExeRelativePath không hợp lệ hoặc file exe không tồn tại.");
			return null;
		}
		messages.Add("File chạy game: " + text);
		return text;
	}

	private static string NormalizeRelativePath(string? rawPath)
	{
		string input = (string.IsNullOrWhiteSpace(rawPath) ? string.Empty : rawPath.Trim().Replace('\\', '/'));
		input = Regex.Replace(input, "/+", "/").Trim('/');
		if (string.IsNullOrWhiteSpace(input) || Path.IsPathRooted(input) || input.Split('/').Any((string part) => (part == "." || part == "..") ? true : false) || input.Contains(':', StringComparison.Ordinal))
		{
			throw new InvalidOperationException("Đường dẫn trong package Online Fix không hợp lệ.");
		}
		return input;
	}

	private static string ToPlatformRelativePath(string relativePath)
	{
		return relativePath.Replace('/', Path.DirectorySeparatorChar);
	}

	private static bool IsPathInsideDirectory(string candidatePath, string directoryPath)
	{
		string text = Path.GetFullPath(directoryPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
		return Path.GetFullPath(candidatePath).StartsWith(text + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
	}

	private static bool HasZipPassword(string? value)
	{
		if (value != null)
		{
			return value.Length > 0;
		}
		return false;
	}

	private static string BuildZipReadErrorMessage(bool hasPassword)
	{
		if (!hasPassword)
		{
			return "Không thể giải nén package Online Fix. Package có thể đã đặt mật khẩu nhưng server không gửi password.";
		}
		return "Không thể giải nén package Online Fix. Mật khẩu ZIP có thể không đúng hoặc file zip bị lỗi.";
	}

	private static string? BackupFileIfExists(string sourcePath, string backupRoot, string relativePath)
	{
		if (!File.Exists(sourcePath))
		{
			return null;
		}
		string path = ToPlatformRelativePath(NormalizeRelativePath(relativePath));
		string fullPath = Path.GetFullPath(Path.Combine(backupRoot, path));
		if (!IsPathInsideDirectory(fullPath, backupRoot))
		{
			throw new InvalidOperationException("Đường dẫn backup Online Fix không hợp lệ.");
		}
		Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
		File.Copy(sourcePath, fullPath, overwrite: true);
		return fullPath;
	}

	private static string ResolvePackageFallbackFileName(DesktopLibraryDetailResponse detail, DesktopLibraryOnlineFixResponse online)
	{
		return SanitizeFileName(string.IsNullOrWhiteSpace(online.PackageVersion) ? detail.GameName : (detail.GameName + "-" + online.PackageVersion)) + "-online-fix.zip";
	}

	private static string ResolveClientVersion()
	{
		DesktopAppSettings settings = DesktopAppContext.Settings;
		return $"{settings.CurrentVersion}+{settings.CurrentBuildNumber}";
	}

	private static string CreateWorkingDirectory()
	{
		string text = Path.Combine(Path.GetTempPath(), "NestGLauncher", "online", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(text);
		return text;
	}

	private static string ResolveStateDirectory()
	{
		return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NestGLauncher", "OnlineFixState");
	}

	private static string ResolveBackupDirectory()
	{
		return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NestGLauncher", "OnlineBackups");
	}

	private static string ResolveLogDirectory()
	{
		return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NestGLauncher", "OnlineFixLogs");
	}

	private static void SaveLocalState(OnlineFixLocalInstallState state)
	{
		string text = ResolveStateDirectory();
		Directory.CreateDirectory(text);
		File.WriteAllText(Path.Combine(text, $"{state.AssignmentId:N}.json"), JsonSerializer.Serialize(state, StateJsonOptions));
	}

	private static bool IsZipFile(string filePath)
	{
		Span<byte> buffer = stackalloc byte[4];
		using FileStream fileStream = File.OpenRead(filePath);
		return fileStream.Read(buffer) == 4 && buffer[0] == 80 && buffer[1] == 75 && buffer[2] == 3 && buffer[3] == 4;
	}

	private async Task TryPostEventAsync(Guid libraryId, string accessToken, string eventType, string? packageSha256, string? gameInstallPath, string? message, CancellationToken cancellationToken)
	{
		try
		{
			await storefrontApiClient.PostOnlineFixEventAsync(libraryId, new DesktopOnlineFixEventRequest
			{
				EventType = eventType,
				PackageSha256 = packageSha256,
				GameInstallPath = gameInstallPath,
				ClientVersion = ResolveClientVersion(),
				Message = message
			}, accessToken, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
		catch
		{
		}
	}

	private static string WriteInstallLog(DesktopLibraryDetailResponse detail, DesktopLibraryOnlineFixResponse online, string installDirectory, OnlinePackageInstallResult result, IReadOnlyList<string> messages)
	{
		List<string> list = new List<string>
		{
			"Game: " + detail.GameName,
			"Version: " + detail.VersionName,
			$"Assignment: {online.AssignmentId}",
			("Package: " + online.PackageDisplayName + " " + online.PackageVersion).Trim(),
			"Install directory: " + installDirectory,
			$"Time UTC: {DateTime.UtcNow:O}",
			string.Empty,
			$"Files written: {result.WrittenFiles.Count}",
			$"Files overwritten: {result.OverwrittenFiles.Count}",
			"Backup directory: " + result.BackupDirectory,
			string.Empty,
			"Written files:"
		};
		list.AddRange(result.WrittenFiles.Select((string file) => "- " + file));
		if (messages.Count > 0)
		{
			list.Add(string.Empty);
			list.Add("Messages:");
			list.AddRange(messages.Select((string message) => "- " + message));
		}
		return WriteLogFile(SanitizeFileName(detail.GameName) + "-online-install", list);
	}

	private static string WriteErrorLog(DesktopLibraryDetailResponse detail, DesktopLibraryOnlineFixResponse? online, Exception exception)
	{
		return WriteLogFile(SanitizeFileName(detail.GameName) + "-online-error", new _003C_003Ez__ReadOnlyArray<string>(new string[5]
		{
			"Game: " + detail.GameName,
			"Version: " + detail.VersionName,
			$"Assignment: {online?.AssignmentId}",
			$"Time UTC: {DateTime.UtcNow:O}",
			"Error: " + exception.Message
		}));
	}

	private static string WriteLogFile(string name, IEnumerable<string> lines)
	{
		string text = ResolveLogDirectory();
		Directory.CreateDirectory(text);
		string text2 = Path.Combine(text, $"{name}-{DateTime.UtcNow:yyyyMMddHHmmss}.log");
		File.WriteAllLines(text2, lines);
		return text2;
	}

	private static string SanitizeFileName(string value)
	{
		string text = (string.IsNullOrWhiteSpace(value) ? "online-fix" : value.Trim());
		char[] invalidFileNameChars = Path.GetInvalidFileNameChars();
		foreach (char oldChar in invalidFileNameChars)
		{
			text = text.Replace(oldChar, '_');
		}
		text = Regex.Replace(text, "\\s+", " ");
		text = text.Trim(' ', '.');
		if (!string.IsNullOrWhiteSpace(text))
		{
			return text;
		}
		return "online-fix";
	}

	private static void TryDeleteDirectory(string directory)
	{
		try
		{
			if (Directory.Exists(directory))
			{
				Directory.Delete(directory, recursive: true);
			}
		}
		catch
		{
		}
	}
}
