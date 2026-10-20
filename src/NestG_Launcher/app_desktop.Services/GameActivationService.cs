using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UI_Desktop.Store;
using app_desktop.Services.CloudRedirect;

namespace app_desktop.Services;

internal sealed class GameActivationService(StorefrontApiClient storefrontApiClient)
{
	private const string RuntimeModeName = "BetterSteamTools + CloudRedirect";

	private const string LuaBrandingHeader = "-- Lua and Manifest Created By NestG";

	private const string LuaBrandingWebsite = "-- Website: https://nestg.cloud";

	private readonly CloudRedirectRuntimeService cloudRedirectRuntimeService = new CloudRedirectRuntimeService(storefrontApiClient, DesktopAppContext.Settings.CurrentVersion);

	private static readonly string[] ExpandedEditionMarkers = new string[7] { "dlc", "full", "ultimate", "complete", "premium", "deluxe", "gold" };

	internal static bool UsesOpenSteamToolRuntime => false;

	internal static bool UsesBetterSteamToolsRuntime => true;

	internal static string RuntimeMode => "BetterSteamTools + CloudRedirect";

	private async Task<SteamInstallResult> ActivateAttemptAsync(DesktopLibraryDetailResponse detail, string accessToken, string steamDirectory, bool betterOnly, CancellationToken token, IProgress<GameActivationProgress>? progress, GameActivationLog activationLog)
	{
		string directory = CreateActivationWorkingDirectory();
		try
		{
			progress?.Report(new GameActivationProgress(GameActivationProgressStage.Checking, "Đang kiểm tra phiên bản Steam...", null));
			await Task.Run(delegate
			{
				cloudRedirectRuntimeService.ValidateSteamBuild(steamDirectory);
			}, token).ConfigureAwait(continueOnCapturedContext: false);
			progress?.Report(new GameActivationProgress(GameActivationProgressStage.PreparingActivation, "Đang chuẩn bị file kích hoạt...", null));
			string text = Path.Combine(directory, "downloads");
			string filesDirectory = Path.Combine(directory, "files");
			Directory.CreateDirectory(text);
			Directory.CreateDirectory(filesDirectory);
			DesktopDownloadedFileResponse package = await storefrontApiClient.DownloadLibraryActivationPackageAsync(detail.LibraryId, accessToken, text, ResolveActivationPackageFallbackFileName(detail.VersionName), token).ConfigureAwait(continueOnCapturedContext: false);
			token.ThrowIfCancellationRequested();
			IReadOnlyList<string> readOnlyList;
			if (IsZipFile(package.FullPath))
			{
				readOnlyList = await Task.Run(() => ExtractZipToDirectory(package.FullPath, filesDirectory), token).ConfigureAwait(continueOnCapturedContext: false);
			}
			else
			{
				string fileName = Path.GetFileName(string.IsNullOrWhiteSpace(package.FileName) ? "game.lua" : package.FileName);
				string text2 = Path.Combine(filesDirectory, fileName);
				File.Copy(package.FullPath, text2, overwrite: true);
				readOnlyList = new _003C_003Ez__ReadOnlySingleElementList<string>(text2);
			}
			token.ThrowIfCancellationRequested();
			ValidatePreparedLuaFiles(readOnlyList, detail.SteamAppId);
			RewritePreparedLuaFiles(readOnlyList);
			IReadOnlyList<CloudRedirectActivationFile> activationFiles = BuildCloudRedirectActivationFiles(readOnlyList);
			if (!activationFiles.Any((CloudRedirectActivationFile file) => file.InstallPath.EndsWith(".manifest", StringComparison.OrdinalIgnoreCase)))
			{
				throw new InvalidDataException("Package active không có manifest.");
			}
			PreparedCloudRedirectRuntime runtime = await cloudRedirectRuntimeService.PrepareAsync(accessToken, Path.Combine(directory, "runtime"), token, progress, betterOnly).ConfigureAwait(continueOnCapturedContext: false);
			progress?.Report(new GameActivationProgress(GameActivationProgressStage.RestartingSteam, "Đang chờ Steam đóng để kích hoạt...", null));
			SteamMutationResult<CloudRedirectRuntimeInstallResult> steamMutationResult = await SteamProcessController.MutateAsync(steamDirectory, token, Guid.NewGuid().ToString("N"), () => cloudRedirectRuntimeService.InstallForActivation(runtime, steamDirectory, detail, activationFiles, token)).ConfigureAwait(continueOnCapturedContext: false);
			token.ThrowIfCancellationRequested();
			if (!steamMutationResult.Closure.IsClosed || (object)steamMutationResult.Value == null)
			{
				return new SteamInstallResult(steamDirectory, IsFound: true, Array.Empty<string>(), new _003C_003Ez__ReadOnlySingleElementList<string>(steamMutationResult.Closure.Message), IsSteamRestarted: false)
				{
					BetterOnly = betterOnly,
					FailureMessage = steamMutationResult.Closure.Message
				};
			}
			CloudRedirectRuntimeInstallResult installed = steamMutationResult.Value;
			List<string> messages = installed.Messages.ToList();
			CloudRedirectHealthMarker cloudMarker = (betterOnly ? null : CloudRedirectRuntimeService.CaptureHealthMarker(steamDirectory));
			BetterSteamToolsLogMarker betterMarker = BetterSteamToolsHealthReader.Capture(steamDirectory);
			IReadOnlyList<SteamActivationFileStamp> stamps = CloudRedirectRuntimeService.CaptureActivationFiles(activationFiles);
			await Task.Run(delegate
			{
				cloudRedirectRuntimeService.VerifyActivationAttempt(steamDirectory, detail.SteamAppId.Value, stamps, betterOnly);
			}, token).ConfigureAwait(continueOnCapturedContext: false);
			token.ThrowIfCancellationRequested();
			if (SteamProcessInspector.Capture(steamDirectory).State != 0)
			{
				return new SteamInstallResult(steamDirectory, IsFound: true, installed.WrittenFiles, messages, IsSteamRestarted: false)
				{
					BetterOnly = betterOnly,
					FailureMessage = "Steam đã mở lại trước lượt xác minh. Hãy kiểm tra Steam rồi thử lại."
				};
			}
			if (!StartSteam(steamDirectory, messages))
			{
				return new SteamInstallResult(steamDirectory, IsFound: true, installed.WrittenFiles, messages, IsSteamRestarted: false)
				{
					BetterOnly = betterOnly,
					FailureMessage = "Không thể mở Steam sau khi cài bộ kích hoạt."
				};
			}
			progress?.Report(new GameActivationProgress(GameActivationProgressStage.Verifying, "Đang kiểm tra Steam và bộ kích hoạt...", null));
			SteamStartupResult observed = await SteamStartupMonitor.WaitAsync(steamDirectory, cloudMarker, token, TimeSpan.FromSeconds(betterOnly ? 30 : 60), null, betterMarker, requireBetterHealth: true).ConfigureAwait(continueOnCapturedContext: false);
			activationLog.Add($"Verification: BetterOnly={betterOnly}; Running={observed.IsRunning}; Unknown={observed.ProcessStateUnknown}; Pid={observed.Session?.ProcessId}; StartupFailed={observed.StartupFailed}; CoreFailed={observed.CoreInitializationFailed}; BetterReady={observed.BetterRuntimeReady}; CloudReady={observed.Health?.IsReady}; CloudFailure={observed.Health?.IsDefinitiveFailure}; Message={observed.Message}");
			bool flag = IsAttemptVerified(observed, betterOnly);
			if (flag)
			{
				await Task.Run(delegate
				{
					cloudRedirectRuntimeService.VerifyActivationAttempt(steamDirectory, detail.SteamAppId.Value, stamps, betterOnly);
				}, token).ConfigureAwait(continueOnCapturedContext: false);
				SteamProcessObservation observation = SteamProcessInspector.Capture(steamDirectory).Observation;
				flag = observation.State == SteamProcessState.Running && observation.Session == observed.Session;
			}
			token.ThrowIfCancellationRequested();
			messages.Add(observed.Message);
			return new SteamInstallResult(steamDirectory, IsFound: true, installed.WrittenFiles, messages, observed.IsRunning)
			{
				BetterOnly = betterOnly,
				BetterRuntimeVerified = flag,
				CloudRedirectHealth = observed.Health,
				HealthMarker = cloudMarker,
				CloudRedirectProvider = installed.Provider,
				SteamStartupFailed = observed.StartupFailed,
				RuntimeChanged = installed.RuntimeChanged,
				ConfigurationChanged = installed.ConfigurationChanged,
				ActivationChanged = installed.ActivationChanged,
				FailureMessage = (flag ? "" : "Chưa xác nhận Steam và bộ kích hoạt đã sẵn sàng trong thời gian kiểm tra.")
			};
		}
		finally
		{
			TryDeleteDirectory(directory);
		}
	}

	internal static bool IsAttemptVerified(SteamStartupResult observed, bool betterOnly)
	{
		if (observed.IsRunning && (object)observed.Session != null && !observed.ProcessStateUnknown && !observed.StartupFailed && !observed.CoreInitializationFailed && observed.BetterRuntimeReady)
		{
			if (!betterOnly)
			{
				return observed.Health?.IsReady ?? false;
			}
			return true;
		}
		return false;
	}

	internal static CloudRedirectHealthResult? ResolveActivationHealth(SteamRecoveryOperation operation, SteamStartupResult? startup)
	{
		if (operation.Outcome != SteamRecoveryOutcome.ActiveReady)
		{
			return new CloudRedirectHealthResult(IsReady: false, operation.CloudInitializationFailed, operation.HealthMarker?.LogPath ?? "", operation.Message);
		}
		return startup?.Health;
	}

	public async Task<GameActivationResult> ActivateAsync(DesktopLibraryDetailResponse detail, string accessToken, CancellationToken cancellationToken = default(CancellationToken), IProgress<GameActivationProgress>? progress = null, GameActivationLog? activationLog = null)
	{
		GameActivationLog log = activationLog ?? new GameActivationLog(detail, DesktopAppContext.Settings.CurrentVersion);
		try
		{
			log.Add("Stage=Preflight");
			GameActivationPreflight.ValidateWindowsArchitecture(Environment.Is64BitOperatingSystem);
			cancellationToken.ThrowIfCancellationRequested();
			if (string.IsNullOrWhiteSpace(accessToken))
			{
				throw new InvalidOperationException("Bạn cần đăng nhập để active game.");
			}
			int? steamAppId = detail.SteamAppId;
			if (!steamAppId.HasValue || steamAppId.GetValueOrDefault() <= 0)
			{
				throw new InvalidOperationException("Game chưa có Steam App ID hợp lệ; launcher không thể active an toàn.");
			}
			using (await CloudRedirectOperationCoordinator.AcquireAsync(cancellationToken))
			{
				string steamDirectory = SteamInstallLocator.FindSteamDirectory();
				if (string.IsNullOrWhiteSpace(steamDirectory))
				{
					return CreateAttemptResult(new SteamInstallResult(null, IsFound: false, Array.Empty<string>(), new _003C_003Ez__ReadOnlySingleElementList<string>("Không tìm thấy thư mục Steam trên máy. Hãy cài đặt Steam hoặc mở Steam ít nhất một lần rồi active lại."), IsSteamRestarted: false)
					{
						AttemptFailed = true,
						FailureMessage = "Không tìm thấy Steam trên máy."
					}, log);
				}
				return CreateAttemptResult(await GameActivationAttempts.RunAsync(steamDirectory, (bool betterOnly, CancellationToken token, IProgress<GameActivationProgress>? updates) => ActivateAttemptAsync(detail, accessToken, steamDirectory, betterOnly, token, updates, log), cancellationToken, progress, log), log);
			}
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			log.Finish("CANCELLED");
			throw;
		}
		catch (Exception error)
		{
			log.Fail(error);
			throw;
		}
	}

	private static GameActivationResult CreateAttemptResult(SteamInstallResult install, GameActivationLog log)
	{
		log.Result(install);
		return new GameActivationResult(ResolveActivationLogDirectory(), install.CopiedFiles, install, log.InternalPath)
		{
			ActivationLogId = log.Id
		};
	}

	internal CloudRedirectRuntimeStatus GetCloudRedirectStatus()
	{
		return cloudRedirectRuntimeService.InspectStatus(SteamInstallLocator.FindSteamDirectory());
	}

	internal Task<CloudRedirectManagementResult> DisableCloudRedirectAsync(CancellationToken cancellationToken = default(CancellationToken))
	{
		return ManageCloudRedirectAsync(rollback: false, cancellationToken);
	}

	internal Task<CloudRedirectManagementResult> RollbackLatestCloudRedirectAsync(CancellationToken cancellationToken = default(CancellationToken))
	{
		return ManageCloudRedirectAsync(rollback: true, cancellationToken);
	}

	private async Task<CloudRedirectManagementResult> ManageCloudRedirectAsync(bool rollback, CancellationToken cancellationToken)
	{
		using (await CloudRedirectOperationCoordinator.AcquireAsync(cancellationToken))
		{
			string steamDirectory = SteamInstallLocator.FindSteamDirectory();
			if (string.IsNullOrWhiteSpace(steamDirectory))
			{
				throw new InvalidOperationException("Không tìm thấy thư mục Steam.");
			}
			SteamMutationResult<CloudRedirectRuntimeOperationResult> steamMutationResult = await SteamProcessController.MutateAsync(steamDirectory, cancellationToken, rollback ? "ManagementRollback" : "ManagementDisable", () => (!rollback) ? cloudRedirectRuntimeService.Disable(steamDirectory) : cloudRedirectRuntimeService.RollbackLatestCompleted(steamDirectory));
			if (!steamMutationResult.Closure.IsClosed)
			{
				return new CloudRedirectManagementResult(steamMutationResult.Closure.Message, "", "", IsSteamRestarted: false, new _003C_003Ez__ReadOnlySingleElementList<string>(steamMutationResult.Closure.Message));
			}
			CloudRedirectRuntimeOperationResult value = steamMutationResult.Value;
			List<string> messages = new List<string> { value.Message };
			bool isSteamRestarted = StartSteam(steamDirectory, messages);
			return new CloudRedirectManagementResult(value.Message, value.BackupDirectory, value.TransactionPath, isSteamRestarted, messages);
		}
	}

	private static string CreateActivationWorkingDirectory()
	{
		string text = Path.Combine(Path.GetTempPath(), "NestGLauncher", "activation", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(text);
		return text;
	}

	private static string ResolveActivationPackageFallbackFileName(string? versionName)
	{
		if (string.IsNullOrWhiteSpace(versionName))
		{
			return "basegame.lua";
		}
		string normalized = versionName.Trim().ToLowerInvariant();
		if (!ExpandedEditionMarkers.Any((string marker) => normalized.Contains(marker, StringComparison.Ordinal)))
		{
			return "basegame.lua";
		}
		return "game.lua";
	}

	private static string ResolveActivationLogDirectory()
	{
		return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NestGLauncher", "ActivationLogs");
	}

	private static string SanitizeFileName(string value)
	{
		string text = (string.IsNullOrWhiteSpace(value) ? "game" : value.Trim());
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
		return "game";
	}

	private static bool IsZipFile(string filePath)
	{
		Span<byte> buffer = stackalloc byte[4];
		using FileStream fileStream = File.OpenRead(filePath);
		return fileStream.Read(buffer) == 4 && buffer[0] == 80 && buffer[1] == 75 && buffer[2] == 3 && buffer[3] == 4;
	}

	private static IReadOnlyList<string> ExtractZipToDirectory(string zipPath, string targetDirectory)
	{
		List<string> list = new List<string>();
		string fullPath = Path.GetFullPath(targetDirectory);
		using ZipArchive zipArchive = ZipFile.OpenRead(zipPath);
		foreach (ZipArchiveEntry entry in zipArchive.Entries)
		{
			if (!string.IsNullOrWhiteSpace(entry.Name))
			{
				string fullPath2 = Path.GetFullPath(Path.Combine(fullPath, entry.FullName));
				if (!fullPath2.StartsWith(fullPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
				{
					throw new InvalidOperationException("Package manifest có đường dẫn không hợp lệ.");
				}
				Directory.CreateDirectory(Path.GetDirectoryName(fullPath2));
				entry.ExtractToFile(fullPath2, overwrite: true);
				list.Add(fullPath2);
			}
		}
		return list;
	}

	private static void RewritePreparedLuaFiles(IEnumerable<string> localFiles)
	{
		foreach (string item in localFiles.Where(IsLuaFilePath))
		{
			if (File.Exists(item))
			{
				byte[] bytes = RewriteLuaBranding(File.ReadAllBytes(item));
				File.WriteAllBytes(item, bytes);
			}
		}
	}

	internal static void ValidatePreparedLuaFiles(IReadOnlyList<string> preparedFiles, int? steamAppId)
	{
		if (!steamAppId.HasValue || steamAppId.GetValueOrDefault() <= 0)
		{
			throw new InvalidOperationException("Game chưa có Steam App ID hợp lệ; launcher không thể giới hạn CloudRedirect cho game này.");
		}
		string[] array = preparedFiles.Where(IsLuaFilePath).Where(File.Exists).ToArray();
		if (array.Length == 0)
		{
			throw new InvalidDataException("Package active không có file Lua.");
		}
		Regex appRegistration = new Regex($"\\baddappid\\s*\\(\\s*{steamAppId.Value}\\s*(?:,|\\))", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
		if (!array.Any((string file) => File.ReadLines(file).Any((string line) => !line.TrimStart().StartsWith("--", StringComparison.Ordinal) && appRegistration.IsMatch(line))))
		{
			throw new InvalidDataException($"Lua active không đăng ký Steam App ID {steamAppId.Value}; đã dừng cài đặt để tránh redirect nhầm save.");
		}
	}

	private static byte[] RewriteLuaBranding(byte[] content)
	{
		string @string = Encoding.UTF8.GetString(content);
		string separator = (@string.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n");
		string[] array = @string.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
		if (array.Length == 0)
		{
			return content;
		}
		array[0] = "-- Lua and Manifest Created By NestG";
		for (int i = 0; i < array.Length; i++)
		{
			if (array[i].TrimStart().StartsWith("-- Website:", StringComparison.OrdinalIgnoreCase))
			{
				array[i] = "-- Website: https://nestg.cloud";
				break;
			}
		}
		return Encoding.UTF8.GetBytes(string.Join(separator, array));
	}

	private static bool IsLuaFilePath(string filePath)
	{
		return string.Equals(Path.GetExtension(filePath), ".lua", StringComparison.OrdinalIgnoreCase);
	}

	private static IReadOnlyList<CloudRedirectActivationFile> BuildCloudRedirectActivationFiles(IEnumerable<string> localFiles)
	{
		string[] source = localFiles.ToArray();
		return (from file in source.Where(IsLuaFilePath)
			select new CloudRedirectActivationFile(file, "config/lua/" + Path.GetFileName(file))).Concat(from file in source
			where string.Equals(Path.GetExtension(file), ".manifest", StringComparison.OrdinalIgnoreCase)
			select new CloudRedirectActivationFile(file, "depotcache/" + Path.GetFileName(file))).ToArray();
	}

	internal static SteamInstallResult PendingInstallation(string steamDirectory, string message)
	{
		return new SteamInstallResult(steamDirectory, IsFound: true, Array.Empty<string>(), new _003C_003Ez__ReadOnlySingleElementList<string>(message), IsSteamRestarted: false)
		{
			VerificationPending = true,
			RecoveryMessage = message
		};
	}

	internal static void RecordCommittedInstallation(SteamRecoveryStore store, SteamRecoveryOperation operation, CloudRedirectRuntimeInstallResult result)
	{
		operation.TransactionPath = result.TransactionPath;
		operation.RequiresSteamRestart = operation.BetterOnly || result.RequiresSteamRestart;
		operation.Phase = SteamRecoveryPhase.FilesCommitted;
		operation.Outcome = SteamRecoveryOutcome.None;
		store.Save(operation);
	}

	private static bool StartSteam(string steamDirectory, ICollection<string> messages, Action<Process>? trackLaunch = null)
	{
		string text = Path.Combine(steamDirectory, "steam.exe");
		if (!File.Exists(text))
		{
			messages.Add("Đã copy file vào Steam, nhưng không tìm thấy steam.exe để khởi động lại tự động.");
			return false;
		}
		try
		{
			Process process = Process.Start(new ProcessStartInfo
			{
				FileName = text,
				WorkingDirectory = steamDirectory,
				UseShellExecute = true
			});
			if (process != null)
			{
				if (trackLaunch == null)
				{
					process.Dispose();
				}
				else
				{
					try
					{
						trackLaunch(process);
					}
					catch
					{
						process.Dispose();
					}
				}
			}
			messages.Add("Đã gửi lệnh mở Steam; cần kiểm tra tiến trình và Cloud Save trước khi xác nhận thành công.");
			return true;
		}
		catch (Exception ex)
		{
			messages.Add("Đã copy file vào Steam, nhưng không thể khởi động lại Steam tự động: " + ex.Message);
			return false;
		}
	}

	private static IReadOnlyList<string> CopyFilesToDirectory(IEnumerable<string> files, string destinationDirectory, ICollection<string> messages, bool atomic = false)
	{
		List<string> list = new List<string>();
		try
		{
			Directory.CreateDirectory(destinationDirectory);
		}
		catch (Exception ex)
		{
			messages.Add("Không tạo được thư mục " + destinationDirectory + ": " + ex.Message);
			return list;
		}
		foreach (string file in files)
		{
			if (!File.Exists(file))
			{
				continue;
			}
			string text = Path.Combine(destinationDirectory, Path.GetFileName(file));
			try
			{
				if (atomic)
				{
					AtomicCopy(file, text);
				}
				else
				{
					File.Copy(file, text, overwrite: true);
				}
				list.Add(text);
			}
			catch (Exception ex2)
			{
				messages.Add($"Không copy được {file} -> {text}: {ex2.Message}");
			}
		}
		return list;
	}

	private static IReadOnlyList<string> CopyFilesPreservingRelativePaths(IReadOnlyList<string> sourceFiles, string sourceDirectory, string destinationDirectory, ICollection<string> messages)
	{
		string fullPath = Path.GetFullPath(sourceDirectory);
		string fullPath2 = Path.GetFullPath(destinationDirectory);
		List<string> list = new List<string>();
		foreach (string sourceFile in sourceFiles)
		{
			string relativePath = Path.GetRelativePath(fullPath, sourceFile);
			string fullPath3 = Path.GetFullPath(Path.Combine(fullPath2, relativePath));
			if (!fullPath3.StartsWith(fullPath2 + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
			{
				throw new InvalidOperationException("Offset OpenSteamTool có đường dẫn không hợp lệ: " + relativePath);
			}
			try
			{
				AtomicCopy(sourceFile, fullPath3);
				list.Add(fullPath3);
			}
			catch (Exception ex)
			{
				throw new InvalidOperationException("Không copy được offset OpenSteamTool " + relativePath + ": " + ex.Message, ex);
			}
		}
		messages.Add($"Đã copy {list.Count} file offsets OpenSteamTool.");
		return list;
	}

	private static void AtomicCopy(string sourcePath, string destinationPath)
	{
		WriteFlushAndPublish(destinationPath, delegate(FileStream destinationStream)
		{
			using FileStream fileStream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.SequentialScan);
			fileStream.CopyTo(destinationStream);
		});
	}

	private static void AtomicWriteAllText(string destinationPath, string content)
	{
		byte[] bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(content);
		WriteFlushAndPublish(destinationPath, delegate(FileStream destinationStream)
		{
			destinationStream.Write(bytes);
		});
	}

	private static void WriteFlushAndPublish(string destinationPath, Action<FileStream> writer)
	{
		Directory.CreateDirectory(Path.GetDirectoryName(destinationPath));
		string text = destinationPath + ".tmp";
		try
		{
			using (FileStream fileStream = new FileStream(text, FileMode.Create, FileAccess.Write, FileShare.None, 81920, FileOptions.None))
			{
				writer(fileStream);
				fileStream.Flush(flushToDisk: true);
			}
			File.Move(text, destinationPath, overwrite: true);
		}
		catch
		{
			try
			{
				File.Delete(text);
			}
			catch
			{
			}
			throw;
		}
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
