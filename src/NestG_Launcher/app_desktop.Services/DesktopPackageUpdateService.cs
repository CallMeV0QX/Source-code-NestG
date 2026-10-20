using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using app_desktop.Models;

namespace app_desktop.Services;

internal sealed class DesktopPackageUpdateService
{
	private const string LauncherExecutableName = "NestG launcher.exe";

	private const string UpdaterExecutableName = "NestG.Updater.exe";

	private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
	{
		PropertyNameCaseInsensitive = true
	};

	private readonly HttpClient httpClient;

	private readonly string storageFolderName;

	public DesktopPackageUpdateService(string apiBaseUrl, string storageFolderName)
	{
		this.storageFolderName = (string.IsNullOrWhiteSpace(storageFolderName) ? "NestGLauncher" : storageFolderName.Trim());
		httpClient = new HttpClient
		{
			BaseAddress = new Uri(NormalizeBaseUrl(apiBaseUrl)),
			Timeout = TimeSpan.FromMinutes(30.0)
		};
		httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("NestGLauncherDesktop/1.0");
	}

	public async Task<DesktopLauncherPackageMetadata?> GetPackageMetadataAsync(DesktopAppUpdateInfo update, CancellationToken cancellationToken = default(CancellationToken))
	{
		ArgumentNullException.ThrowIfNull(update, "update");
		using HttpResponseMessage response = await httpClient.GetAsync(BuildPackageMetadataEndpoint(update), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		string text = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		if (!response.IsSuccessStatusCode)
		{
			throw new InvalidOperationException(ParseErrorMessage(text, response.ReasonPhrase));
		}
		LauncherPackageMetadataResponse launcherPackageMetadataResponse = JsonSerializer.Deserialize<LauncherPackageMetadataResponse>(text, JsonOptions);
		return (launcherPackageMetadataResponse == null) ? null : new DesktopLauncherPackageMetadata(launcherPackageMetadataResponse.FileName, Math.Max(0L, launcherPackageMetadataResponse.Size), launcherPackageMetadataResponse.SizeText, launcherPackageMetadataResponse.DownloadUrl);
	}

	public async Task<DesktopPackageUpdateStageResult> PreparePackageUpdateAsync(DesktopAppUpdateInfo update, IProgress<string>? progress, CancellationToken cancellationToken = default(CancellationToken))
	{
		ArgumentNullException.ThrowIfNull(update, "update");
		string updateDirectory = BuildUpdateDirectory(update);
		string updateRoot = BuildUpdatesRoot();
		Directory.CreateDirectory(updateDirectory);
		string logPath = BuildLogPath();
		Directory.CreateDirectory(Path.GetDirectoryName(logPath));
		string packagePath = Path.Combine(updateDirectory, BuildPackageFileName(update));
		string stageDirectory = Path.Combine(updateDirectory, "_stage");
		await AppendLogAsync(logPath, "NestG package update staging started.", cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		await AppendLogAsync(logPath, $"Version: {update.Version} build {update.BuildNumber}", cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		await AppendLogAsync(logPath, "Update directory: " + updateDirectory, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		await AppendLogAsync(logPath, "Package path: " + packagePath, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		await AppendLogAsync(logPath, "Stage path: " + stageDirectory, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		EnsurePathInsideRoot(stageDirectory, updateRoot);
		progress?.Report("Đang tải gói cập nhật launcher...");
		await DownloadPackageAsync(update, packagePath, logPath, progress, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		progress?.Report("Đang giải nén gói cập nhật launcher...");
		await ExtractPackageAsync(packagePath, stageDirectory, logPath, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		progress?.Report("Đang kiểm tra gói cập nhật launcher...");
		ValidateStage(stageDirectory);
		progress?.Report("Đang chuẩn bị trình cập nhật...");
		string updaterExecutablePath = PrepareUpdaterHelper(updateDirectory, logPath);
		string installDirectory = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
		string restartExecutablePath = ResolveRestartPath(installDirectory);
		await AppendLogAsync(logPath, "Install target: " + installDirectory, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		await AppendLogAsync(logPath, "Updater helper: " + updaterExecutablePath, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		await AppendLogAsync(logPath, "Restart exe: " + restartExecutablePath, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		await AppendLogAsync(logPath, "NestG package update staging completed.", cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		return new DesktopPackageUpdateStageResult(stageDirectory, installDirectory, restartExecutablePath, updaterExecutablePath, logPath);
	}

	public void StartUpdater(DesktopPackageUpdateStageResult stageResult, int launcherProcessId)
	{
		ArgumentNullException.ThrowIfNull(stageResult, "stageResult");
		if (!File.Exists(stageResult.UpdaterExecutablePath))
		{
			throw new FileNotFoundException("Không tìm thấy NestG.Updater.exe.", stageResult.UpdaterExecutablePath);
		}
		Process.Start(new ProcessStartInfo
		{
			FileName = stageResult.UpdaterExecutablePath,
			WorkingDirectory = (Path.GetDirectoryName(stageResult.UpdaterExecutablePath) ?? AppContext.BaseDirectory),
			UseShellExecute = false,
			ArgumentList = 
			{
				"--pid",
				launcherProcessId.ToString(CultureInfo.InvariantCulture),
				"--stage",
				stageResult.StageDirectory,
				"--target",
				stageResult.InstallDirectory,
				"--restart",
				stageResult.RestartExecutablePath,
				"--log",
				stageResult.LogFilePath
			}
		});
	}

	private async Task DownloadPackageAsync(DesktopAppUpdateInfo update, string destinationPath, string logPath, IProgress<string>? progress, CancellationToken cancellationToken)
	{
		string endpoint = BuildPackageDownloadEndpoint(update);
		await AppendLogAsync(logPath, "Download endpoint: " + endpoint, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		string temporaryPath = destinationPath + ".download";
		TryDeleteFile(temporaryPath);
		try
		{
			using HttpResponseMessage response = await httpClient.GetAsync(endpoint, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			string text = ((!response.IsSuccessStatusCode) ? (await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false)) : string.Empty);
			string rawContent = text;
			if (!response.IsSuccessStatusCode)
			{
				throw new InvalidOperationException(ParseErrorMessage(rawContent, response.ReasonPhrase));
			}
			await AppendLogAsync(logPath, $"Resolved download URL: {response.RequestMessage?.RequestUri}", cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			long totalBytes = response.Content.Headers.ContentLength.GetValueOrDefault();
			await using (Stream sourceStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false))
			{
				await using FileStream targetStream = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None, 131072, useAsync: true);
				byte[] buffer = new byte[131072];
				long readBytes = 0L;
				while (true)
				{
					int bytesRead = await sourceStream.ReadAsync(buffer, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
					if (bytesRead == 0)
					{
						break;
					}
					await targetStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
					readBytes += bytesRead;
					progress?.Report((totalBytes > 0) ? ("Đang tải gói cập nhật launcher " + FormatSize(readBytes) + " / " + FormatSize(totalBytes)) : ("Đang tải gói cập nhật launcher " + FormatSize(readBytes)));
				}
			}
			if (File.Exists(destinationPath))
			{
				File.Delete(destinationPath);
			}
			File.Move(temporaryPath, destinationPath);
			await AppendLogAsync(logPath, "Package download completed.", cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
		catch
		{
			TryDeleteFile(temporaryPath);
			throw;
		}
	}

	private static async Task ExtractPackageAsync(string packagePath, string stageDirectory, string logPath, CancellationToken cancellationToken)
	{
		string path = Path.GetDirectoryName(stageDirectory) ?? throw new InvalidOperationException("Thư mục chuẩn bị cập nhật không hợp lệ.");
		string rawDirectory = Path.Combine(path, "_raw");
		ResetDirectory(rawDirectory);
		ResetDirectory(stageDirectory);
		using (ZipArchive archive = ZipFile.OpenRead(packagePath))
		{
			foreach (ZipArchiveEntry entry in archive.Entries)
			{
				cancellationToken.ThrowIfCancellationRequested();
				string text = NormalizeArchivePath(entry.FullName);
				if (string.IsNullOrWhiteSpace(text))
				{
					continue;
				}
				if (ShouldSkipArchiveEntry(text))
				{
					await AppendLogAsync(logPath, "Skipped archive entry: " + text, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
					continue;
				}
				string fullPath = Path.GetFullPath(Path.Combine(rawDirectory, text));
				EnsurePathInsideRoot(fullPath, rawDirectory);
				if (string.IsNullOrWhiteSpace(entry.Name))
				{
					Directory.CreateDirectory(fullPath);
					continue;
				}
				string directoryName = Path.GetDirectoryName(fullPath);
				if (!string.IsNullOrWhiteSpace(directoryName))
				{
					Directory.CreateDirectory(directoryName);
				}
				entry.ExtractToFile(fullPath, overwrite: true);
			}
		}
		string contentRoot = ResolveContentRoot(rawDirectory);
		await AppendLogAsync(logPath, "ZIP content root: " + contentRoot, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		CopyDirectory(contentRoot, stageDirectory, cancellationToken);
		TryDeleteDirectory(rawDirectory);
	}

	private static string PrepareUpdaterHelper(string updateDirectory, string logPath)
	{
		string baseDirectory = AppContext.BaseDirectory;
		string text = Path.Combine(baseDirectory, "NestG.Updater.exe");
		if (!File.Exists(text))
		{
			throw new FileNotFoundException("NestG.Updater.exe chưa được đóng gói kèm launcher.", text);
		}
		string text2 = Path.Combine(updateDirectory, "_updater");
		ResetDirectory(text2);
		int num = 0;
		foreach (string item in Directory.EnumerateFiles(baseDirectory, "NestG.Updater.*", SearchOption.TopDirectoryOnly))
		{
			string extension = Path.GetExtension(item);
			if (!extension.Equals(".pdb", StringComparison.OrdinalIgnoreCase) && !extension.Equals(".xml", StringComparison.OrdinalIgnoreCase))
			{
				File.Copy(item, Path.Combine(text2, Path.GetFileName(item)), overwrite: true);
				num++;
			}
		}
		File.AppendAllText(logPath, $"[{DateTimeOffset.Now:O}] Copied updater helper files: {num}{Environment.NewLine}");
		string text3 = Path.Combine(text2, "NestG.Updater.exe");
		if (!File.Exists(text3))
		{
			throw new FileNotFoundException("Không thể chuẩn bị NestG.Updater.exe.", text3);
		}
		return text3;
	}

	internal static void ValidateStage(string stageDirectory)
	{
		if (!Directory.Exists(stageDirectory))
		{
			throw new DirectoryNotFoundException("Không tìm thấy thư mục chuẩn bị cập nhật: " + stageDirectory);
		}
		if (!Directory.EnumerateFiles(stageDirectory, "*", SearchOption.AllDirectories).Any())
		{
			throw new InvalidOperationException("Thư mục chuẩn bị cập nhật không có tệp nào.");
		}
		string text = Path.Combine(stageDirectory, "NestG launcher.exe");
		if (!File.Exists(text))
		{
			throw new FileNotFoundException("Gói ZIP phải đặt NestG launcher.exe ở thư mục gốc hoặc trong một thư mục gốc duy nhất.", text);
		}
		if (!File.Exists(Path.Combine(stageDirectory, "appsettings.enc")) || Directory.EnumerateFiles(stageDirectory, "appsettings*.json", SearchOption.AllDirectories).Any())
		{
			throw new InvalidOperationException("Gói cập nhật thiếu appsettings.enc hoặc còn cấu hình JSON dạng rõ.");
		}
	}

	private static string ResolveContentRoot(string rawDirectory)
	{
		string[] array = Directory.EnumerateFiles(rawDirectory, "*", SearchOption.TopDirectoryOnly).ToArray();
		string[] array2 = Directory.EnumerateDirectories(rawDirectory, "*", SearchOption.TopDirectoryOnly).ToArray();
		if (array.Length == 0 && array2.Length == 1 && File.Exists(Path.Combine(array2[0], "NestG launcher.exe")))
		{
			return array2[0];
		}
		return rawDirectory;
	}

	private static void CopyDirectory(string sourceDirectory, string destinationDirectory, CancellationToken cancellationToken)
	{
		foreach (string item in Directory.EnumerateDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
		{
			cancellationToken.ThrowIfCancellationRequested();
			string relativePath = Path.GetRelativePath(sourceDirectory, item);
			Directory.CreateDirectory(Path.Combine(destinationDirectory, relativePath));
		}
		foreach (string item2 in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
		{
			cancellationToken.ThrowIfCancellationRequested();
			string relativePath2 = Path.GetRelativePath(sourceDirectory, item2);
			string text = Path.Combine(destinationDirectory, relativePath2);
			string directoryName = Path.GetDirectoryName(text);
			if (!string.IsNullOrWhiteSpace(directoryName))
			{
				Directory.CreateDirectory(directoryName);
			}
			File.Copy(item2, text, overwrite: true);
		}
	}

	private string BuildPackageDownloadEndpoint(DesktopAppUpdateInfo update)
	{
		if (update.DesktopAppVersionId != Guid.Empty)
		{
			return $"api/launcher-distribution/launcher-package/versions/{update.DesktopAppVersionId:D}/download";
		}
		string stringToEscape = (string.IsNullOrWhiteSpace(update.Platform) ? "windows" : update.Platform.Trim());
		string stringToEscape2 = (string.IsNullOrWhiteSpace(update.ReleaseChannel) ? "stable" : update.ReleaseChannel.Trim());
		return "api/launcher-distribution/launcher-package/latest/download?platform=" + Uri.EscapeDataString(stringToEscape) + "&releaseChannel=" + Uri.EscapeDataString(stringToEscape2);
	}

	private static string BuildPackageMetadataEndpoint(DesktopAppUpdateInfo update)
	{
		string stringToEscape = (string.IsNullOrWhiteSpace(update.Platform) ? "windows" : update.Platform.Trim());
		string stringToEscape2 = (string.IsNullOrWhiteSpace(update.ReleaseChannel) ? "stable" : update.ReleaseChannel.Trim());
		return "api/launcher-distribution/launcher-package/latest?platform=" + Uri.EscapeDataString(stringToEscape) + "&releaseChannel=" + Uri.EscapeDataString(stringToEscape2);
	}

	private string BuildUpdateDirectory(DesktopAppUpdateInfo update)
	{
		string value = NormalizePathSegment(string.IsNullOrWhiteSpace(update.Version) ? "latest" : update.Version);
		string value2 = Math.Max(0, update.BuildNumber).ToString(CultureInfo.InvariantCulture);
		string value3 = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
		return Path.Combine(BuildUpdatesRoot(), $"{value}-{value2}-{value3}");
	}

	private string BuildUpdatesRoot()
	{
		return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), storageFolderName, "Updates");
	}

	private string BuildLogPath()
	{
		return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), storageFolderName, "UpdateLogs", $"update-{DateTime.Now:yyyyMMdd-HHmmss}.log");
	}

	private static string BuildPackageFileName(DesktopAppUpdateInfo update)
	{
		string text = NormalizePathSegment(string.IsNullOrWhiteSpace(update.Version) ? "latest" : update.Version);
		return "NestG Launcher " + text + ".zip";
	}

	private static string ResolveRestartPath(string installDirectory)
	{
		string text = Path.Combine(installDirectory, "NestG launcher.exe");
		if (File.Exists(text))
		{
			return text;
		}
		return Environment.ProcessPath ?? text;
	}

	private static string NormalizeArchivePath(string entryName)
	{
		if (string.IsNullOrWhiteSpace(entryName) || Path.IsPathFullyQualified(entryName))
		{
			return string.Empty;
		}
		string[] array = entryName.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
		if (array.Length == 0)
		{
			return string.Empty;
		}
		if (array.Any((string segment) => segment.Equals(".", StringComparison.Ordinal) || segment.Equals("..", StringComparison.Ordinal) || segment.Contains(':', StringComparison.Ordinal)))
		{
			throw new InvalidOperationException("Mục trong tệp ZIP không hợp lệ: " + entryName);
		}
		return Path.Combine(array);
	}

	private static bool ShouldSkipArchiveEntry(string relativePath)
	{
		string fileName = Path.GetFileName(relativePath);
		if (fileName.Equals("Thumbs.db", StringComparison.OrdinalIgnoreCase) || fileName.Equals(".DS_Store", StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}
		string extension = Path.GetExtension(fileName);
		if (extension.Equals(".pdb", StringComparison.OrdinalIgnoreCase) || extension.Equals(".xml", StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}
		return relativePath.Split(new char[2]
		{
			Path.DirectorySeparatorChar,
			Path.AltDirectorySeparatorChar
		}, StringSplitOptions.RemoveEmptyEntries).Any((string segment) => segment.EndsWith(".WebView2", StringComparison.OrdinalIgnoreCase) || segment.Equals("EBWebView", StringComparison.OrdinalIgnoreCase) || segment.Equals("WebView2Cache", StringComparison.OrdinalIgnoreCase));
	}

	private static void EnsurePathInsideRoot(string path, string root)
	{
		string value = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
		string fullPath = Path.GetFullPath(path);
		if (!fullPath.StartsWith(value, StringComparison.OrdinalIgnoreCase))
		{
			throw new InvalidOperationException("Đường dẫn vượt ngoài thư mục cho phép: " + fullPath);
		}
	}

	private static void ResetDirectory(string path)
	{
		TryDeleteDirectory(path);
		Directory.CreateDirectory(path);
	}

	private static void TryDeleteDirectory(string path)
	{
		try
		{
			if (Directory.Exists(path))
			{
				Directory.Delete(path, recursive: true);
			}
		}
		catch
		{
		}
	}

	private static void TryDeleteFile(string path)
	{
		try
		{
			if (File.Exists(path))
			{
				File.Delete(path);
			}
		}
		catch
		{
		}
	}

	private static Task AppendLogAsync(string logPath, string message, CancellationToken cancellationToken)
	{
		return File.AppendAllTextAsync(logPath, $"[{DateTimeOffset.Now:O}] {message}{Environment.NewLine}", cancellationToken);
	}

	private static string ParseErrorMessage(string rawContent, string? fallbackReason)
	{
		if (string.IsNullOrWhiteSpace(rawContent))
		{
			return fallbackReason ?? "Không thể tải package launcher.";
		}
		try
		{
			using JsonDocument jsonDocument = JsonDocument.Parse(rawContent);
			if (jsonDocument.RootElement.TryGetProperty("message", out var value) && value.ValueKind == JsonValueKind.String)
			{
				return value.GetString() ?? fallbackReason ?? "Không thể tải package launcher.";
			}
		}
		catch (JsonException)
		{
		}
		return fallbackReason ?? "Không thể tải package launcher.";
	}

	private static string NormalizePathSegment(string value)
	{
		string text = (string.IsNullOrWhiteSpace(value) ? "latest" : value.Trim());
		char[] invalidFileNameChars = Path.GetInvalidFileNameChars();
		foreach (char oldChar in invalidFileNameChars)
		{
			text = text.Replace(oldChar, '_');
		}
		return text;
	}

	private static string FormatSize(long bytes)
	{
		string[] array = new string[5] { "B", "KB", "MB", "GB", "TB" };
		double num = Math.Max(0L, bytes);
		int num2 = 0;
		while (num >= 1024.0 && num2 < array.Length - 1)
		{
			num /= 1024.0;
			num2++;
		}
		return $"{num:0.##} {array[num2]}";
	}

	private static string NormalizeBaseUrl(string apiBaseUrl)
	{
		if (string.IsNullOrWhiteSpace(apiBaseUrl))
		{
			return "https://localhost:7023/";
		}
		if (!apiBaseUrl.EndsWith("/", StringComparison.Ordinal))
		{
			return apiBaseUrl + "/";
		}
		return apiBaseUrl;
	}
}
