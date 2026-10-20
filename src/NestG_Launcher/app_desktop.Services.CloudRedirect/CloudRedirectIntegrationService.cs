using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using UI_Desktop.Store;
using app_desktop.Services.CloudRedirect.Patching;

namespace app_desktop.Services.CloudRedirect;

internal sealed class CloudRedirectIntegrationService(StorefrontApiClient storefrontApiClient)
{
	private const string CloudRedirectDllName = "cloud_redirect.dll";

	public async Task<CloudRedirectInstallResult> InstallAsync(string accessToken, IProgress<CloudRedirectInstallProgress>? progress = null, CancellationToken cancellationToken = default(CancellationToken))
	{
		if (GameActivationService.UsesBetterSteamToolsRuntime)
		{
			throw new InvalidOperationException("CloudRedirect cũ không áp dụng với BetterSteamTools runtime.");
		}
		if (string.IsNullOrWhiteSpace(accessToken))
		{
			throw new InvalidOperationException("Phiên đăng nhập đã hết hạn. Hãy đăng nhập lại để sửa lỗi Steam Cloud.");
		}
		string workingDirectory = CreateWorkingDirectory();
		List<string> messages = new List<string>();
		string steamDirectory = null;
		try
		{
			Log("Đang tìm thư mục Steam...");
			steamDirectory = SteamInstallLocator.FindSteamDirectory();
			string logFilePath;
			if (string.IsNullOrWhiteSpace(steamDirectory))
			{
				logFilePath = WriteInstallLog(succeeded: false, steamDirectory, messages);
				return Finish(succeeded: false, "Không tìm thấy thư mục Steam trên máy.", steamDirectory, logFilePath, messages);
			}
			Log("Steam: " + steamDirectory);
			await Task.Run(delegate
			{
				EnsureSteamClosed(steamDirectory, Log);
			}, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			cancellationToken.ThrowIfCancellationRequested();
			Log("Đang tải cloud_redirect.dll từ server launcher...");
			string cloudRedirectDllPath = await ResolveCloudRedirectDllAsync(accessToken, steamDirectory, workingDirectory, Log, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			cancellationToken.ThrowIfCancellationRequested();
			await TryInstallSteamToolsCoreDllsFromServerAsync(accessToken, steamDirectory, workingDirectory, Log, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			cancellationToken.ThrowIfCancellationRequested();
			Log("Đang ghi cấu hình CloudRedirect...");
			await Task.Run(delegate
			{
				WriteCloudRedirectConfig(steamDirectory, Log);
			}, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			cancellationToken.ThrowIfCancellationRequested();
			Patcher patcher = new Patcher(steamDirectory, Log, cloudRedirectDllPath);
			if (!patcher.HasCoreDll())
			{
				Log("Chưa có core DLL của SteamTools. Đang chuẩn bị core DLL...");
				PatchResult patchResult = await patcher.RepairCoreDllsAsync().ConfigureAwait(continueOnCapturedContext: false);
				if (!patchResult.Succeeded)
				{
					logFilePath = WriteInstallLog(succeeded: false, steamDirectory, messages);
					return Finish(succeeded: false, patchResult.Error ?? "Không chuẩn bị được core DLL của SteamTools.", steamDirectory, logFilePath, messages);
				}
			}
			cancellationToken.ThrowIfCancellationRequested();
			Log("Đang patch CloudRedirect namespace...");
			PatchResult patchResult2 = await Task.Run((Func<PatchResult>)patcher.ApplyCloudRedirectNamespace, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			if (!patchResult2.Succeeded)
			{
				logFilePath = WriteInstallLog(succeeded: false, steamDirectory, messages);
				return Finish(succeeded: false, patchResult2.Error ?? "Không patch được CloudRedirect namespace.", steamDirectory, logFilePath, messages);
			}
			cancellationToken.ThrowIfCancellationRequested();
			Log("Đang patch SteamTools.exe nếu có...");
			int num = await Task.Run((Func<int>)patcher.PatchSteamToolsExe, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			if (num > 0)
			{
				Log("Đã patch SteamTools.exe.");
			}
			else if (num == 0)
			{
				Log("Không tìm thấy SteamTools.exe, bỏ qua bước này.");
			}
			else
			{
				Log("Không patch được SteamTools.exe. CloudRedirect vẫn đã được áp dụng.");
			}
			await Task.Run(delegate
			{
				RestartSteam(steamDirectory, Log);
			}, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			logFilePath = WriteInstallLog(succeeded: true, steamDirectory, messages);
			return Finish(succeeded: true, "Đã kích hoạt CloudRedirect. Steam đã được mở lại để nạp cấu hình mới.", steamDirectory, logFilePath, messages);
		}
		catch (OperationCanceledException)
		{
			string logFilePath = WriteInstallLog(succeeded: false, steamDirectory, messages);
			return Finish(succeeded: false, "Đã hủy thao tác sửa lỗi Steam Cloud.", steamDirectory, logFilePath, messages);
		}
		catch (Exception ex2)
		{
			Log("Lỗi: " + ex2.Message);
			string logFilePath = WriteInstallLog(succeeded: false, steamDirectory, messages);
			return Finish(succeeded: false, ex2.Message, steamDirectory, logFilePath, messages);
		}
		finally
		{
			TryDeleteDirectory(workingDirectory);
		}
		void Log(string message)
		{
			messages.Add(message);
			progress?.Report(new CloudRedirectInstallProgress(message));
		}
	}

	private async Task<string> ResolveCloudRedirectDllAsync(string accessToken, string steamDirectory, string workingDirectory, Action<string> log, CancellationToken cancellationToken)
	{
		string existingDllPath = Path.Combine(steamDirectory, "cloud_redirect.dll");
		try
		{
			DesktopDllFileItemResponse desktopDllFileItemResponse = (await storefrontApiClient.GetDllFilesAsync(accessToken, cancellationToken).ConfigureAwait(continueOnCapturedContext: false)).Items.FirstOrDefault((DesktopDllFileItemResponse item) => string.Equals(item.FileName, "cloud_redirect.dll", StringComparison.OrdinalIgnoreCase));
			if (desktopDllFileItemResponse != null && !string.IsNullOrWhiteSpace(desktopDllFileItemResponse.DownloadUrl))
			{
				DesktopDownloadedFileResponse desktopDownloadedFileResponse = await storefrontApiClient.DownloadDllFileAsync(desktopDllFileItemResponse.DownloadUrl, accessToken, workingDirectory, "cloud_redirect.dll", cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				log("Đã tải " + desktopDownloadedFileResponse.FileName + ".");
				return desktopDownloadedFileResponse.FullPath;
			}
		}
		catch (Exception ex)
		{
			log("Không tải được danh sách DLL từ server: " + ex.Message);
		}
		if (File.Exists(existingDllPath))
		{
			log("Server chưa có cloud_redirect.dll, dùng file đang có trong thư mục Steam.");
			return existingDllPath;
		}
		throw new InvalidOperationException("Server chưa có cloud_redirect.dll. Hãy upload file này vào mục DLL của admin trước khi bấm sửa lỗi Steam Cloud.");
	}

	private async Task TryInstallSteamToolsCoreDllsFromServerAsync(string accessToken, string steamDirectory, string workingDirectory, Action<string> log, CancellationToken cancellationToken)
	{
		_ = 1;
		try
		{
			DesktopDllFileListResponse dllFiles = await storefrontApiClient.GetDllFilesAsync(accessToken, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			string[] array = new string[2] { "xinput1_4.dll", "dwmapi.dll" };
			string[] array2 = array;
			foreach (string coreDllName in array2)
			{
				DesktopDllFileItemResponse desktopDllFileItemResponse = dllFiles.Items.FirstOrDefault((DesktopDllFileItemResponse item) => string.Equals(item.FileName, coreDllName, StringComparison.OrdinalIgnoreCase));
				if (desktopDllFileItemResponse != null && !string.IsNullOrWhiteSpace(desktopDllFileItemResponse.DownloadUrl))
				{
					DesktopDownloadedFileResponse desktopDownloadedFileResponse = await storefrontApiClient.DownloadDllFileAsync(desktopDllFileItemResponse.DownloadUrl, accessToken, workingDirectory, coreDllName, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
					FileUtils.AtomicWriteAllBytes(Path.Combine(steamDirectory, coreDllName), File.ReadAllBytes(desktopDownloadedFileResponse.FullPath));
					log("Đã cài " + coreDllName + " vào Steam.");
				}
			}
		}
		catch (Exception ex)
		{
			log("Không cài được core DLL từ server, sẽ dùng fallback của CloudRedirect nếu cần: " + ex.Message);
		}
	}

	private static void WriteCloudRedirectConfig(string steamDirectory, Action<string> log)
	{
		string localCloudPath = Path.Combine(steamDirectory, "localcloud");
		Directory.CreateDirectory(localCloudPath);
		ConfigHelper.SaveConfig(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CloudRedirect", "config.json"), new _003C_003Ez__ReadOnlyArray<string>(new string[2] { "provider", "sync_path" }), delegate(Utf8JsonWriter writer)
		{
			writer.WriteString("provider", "folder");
			writer.WriteString("sync_path", localCloudPath);
		});
		ConfigHelper.SaveConfig(Path.Combine(steamDirectory, "cloud_redirect", "config.json"), new _003C_003Ez__ReadOnlyArray<string>(new string[2] { "cloud_redirect", "auto_update_dll" }), delegate(Utf8JsonWriter writer)
		{
			writer.WriteBoolean("cloud_redirect", value: true);
			writer.WriteBoolean("auto_update_dll", value: false);
		});
		log("Cấu hình provider folder: " + localCloudPath);
	}

	private static void EnsureSteamClosed(string steamDirectory, Action<string> log)
	{
		if (!IsSteamRunning())
		{
			log("Steam chưa chạy, có thể patch ngay.");
			return;
		}
		string text = Path.Combine(steamDirectory, "steam.exe");
		if (File.Exists(text))
		{
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
			catch (Exception ex)
			{
				log("Không gửi được lệnh tắt Steam: " + ex.Message);
			}
		}
		WaitForSteamProcessesToExit(TimeSpan.FromSeconds(15.0));
		List<Process> runningSteamProcesses = GetRunningSteamProcesses();
		if (runningSteamProcesses.Count == 0)
		{
			log("Đã đóng Steam.");
			return;
		}
		log("Steam chưa tắt hết, launcher sẽ đóng cưỡng bức để patch file.");
		foreach (Process item in runningSteamProcesses)
		{
			try
			{
				if (!item.HasExited)
				{
					item.Kill(entireProcessTree: true);
					item.WaitForExit(5000);
				}
			}
			catch (Exception ex2)
			{
				log($"Không đóng được Steam process {item.Id}: {ex2.Message}");
			}
			finally
			{
				item.Dispose();
			}
		}
		if (!IsSteamRunning())
		{
			return;
		}
		throw new InvalidOperationException("Steam vẫn đang chạy. Hãy tắt Steam thủ công rồi thử lại.");
	}

	private static void RestartSteam(string steamDirectory, Action<string> log)
	{
		string text = Path.Combine(steamDirectory, "steam.exe");
		if (!File.Exists(text))
		{
			log("Không tìm thấy steam.exe để mở lại Steam.");
			return;
		}
		if (IsSteamRunning())
		{
			log("Steam đang chạy lại, bỏ qua bước mở Steam.");
			return;
		}
		try
		{
			Process.Start(new ProcessStartInfo
			{
				FileName = text,
				WorkingDirectory = steamDirectory,
				UseShellExecute = true
			})?.Dispose();
			log("Đã mở lại Steam.");
		}
		catch (Exception ex)
		{
			log("Không mở lại được Steam tự động: " + ex.Message);
		}
	}

	private static bool IsSteamRunning()
	{
		List<Process> runningSteamProcesses = GetRunningSteamProcesses();
		try
		{
			return runningSteamProcesses.Count > 0;
		}
		finally
		{
			foreach (Process item in runningSteamProcesses)
			{
				item.Dispose();
			}
		}
	}

	private static List<Process> GetRunningSteamProcesses()
	{
		return (from process in Process.GetProcessesByName("steam")
			where !process.HasExited
			select process).ToList();
	}

	private static void WaitForSteamProcessesToExit(TimeSpan timeout)
	{
		Stopwatch stopwatch = Stopwatch.StartNew();
		while (stopwatch.Elapsed < timeout && IsSteamRunning())
		{
			Thread.Sleep(350);
		}
	}

	private static string CreateWorkingDirectory()
	{
		string text = Path.Combine(Path.GetTempPath(), "NestGLauncher", "cloud-redirect", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(text);
		return text;
	}

	private static string ResolveLogDirectory()
	{
		return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NestGLauncher", "CloudRedirectLogs");
	}

	private static string WriteInstallLog(bool succeeded, string? steamDirectory, IReadOnlyList<string> messages)
	{
		string text = ResolveLogDirectory();
		Directory.CreateDirectory(text);
		string text2 = Path.Combine(text, $"{DateTime.UtcNow:yyyyMMddHHmmss}-cloud-redirect.log");
		List<string> list = new List<string>
		{
			"Status: " + (succeeded ? "Succeeded" : "Failed"),
			$"Time UTC: {DateTime.UtcNow:O}",
			"Steam directory: " + (steamDirectory ?? "Not found"),
			string.Empty,
			"Messages:"
		};
		list.AddRange(messages.Select((string message) => "- " + message));
		File.WriteAllLines(text2, list);
		return text2;
	}

	private static CloudRedirectInstallResult Finish(bool succeeded, string message, string? steamDirectory, string? logFilePath, IReadOnlyList<string> messages)
	{
		return new CloudRedirectInstallResult(succeeded, message, steamDirectory, logFilePath, messages.ToArray());
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
