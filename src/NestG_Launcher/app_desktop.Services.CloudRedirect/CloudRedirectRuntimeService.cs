using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Text.RegularExpressions.Generated;
using System.Threading;
using System.Threading.Tasks;
using UI_Desktop.Store;

namespace app_desktop.Services.CloudRedirect;

internal sealed class CloudRedirectRuntimeService
{
	private sealed record ActivationCopy(string Source, string RelativePath, string Hash);

	private readonly CloudRedirectRuntimeProfile targetProfile;

	private readonly StorefrontApiClient storefrontApiClient;

	private readonly string currentDesktopVersion;

	private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
	{
		PropertyNameCaseInsensitive = true,
		WriteIndented = true
	};

	private const string SteamRecoveryOperation = "steam_recovery";

	private static readonly string[] SteamLoaderNames = new string[2] { "dwmapi.dll", "xinput1_4.dll" };

	private string ExpectedBundleId => targetProfile.BundleId;

	private string SupportedBundleVersion => targetProfile.Version;

	private string SupportedOpenSteamToolCommit => targetProfile.Commit;

	private string SupportedCloudRedirectVersion => targetProfile.CloudRedirectVersion;

	private IReadOnlyDictionary<string, string> TrustedRuntimeFiles => targetProfile.Hashes;

	internal static bool IsActivationFilePath(string path)
	{
		if (string.IsNullOrWhiteSpace(path))
		{
			return false;
		}
		string[] array = path.Split('/');
		if (!path.Contains('\\') && !array.Any((string part) => (((part != null && part.Length == 0) || part == "." || part == "..") ? true : false) || part.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0))
		{
			if (array.Length != 3 || !(array[0] == "config") || !(array[1] == "lua") || !path.EndsWith(".lua", StringComparison.OrdinalIgnoreCase))
			{
				if (array.Length == 2 && array[0] == "depotcache")
				{
					return path.EndsWith(".manifest", StringComparison.OrdinalIgnoreCase);
				}
				return false;
			}
			return true;
		}
		return false;
	}

	internal static IReadOnlyList<SteamActivationFileStamp> CaptureActivationFiles(IReadOnlyList<CloudRedirectActivationFile> files)
	{
		return files.Select(delegate(CloudRedirectActivationFile file)
		{
			if (!IsActivationFilePath(file.InstallPath))
			{
				throw new InvalidDataException("Invalid activation file path.");
			}
			using FileStream source = File.OpenRead(file.SourcePath);
			return new SteamActivationFileStamp(file.InstallPath, Convert.ToHexString(SHA256.HashData(source)));
		}).ToArray();
	}

	internal void VerifyBetterActivation(string steamDirectory, int steamAppId, IReadOnlyList<SteamActivationFileStamp> files, bool requireCloudDisabled)
	{
		targetProfile.ValidateSteamBuild(steamDirectory);
		VerifyInstalledRuntime(steamDirectory, targetProfile, betterOnly: true);
		string path = ResolveContainedPath(steamDirectory, "opensteamtool.toml");
		if (requireCloudDisabled && !BetterSteamToolsConfiguration.IsReady(ReadTextIfExists(path), steamDirectory, cloudEnabled: false))
		{
			throw new InvalidDataException("Cấu hình BetterSteamTools chưa sẵn sàng.");
		}
		VerifyActivationFiles(steamDirectory, steamAppId, files);
	}

	internal CloudRedirectRuntimeOperationResult DisableCloudForActivation(string steamDirectory, string recoveryId, string? localAppDataOverride = null)
	{
		if (!Guid.TryParseExact(recoveryId, "N", out var _))
		{
			throw new InvalidDataException("Invalid fallback ID.");
		}
		string fullPath = Path.GetFullPath(localAppDataOverride ?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
		string fullPath2 = Path.GetFullPath(steamDirectory);
		string text = ResolveContainedPath(fullPath2, "opensteamtool.toml");
		EnsureRecoveryPathIsNotLinked(text);
		string text2 = Path.Combine(fullPath, "NestGLauncher", "CloudRedirect", "Backups", "better-fallback-" + recoveryId);
		EnsureRecoveryPathIsNotLinked(text2);
		string text3 = Path.Combine(text2, "transaction.json");
		CloudRedirectInstallTransaction transaction = new CloudRedirectInstallTransaction
		{
			SchemaVersion = 2,
			TransactionId = Guid.NewGuid().ToString("N"),
			LocalAppDataDirectory = fullPath,
			RoamingAppDataDirectory = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
			Operation = "better_fallback",
			BundleId = targetProfile.BundleId,
			BundleVersion = targetProfile.Version,
			SteamDirectory = fullPath2,
			CreatedAtUtc = DateTime.UtcNow,
			Status = "installing"
		};
		if (CloudRedirectTransactionJournal.Exists(text3))
		{
			bool flag = !TryLoadTransaction(text3, out transaction) || transaction.Operation != "better_fallback" || !PathsEqual(transaction.SteamDirectory, fullPath2) || transaction.BundleId != targetProfile.BundleId || transaction.BundleVersion != targetProfile.Version || transaction.Entries.Count > 1 || (transaction.Entries.Count == 1 && (!PathsEqual(transaction.Entries[0].OriginalPath, text) || transaction.Entries[0].PostImageDeleted));
			if (!flag)
			{
				string status = transaction.Status;
				bool flag2 = ((status == "installing" || status == "completed") ? true : false);
				flag = !flag2;
			}
			if (flag)
			{
				throw new InvalidDataException("Fallback transaction cannot be resumed.");
			}
			ValidateRollbackTransaction(text3, transaction, fullPath2, fullPath, Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));
			if (FindRollbackDriftErrors(transaction).Count > 0)
			{
				throw new InvalidDataException("Better config changed outside activation; fallback stopped.");
			}
			if (transaction.Status == "completed")
			{
				if (transaction.Entries.Count != 1 || !TryComputeSha256(text, out string hash) || hash != transaction.Entries[0].PostImageSha256)
				{
					throw new InvalidDataException("Better config changed after fallback.");
				}
				return new CloudRedirectRuntimeOperationResult(Succeeded: true, "Better configuration verified.", text2, text3);
			}
		}
		if (!CloudRedirectTransactionJournal.Exists(text3))
		{
			WriteTransaction(text3, transaction);
		}
		try
		{
			string content = BetterSteamToolsConfiguration.Update(ReadTextIfExists(text), fullPath2, cloudEnabled: false);
			Directory.CreateDirectory(text2);
			if (transaction.Entries.Count == 0)
			{
				SnapshotFile(text, Path.Combine(text2, Guid.NewGuid().ToString("N")), transaction, text3);
			}
			RecordExpectedPostImage(text, ComputeTextSha256(content), transaction, text3);
			VerifyBeforeMutation(transaction, text3, text);
			AtomicWriteAllText(text, content);
			RecordPostImage(text, transaction, text3);
			transaction.Status = "completed";
			transaction.CompletedAtUtc = DateTime.UtcNow;
			WriteTransaction(text3, transaction);
			return new CloudRedirectRuntimeOperationResult(Succeeded: true, "Better configuration applied.", text2, text3);
		}
		catch
		{
			IReadOnlyList<string> readOnlyList = Rollback(transaction, text3);
			transaction.Status = ((readOnlyList.Count == 0) ? "rolled_back" : "rollback_failed");
			transaction.RollbackErrors = readOnlyList.ToList();
			WriteTransactionBestEffort(text3, transaction);
			throw;
		}
	}

	internal CloudRedirectRuntimeInstallResult InstallForActivation(PreparedCloudRedirectRuntime runtime, string steamDirectory, DesktopLibraryDetailResponse detail, IReadOnlyList<CloudRedirectActivationFile> activationFiles, CancellationToken token, string? localAppDataOverride = null, string? roamingAppDataOverride = null, Func<SteamProcessSnapshot>? capture = null, Action<string>? validateSteamBuild = null)
	{
		string steamRoot = Path.GetFullPath(steamDirectory);
		CloudRedirectRuntimeProfile cloudRedirectRuntimeProfile = CloudRedirectRuntimeProfile.Resolve(runtime.BundleId, runtime.Version);
		if (cloudRedirectRuntimeProfile != targetProfile || !cloudRedirectRuntimeProfile.IsBetterSteamTools || runtime.Files.Count == 0)
		{
			throw new InvalidDataException("ACTIVE yêu cầu bundle BetterSteamTools hiện hành đã xác minh.");
		}
		GuardActivationPath(Path.Combine(steamRoot, "steam.exe"));
		List<string> written;
		bool configChanged;
		if (File.Exists(Path.Combine(steamRoot, "steam.exe")))
		{
			int? steamAppId = detail.SteamAppId;
			if (steamAppId.HasValue && steamAppId.GetValueOrDefault() > 0)
			{
				token.ThrowIfCancellationRequested();
				(validateSteamBuild ?? new Action<string>(cloudRedirectRuntimeProfile.ValidateSteamBuild))(steamRoot);
				ValidatePreparedRuntime(runtime);
				ValidateMainDllOwnership(steamRoot, cloudRedirectRuntimeProfile);
				IReadOnlyList<SteamActivationFileStamp> stamps = CaptureActivationFiles(activationFiles);
				if (!stamps.Any((SteamActivationFileStamp file) => file.InstallPath.EndsWith(".lua", StringComparison.OrdinalIgnoreCase)) || !stamps.Any((SteamActivationFileStamp file) => file.InstallPath.EndsWith(".manifest", StringComparison.OrdinalIgnoreCase)))
				{
					throw new InvalidDataException("Package ACTIVE phải có Lua và manifest.");
				}
				ActivationCopy[] array = runtime.Files.Select((PreparedCloudRedirectRuntimeFile file) => new ActivationCopy(file.SourcePath, file.InstallPath, file.Sha256)).Concat(activationFiles.Select((CloudRedirectActivationFile file, int index) => new ActivationCopy(file.SourcePath, file.InstallPath, stamps[index].Sha256))).ToArray();
				EnsureUniqueInstallPaths(array.Select((ActivationCopy file) => new CloudRedirectInstallItem(file.Source, file.RelativePath)));
				ActivationCopy[] array2 = array;
				foreach (ActivationCopy activationCopy in array2)
				{
					if (!cloudRedirectRuntimeProfile.RequiredFiles(runtime.BetterOnly).ContainsKey(activationCopy.RelativePath) && !IsActivationFilePath(activationCopy.RelativePath))
					{
						throw new InvalidDataException("File không thuộc phạm vi ACTIVE.");
					}
					GuardActivationPath(activationCopy.Source);
					GuardActivationPath(ResolveContainedPath(steamRoot, activationCopy.RelativePath));
				}
				string fullPath = Path.GetFullPath(localAppDataOverride ?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
				string fullPath2 = Path.GetFullPath(roamingAppDataOverride ?? Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));
				string path2 = ResolveContainedPath(steamRoot, "opensteamtool.toml");
				GuardActivationPath(path2);
				string before2 = ActivationFileHash(path2);
				string content2 = BetterSteamToolsConfiguration.Update(ReadTextIfExists(path2), steamRoot, !runtime.BetterOnly);
				written = new List<string>();
				List<string> list = new List<string>();
				bool runtimeChanged = false;
				bool activationChanged = false;
				configChanged = false;
				if (runtime.BetterOnly)
				{
					WriteText(path2, content2, before2);
				}
				array2 = array;
				foreach (ActivationCopy item in array2)
				{
					string text = ResolveContainedPath(steamRoot, item.RelativePath);
					string text2 = ActivationFileHash(text);
					if (string.Equals(text2, item.Hash, StringComparison.OrdinalIgnoreCase))
					{
						continue;
					}
					PublishActivationFile(text, text2, delegate(FileStream stream)
					{
						GuardActivationPath(item.Source);
						using FileStream fileStream = new FileStream(item.Source, FileMode.Open, FileAccess.Read, FileShare.Read);
						if (!Convert.ToHexString(SHA256.HashData(fileStream)).Equals(item.Hash, StringComparison.OrdinalIgnoreCase))
						{
							throw new InvalidDataException("File nguồn đã thay đổi sau khi xác minh.");
						}
						fileStream.Position = 0L;
						byte[] array3 = new byte[81920];
						int count;
						while ((count = fileStream.Read(array3)) > 0)
						{
							token.ThrowIfCancellationRequested();
							stream.Write(array3, 0, count);
						}
					}, token, delegate
					{
						CaptureStoppedSteam(steamRoot, capture);
					});
					written.Add(text);
					if (IsActivationFilePath(item.RelativePath))
					{
						activationChanged = true;
					}
					else
					{
						runtimeChanged = true;
					}
				}
				foreach (string retiredMainDllName in cloudRedirectRuntimeProfile.RetiredMainDllNames)
				{
					string text3 = ResolveContainedPath(steamRoot, retiredMainDllName);
					GuardActivationPath(text3);
					string text4 = ActivationFileHash(text3);
					if (text4 != null)
					{
						if (!CloudRedirectRuntimeProfile.IsKnownFileHash(retiredMainDllName, text4))
						{
							throw new InvalidDataException("DLL cũ chưa được NestG xác minh.");
						}
						token.ThrowIfCancellationRequested();
						CaptureStoppedSteam(steamRoot, capture);
						GuardActivationPath(text3);
						if (ActivationFileHash(text3) != text4)
						{
							throw new IOException("DLL cũ đã thay đổi trước khi chuyển đổi.");
						}
						File.Delete(text3);
						written.Add(text3);
						runtimeChanged = true;
					}
				}
				if (!runtime.BetterOnly)
				{
					WriteText(path2, content2, before2);
				}
				string text5 = "";
				string text6 = "";
				if (!runtime.BetterOnly)
				{
					string path3 = ResolveContainedPath(steamRoot, "cloud_redirect/config.json");
					GuardActivationPath(path3);
					string before3 = ActivationFileHash(path3);
					string content3 = CloudRedirectConfiguration.UpdateSteamConfig(ReadTextIfExists(path3));
					WriteText(path3, content3, before3);
					string path4 = Path.Combine(fullPath2, "CloudRedirect", "config.json");
					GuardActivationPath(path4);
					string before4 = ActivationFileHash(path4);
					string content4 = CloudRedirectConfiguration.UpdateUserConfig(ReadTextIfExists(path4), Path.Combine(fullPath, "NestGLauncher", "CloudSaves"));
					CloudRedirectProviderStatus cloudRedirectProviderStatus = CloudRedirectConfiguration.InspectProvider(content4);
					if (!cloudRedirectProviderStatus.IsReady)
					{
						throw new InvalidOperationException(cloudRedirectProviderStatus.Message);
					}
					text5 = CloudRedirectConfiguration.ResolveProviderName(content4);
					text6 = CloudRedirectConfiguration.ResolveSyncPath(content4);
					if (string.Equals(text5, "folder", StringComparison.OrdinalIgnoreCase))
					{
						CreateDirectory(text6);
					}
					CreateDirectory(ResolveContainedPath(steamRoot, "cloud_redirect/storage"));
					WriteText(path4, content4, before4);
				}
				string text7 = Path.Combine(fullPath, "NestGLauncher", "CloudRedirect", "state.json");
				GuardActivationPath(text7);
				string before5 = ActivationFileHash(text7);
				bool changed;
				string content5 = BuildLocalStateContent(text7, runtime, steamRoot, text6, detail, activationFiles, written.Count > 0, out changed);
				if (changed)
				{
					WriteText(text7, content5, before5);
				}
				VerifyActivationAttempt(steamRoot, detail.SteamAppId.Value, stamps, runtime.BetterOnly, validateSteamBuild);
				token.ThrowIfCancellationRequested();
				list.Add(runtime.BetterOnly ? "Đã cài Better + Lua + Manifest; cloud host đã tắt." : "Đã cài Better + Lua + Manifest + CloudRedirect.");
				return new CloudRedirectRuntimeInstallResult(written, list, "", text6, text5, "")
				{
					RuntimeChanged = runtimeChanged,
					ConfigurationChanged = configChanged,
					ActivationChanged = activationChanged,
					RequiresSteamRestart = true
				};
			}
		}
		throw new InvalidDataException("Steam hoặc Steam App ID không hợp lệ.");
		void CreateDirectory(string path)
		{
			token.ThrowIfCancellationRequested();
			GuardActivationPath(path);
			CaptureStoppedSteam(steamRoot, capture);
			Directory.CreateDirectory(path);
		}
		void WriteText(string path, string content, string? before)
		{
			if (!(before == ComputeTextSha256(content)))
			{
				byte[] bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(content);
				PublishActivationFile(path, before, delegate(FileStream stream)
				{
					stream.Write(bytes);
				}, token, delegate
				{
					CaptureStoppedSteam(steamRoot, capture);
				});
				written.Add(path);
				configChanged = true;
			}
		}
	}

	internal void VerifyActivationAttempt(string steamDirectory, int steamAppId, IReadOnlyList<SteamActivationFileStamp> files, bool betterOnly, Action<string>? validateSteamBuild = null)
	{
		GuardActivationPath(Path.Combine(steamDirectory, "steam.exe"));
		(validateSteamBuild ?? new Action<string>(targetProfile.ValidateSteamBuild))(steamDirectory);
		foreach (string item in targetProfile.RequiredFiles(betterOnly).Keys.Concat(files.Select((SteamActivationFileStamp file) => file.InstallPath)))
		{
			GuardActivationPath(ResolveContainedPath(steamDirectory, item));
		}
		VerifyInstalledRuntime(steamDirectory, targetProfile, betterOnly);
		VerifyActivationFiles(steamDirectory, steamAppId, files);
		string path = ResolveContainedPath(steamDirectory, "opensteamtool.toml");
		GuardActivationPath(path);
		if (!BetterSteamToolsConfiguration.IsReady(ReadTextIfExists(path), steamDirectory, !betterOnly))
		{
			throw new InvalidDataException("Cấu hình BetterSteamTools chưa sẵn sàng cho lượt ACTIVE.");
		}
	}

	private static void GuardActivationPath(string path)
	{
		string fullPath = Path.GetFullPath(path);
		if (fullPath.Substring(Path.GetPathRoot(fullPath).Length).Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Any((string part) => part.EndsWith('.') || part.EndsWith(' ') || part.Contains(':') || part.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || IsWindowsDeviceName(part)))
		{
			throw new InvalidDataException("Đường dẫn ACTIVE nhập nhằng hoặc không hợp lệ.");
		}
		EnsureRecoveryPathIsNotLinked(fullPath);
	}

	private static bool IsWindowsDeviceName(string part)
	{
		string text = part.Split('.')[0].ToUpperInvariant();
		bool flag;
		switch (text)
		{
		case "CON":
		case "PRN":
		case "AUX":
		case "NUL":
		case "CONIN$":
		case "CONOUT$":
			flag = true;
			break;
		default:
			flag = false;
			break;
		}
		if (!flag)
		{
			if (text.Length == 4 && (text.StartsWith("COM", StringComparison.Ordinal) || text.StartsWith("LPT", StringComparison.Ordinal)))
			{
				char c = text[3];
				if (c >= '1')
				{
					return c <= '9';
				}
				return false;
			}
			return false;
		}
		return true;
	}

	private static string? ActivationFileHash(string path)
	{
		GuardActivationPath(path);
		if (Directory.Exists(path))
		{
			throw new InvalidDataException("Đường dẫn file ACTIVE đang là thư mục.");
		}
		if (!File.Exists(path))
		{
			return null;
		}
		using FileStream source = File.OpenRead(path);
		return Convert.ToHexString(SHA256.HashData(source));
	}

	private static void CaptureStoppedSteam(string steamDirectory, Func<SteamProcessSnapshot>? capture)
	{
		if ((capture?.Invoke() ?? SteamProcessInspector.Capture(steamDirectory)).State != 0)
		{
			throw new SteamRecoveryPendingException("Steam chưa đóng hoàn toàn; launcher dừng ghi file ACTIVE.");
		}
	}

	private static void PublishActivationFile(string destination, string? before, Action<FileStream> write, CancellationToken token, Action requireStoppedSteam)
	{
		token.ThrowIfCancellationRequested();
		GuardActivationPath(destination);
		requireStoppedSteam();
		Directory.CreateDirectory(Path.GetDirectoryName(destination));
		GuardActivationPath(destination);
		string text = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
		try
		{
			using (FileStream fileStream = new FileStream(text, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.WriteThrough))
			{
				write(fileStream);
				fileStream.Flush(flushToDisk: true);
			}
			token.ThrowIfCancellationRequested();
			GuardActivationPath(destination);
			requireStoppedSteam();
			if (ActivationFileHash(destination) != before)
			{
				throw new IOException("File đã thay đổi bên ngoài lượt ACTIVE; launcher giữ nguyên thay đổi hiện tại.");
			}
			token.ThrowIfCancellationRequested();
			File.Move(text, destination, overwrite: true);
		}
		finally
		{
			try
			{
				File.Delete(text);
			}
			catch
			{
			}
		}
	}

	internal CloudRedirectRuntimeService(StorefrontApiClient storefrontApiClient, string? currentDesktopVersion = null, CloudRedirectRuntimeProfile? targetProfile = null)
	{
		this.storefrontApiClient = storefrontApiClient;
		this.targetProfile = targetProfile ?? CloudRedirectRuntimeProfile.Better;
		this.currentDesktopVersion = (string.IsNullOrWhiteSpace(currentDesktopVersion) ? DesktopAppContext.Settings.CurrentVersion : currentDesktopVersion.Trim());
	}

	internal async Task<PreparedCloudRedirectRuntime> PrepareAsync(string accessToken, string destinationDirectory, CancellationToken cancellationToken, IProgress<GameActivationProgress>? progress = null, bool betterOnly = false)
	{
		progress?.Report(new GameActivationProgress(GameActivationProgressStage.PreparingCloudRedirect, "Đang lấy thông tin thành phần kích hoạt...", 55));
		DesktopDiagnosticLog.Write("CloudRedirectRuntime", "MetadataRequested", "Bundle=" + ExpectedBundleId);
		DesktopRuntimeBundleResponse bundle;
		try
		{
			bundle = await storefrontApiClient.GetCloudRuntimeAsync(targetProfile.BundleId, accessToken, cancellationToken, targetProfile.IsBetterSteamTools ? targetProfile.Version : null, betterOnly);
		}
		catch (Exception ex) when (((Func<bool>)delegate
		{
			// Could not convert BlockContainer to single expression
			bool flag = !betterOnly && targetProfile.IsBetterSteamTools;
			if (flag)
			{
				flag = ((ex is RuntimeBundleUnavailableException || ex is TimeoutException || ex is HttpRequestException) ? true : false);
			}
			return flag;
		}).Invoke())
		{
			throw new CloudRedirectOptionalException("bundle-metadata", ex);
		}
		targetProfile.Validate(bundle, currentDesktopVersion, betterOnly);
		string bundleDirectory = Path.Combine(destinationDirectory, bundle.Version);
		Directory.CreateDirectory(bundleDirectory);
		List<PreparedCloudRedirectRuntimeFile> preparedFiles = new List<PreparedCloudRedirectRuntimeFile>(bundle.Files.Count);
		long totalBytes = bundle.Files.Sum((DesktopRuntimeBundleFileResponse file) => file.Size);
		long completedBytes = 0L;
		int fileIndex = 0;
		bool flag2 = default(bool);
		foreach (DesktopRuntimeBundleFileResponse descriptor in bundle.Files)
		{
			int num = fileIndex;
			fileIndex = num + 1;
			cancellationToken.ThrowIfCancellationRequested();
			string relativePath = NormalizeRelativePath(descriptor.RelativePath, "relativePath");
			string installPath = NormalizeRelativePath(descriptor.InstallPath, "installPath");
			string fileDirectory = ResolveContainedPath(bundleDirectory, Path.GetDirectoryName(relativePath.Replace('/', Path.DirectorySeparatorChar)) ?? string.Empty);
			Directory.CreateDirectory(fileDirectory);
			string expectedFileName = Path.GetFileName(relativePath);
			DesktopDiagnosticLog.Write("CloudRedirectRuntime", "DownloadStarted", $"File={relativePath}; Size={descriptor.Size}; Index={fileIndex}/{bundle.Files.Count}");
			InlineProgress<DesktopFileDownloadProgress> progress2 = new InlineProgress<DesktopFileDownloadProgress>(delegate(DesktopFileDownloadProgress update)
			{
				long num2 = completedBytes + Math.Clamp(update.BytesReceived, 0L, descriptor.Size);
				progress?.Report(new GameActivationProgress(GameActivationProgressStage.PreparingCloudRedirect, $"Đang tải thành phần kích hoạt: {(double)num2 / 1048576.0:0.0}/{(double)totalBytes / 1048576.0:0.0} MB (tệp {fileIndex}/{bundle.Files.Count})...", 55 + (int)(15.0 * (double)num2 / (double)Math.Max(1L, totalBytes))));
			});
			try
			{
				DesktopDownloadedFileResponse desktopDownloadedFileResponse = await storefrontApiClient.DownloadRuntimeBundleFileAsync(descriptor.DownloadUrl, accessToken, fileDirectory, expectedFileName, cancellationToken, progress2);
				string normalizedDownloadedPath = Path.Combine(fileDirectory, expectedFileName);
				if (!string.Equals(Path.GetFullPath(desktopDownloadedFileResponse.FullPath), Path.GetFullPath(normalizedDownloadedPath), StringComparison.OrdinalIgnoreCase))
				{
					File.Copy(desktopDownloadedFileResponse.FullPath, normalizedDownloadedPath, overwrite: true);
				}
				progress?.Report(new GameActivationProgress(GameActivationProgressStage.PreparingCloudRedirect, $"Đang kiểm tra tệp kích hoạt {fileIndex}/{bundle.Files.Count}...", 55 + (int)(15.0 * (double)(completedBytes + descriptor.Size) / (double)Math.Max(1L, totalBytes))));
				await VerifyFileAsync(normalizedDownloadedPath, descriptor.Size, descriptor.Sha256, cancellationToken);
				if (string.Equals(Path.GetExtension(installPath), ".dll", StringComparison.OrdinalIgnoreCase))
				{
					ValidatePeArchitecture(normalizedDownloadedPath, 34404);
				}
				preparedFiles.Add(new PreparedCloudRedirectRuntimeFile(normalizedDownloadedPath, relativePath, installPath, descriptor.Size, descriptor.Sha256.ToUpperInvariant()));
				completedBytes += descriptor.Size;
				DesktopDiagnosticLog.Write("CloudRedirectRuntime", "FileVerified", "File=" + relativePath);
			}
			catch (Exception ex2) when (((Func<bool>)delegate
			{
				// Could not convert BlockContainer to single expression
				flag2 = !betterOnly && targetProfile.IsBetterSteamTools && CloudRedirectRuntimeProfile.IsCloudFile(installPath);
				if (flag2)
				{
					flag2 = ((ex2 is IOException || ex2 is InvalidDataException || ex2 is TimeoutException || ex2 is HttpRequestException || ex2 is RuntimeBundleUnavailableException) ? true : false);
				}
				return flag2;
			}).Invoke())
			{
				throw new CloudRedirectOptionalException("cloud-file", ex2);
			}
		}
		return new PreparedCloudRedirectRuntime(bundle.BundleId, bundle.Version, bundle.OpenSteamToolCommit, bundle.CloudRedirectVersion, preparedFiles)
		{
			BetterOnly = betterOnly
		};
	}

	internal static bool NeedsPreparedRuntime(CloudRedirectReadinessResult readiness)
	{
		switch (readiness.RecommendedAction)
		{
		case CloudRedirectReadinessAction.Install:
		case CloudRedirectReadinessAction.Repair:
		case CloudRedirectReadinessAction.Recover:
		case CloudRedirectReadinessAction.Upgrade:
			return true;
		default:
			return false;
		}
	}

	internal void ValidateSteamBuild(string steamDirectory)
	{
		targetProfile.ValidateSteamBuild(steamDirectory);
	}

	internal async Task<PreparedCloudRedirectEnsureOperation> PrepareEnsureReadyAsync(string accessToken, string destinationDirectory, string? steamDirectory, DesktopLibraryDetailResponse detail, CancellationToken cancellationToken, IProgress<GameActivationProgress>? progress = null, bool betterOnly = false)
	{
		if (string.IsNullOrWhiteSpace(accessToken))
		{
			throw new InvalidOperationException("Bạn cần đăng nhập để active game.");
		}
		progress?.Report(new GameActivationProgress(GameActivationProgressStage.PreparingCloudRedirect, "Đang kiểm tra thành phần kích hoạt đã cài...", 55));
		CloudRedirectReadinessResult readiness = await Task.Run(() => InspectReadiness(steamDirectory, detail.SteamAppId, null, null, betterOnly), cancellationToken);
		if (readiness.RecommendedAction == CloudRedirectReadinessAction.Blocked)
		{
			throw new CloudRedirectReadinessException(readiness);
		}
		await Task.Run(delegate
		{
			targetProfile.ValidateSteamBuild(readiness.SteamDirectory);
		}, cancellationToken);
		PreparedCloudRedirectRuntime preparedCloudRedirectRuntime = ((!NeedsPreparedRuntime(readiness)) ? null : (await PrepareAsync(accessToken, destinationDirectory, cancellationToken, progress, betterOnly)));
		PreparedCloudRedirectRuntime runtime = preparedCloudRedirectRuntime;
		return new PreparedCloudRedirectEnsureOperation(readiness, runtime)
		{
			BetterOnly = betterOnly
		};
	}

	internal static bool RequiresSteamRestart(PreparedCloudRedirectEnsureOperation operation, IReadOnlyList<CloudRedirectActivationFile> activationFiles)
	{
		if (operation.Readiness.RecommendedAction != 0)
		{
			return true;
		}
		return activationFiles.Any(delegate(CloudRedirectActivationFile file)
		{
			string destinationPath = ResolveContainedPath(operation.Readiness.SteamDirectory, NormalizeRelativePath(file.InstallPath, "activation installPath"));
			return !FilesHaveSameContent(file.SourcePath, destinationPath);
		});
	}

	internal CloudRedirectRuntimeInstallResult EnsureReady(PreparedCloudRedirectRuntime? preparedRuntime, string steamDirectory, DesktopLibraryDetailResponse detail, IReadOnlyList<CloudRedirectActivationFile> activationFiles, string? localAppDataOverride = null, string? roamingAppDataOverride = null, bool steamWritesAuthorized = true, Action<string>? transactionPrepared = null, bool betterOnly = false)
	{
		CloudRedirectReadinessResult readiness;
		CloudRedirectReadinessResult initialReadiness = (readiness = InspectReadiness(steamDirectory, detail.SteamAppId, localAppDataOverride, roamingAppDataOverride, betterOnly));
		bool recoveredInterruptedTransaction = false;
		if (readiness.RecommendedAction == CloudRedirectReadinessAction.Recover)
		{
			if (!steamWritesAuthorized)
			{
				throw new InvalidOperationException("Chưa thể ghi file Steam; cần kiểm tra và đóng Steam trước khi phục hồi transaction.");
			}
			RecoverIncompleteTransaction(readiness.IncompleteTransactionPath, readiness.SteamDirectory, localAppDataOverride, roamingAppDataOverride);
			recoveredInterruptedTransaction = true;
			readiness = InspectReadiness(steamDirectory, detail.SteamAppId, localAppDataOverride, roamingAppDataOverride, betterOnly);
		}
		if (readiness.RecommendedAction == CloudRedirectReadinessAction.Blocked)
		{
			throw new CloudRedirectReadinessException(readiness);
		}
		if (NeedsPreparedRuntime(readiness) && (object)preparedRuntime == null)
		{
			throw new InvalidOperationException("CloudRedirect cần runtime bundle đã verify để cài đặt hoặc repair.");
		}
		PreparedCloudRedirectRuntime preparedCloudRedirectRuntime = preparedRuntime ?? CreateKnownRuntimeMetadata()with
		{
			BetterOnly = betterOnly
		};
		if (preparedCloudRedirectRuntime.BetterOnly != betterOnly)
		{
			throw new InvalidDataException("Prepared runtime activation mode mismatch.");
		}
		if (preparedCloudRedirectRuntime.BundleId != targetProfile.BundleId || preparedCloudRedirectRuntime.Version != targetProfile.Version)
		{
			throw new InvalidDataException("Runtime đã chuẩn bị không đúng bundle đang dùng.");
		}
		PreparedCloudRedirectEnsureOperation operation = new PreparedCloudRedirectEnsureOperation(readiness, preparedRuntime);
		if (!steamWritesAuthorized && RequiresSteamRestart(operation, activationFiles))
		{
			throw new InvalidOperationException("Trạng thái Steam/CloudRedirect đã thay đổi; cần xác nhận khởi động lại Steam trước khi ghi file.");
		}
		CloudRedirectRuntimeInstallResult cloudRedirectRuntimeInstallResult = Install(preparedCloudRedirectRuntime, readiness.SteamDirectory, detail, activationFiles, localAppDataOverride, roamingAppDataOverride, readiness.RecommendedAction == CloudRedirectReadinessAction.Repair, (readiness.RecommendedAction == CloudRedirectReadinessAction.NoChange) ? "activate" : readiness.RecommendedAction.ToString().ToLowerInvariant(), transactionPrepared);
		CloudRedirectReadinessResult finalReadiness = InspectReadiness(steamDirectory, detail.SteamAppId, localAppDataOverride, roamingAppDataOverride, betterOnly);
		bool runtimeChanged = cloudRedirectRuntimeInstallResult.WrittenFiles.Any((string path) => TrustedRuntimeFiles.Keys.Concat(targetProfile.RetiredMainDllNames).Any((string installPath) => PathsEqual(path, ResolveContainedPath(readiness.SteamDirectory, installPath))));
		bool activationChanged = cloudRedirectRuntimeInstallResult.WrittenFiles.Any((string path) => activationFiles.Any((CloudRedirectActivationFile file) => PathsEqual(path, ResolveContainedPath(readiness.SteamDirectory, file.InstallPath))));
		bool configurationChanged = cloudRedirectRuntimeInstallResult.WrittenFiles.Any((string path) => PathsEqual(path, Path.Combine(readiness.SteamDirectory, "opensteamtool.toml")) || PathsEqual(path, Path.Combine(readiness.SteamDirectory, "cloud_redirect", "config.json")));
		return cloudRedirectRuntimeInstallResult with
		{
			InitialReadiness = initialReadiness,
			FinalReadiness = finalReadiness,
			AppliedAction = readiness.RecommendedAction,
			RecoveredInterruptedTransaction = recoveredInterruptedTransaction,
			RuntimeChanged = runtimeChanged,
			ConfigurationChanged = configurationChanged,
			ActivationChanged = activationChanged,
			RequiresSteamRestart = cloudRedirectRuntimeInstallResult.WrittenFiles.Any((string path) => IsPathWithin(path, readiness.SteamDirectory))
		};
	}

	internal CloudRedirectRuntimeInstallResult Install(PreparedCloudRedirectRuntime runtime, string steamDirectory, DesktopLibraryDetailResponse detail, IReadOnlyList<CloudRedirectActivationFile> activationFiles, string? localAppDataOverride = null, string? roamingAppDataOverride = null, bool repairInvalidLauncherConfigs = false, string operation = "install", Action<string>? transactionPrepared = null)
	{
		string fullPath = Path.GetFullPath(steamDirectory);
		if (!File.Exists(Path.Combine(fullPath, "steam.exe")))
		{
			throw new InvalidOperationException("Thư mục Steam không chứa steam.exe.");
		}
		int? steamAppId = detail.SteamAppId;
		if (!steamAppId.HasValue || steamAppId.GetValueOrDefault() <= 0)
		{
			throw new InvalidOperationException("Game chưa có Steam App ID hợp lệ để cấu hình CloudRedirect.");
		}
		ValidatePreparedRuntime(runtime);
		CloudRedirectRuntimeProfile cloudRedirectRuntimeProfile = CloudRedirectRuntimeProfile.Resolve(runtime.BundleId, runtime.Version);
		ValidateMainDllOwnership(fullPath, cloudRedirectRuntimeProfile);
		if (runtime.Files.Count == 0 && !cloudRedirectRuntimeProfile.IsInstalled(fullPath, runtime.BetterOnly))
		{
			throw new InvalidDataException("Bộ runtime chưa được cài đủ và xác thực; cần tải lại bundle.");
		}
		string text = localAppDataOverride ?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
		string text2 = roamingAppDataOverride ?? Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
		string path2 = Path.Combine(text, "NestGLauncher", "CloudRedirect");
		string syncPath = Path.Combine(text, "NestGLauncher", "CloudSaves");
		string text3 = Path.Combine(path2, "Backups", $"{DateTime.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}");
		Directory.CreateDirectory(text3);
		CloudRedirectInstallTransaction cloudRedirectInstallTransaction = new CloudRedirectInstallTransaction
		{
			SchemaVersion = 2,
			TransactionId = Guid.NewGuid().ToString("N"),
			LocalAppDataDirectory = Path.GetFullPath(text),
			RoamingAppDataDirectory = Path.GetFullPath(text2),
			ActivationFiles = CaptureActivationFiles(activationFiles).ToList(),
			BetterOnly = runtime.BetterOnly,
			Operation = operation,
			BundleId = runtime.BundleId,
			BundleVersion = runtime.Version,
			SteamDirectory = fullPath,
			CreatedAtUtc = DateTime.UtcNow,
			ActivationReceipt = (transactionPrepared != null),
			Status = "installing"
		};
		string text4 = Path.Combine(text3, "transaction.json");
		List<string> list = new List<string>();
		List<string> list2 = new List<string>();
		bool flag = false;
		WriteTransaction(text4, cloudRedirectInstallTransaction);
		transactionPrepared?.Invoke(text4);
		try
		{
			CloudRedirectInstallItem[] array = runtime.Files.Select((PreparedCloudRedirectRuntimeFile file) => new CloudRedirectInstallItem(file.SourcePath, file.InstallPath)).Concat(activationFiles.Select((CloudRedirectActivationFile file) => new CloudRedirectInstallItem(file.SourcePath, NormalizeRelativePath(file.InstallPath, "activation installPath")))).ToArray();
			EnsureUniqueInstallPaths(array);
			CloudRedirectInstallItem[] array2 = array;
			foreach (CloudRedirectInstallItem cloudRedirectInstallItem in array2)
			{
				flag = CloudRedirectRuntimeProfile.IsCloudFile(cloudRedirectInstallItem.InstallPath);
				string text5 = ResolveContainedPath(fullPath, cloudRedirectInstallItem.InstallPath);
				if (!FilesHaveSameContent(cloudRedirectInstallItem.SourcePath, text5))
				{
					SnapshotFile(text5, text3, cloudRedirectInstallTransaction, text4);
					if (!TryComputeSha256(cloudRedirectInstallItem.SourcePath, out string hash))
					{
						throw new IOException("Không đọc được file nguồn trước khi ghi: " + cloudRedirectInstallItem.SourcePath);
					}
					RecordExpectedPostImage(text5, hash, cloudRedirectInstallTransaction, text4);
					VerifyBeforeMutation(cloudRedirectInstallTransaction, text4, text5);
					AtomicCopy(cloudRedirectInstallItem.SourcePath, text5, hash);
					RecordPostImage(text5, cloudRedirectInstallTransaction, text4);
					list.Add(text5);
				}
			}
			flag = false;
			foreach (string retiredMainDllName in cloudRedirectRuntimeProfile.RetiredMainDllNames)
			{
				string text6 = ResolveContainedPath(fullPath, retiredMainDllName);
				if (File.Exists(text6))
				{
					ValidateMainDllOwnership(fullPath, cloudRedirectRuntimeProfile);
					SnapshotFile(text6, text3, cloudRedirectInstallTransaction, text4);
					CloudRedirectInstallTransactionEntry cloudRedirectInstallTransactionEntry = cloudRedirectInstallTransaction.Entries.Last();
					if (!CloudRedirectRuntimeProfile.IsKnownFileHash(retiredMainDllName, cloudRedirectInstallTransactionEntry.PreImageSha256))
					{
						throw new InvalidDataException(retiredMainDllName + " đã thay đổi trước khi chuyển đổi.");
					}
					cloudRedirectInstallTransactionEntry.PostImageDeleted = true;
					WriteTransaction(text4, cloudRedirectInstallTransaction);
					VerifyBeforeMutation(cloudRedirectInstallTransaction, text4, text6, deleting: true);
					File.Delete(text6);
					list.Add(text6);
				}
			}
			string text7 = Path.Combine(fullPath, "opensteamtool.toml");
			string text8 = ReadTextIfExists(text7);
			if (repairInvalidLauncherConfigs && !(cloudRedirectRuntimeProfile.IsBetterSteamTools ? BetterSteamToolsConfiguration.InspectCloud(text8).IsValid : CloudRedirectConfiguration.InspectOpenSteamToolConfig(text8).IsValid))
			{
				throw new InvalidDataException("opensteamtool.toml hiện hữu không hợp lệ; launcher sẽ không ghi đè toàn bộ cấu hình tùy chỉnh.");
			}
			string content = (cloudRedirectRuntimeProfile.IsBetterSteamTools ? BetterSteamToolsConfiguration.Update(text8, fullPath, !runtime.BetterOnly) : CloudRedirectConfiguration.UpdateOpenSteamToolConfig(text8));
			if (!TextFileEquals(text7, content))
			{
				SnapshotFile(text7, text3, cloudRedirectInstallTransaction, text4);
				RecordExpectedPostImage(text7, ComputeTextSha256(content), cloudRedirectInstallTransaction, text4);
				VerifyBeforeMutation(cloudRedirectInstallTransaction, text4, text7);
				AtomicWriteAllText(text7, content);
				RecordPostImage(text7, cloudRedirectInstallTransaction, text4);
				list.Add(text7);
			}
			string text9 = string.Empty;
			string text10 = string.Empty;
			if (!runtime.BetterOnly)
			{
				flag = true;
				string text11 = Path.Combine(fullPath, "cloud_redirect", "config.json");
				string text12 = ReadTextIfExists(text11);
				if (repairInvalidLauncherConfigs && !CloudRedirectConfiguration.InspectSteamConfig(text12).IsValid)
				{
					throw new InvalidDataException("CloudRedirect Steam config hiện hữu không hợp lệ; launcher sẽ không ghi đè cấu hình tùy chỉnh.");
				}
				string content2 = CloudRedirectConfiguration.UpdateSteamConfig(text12);
				if (!TextFileEquals(text11, content2))
				{
					SnapshotFile(text11, text3, cloudRedirectInstallTransaction, text4);
					RecordExpectedPostImage(text11, ComputeTextSha256(content2), cloudRedirectInstallTransaction, text4);
					VerifyBeforeMutation(cloudRedirectInstallTransaction, text4, text11);
					AtomicWriteAllText(text11, content2);
					RecordPostImage(text11, cloudRedirectInstallTransaction, text4);
					list.Add(text11);
				}
				Directory.CreateDirectory(Path.Combine(fullPath, "cloud_redirect", "storage"));
				string text13 = Path.Combine(text2, "CloudRedirect", "config.json");
				string content3 = CloudRedirectConfiguration.UpdateUserConfig(ReadTextIfExists(text13), syncPath);
				text9 = CloudRedirectConfiguration.ResolveProviderName(content3);
				text10 = CloudRedirectConfiguration.ResolveSyncPath(content3);
				CloudRedirectProviderStatus cloudRedirectProviderStatus = CloudRedirectConfiguration.InspectProvider(content3);
				if (!cloudRedirectProviderStatus.IsReady)
				{
					throw new InvalidOperationException(cloudRedirectProviderStatus.Message);
				}
				if (string.Equals(text9, "folder", StringComparison.OrdinalIgnoreCase))
				{
					Directory.CreateDirectory(text10);
				}
				if (!TextFileEquals(text13, content3))
				{
					SnapshotFile(text13, text3, cloudRedirectInstallTransaction, text4);
					RecordExpectedPostImage(text13, ComputeTextSha256(content3), cloudRedirectInstallTransaction, text4);
					VerifyBeforeMutation(cloudRedirectInstallTransaction, text4, text13);
					AtomicWriteAllText(text13, content3);
					RecordPostImage(text13, cloudRedirectInstallTransaction, text4);
					list.Add(text13);
				}
				flag = false;
			}
			string text14 = Path.Combine(path2, "state.json");
			bool changed;
			string content4 = BuildLocalStateContent(text14, runtime, fullPath, text10, detail, activationFiles, list.Count > 0, out changed);
			if (changed)
			{
				SnapshotFile(text14, text3, cloudRedirectInstallTransaction, text4);
				RecordExpectedPostImage(text14, ComputeTextSha256(content4), cloudRedirectInstallTransaction, text4);
				VerifyBeforeMutation(cloudRedirectInstallTransaction, text4, text14);
				AtomicWriteAllText(text14, content4);
				RecordPostImage(text14, cloudRedirectInstallTransaction, text4);
				list.Add(text14);
			}
			if (cloudRedirectInstallTransaction.Entries.Count > 0 || cloudRedirectInstallTransaction.ActivationReceipt)
			{
				cloudRedirectInstallTransaction.Status = "completed";
				cloudRedirectInstallTransaction.CompletedAtUtc = DateTime.UtcNow;
				WriteTransaction(text4, cloudRedirectInstallTransaction);
			}
			else
			{
				cloudRedirectInstallTransaction.Status = "no_change";
				cloudRedirectInstallTransaction.CompletedAtUtc = DateTime.UtcNow;
				WriteTransaction(text4, cloudRedirectInstallTransaction);
			}
			list2.Add($"Runtime bundle: {runtime.Version}; BetterOnly={runtime.BetterOnly}");
			list2.Add("Runtime native: " + cloudRedirectRuntimeProfile.DisplayName + "; DLL: " + cloudRedirectRuntimeProfile.MainDllName);
			if (!runtime.BetterOnly)
			{
				list2.Add("CloudRedirect DLL: " + runtime.CloudRedirectVersion);
				if (string.Equals(text9, "folder", StringComparison.OrdinalIgnoreCase))
				{
					list2.Add("Folder provider đã được cấu hình; không cần đăng nhập cloud provider.");
					list2.Add("Cloud save folder: " + text10);
				}
				else
				{
					list2.Add("Đã giữ nguyên CloudRedirect provider hiện hữu: " + text9 + ".");
				}
				list2.Add("Save được tách bởi Steam account ID và App ID trong cloud_redirect/storage.");
			}
			list2.Add((cloudRedirectInstallTransaction.Entries.Count > 0) ? ("Backup cài đặt: " + text3) : "CloudRedirect runtime/config đã đúng; không có file cần thay đổi.");
			return new CloudRedirectRuntimeInstallResult(list.Distinct<string>(StringComparer.OrdinalIgnoreCase).OrderBy<string, string>((string path) => path, StringComparer.OrdinalIgnoreCase).ToArray(), list2, (cloudRedirectInstallTransaction.Entries.Count > 0 || cloudRedirectInstallTransaction.ActivationReceipt) ? text3 : string.Empty, text10, text9, (cloudRedirectInstallTransaction.Entries.Count > 0 || cloudRedirectInstallTransaction.ActivationReceipt) ? text4 : string.Empty);
		}
		catch (Exception ex)
		{
			IReadOnlyList<string> readOnlyList = Rollback(cloudRedirectInstallTransaction, text4);
			cloudRedirectInstallTransaction.Status = ((readOnlyList.Count == 0) ? "rolled_back" : "rollback_failed");
			cloudRedirectInstallTransaction.CompletedAtUtc = DateTime.UtcNow;
			cloudRedirectInstallTransaction.Error = ex.Message;
			cloudRedirectInstallTransaction.RollbackErrors = readOnlyList.ToList();
			WriteTransactionBestEffort(text4, cloudRedirectInstallTransaction);
			bool flag2 = cloudRedirectRuntimeProfile.IsBetterSteamTools && flag && readOnlyList.Count == 0;
			if (flag2)
			{
				bool flag3 = ((ex is IOException || ex is InvalidDataException || ex is UnauthorizedAccessException || ex is InvalidOperationException) ? true : false);
				flag2 = flag3;
			}
			if (flag2)
			{
				throw new CloudRedirectOptionalException("cloud-install", ex);
			}
			string text15 = ((readOnlyList.Count == 0) ? "Các thay đổi đã được rollback." : $"Rollback còn {readOnlyList.Count} lỗi; xem {text4}.");
			throw new InvalidOperationException("Không thể cài CloudRedirect runtime: " + ex.Message + " " + text15, ex);
		}
	}

	internal CloudRedirectRuntimeOperationResult Disable(string steamDirectory, string? localAppDataOverride = null)
	{
		string fullPath = Path.GetFullPath(steamDirectory);
		if (!File.Exists(Path.Combine(fullPath, "steam.exe")))
		{
			throw new InvalidOperationException("Thư mục Steam không chứa steam.exe.");
		}
		string text = localAppDataOverride ?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
		string text2 = Path.Combine(text, "NestGLauncher", "CloudRedirect", "Backups", $"{DateTime.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}");
		Directory.CreateDirectory(text2);
		CloudRedirectRuntimeProfile cloudRedirectRuntimeProfile = CloudRedirectRuntimeProfile.FindInstalled(fullPath) ?? targetProfile;
		CloudRedirectInstallTransaction cloudRedirectInstallTransaction = new CloudRedirectInstallTransaction
		{
			SchemaVersion = 2,
			TransactionId = Guid.NewGuid().ToString("N"),
			LocalAppDataDirectory = Path.GetFullPath(text),
			RoamingAppDataDirectory = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
			Operation = "disable",
			BundleId = cloudRedirectRuntimeProfile.BundleId,
			BundleVersion = cloudRedirectRuntimeProfile.Version,
			SteamDirectory = fullPath,
			CreatedAtUtc = DateTime.UtcNow,
			Status = "installing"
		};
		string text3 = Path.Combine(text2, "transaction.json");
		WriteTransaction(text3, cloudRedirectInstallTransaction);
		try
		{
			string text4 = Path.Combine(fullPath, "opensteamtool.toml");
			if (!File.Exists(text4))
			{
				throw new FileNotFoundException("Không tìm thấy opensteamtool.toml để tắt CloudRedirect.", text4);
			}
			SnapshotFile(text4, text2, cloudRedirectInstallTransaction, text3);
			string content = (targetProfile.IsBetterSteamTools ? BetterSteamToolsConfiguration.SetCloudEnabled(File.ReadAllText(text4), enabled: false) : CloudRedirectConfiguration.SetOpenSteamToolCloudEnabled(File.ReadAllText(text4), enabled: false));
			RecordExpectedPostImage(text4, ComputeTextSha256(content), cloudRedirectInstallTransaction, text3);
			VerifyBeforeMutation(cloudRedirectInstallTransaction, text3, text4);
			AtomicWriteAllText(text4, content);
			RecordPostImage(text4, cloudRedirectInstallTransaction, text3);
			string text5 = Path.Combine(fullPath, "cloud_redirect", "config.json");
			SnapshotFile(text5, text2, cloudRedirectInstallTransaction, text3);
			string content2 = CloudRedirectConfiguration.SetSteamConfigEnabled(ReadTextIfExists(text5), enabled: false);
			RecordExpectedPostImage(text5, ComputeTextSha256(content2), cloudRedirectInstallTransaction, text3);
			VerifyBeforeMutation(cloudRedirectInstallTransaction, text3, text5);
			AtomicWriteAllText(text5, content2);
			RecordPostImage(text5, cloudRedirectInstallTransaction, text3);
			cloudRedirectInstallTransaction.Status = "completed";
			cloudRedirectInstallTransaction.CompletedAtUtc = DateTime.UtcNow;
			WriteTransaction(text3, cloudRedirectInstallTransaction);
			return new CloudRedirectRuntimeOperationResult(Succeeded: true, "CloudRedirect đã được tắt. Lua và DLL được giữ lại để có thể repair hoặc bật lại sau.", text2, text3);
		}
		catch (Exception ex)
		{
			IReadOnlyList<string> readOnlyList = Rollback(cloudRedirectInstallTransaction, text3);
			cloudRedirectInstallTransaction.Status = ((readOnlyList.Count == 0) ? "rolled_back" : "rollback_failed");
			cloudRedirectInstallTransaction.CompletedAtUtc = DateTime.UtcNow;
			cloudRedirectInstallTransaction.Error = ex.Message;
			cloudRedirectInstallTransaction.RollbackErrors = readOnlyList.ToList();
			WriteTransactionBestEffort(text3, cloudRedirectInstallTransaction);
			throw new InvalidOperationException((readOnlyList.Count == 0) ? ("Không thể tắt CloudRedirect: " + ex.Message + " Các thay đổi đã được rollback.") : $"Không thể tắt CloudRedirect: {ex.Message} Rollback còn lỗi; xem {text3}.", ex);
		}
	}

	internal CloudRedirectRuntimeOperationResult RollbackLatestCompleted(string steamDirectory, string? localAppDataOverride = null, string? roamingAppDataOverride = null)
	{
		string steamRoot = Path.GetFullPath(steamDirectory);
		string fullPath = Path.GetFullPath(localAppDataOverride ?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
		string fullPath2 = Path.GetFullPath(roamingAppDataOverride ?? Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));
		string text = Path.Combine(fullPath, "NestGLauncher", "CloudRedirect", "Backups");
		if (!Directory.Exists(text))
		{
			throw new InvalidOperationException("Chưa có backup CloudRedirect để rollback.");
		}
		return RollbackTransaction(((from path in CloudRedirectTransactionJournal.EnumeratePaths(text)
			select new
			{
				Path = path,
				Transaction = CloudRedirectTransactionJournal.Read(path)
			} into item
			where item != null && string.Equals(item.Transaction.Status, "completed", StringComparison.OrdinalIgnoreCase) && PathsEqual(item.Transaction.SteamDirectory, steamRoot)
			orderby item.Transaction.CompletedAtUtc ?? item.Transaction.CreatedAtUtc descending
			select item).FirstOrDefault() ?? throw new InvalidOperationException("Không tìm thấy transaction CloudRedirect có thể rollback cho Steam này.")).Path, steamRoot, fullPath, fullPath2, "rolled_back_by_user", "Đã rollback thao tác CloudRedirect gần nhất");
	}

	internal CloudRedirectRuntimeOperationResult RollbackFailedInstallTransaction(string transactionPath, string steamDirectory, string? localAppDataOverride = null, string? roamingAppDataOverride = null)
	{
		string fullPath = Path.GetFullPath(steamDirectory);
		string fullPath2 = Path.GetFullPath(localAppDataOverride ?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
		string fullPath3 = Path.GetFullPath(roamingAppDataOverride ?? Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));
		return RollbackTransaction(transactionPath, fullPath, fullPath2, fullPath3, "rolled_back_after_health_failure", "Đã rollback transaction CloudRedirect vừa cài lỗi");
	}

	internal CloudRedirectRuntimeOperationResult RecoverIncompleteTransaction(string transactionPath, string steamDirectory, string? localAppDataOverride = null, string? roamingAppDataOverride = null)
	{
		string fullPath = Path.GetFullPath(steamDirectory);
		string fullPath2 = Path.GetFullPath(localAppDataOverride ?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
		string fullPath3 = Path.GetFullPath(roamingAppDataOverride ?? Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));
		string fullPath4 = Path.GetFullPath(transactionPath);
		if (!TryLoadTransaction(fullPath4, out CloudRedirectInstallTransaction transaction) || !string.Equals(transaction.Status, "installing", StringComparison.OrdinalIgnoreCase) || !PathsEqual(transaction.SteamDirectory, fullPath))
		{
			throw new InvalidOperationException("Transaction CloudRedirect đang dở không tồn tại hoặc không thuộc Steam này.");
		}
		ValidateRollbackTransaction(fullPath4, transaction, fullPath, fullPath2, fullPath3);
		IReadOnlyList<string> readOnlyList = Rollback(transaction, fullPath4);
		transaction.Status = ((readOnlyList.Count == 0) ? "recovered_after_interruption" : "recovery_failed");
		transaction.CompletedAtUtc = DateTime.UtcNow;
		transaction.RollbackErrors = readOnlyList.ToList();
		WriteTransactionBestEffort(fullPath4, transaction);
		if (readOnlyList.Count > 0)
		{
			throw new InvalidOperationException($"Phục hồi CloudRedirect còn {readOnlyList.Count} lỗi; xem {fullPath4}.");
		}
		return new CloudRedirectRuntimeOperationResult(Succeeded: true, "Đã phục hồi lần cập nhật CloudRedirect bị gián đoạn.", Path.GetDirectoryName(fullPath4), fullPath4);
	}

	private static CloudRedirectRuntimeOperationResult RollbackTransaction(string transactionPath, string steamRoot, string localAppData, string roamingAppData, string successStatus, string successMessage)
	{
		string fullPath = Path.GetFullPath(transactionPath);
		if (!TryLoadTransaction(fullPath, out CloudRedirectInstallTransaction transaction) || !string.Equals(transaction.Status, "completed", StringComparison.OrdinalIgnoreCase) || !PathsEqual(transaction.SteamDirectory, steamRoot))
		{
			throw new InvalidOperationException("Transaction CloudRedirect không tồn tại, không còn ở trạng thái completed, hoặc không thuộc Steam này.");
		}
		ValidateRollbackTransaction(fullPath, transaction, steamRoot, localAppData, roamingAppData);
		IReadOnlyList<string> readOnlyList = Rollback(transaction, fullPath);
		transaction.Status = ((readOnlyList.Count == 0) ? successStatus : "rollback_failed");
		transaction.CompletedAtUtc = DateTime.UtcNow;
		transaction.RollbackErrors = readOnlyList.ToList();
		WriteTransactionBestEffort(fullPath, transaction);
		if (readOnlyList.Count > 0)
		{
			throw new InvalidOperationException($"Rollback CloudRedirect còn {readOnlyList.Count} lỗi; xem {fullPath}.");
		}
		return new CloudRedirectRuntimeOperationResult(Succeeded: true, successMessage + " (" + transaction.Operation + ").", Path.GetDirectoryName(fullPath), fullPath);
	}

	internal CloudRedirectReadinessResult InspectReadiness(string? steamDirectory, int? steamAppId, string? localAppDataOverride = null, string? roamingAppDataOverride = null, bool betterOnly = false)
	{
		if (string.IsNullOrWhiteSpace(steamDirectory))
		{
			return CloudRedirectReadinessResult.Blocked(CloudRedirectReadinessState.SteamMissing, "Không tìm thấy Steam trên máy.");
		}
		string steamRoot = Path.GetFullPath(steamDirectory);
		if (!File.Exists(Path.Combine(steamRoot, "steam.exe")))
		{
			return CloudRedirectReadinessResult.Blocked(CloudRedirectReadinessState.SteamMissing, "Thư mục Steam không chứa steam.exe.", steamRoot);
		}
		if (!steamAppId.HasValue || steamAppId.GetValueOrDefault() <= 0)
		{
			return CloudRedirectReadinessResult.Blocked(CloudRedirectReadinessState.InvalidSteamAppId, "Game chưa có Steam App ID hợp lệ.", steamRoot);
		}
		string fullPath = Path.GetFullPath(localAppDataOverride ?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
		string fullPath2 = Path.GetFullPath(roamingAppDataOverride ?? Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));
		List<string> list = new List<string>();
		int num = 0;
		int num2 = 0;
		IReadOnlyDictionary<string, DesktopRuntimeBundleFileResponse> readOnlyDictionary = targetProfile.RequiredFiles(betterOnly);
		foreach (KeyValuePair<string, DesktopRuntimeBundleFileResponse> item in readOnlyDictionary)
		{
			string path = ResolveContainedPath(steamRoot, item.Key);
			if (!File.Exists(path))
			{
				list.Add("Thiếu runtime file: " + item.Key);
				continue;
			}
			num++;
			if (TryComputeSha256(path, out string hash) && string.Equals(hash, item.Value.Sha256, StringComparison.OrdinalIgnoreCase))
			{
				num2++;
			}
			else
			{
				list.Add("Runtime file sai hash hoặc không đọc được: " + item.Key);
			}
		}
		bool flag = num == readOnlyDictionary.Count;
		bool flag2 = num2 == readOnlyDictionary.Count;
		CloudRedirectRuntimeProfile cloudRedirectRuntimeProfile = CloudRedirectRuntimeProfile.FindInstalled(steamRoot);
		bool flag3 = cloudRedirectRuntimeProfile != null && cloudRedirectRuntimeProfile != targetProfile;
		string text = null;
		RuntimeDllOwnershipIssue runtimeDllOwnershipIssue = null;
		try
		{
			runtimeDllOwnershipIssue = InspectMainDllOwnership(steamRoot, targetProfile);
			if ((object)runtimeDllOwnershipIssue != null)
			{
				text = runtimeDllOwnershipIssue.Message;
				list.Add(text);
			}
		}
		catch (Exception ex) when (((ex is IOException || ex is UnauthorizedAccessException || ex is InvalidDataException) ? 1 : 0) != 0)
		{
			text = ex.Message;
			list.Add(text);
		}
		bool flag4 = !targetProfile.IsBetterSteamTools;
		string path2 = Path.Combine(steamRoot, "opensteamtool.toml");
		CloudRedirectOpenSteamToolConfigStatus cloudRedirectOpenSteamToolConfigStatus;
		try
		{
			cloudRedirectOpenSteamToolConfigStatus = (targetProfile.IsBetterSteamTools ? BetterSteamToolsConfiguration.InspectCloud(ReadTextIfExists(path2)) : CloudRedirectConfiguration.InspectOpenSteamToolConfig(ReadTextIfExists(path2)));
			if (targetProfile.IsBetterSteamTools)
			{
				flag4 = BetterSteamToolsConfiguration.IsReady(ReadTextIfExists(path2), steamRoot, !betterOnly);
			}
			if (betterOnly)
			{
				cloudRedirectOpenSteamToolConfigStatus = new CloudRedirectOpenSteamToolConfigStatus(IsValid: true, flag4, flag4, "BetterSteamTools configuration.");
			}
		}
		catch (Exception ex2) when (((ex2 is IOException || ex2 is UnauthorizedAccessException || ex2 is InvalidDataException) ? 1 : 0) != 0)
		{
			cloudRedirectOpenSteamToolConfigStatus = new CloudRedirectOpenSteamToolConfigStatus(IsValid: false, IsEnabled: false, HasExpectedLibrary: false, "Không đọc được opensteamtool.toml: " + ex2.Message);
		}
		if (!cloudRedirectOpenSteamToolConfigStatus.IsReady)
		{
			list.Add(cloudRedirectOpenSteamToolConfigStatus.Message);
		}
		string path3 = Path.Combine(steamRoot, "cloud_redirect", "config.json");
		CloudRedirectSteamConfigStatus cloudRedirectSteamConfigStatus;
		try
		{
			cloudRedirectSteamConfigStatus = (betterOnly ? new CloudRedirectSteamConfigStatus(IsValid: true, IsEnabled: true, "CloudRedirect optional.") : CloudRedirectConfiguration.InspectSteamConfig(ReadTextIfExists(path3)));
		}
		catch (Exception ex3) when (((ex3 is IOException || ex3 is UnauthorizedAccessException) ? 1 : 0) != 0)
		{
			cloudRedirectSteamConfigStatus = new CloudRedirectSteamConfigStatus(IsValid: false, IsEnabled: false, "Không đọc được CloudRedirect Steam config: " + ex3.Message);
		}
		if (!cloudRedirectSteamConfigStatus.IsReady)
		{
			list.Add(cloudRedirectSteamConfigStatus.Message);
		}
		string path4 = Path.Combine(fullPath2, "CloudRedirect", "config.json");
		bool flag5 = betterOnly || File.Exists(path4);
		CloudRedirectProviderStatus cloudRedirectProviderStatus;
		try
		{
			cloudRedirectProviderStatus = (betterOnly ? new CloudRedirectProviderStatus("disabled", IsReady: true, "CloudRedirect optional.", string.Empty, string.Empty) : (flag5 ? CloudRedirectConfiguration.InspectProvider(File.ReadAllText(path4)) : new CloudRedirectProviderStatus(string.Empty, IsReady: false, "CloudRedirect provider chưa được cấu hình.", string.Empty, string.Empty)));
		}
		catch (Exception ex4) when (((ex4 is IOException || ex4 is UnauthorizedAccessException || ex4 is InvalidDataException) ? 1 : 0) != 0)
		{
			cloudRedirectProviderStatus = new CloudRedirectProviderStatus("invalid", IsReady: false, "CloudRedirect provider config không hợp lệ: " + ex4.Message, string.Empty, string.Empty);
		}
		bool flag6 = !string.Equals(cloudRedirectProviderStatus.Provider, "folder", StringComparison.OrdinalIgnoreCase) || (!string.IsNullOrWhiteSpace(cloudRedirectProviderStatus.SyncPath) && Directory.Exists(cloudRedirectProviderStatus.SyncPath));
		if (!cloudRedirectProviderStatus.IsReady || !flag6)
		{
			list.Add(cloudRedirectProviderStatus.IsReady ? "Folder provider chưa có thư mục sync." : cloudRedirectProviderStatus.Message);
		}
		string text2 = FindTransactionPath(Path.Combine(fullPath, "NestGLauncher", "CloudRedirect", "Backups"), steamRoot, "installing");
		bool flag7 = IsSteamAppRegistered(Path.Combine(steamRoot, "config", "lua"), steamAppId.Value);
		if (!flag7)
		{
			list.Add($"Steam App ID {steamAppId.Value} chưa có trong Lua activation đã cài.");
		}
		CloudRedirectReadinessState state;
		CloudRedirectReadinessAction recommendedAction;
		string message;
		if (!string.IsNullOrWhiteSpace(text2))
		{
			state = CloudRedirectReadinessState.PendingRecovery;
			recommendedAction = CloudRedirectReadinessAction.Recover;
			message = "Phát hiện lần cập nhật CloudRedirect chưa hoàn tất.";
		}
		else if (text != null)
		{
			state = CloudRedirectReadinessState.ConfigurationBlocked;
			recommendedAction = CloudRedirectReadinessAction.Blocked;
			message = text;
		}
		else if (!cloudRedirectOpenSteamToolConfigStatus.IsValid || !cloudRedirectSteamConfigStatus.IsValid)
		{
			state = CloudRedirectReadinessState.ConfigurationBlocked;
			recommendedAction = CloudRedirectReadinessAction.Blocked;
			message = "Cấu hình CloudRedirect hiện hữu không hợp lệ; launcher đã dừng để tránh ghi đè cấu hình tùy chỉnh.";
		}
		else if (num == 0)
		{
			state = CloudRedirectReadinessState.NotInstalled;
			recommendedAction = CloudRedirectReadinessAction.Install;
			message = "CloudRedirect chưa được cài.";
		}
		else if (flag3)
		{
			state = CloudRedirectReadinessState.UpgradeAvailable;
			recommendedAction = CloudRedirectReadinessAction.Upgrade;
			message = $"ACTIVE sẽ cập nhật {targetProfile.DisplayName} {targetProfile.Version} ({targetProfile.MainDllName}) và giữ nguyên Lua, game và cấu hình cloud hiện có.";
		}
		else if (!flag || !flag2 || targetProfile.RetiredMainDllNames.Any((string name) => File.Exists(Path.Combine(steamRoot, name))) || !cloudRedirectOpenSteamToolConfigStatus.IsValid || !cloudRedirectSteamConfigStatus.IsValid)
		{
			state = CloudRedirectReadinessState.NeedsRepair;
			recommendedAction = CloudRedirectReadinessAction.Repair;
			message = "CloudRedirect thiếu file, sai hash hoặc có config cần sửa.";
		}
		else if (!cloudRedirectOpenSteamToolConfigStatus.IsReady || !cloudRedirectSteamConfigStatus.IsReady || !flag4)
		{
			state = CloudRedirectReadinessState.Disabled;
			recommendedAction = CloudRedirectReadinessAction.Enable;
			message = "CloudRedirect đã cài nhưng cloud host đang tắt hoặc chưa được cấu hình.";
		}
		else if (!flag5 || string.IsNullOrWhiteSpace(cloudRedirectProviderStatus.Provider) || (string.Equals(cloudRedirectProviderStatus.Provider, "folder", StringComparison.OrdinalIgnoreCase) && cloudRedirectProviderStatus.IsReady && !flag6))
		{
			state = CloudRedirectReadinessState.ProviderMissing;
			recommendedAction = CloudRedirectReadinessAction.Enable;
			message = "Folder provider local sẽ được cấu hình tự động.";
		}
		else if (!cloudRedirectProviderStatus.IsReady)
		{
			state = CloudRedirectReadinessState.ProviderBlocked;
			recommendedAction = CloudRedirectReadinessAction.Blocked;
			message = cloudRedirectProviderStatus.Message;
		}
		else
		{
			state = CloudRedirectReadinessState.Ready;
			recommendedAction = CloudRedirectReadinessAction.NoChange;
			message = (flag7 ? "CloudRedirect đã sẵn sàng cho game này." : "CloudRedirect đã sẵn sàng; Active sẽ đăng ký game này.");
		}
		return new CloudRedirectReadinessResult(state, recommendedAction, SteamFound: true, flag, flag2, cloudRedirectOpenSteamToolConfigStatus.IsReady && flag4, cloudRedirectSteamConfigStatus.IsReady, cloudRedirectProviderStatus.Provider, cloudRedirectProviderStatus.IsReady && flag6, flag7, !string.IsNullOrWhiteSpace(text2), text2, steamRoot, message, list.Distinct<string>(StringComparer.OrdinalIgnoreCase).ToArray())
		{
			OwnershipIssue = runtimeDllOwnershipIssue
		};
	}

	internal CloudRedirectRuntimeStatus InspectStatus(string? steamDirectory, string? localAppDataOverride = null, string? roamingAppDataOverride = null)
	{
		if (string.IsNullOrWhiteSpace(steamDirectory))
		{
			return new CloudRedirectRuntimeStatus(SteamFound: false, RuntimeInstalled: false, IsEnabled: false, string.Empty, string.Empty, "Không tìm thấy Steam.", CanRollback: false);
		}
		string steamRoot = Path.GetFullPath(steamDirectory);
		string path = localAppDataOverride ?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
		string path2 = roamingAppDataOverride ?? Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
		string path3 = Path.Combine(steamRoot, "opensteamtool.toml");
		CloudRedirectRuntimeProfile cloudRedirectRuntimeProfile = CloudRedirectRuntimeProfile.FindInstalled(steamRoot);
		bool runtimeInstalled = (cloudRedirectRuntimeProfile ?? targetProfile).RootDllNames.All((string file) => File.Exists(Path.Combine(steamRoot, file)));
		bool flag = cloudRedirectRuntimeProfile != null && cloudRedirectRuntimeProfile != targetProfile;
		bool flag2 = targetProfile.IsInstalled(steamRoot);
		bool flag3 = false;
		try
		{
			flag3 = File.Exists(path3) && (targetProfile.IsBetterSteamTools ? BetterSteamToolsConfiguration.InspectCloud(File.ReadAllText(path3)).IsEnabled : CloudRedirectConfiguration.IsOpenSteamToolCloudEnabled(File.ReadAllText(path3)));
		}
		catch (Exception ex) when (((ex is IOException || ex is UnauthorizedAccessException || ex is InvalidDataException) ? 1 : 0) != 0)
		{
		}
		string path4 = Path.Combine(path2, "CloudRedirect", "config.json");
		CloudRedirectProviderStatus cloudRedirectProviderStatus;
		try
		{
			cloudRedirectProviderStatus = (File.Exists(path4) ? CloudRedirectConfiguration.InspectProvider(File.ReadAllText(path4)) : new CloudRedirectProviderStatus(string.Empty, IsReady: false, "CloudRedirect provider chưa được cấu hình.", string.Empty, string.Empty));
		}
		catch (Exception ex2) when (((ex2 is InvalidDataException || ex2 is IOException || ex2 is UnauthorizedAccessException) ? 1 : 0) != 0)
		{
			cloudRedirectProviderStatus = new CloudRedirectProviderStatus("invalid", IsReady: false, "CloudRedirect provider config không hợp lệ.", string.Empty, string.Empty);
		}
		bool canRollback = HasCompletedTransaction(Path.Combine(path, "NestGLauncher", "CloudRedirect", "Backups"), steamRoot);
		string message = (flag ? $"Đang dùng {cloudRedirectRuntimeProfile.DisplayName} {cloudRedirectRuntimeProfile.Version}. ACTIVE sẽ cập nhật {targetProfile.MainDllName} và giữ Lua/cấu hình cloud hiện có." : ((!flag2) ? "CloudRedirect runtime chưa được cài đầy đủ." : ((!flag3) ? "CloudRedirect đã cài nhưng đang bị tắt." : ((!cloudRedirectProviderStatus.IsReady) ? cloudRedirectProviderStatus.Message : $"{targetProfile.DisplayName} {targetProfile.Version} ({targetProfile.MainDllName}); CloudRedirect đang bật với provider '{cloudRedirectProviderStatus.Provider}'."))));
		return new CloudRedirectRuntimeStatus(SteamFound: true, runtimeInstalled, flag3, cloudRedirectProviderStatus.Provider, steamRoot, message, canRollback, cloudRedirectProviderStatus.IsReady);
	}

	internal static CloudRedirectHealthMarker CaptureHealthMarker(string steamDirectory)
	{
		string text = Path.Combine(steamDirectory, "cloud_redirect.log");
		FileInfo fileInfo = new FileInfo(text);
		return new CloudRedirectHealthMarker(text, fileInfo.Exists ? fileInfo.Length : 0, fileInfo.Exists ? fileInfo.LastWriteTimeUtc : DateTime.MinValue);
	}

	internal static async Task<CloudRedirectHealthResult> CheckHealthAsync(CloudRedirectHealthMarker marker, TimeSpan timeout, CancellationToken cancellationToken)
	{
		Stopwatch stopwatch = Stopwatch.StartNew();
		while (stopwatch.Elapsed < timeout)
		{
			cancellationToken.ThrowIfCancellationRequested();
			CloudRedirectHealthResult cloudRedirectHealthResult = InspectHealthLog(marker);
			if (cloudRedirectHealthResult.IsReady || cloudRedirectHealthResult.IsDefinitiveFailure)
			{
				return cloudRedirectHealthResult;
			}
			await Task.Delay(TimeSpan.FromMilliseconds(500.0), cancellationToken);
		}
		CloudRedirectHealthResult cloudRedirectHealthResult2 = InspectHealthLog(marker);
		return (cloudRedirectHealthResult2.IsReady || cloudRedirectHealthResult2.IsDefinitiveFailure) ? cloudRedirectHealthResult2 : new CloudRedirectHealthResult(IsReady: false, IsDefinitiveFailure: false, marker.LogPath, "Chưa xác nhận được CloudRedirect từ log sau khi Steam khởi động; hãy chờ Steam nạp xong và kiểm tra cloud_redirect.log.");
	}

	internal static CloudRedirectHealthResult InspectHealthLog(CloudRedirectHealthMarker marker)
	{
		if (!File.Exists(marker.LogPath))
		{
			return new CloudRedirectHealthResult(IsReady: false, IsDefinitiveFailure: false, marker.LogPath, "CloudRedirect chưa tạo log.");
		}
		FileInfo fileInfo = new FileInfo(marker.LogPath);
		if (fileInfo.Length == marker.Length && !(fileInfo.LastWriteTimeUtc > marker.LastWriteTimeUtc))
		{
			return new CloudRedirectHealthResult(IsReady: false, IsDefinitiveFailure: false, marker.LogPath, "CloudRedirect log chưa có dữ liệu mới.");
		}
		string text;
		try
		{
			using FileStream fileStream = new FileStream(marker.LogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
			if (fileStream.Length > marker.Length)
			{
				fileStream.Seek(marker.Length, SeekOrigin.Begin);
			}
			using StreamReader streamReader = new StreamReader(fileStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
			text = streamReader.ReadToEnd();
		}
		catch (IOException)
		{
			return new CloudRedirectHealthResult(IsReady: false, IsDefinitiveFailure: false, marker.LogPath, "CloudRedirect log đang được ghi.");
		}
		if (text.Contains("CR_InitCloudSave FAILED", StringComparison.OrdinalIgnoreCase))
		{
			return new CloudRedirectHealthResult(IsReady: false, IsDefinitiveFailure: true, marker.LogPath, "CloudRedirect không thể khởi tạo; xem cloud_redirect.log.");
		}
		bool flag = text.Contains("CR_InitCloudSave complete", StringComparison.OrdinalIgnoreCase);
		bool flag2 = RegisteredAppsRegex().IsMatch(text);
		int num = (from Match match in VtableSlotRegex().Matches(text)
			select match.Groups["slot"].Value).Distinct<string>(StringComparer.Ordinal).Count();
		if (flag && flag2 && num == 4)
		{
			return new CloudRedirectHealthResult(IsReady: true, IsDefinitiveFailure: false, marker.LogPath, "CloudRedirect đã nạp, đăng ký Lua apps và kích hoạt đầy đủ cloud RPC hooks.");
		}
		if (flag && flag2 && (text.Contains("[VtHook] REFUSED", StringComparison.OrdinalIgnoreCase) || text.Contains("[VtHook] WARNING: Some hooks failed", StringComparison.OrdinalIgnoreCase)))
		{
			return new CloudRedirectHealthResult(IsReady: false, IsDefinitiveFailure: false, marker.LogPath, "CloudRedirect đã khởi tạo và đăng ký Lua apps; vtable hooks không đầy đủ nên runtime sẽ thử packet-layer fallback. Cần kiểm tra save thực tế.");
		}
		return new CloudRedirectHealthResult(IsReady: false, IsDefinitiveFailure: false, marker.LogPath, "CloudRedirect đang khởi tạo.");
	}

	internal CloudRedirectHealthResult? GetVerifiedHealthForSteamSession(string steamDirectory, CloudRedirectSteamSession? session, string? localAppDataOverride = null)
	{
		if ((object)session == null || session.StartedAtUtc == default(DateTime))
		{
			return null;
		}
		string path = Path.Combine(localAppDataOverride ?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NestGLauncher", "CloudRedirect", "health-verification.json");
		try
		{
			if (!File.Exists(path))
			{
				return null;
			}
			CloudRedirectVerifiedHealth cloudRedirectVerifiedHealth = JsonSerializer.Deserialize<CloudRedirectVerifiedHealth>(File.ReadAllText(path), JsonOptions);
			if (cloudRedirectVerifiedHealth == null || !PathsEqual(cloudRedirectVerifiedHealth.SteamDirectory, steamDirectory) || cloudRedirectVerifiedHealth.ProcessId != session.ProcessId || Math.Abs((cloudRedirectVerifiedHealth.ProcessStartedAtUtc - session.StartedAtUtc).TotalSeconds) > 1.0 || !PathsEqual(cloudRedirectVerifiedHealth.LogPath, Path.Combine(steamDirectory, "cloud_redirect.log")) || !File.Exists(cloudRedirectVerifiedHealth.LogPath))
			{
				return null;
			}
			FileInfo fileInfo = new FileInfo(cloudRedirectVerifiedHealth.LogPath);
			if (fileInfo.Length < cloudRedirectVerifiedHealth.LogLength || fileInfo.LastWriteTimeUtc < cloudRedirectVerifiedHealth.LogLastWriteTimeUtc)
			{
				return null;
			}
			if (fileInfo.Length != cloudRedirectVerifiedHealth.LogLength || fileInfo.LastWriteTimeUtc > cloudRedirectVerifiedHealth.LogLastWriteTimeUtc)
			{
				CloudRedirectHealthResult cloudRedirectHealthResult = InspectHealthLog(new CloudRedirectHealthMarker(cloudRedirectVerifiedHealth.LogPath, cloudRedirectVerifiedHealth.LogLength, cloudRedirectVerifiedHealth.LogLastWriteTimeUtc));
				if (cloudRedirectHealthResult.IsDefinitiveFailure)
				{
					return cloudRedirectHealthResult;
				}
			}
			return new CloudRedirectHealthResult(IsReady: true, IsDefinitiveFailure: false, cloudRedirectVerifiedHealth.LogPath, "CloudRedirect đã được xác nhận trong phiên Steam hiện tại.");
		}
		catch (Exception ex) when (((ex is IOException || ex is UnauthorizedAccessException || ex is JsonException) ? 1 : 0) != 0)
		{
			return null;
		}
	}

	internal bool RecordVerifiedHealth(string steamDirectory, CloudRedirectSteamSession? session, CloudRedirectHealthResult health, string? localAppDataOverride = null)
	{
		if ((object)session == null || session.StartedAtUtc == default(DateTime) || !health.IsReady || !File.Exists(health.LogPath))
		{
			return false;
		}
		string? path = localAppDataOverride ?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
		string path2 = Path.Combine(path, "NestGLauncher", "CloudRedirect", "state.json");
		string destinationPath = Path.Combine(path, "NestGLauncher", "CloudRedirect", "health-verification.json");
		try
		{
			if (!File.Exists(path2))
			{
				return false;
			}
			if (!PathsEqual((JsonSerializer.Deserialize<CloudRedirectLocalState>(File.ReadAllText(path2), JsonOptions) ?? new CloudRedirectLocalState()).SteamDirectory, steamDirectory))
			{
				return false;
			}
			FileInfo fileInfo = new FileInfo(health.LogPath);
			CloudRedirectVerifiedHealth value = new CloudRedirectVerifiedHealth
			{
				SteamDirectory = Path.GetFullPath(steamDirectory),
				ProcessId = session.ProcessId,
				ProcessStartedAtUtc = session.StartedAtUtc,
				VerifiedAtUtc = DateTime.UtcNow,
				LogPath = Path.GetFullPath(health.LogPath),
				LogLength = fileInfo.Length,
				LogLastWriteTimeUtc = fileInfo.LastWriteTimeUtc
			};
			AtomicWriteAllText(destinationPath, JsonSerializer.Serialize(value, JsonOptions) + Environment.NewLine);
			return true;
		}
		catch (Exception ex) when (((ex is IOException || ex is UnauthorizedAccessException || ex is JsonException) ? 1 : 0) != 0)
		{
			return false;
		}
	}

	internal void ValidateBundle(DesktopRuntimeBundleResponse bundle)
	{
		CloudRedirectRuntimeProfile.Resolve(bundle.BundleId, bundle.Version).Validate(bundle, currentDesktopVersion);
	}

	private static void ValidateMainDllOwnership(string steamRoot, CloudRedirectRuntimeProfile profile)
	{
		RuntimeDllOwnershipIssue runtimeDllOwnershipIssue = InspectMainDllOwnership(steamRoot, profile);
		if ((object)runtimeDllOwnershipIssue != null)
		{
			throw new InvalidDataException(runtimeDllOwnershipIssue.Message);
		}
	}

	private static RuntimeDllOwnershipIssue? InspectMainDllOwnership(string steamRoot, CloudRedirectRuntimeProfile profile)
	{
		if (profile.RetiredMainDllNames.Count == 0)
		{
			return null;
		}
		foreach (string item in profile.RetiredMainDllNames.Append(profile.MainDllName))
		{
			string path = ResolveContainedPath(steamRoot, item);
			if (profile.ReplacesExistingMainDll)
			{
				EnsureRecoveryPathIsNotLinked(path);
			}
			if (Directory.Exists(path))
			{
				return new RuntimeDllOwnershipIssue(item, RuntimeDllOwnershipFailure.Directory, "Đường dẫn DLL đang là thư mục: " + item + ". Launcher đã giữ nguyên dữ liệu.");
			}
			if (File.Exists(path) && (!profile.ReplacesExistingMainDll || !(item == profile.MainDllName)))
			{
				if (!TryComputeSha256(path, out string hash))
				{
					return new RuntimeDllOwnershipIssue(item, RuntimeDllOwnershipFailure.Unreadable, "Không đọc được " + item + " để xác minh. Launcher đã dừng để giữ nguyên file hiện có.");
				}
				if (!CloudRedirectRuntimeProfile.IsKnownFileHash(item, hash))
				{
					return new RuntimeDllOwnershipIssue(item, RuntimeDllOwnershipFailure.UnknownHash, item + " có hash chưa được NestG xác nhận. Launcher đã dừng để giữ nguyên file hiện có.");
				}
			}
		}
		return null;
	}

	private static void ValidatePreparedRuntime(PreparedCloudRedirectRuntime runtime)
	{
		CloudRedirectRuntimeProfile cloudRedirectRuntimeProfile = CloudRedirectRuntimeProfile.Resolve(runtime.BundleId, runtime.Version);
		IReadOnlyDictionary<string, DesktopRuntimeBundleFileResponse> readOnlyDictionary = cloudRedirectRuntimeProfile.RequiredFiles(runtime.BetterOnly);
		if (runtime.Version != cloudRedirectRuntimeProfile.Version || runtime.CloudRedirectVersion != cloudRedirectRuntimeProfile.CloudRedirectVersion || !string.Equals(runtime.OpenSteamToolCommit, cloudRedirectRuntimeProfile.Commit, StringComparison.OrdinalIgnoreCase))
		{
			throw new InvalidDataException("Runtime đã tải không tương thích CloudRedirect.");
		}
		bool flag = default(bool);
		foreach (PreparedCloudRedirectRuntimeFile file in runtime.Files)
		{
			if (!readOnlyDictionary.TryGetValue(file.InstallPath, out var value) || file.RelativePath != value.RelativePath || file.Size != value.Size || !string.Equals(file.Sha256, value.Sha256, StringComparison.OrdinalIgnoreCase))
			{
				throw new InvalidDataException("Runtime đã tải không khớp client pin.");
			}
			try
			{
				if (!File.Exists(file.SourcePath))
				{
					throw new FileNotFoundException("Thiếu file runtime đã tải: " + file.RelativePath, file.SourcePath);
				}
				FileInfo fileInfo = new FileInfo(file.SourcePath);
				using FileStream source = File.OpenRead(file.SourcePath);
				string a = Convert.ToHexString(SHA256.HashData(source));
				if (fileInfo.Length != file.Size || !string.Equals(a, file.Sha256, StringComparison.OrdinalIgnoreCase))
				{
					throw new InvalidDataException("Runtime đã tải bị thay đổi trước khi cài: " + file.RelativePath);
				}
				if (file.InstallPath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
				{
					ValidatePeArchitecture(file.SourcePath, 34404);
				}
			}
			catch (Exception ex) when (((Func<bool>)delegate
			{
				// Could not convert BlockContainer to single expression
				flag = !runtime.BetterOnly && cloudRedirectRuntimeProfile.IsBetterSteamTools && CloudRedirectRuntimeProfile.IsCloudFile(file.InstallPath);
				if (flag)
				{
					flag = ((ex is IOException || ex is InvalidDataException) ? true : false);
				}
				return flag;
			}).Invoke())
			{
				throw new CloudRedirectOptionalException("cloud-file-validation", ex);
			}
		}
		if (runtime.Files.Count != 0 && (runtime.Files.Count != readOnlyDictionary.Count || runtime.Files.Select((PreparedCloudRedirectRuntimeFile file) => file.InstallPath).Distinct<string>(StringComparer.OrdinalIgnoreCase).Count() != readOnlyDictionary.Count))
		{
			throw new InvalidDataException("Runtime đã tải thiếu file hoặc chứa file trùng lặp.");
		}
	}

	private static async Task VerifyFileAsync(string filePath, long expectedSize, string expectedHash, CancellationToken cancellationToken)
	{
		FileInfo fileInfo = new FileInfo(filePath);
		if (!fileInfo.Exists || fileInfo.Length != expectedSize)
		{
			throw new InvalidDataException("Sai kích thước file runtime: " + Path.GetFileName(filePath));
		}
		await using FileStream stream = File.OpenRead(filePath);
		if (!string.Equals(Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken)), expectedHash, StringComparison.OrdinalIgnoreCase))
		{
			throw new InvalidDataException("Sai SHA-256 file runtime: " + Path.GetFileName(filePath));
		}
	}

	internal static void ValidatePeArchitecture(string filePath, ushort expectedMachine)
	{
		using FileStream fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
		using BinaryReader binaryReader = new BinaryReader(fileStream, Encoding.UTF8, leaveOpen: true);
		if (fileStream.Length < 64 || binaryReader.ReadUInt16() != 23117)
		{
			throw new InvalidDataException("File không có PE header hợp lệ: " + Path.GetFileName(filePath));
		}
		fileStream.Position = 60L;
		int num = binaryReader.ReadInt32();
		if (num < 0 || num > fileStream.Length - 6)
		{
			throw new InvalidDataException("File có PE offset không hợp lệ: " + Path.GetFileName(filePath));
		}
		fileStream.Position = num;
		if (binaryReader.ReadUInt32() != 17744)
		{
			throw new InvalidDataException("File thiếu PE signature: " + Path.GetFileName(filePath));
		}
		ushort num2 = binaryReader.ReadUInt16();
		if (num2 != expectedMachine)
		{
			throw new InvalidDataException($"DLL sai kiến trúc: {Path.GetFileName(filePath)} (machine 0x{num2:X4}).");
		}
	}

	private static void EnsureUniqueInstallPaths(IEnumerable<CloudRedirectInstallItem> items)
	{
		HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (CloudRedirectInstallItem item in items)
		{
			if (!File.Exists(item.SourcePath))
			{
				throw new FileNotFoundException("Không tìm thấy file cần cài.", item.SourcePath);
			}
			if (!hashSet.Add(item.InstallPath))
			{
				throw new InvalidDataException("Trùng đường dẫn cài đặt: " + item.InstallPath);
			}
		}
	}

	private PreparedCloudRedirectRuntime CreateKnownRuntimeMetadata()
	{
		return new PreparedCloudRedirectRuntime(ExpectedBundleId, SupportedBundleVersion, SupportedOpenSteamToolCommit, SupportedCloudRedirectVersion, Array.Empty<PreparedCloudRedirectRuntimeFile>());
	}

	private static bool FilesHaveSameContent(string sourcePath, string destinationPath)
	{
		if (!File.Exists(destinationPath))
		{
			return false;
		}
		FileInfo fileInfo = new FileInfo(sourcePath);
		FileInfo fileInfo2 = new FileInfo(destinationPath);
		if (fileInfo.Length != fileInfo2.Length || !TryComputeSha256(sourcePath, out string hash) || !TryComputeSha256(destinationPath, out string hash2))
		{
			return false;
		}
		return string.Equals(hash, hash2, StringComparison.OrdinalIgnoreCase);
	}

	private static bool TextFileEquals(string path, string content)
	{
		try
		{
			return File.Exists(path) && string.Equals(File.ReadAllText(path), content, StringComparison.Ordinal);
		}
		catch (Exception ex) when (((ex is IOException || ex is UnauthorizedAccessException) ? 1 : 0) != 0)
		{
			return false;
		}
	}

	private static void SnapshotFile(string destinationPath, string backupDirectory, CloudRedirectInstallTransaction transaction, string transactionPath)
	{
		string fullPath = Path.GetFullPath(destinationPath);
		ValidateManagedTarget(transaction, fullPath);
		EnsureRecoveryPathIsNotLinked(backupDirectory);
		if (transaction.Entries.Any((CloudRedirectInstallTransactionEntry entry) => string.Equals(entry.OriginalPath, fullPath, StringComparison.OrdinalIgnoreCase)))
		{
			return;
		}
		bool flag = ExistingFile(fullPath);
		string text = null;
		string hash = string.Empty;
		if (flag)
		{
			text = Path.Combine(backupDirectory, "files", $"{transaction.Entries.Count:D4}.bak");
			Directory.CreateDirectory(Path.GetDirectoryName(text));
			EnsureRecoveryPathIsNotLinked(text);
			using (FileStream fileStream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read))
			{
				using FileStream fileStream2 = new FileStream(text, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.WriteThrough);
				fileStream.CopyTo(fileStream2);
				fileStream2.Flush(flushToDisk: true);
			}
			if (!TryComputeSha256(text, out hash) || !TryComputeSha256(fullPath, out string hash2) || !string.Equals(hash, hash2, StringComparison.OrdinalIgnoreCase))
			{
				throw new IOException("Không xác nhận được file backup: " + text);
			}
		}
		transaction.Entries.Add(new CloudRedirectInstallTransactionEntry
		{
			OriginalPath = fullPath,
			Existed = flag,
			BackupPath = text,
			PreImageSha256 = hash
		});
		WriteTransaction(transactionPath, transaction);
	}

	private static void RecordPostImage(string destinationPath, CloudRedirectInstallTransaction transaction, string transactionPath)
	{
		string fullPath = Path.GetFullPath(destinationPath);
		CloudRedirectInstallTransactionEntry cloudRedirectInstallTransactionEntry = transaction.Entries.LastOrDefault((CloudRedirectInstallTransactionEntry item) => string.Equals(item.OriginalPath, fullPath, StringComparison.OrdinalIgnoreCase));
		if (cloudRedirectInstallTransactionEntry == null || !TryComputeSha256(fullPath, out string hash))
		{
			throw new IOException("Không xác nhận được file vừa ghi: " + fullPath);
		}
		if (!string.IsNullOrWhiteSpace(cloudRedirectInstallTransactionEntry.PostImageSha256) && !string.Equals(cloudRedirectInstallTransactionEntry.PostImageSha256, hash, StringComparison.OrdinalIgnoreCase))
		{
			throw new IOException("File vừa ghi không khớp post-image dự kiến: " + fullPath);
		}
		cloudRedirectInstallTransactionEntry.PostImageSha256 = hash;
		WriteTransaction(transactionPath, transaction);
	}

	private static void RecordExpectedPostImage(string destinationPath, string expectedSha256, CloudRedirectInstallTransaction transaction, string transactionPath)
	{
		string fullPath = Path.GetFullPath(destinationPath);
		CloudRedirectInstallTransactionEntry? cloudRedirectInstallTransactionEntry = transaction.Entries.LastOrDefault((CloudRedirectInstallTransactionEntry item) => string.Equals(item.OriginalPath, fullPath, StringComparison.OrdinalIgnoreCase));
		if (cloudRedirectInstallTransactionEntry == null || !Sha256Regex().IsMatch(expectedSha256))
		{
			throw new IOException("Không ghi được post-image dự kiến: " + fullPath);
		}
		cloudRedirectInstallTransactionEntry.PostImageSha256 = expectedSha256.ToUpperInvariant();
		WriteTransaction(transactionPath, transaction);
	}

	private static IReadOnlyList<string> Rollback(CloudRedirectInstallTransaction transaction, string transactionPath)
	{
		List<string> list = new List<string>();
		try
		{
			CloudRedirectInstallTransaction cloudRedirectInstallTransaction = CloudRedirectTransactionJournal.Read(transactionPath, requireProtected: true);
			if (cloudRedirectInstallTransaction.TransactionId != transaction.TransactionId || !PathsEqual(cloudRedirectInstallTransaction.SteamDirectory, transaction.SteamDirectory))
			{
				throw new InvalidDataException("Nhật ký không thuộc lần thao tác này.");
			}
			transaction = cloudRedirectInstallTransaction;
			ValidateRollbackTransaction(transactionPath, transaction, transaction.SteamDirectory, transaction.LocalAppDataDirectory, transaction.RoamingAppDataDirectory);
			list.AddRange(FindRollbackDriftErrors(transaction));
		}
		catch (Exception ex) when (((ex is SteamRecoveryPendingException || ex is InvalidDataException || ex is IOException || ex is InvalidOperationException || ex is UnauthorizedAccessException || ex is CryptographicException) ? 1 : 0) != 0)
		{
			list.Add("Không đủ bằng chứng rollback an toàn: " + ex.GetType().Name);
		}
		if (list.Count > 0)
		{
			return list;
		}
		foreach (CloudRedirectInstallTransactionEntry item in transaction.Entries.AsEnumerable().Reverse())
		{
			try
			{
				ValidateManagedTarget(transaction, item.OriginalPath);
				if (FindRollbackDriftErrors(transaction, new _003C_003Ez__ReadOnlySingleElementList<CloudRedirectInstallTransactionEntry>(item)).Count > 0)
				{
					throw new InvalidDataException("File thay đổi trong lúc rollback; giữ nguyên file còn lại.");
				}
				if (item.Existed)
				{
					if (string.IsNullOrWhiteSpace(item.BackupPath) || !File.Exists(item.BackupPath))
					{
						throw new FileNotFoundException("Thiếu file backup.", item.BackupPath);
					}
					if (string.IsNullOrWhiteSpace(item.PreImageSha256) || !TryComputeSha256(item.OriginalPath, out string hash) || !string.Equals(hash, item.PreImageSha256, StringComparison.OrdinalIgnoreCase))
					{
						AtomicCopy(item.BackupPath, item.OriginalPath, item.PreImageSha256);
					}
				}
				else if (ExistingFile(item.OriginalPath))
				{
					File.Delete(item.OriginalPath);
				}
			}
			catch (Exception ex2)
			{
				list.Add(item.OriginalPath + ": " + ex2.Message);
			}
		}
		return list;
	}

	private static IReadOnlyList<string> FindRollbackDriftErrors(CloudRedirectInstallTransaction transaction, IEnumerable<CloudRedirectInstallTransactionEntry>? entries = null)
	{
		List<string> list = new List<string>();
		foreach (CloudRedirectInstallTransactionEntry item in entries ?? transaction.Entries)
		{
			EnsureRecoveryPathIsNotLinked(item.OriginalPath);
			bool flag = ExistingFile(item.OriginalPath);
			string hash3;
			if (item.PostImageDeleted)
			{
				if (flag && (!TryComputeSha256(item.OriginalPath, out string hash) || !string.Equals(hash, item.PreImageSha256, StringComparison.OrdinalIgnoreCase)))
				{
					list.Add(item.OriginalPath + ": file đã được tạo lại hoặc thay đổi sau khi xóa; rollback đã dừng để giữ nguyên file.");
				}
			}
			else if (string.IsNullOrWhiteSpace(item.PostImageSha256))
			{
				if ((item.Existed || flag) && (!(item.Existed && flag) || string.IsNullOrWhiteSpace(item.PreImageSha256) || !TryComputeSha256(item.OriginalPath, out string hash2) || !string.Equals(hash2, item.PreImageSha256, StringComparison.OrdinalIgnoreCase)))
				{
					list.Add(item.OriginalPath + ": transaction thiếu post-image hash; rollback bị dừng để tránh ghi đè.");
				}
			}
			else if (!flag)
			{
				if (item.Existed)
				{
					list.Add(item.OriginalPath + ": file đã bị xóa sau transaction; rollback bị dừng để tránh ghi đè.");
				}
			}
			else if (!TryComputeSha256(item.OriginalPath, out hash3))
			{
				list.Add(item.OriginalPath + ": không đọc được file hiện tại để rollback an toàn.");
			}
			else if ((!item.Existed || string.IsNullOrWhiteSpace(item.PreImageSha256) || !string.Equals(hash3, item.PreImageSha256, StringComparison.OrdinalIgnoreCase)) && !string.Equals(hash3, item.PostImageSha256, StringComparison.OrdinalIgnoreCase))
			{
				list.Add(item.OriginalPath + ": file đã thay đổi sau transaction; rollback bị dừng để tránh ghi đè.");
			}
		}
		return list;
	}

	private static bool TryLoadTransaction(string transactionPath, out CloudRedirectInstallTransaction transaction)
	{
		try
		{
			transaction = CloudRedirectTransactionJournal.Read(transactionPath);
			return !string.IsNullOrWhiteSpace(transaction.SteamDirectory) && transaction.Entries != null && (transaction.Entries.Count > 0 || transaction.ActivationReceipt || transaction.SchemaVersion == 2);
		}
		catch (Exception ex) when (((ex is SteamRecoveryPendingException || ex is InvalidDataException || ex is IOException || ex is UnauthorizedAccessException || ex is JsonException) ? 1 : 0) != 0)
		{
			transaction = new CloudRedirectInstallTransaction();
			return false;
		}
	}

	private static void ValidateRollbackTransaction(string transactionPath, CloudRedirectInstallTransaction transaction, string steamDirectory, string localAppData, string roamingAppData, bool requireReadableBackups = false)
	{
		CloudRedirectInstallTransaction cloudRedirectInstallTransaction = CloudRedirectTransactionJournal.Read(transactionPath, requireProtected: true);
		ValidateTransactionScope(transactionPath, transaction);
		if (cloudRedirectInstallTransaction.TransactionId != transaction.TransactionId || !PathsEqual(transaction.LocalAppDataDirectory, localAppData) || (transaction.Entries.Any((CloudRedirectInstallTransactionEntry entry) => PathsEqual(entry.OriginalPath, Path.Combine(transaction.RoamingAppDataDirectory, "CloudRedirect", "config.json"))) && !PathsEqual(transaction.RoamingAppDataDirectory, roamingAppData)) || !PathsEqual(transaction.SteamDirectory, steamDirectory))
		{
			throw new InvalidDataException("Nhật ký không thuộc môi trường phục hồi này.");
		}
		string rootDirectory = Path.Combine(localAppData, "NestGLauncher", "CloudRedirect", "Backups");
		if (!IsPathWithin(transactionPath, rootDirectory))
		{
			throw new InvalidDataException("Transaction rollback nằm ngoài thư mục backup cho phép.");
		}
		string directoryName = Path.GetDirectoryName(Path.GetFullPath(transactionPath));
		string b = Path.Combine(localAppData, "NestGLauncher", "CloudRedirect", "state.json");
		string b2 = Path.Combine(roamingAppData, "CloudRedirect", "config.json");
		foreach (CloudRedirectInstallTransactionEntry entry2 in transaction.Entries)
		{
			string originalPath = Path.GetFullPath(entry2.OriginalPath);
			if (transaction.Operation == "steam_recovery")
			{
				if (!SteamLoaderNames.Any((string name) => PathsEqual(originalPath, Path.Combine(steamDirectory, name)) && CloudRedirectRuntimeProfile.IsKnownFileHash(name, entry2.PreImageSha256)) || !entry2.Existed || !string.IsNullOrEmpty(entry2.PostImageSha256))
				{
					throw new InvalidDataException("Transaction phục hồi Steam chứa file không được phép.");
				}
				EnsureRecoveryPathIsNotLinked(originalPath);
				EnsureRecoveryPathIsNotLinked(transactionPath);
				if (!string.IsNullOrWhiteSpace(entry2.BackupPath))
				{
					EnsureRecoveryPathIsNotLinked(entry2.BackupPath);
				}
			}
			if (entry2.PostImageDeleted && !IsSteamRecoveryEntry(transaction, entry2, steamDirectory) && (!entry2.Existed || !string.IsNullOrEmpty(entry2.PostImageSha256) || !CloudRedirectRuntimeProfile.IsKnownFileHash(Path.GetFileName(originalPath), entry2.PreImageSha256) || !CloudRedirectRuntimeProfile.Known.Any((CloudRedirectRuntimeProfile profile) => profile.BundleId == transaction.BundleId && profile.Version == transaction.BundleVersion && profile.RetiredMainDllNames.Any((string name) => string.Equals(originalPath, Path.Combine(steamDirectory, name), StringComparison.OrdinalIgnoreCase)))))
			{
				throw new InvalidDataException("Transaction chứa thao tác xóa DLL không hợp lệ.");
			}
			if (!IsPathWithin(originalPath, steamDirectory) && !string.Equals(originalPath, b, StringComparison.OrdinalIgnoreCase) && !string.Equals(originalPath, b2, StringComparison.OrdinalIgnoreCase))
			{
				throw new InvalidDataException("Transaction rollback chứa đường dẫn không an toàn: " + originalPath);
			}
			if (entry2.Existed && (string.IsNullOrWhiteSpace(entry2.BackupPath) || !IsPathWithin(entry2.BackupPath, directoryName)))
			{
				throw new InvalidDataException("Transaction rollback có file backup không an toàn.");
			}
			if (entry2.Existed && !string.IsNullOrWhiteSpace(entry2.PreImageSha256))
			{
				string hash;
				if (requireReadableBackups)
				{
					VerifyExpectedFile(entry2.BackupPath, entry2.PreImageSha256);
				}
				else if (!File.Exists(entry2.BackupPath) || !TryComputeSha256(entry2.BackupPath, out hash) || !string.Equals(hash, entry2.PreImageSha256, StringComparison.OrdinalIgnoreCase))
				{
					throw new InvalidDataException("Transaction rollback có file backup sai hash.");
				}
			}
			if ((!string.IsNullOrWhiteSpace(entry2.PreImageSha256) && !Sha256Regex().IsMatch(entry2.PreImageSha256)) || (!string.IsNullOrWhiteSpace(entry2.PostImageSha256) && !Sha256Regex().IsMatch(entry2.PostImageSha256)))
			{
				throw new InvalidDataException("Transaction rollback có hash không hợp lệ.");
			}
		}
	}

	private static bool IsPathWithin(string path, string rootDirectory)
	{
		string text = Path.GetFullPath(rootDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
		string fullPath = Path.GetFullPath(path);
		if (!string.Equals(fullPath, text, StringComparison.OrdinalIgnoreCase))
		{
			return fullPath.StartsWith(text + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
		}
		return true;
	}

	private static bool PathsEqual(string left, string right)
	{
		try
		{
			return string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);
		}
		catch
		{
			return false;
		}
	}

	private static bool HasCompletedTransaction(string backupRoot, string steamDirectory)
	{
		try
		{
			CloudRedirectInstallTransaction transaction;
			return Directory.Exists(backupRoot) && CloudRedirectTransactionJournal.EnumeratePaths(backupRoot).Any((string path) => TryLoadTransaction(path, out transaction) && string.Equals(transaction.Status, "completed", StringComparison.OrdinalIgnoreCase) && PathsEqual(transaction.SteamDirectory, steamDirectory));
		}
		catch (Exception ex) when (((ex is InvalidDataException || ex is IOException || ex is UnauthorizedAccessException) ? 1 : 0) != 0)
		{
			return false;
		}
	}

	private static string FindTransactionPath(string backupRoot, string steamDirectory, string status)
	{
		try
		{
			if (!Directory.Exists(backupRoot))
			{
				return string.Empty;
			}
			CloudRedirectInstallTransaction transaction;
			return (from path in CloudRedirectTransactionJournal.EnumeratePaths(backupRoot)
				select (!TryLoadTransaction(path, out transaction)) ? null : new
				{
					Path = path,
					Transaction = transaction
				} into item
				where item != null && string.Equals(item.Transaction.Status, status, StringComparison.OrdinalIgnoreCase) && PathsEqual(item.Transaction.SteamDirectory, steamDirectory)
				orderby item.Transaction.CreatedAtUtc descending
				select item.Path).FirstOrDefault() ?? string.Empty;
		}
		catch (Exception ex) when (((ex is InvalidDataException || ex is IOException || ex is UnauthorizedAccessException) ? 1 : 0) != 0)
		{
			return string.Empty;
		}
	}

	private static bool IsSteamAppRegistered(string luaDirectory, int steamAppId)
	{
		if (!Directory.Exists(luaDirectory))
		{
			return false;
		}
		Regex registration = new Regex($"\\baddappid\\s*\\(\\s*{steamAppId}\\s*(?:,|\\))", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
		try
		{
			return Directory.EnumerateFiles(luaDirectory, "*.lua", SearchOption.TopDirectoryOnly).Any((string path) => File.ReadLines(path).Any((string line) => !line.TrimStart().StartsWith("--", StringComparison.Ordinal) && registration.IsMatch(line)));
		}
		catch (Exception ex) when (((ex is IOException || ex is UnauthorizedAccessException) ? 1 : 0) != 0)
		{
			return false;
		}
	}

	private static bool TryComputeSha256(string path, out string hash)
	{
		try
		{
			using FileStream source = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
			hash = Convert.ToHexString(SHA256.HashData(source));
			return true;
		}
		catch (Exception ex) when (((ex is IOException || ex is UnauthorizedAccessException) ? 1 : 0) != 0)
		{
			hash = string.Empty;
			return false;
		}
	}

	private static string ComputeTextSha256(string content)
	{
		return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content)));
	}

	private static string BuildLocalStateContent(string statePath, PreparedCloudRedirectRuntime runtime, string steamDirectory, string syncPath, DesktopLibraryDetailResponse detail, IReadOnlyList<CloudRedirectActivationFile> activationFiles, bool forceUpdate, out bool changed)
	{
		string text = ReadTextIfExists(statePath);
		bool flag = false;
		CloudRedirectLocalState cloudRedirectLocalState;
		try
		{
			cloudRedirectLocalState = ((!string.IsNullOrWhiteSpace(text)) ? (JsonSerializer.Deserialize<CloudRedirectLocalState>(text, JsonOptions) ?? new CloudRedirectLocalState()) : new CloudRedirectLocalState());
			flag = !string.IsNullOrWhiteSpace(text);
		}
		catch (JsonException)
		{
			cloudRedirectLocalState = new CloudRedirectLocalState();
		}
		List<string> list = activationFiles.Select((CloudRedirectActivationFile file) => file.InstallPath).OrderBy<string, string>((string path) => path, StringComparer.OrdinalIgnoreCase).ToList();
		CloudRedirectInstalledGame cloudRedirectInstalledGame = cloudRedirectLocalState.Games.FirstOrDefault((CloudRedirectInstalledGame game) => game.LibraryId == detail.LibraryId || game.SteamAppId == detail.SteamAppId);
		bool flag2 = !string.Equals(cloudRedirectLocalState.BundleId, runtime.BundleId, StringComparison.Ordinal) || !string.Equals(cloudRedirectLocalState.BundleVersion, runtime.Version, StringComparison.Ordinal) || !string.Equals(cloudRedirectLocalState.OpenSteamToolCommit, runtime.OpenSteamToolCommit, StringComparison.OrdinalIgnoreCase) || !string.Equals(cloudRedirectLocalState.CloudRedirectVersion, runtime.CloudRedirectVersion, StringComparison.OrdinalIgnoreCase) || !PathsEqual(cloudRedirectLocalState.SteamDirectory, steamDirectory) || (!runtime.BetterOnly && !PathsEqual(cloudRedirectLocalState.SyncPath, syncPath));
		bool flag3 = cloudRedirectInstalledGame == null || cloudRedirectInstalledGame.LibraryId != detail.LibraryId || cloudRedirectInstalledGame.GameId != detail.GameId || cloudRedirectInstalledGame.SteamAppId != detail.SteamAppId || !string.Equals(cloudRedirectInstalledGame.GameName, detail.GameName, StringComparison.Ordinal) || !cloudRedirectInstalledGame.InstalledFiles.SequenceEqual<string>(list, StringComparer.OrdinalIgnoreCase);
		changed = forceUpdate || !flag || flag2 || flag3;
		if (!changed)
		{
			return text;
		}
		DateTime utcNow = DateTime.UtcNow;
		cloudRedirectLocalState.BundleId = runtime.BundleId;
		cloudRedirectLocalState.BundleVersion = runtime.Version;
		cloudRedirectLocalState.OpenSteamToolCommit = runtime.OpenSteamToolCommit;
		cloudRedirectLocalState.CloudRedirectVersion = runtime.CloudRedirectVersion;
		cloudRedirectLocalState.SteamDirectory = steamDirectory;
		if (!runtime.BetterOnly)
		{
			cloudRedirectLocalState.SyncPath = syncPath;
		}
		cloudRedirectLocalState.LastInstalledAtUtc = utcNow;
		cloudRedirectLocalState.Games.RemoveAll((CloudRedirectInstalledGame game) => game.LibraryId == detail.LibraryId || game.SteamAppId == detail.SteamAppId);
		cloudRedirectLocalState.Games.Add(new CloudRedirectInstalledGame
		{
			LibraryId = detail.LibraryId,
			GameId = detail.GameId,
			SteamAppId = detail.SteamAppId.Value,
			GameName = detail.GameName,
			InstalledAtUtc = utcNow,
			InstalledFiles = list
		});
		return JsonSerializer.Serialize(cloudRedirectLocalState, JsonOptions) + Environment.NewLine;
	}

	private static void WriteTransaction(string transactionPath, CloudRedirectInstallTransaction transaction)
	{
		ValidateTransactionScope(transactionPath, transaction);
		CloudRedirectTransactionJournal.Save(transactionPath, transaction);
	}

	private static void WriteTransactionBestEffort(string transactionPath, CloudRedirectInstallTransaction transaction)
	{
		try
		{
			CloudRedirectInstallTransaction cloudRedirectInstallTransaction = CloudRedirectTransactionJournal.Read(transactionPath, requireProtected: true);
			if (cloudRedirectInstallTransaction.TransactionId != transaction.TransactionId)
			{
				throw new InvalidDataException("Nhật ký không thuộc lần thao tác này.");
			}
			cloudRedirectInstallTransaction.Status = transaction.Status;
			cloudRedirectInstallTransaction.CompletedAtUtc = transaction.CompletedAtUtc;
			cloudRedirectInstallTransaction.Error = transaction.Error;
			cloudRedirectInstallTransaction.RollbackErrors = transaction.RollbackErrors;
			WriteTransaction(transactionPath, cloudRedirectInstallTransaction);
		}
		catch
		{
		}
	}

	private static string? ReadTextIfExists(string path)
	{
		if (!File.Exists(path))
		{
			return null;
		}
		return File.ReadAllText(path);
	}

	private static string NormalizeRelativePath(string value, string fieldName)
	{
		string text = (value ?? string.Empty).Trim().Replace('\\', '/');
		string[] array = text.Split('/', StringSplitOptions.RemoveEmptyEntries);
		if (array.Length == 0 || Path.IsPathRooted(text) || array.Any((string segment) => ((segment == "." || segment == "..") ? true : false) || segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0))
		{
			throw new InvalidDataException(fieldName + " không hợp lệ.");
		}
		return string.Join('/', array);
	}

	private static string ResolveContainedPath(string rootDirectory, string relativePath)
	{
		string fullPath = Path.GetFullPath(rootDirectory);
		if (string.IsNullOrWhiteSpace(relativePath))
		{
			return fullPath;
		}
		string fullPath2 = Path.GetFullPath(Path.Combine(fullPath, relativePath.Replace('/', Path.DirectorySeparatorChar)));
		if (!fullPath2.StartsWith(fullPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
		{
			throw new InvalidDataException("Đường dẫn cài đặt vượt ra ngoài thư mục cho phép.");
		}
		return fullPath2;
	}

	private static void AtomicCopy(string sourcePath, string destinationPath, string? expectedHash = null)
	{
		WriteFlushAndPublish(destinationPath, delegate(FileStream destinationStream)
		{
			using FileStream fileStream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.SequentialScan);
			if (expectedHash != null)
			{
				if (!Convert.ToHexString(SHA256.HashData(fileStream)).Equals(expectedHash, StringComparison.OrdinalIgnoreCase))
				{
					throw new InvalidDataException("File nguồn không khớp bằng chứng đã xác minh.");
				}
				fileStream.Position = 0L;
			}
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
		string text = $"{destinationPath}.{Guid.NewGuid():N}.tmp";
		try
		{
			using (FileStream fileStream = new FileStream(text, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.WriteThrough))
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

	// [GeneratedRegex("^[A-Fa-f0-9]{64}$")]
	[GeneratedCode("System.Text.RegularExpressions.Generator", "8.0.14.36720")]
	private static Regex Sha256Regex()
	{
		return _003CRegexGenerator_g_003EF0EE122E5B50AE7A6E112AFB8BF1EBFE9D7DDEF874BBD113E5DA346C1494B7BE9__Sha256Regex_3.Instance;
	}

	// [GeneratedRegex("\\[CR_API\\]\\s+SetApps:\\s+[1-9][0-9]*\\s+app\\(s\\)", RegexOptions.IgnoreCase)]
	[GeneratedCode("System.Text.RegularExpressions.Generator", "8.0.14.36720")]
	private static Regex RegisteredAppsRegex()
	{
		return _003CRegexGenerator_g_003EF0EE122E5B50AE7A6E112AFB8BF1EBFE9D7DDEF874BBD113E5DA346C1494B7BE9__RegisteredAppsRegex_4.Instance;
	}

	// [GeneratedRegex("\\[VtHook\\]\\s+Vtable slot (?<slot>4|5|7|8) patched:.*\\(ok=1\\)", RegexOptions.IgnoreCase)]
	[GeneratedCode("System.Text.RegularExpressions.Generator", "8.0.14.36720")]
	private static Regex VtableSlotRegex()
	{
		return _003CRegexGenerator_g_003EF0EE122E5B50AE7A6E112AFB8BF1EBFE9D7DDEF874BBD113E5DA346C1494B7BE9__VtableSlotRegex_5.Instance;
	}

	internal CloudRedirectRuntimeOperationResult RecoverSteamStartup(string steamDirectory, bool steamWritesAuthorized, string? localAppDataOverride = null, string? recoveryId = null)
	{
		if (!steamWritesAuthorized)
		{
			throw new InvalidOperationException("Hãy thoát Steam và game trước khi phục hồi Steam.");
		}
		string fullPath = Path.GetFullPath(steamDirectory);
		EnsureRecoveryPathIsNotLinked(Path.Combine(fullPath, "steam.exe"));
		if (!File.Exists(Path.Combine(fullPath, "steam.exe")))
		{
			throw new InvalidOperationException("Không tìm thấy steam.exe để phục hồi.");
		}
		List<(string, string)> list = new List<(string, string)>();
		string[] steamLoaderNames = SteamLoaderNames;
		foreach (string text in steamLoaderNames)
		{
			string text2 = Path.Combine(fullPath, text);
			EnsureRecoveryPathIsNotLinked(text2);
			if (Directory.Exists(text2))
			{
				throw new InvalidDataException(text + " là thư mục; đã dừng phục hồi.");
			}
			if (File.Exists(text2))
			{
				if (!TryComputeSha256(text2, out string hash) || !CloudRedirectRuntimeProfile.IsKnownFileHash(text, hash))
				{
					throw new InvalidDataException(text + " không khớp bộ hỗ trợ đã xác minh. Launcher giữ nguyên file này; hãy liên hệ hỗ trợ.");
				}
				list.Add((text2, hash));
			}
		}
		if (recoveryId != null && !Guid.TryParseExact(recoveryId, "N", out var _))
		{
			throw new InvalidDataException("Recovery ID is invalid.");
		}
		string fullPath2 = Path.GetFullPath(localAppDataOverride ?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
		string text3 = Path.Combine(Path.GetFullPath(localAppDataOverride ?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)), "NestGLauncher", "CloudRedirect", "Backups", (recoveryId == null) ? $"{DateTime.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}" : ("steam-recovery-" + recoveryId));
		EnsureRecoveryPathIsNotLinked(text3);
		Directory.CreateDirectory(text3);
		string text4 = Path.Combine(text3, "transaction.json");
		CloudRedirectInstallTransaction transaction = new CloudRedirectInstallTransaction
		{
			SchemaVersion = 2,
			TransactionId = Guid.NewGuid().ToString("N"),
			LocalAppDataDirectory = fullPath2,
			RoamingAppDataDirectory = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
			Operation = "steam_recovery",
			BundleId = targetProfile.BundleId,
			BundleVersion = targetProfile.Version,
			SteamDirectory = fullPath,
			CreatedAtUtc = DateTime.UtcNow,
			Status = "installing"
		};
		if (CloudRedirectTransactionJournal.Exists(text4))
		{
			bool flag = !TryLoadTransaction(text4, out transaction) || transaction.Operation != "steam_recovery" || !PathsEqual(transaction.SteamDirectory, fullPath);
			if (!flag)
			{
				string status = transaction.Status;
				bool flag2 = ((status == "installing" || status == "completed") ? true : false);
				flag = !flag2;
			}
			if (flag)
			{
				throw new InvalidDataException("Recovery transaction cannot be resumed.");
			}
			ValidateRollbackTransaction(text4, transaction, fullPath, fullPath2, Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));
			if (transaction.Status == "completed")
			{
				if (list.Count != 0)
				{
					throw new InvalidDataException("Loaders changed after recovery completed.");
				}
				return new CloudRedirectRuntimeOperationResult(Succeeded: true, "Bộ nạp đã được ngắt trong lần phục hồi này.", text3, text4);
			}
			if (FindRollbackDriftErrors(transaction).Count > 0)
			{
				throw new InvalidDataException("Recovery files changed; resume was stopped.");
			}
		}
		if (list.Count == 0 && transaction.Entries.Count == 0)
		{
			return new CloudRedirectRuntimeOperationResult(Succeeded: true, "Không còn bộ nạp NestG trong Steam. Launcher sẽ thử mở Steam để kiểm tra.", "", "");
		}
		if (!CloudRedirectTransactionJournal.Exists(text4))
		{
			WriteTransaction(text4, transaction);
		}
		try
		{
			foreach (var loader in list)
			{
				if (!transaction.Entries.Any((CloudRedirectInstallTransactionEntry entry) => PathsEqual(entry.OriginalPath, loader.Item1)))
				{
					SnapshotFile(loader.Item1, Path.Combine(text3, Guid.NewGuid().ToString("N")), transaction, text4);
					if (!string.Equals(transaction.Entries.Last().PreImageSha256, loader.Item2, StringComparison.OrdinalIgnoreCase))
					{
						throw new IOException("Bộ nạp Steam đã thay đổi trong lúc sao lưu; đã dừng phục hồi.");
					}
				}
			}
			foreach (CloudRedirectInstallTransactionEntry entry in transaction.Entries)
			{
				EnsureRecoveryPathIsNotLinked(entry.OriginalPath);
				if (!entry.PostImageDeleted || File.Exists(entry.OriginalPath))
				{
					if (!TryComputeSha256(entry.OriginalPath, out string hash2) || !string.Equals(hash2, entry.PreImageSha256, StringComparison.OrdinalIgnoreCase))
					{
						throw new IOException("Bộ nạp Steam đã thay đổi; đã dừng phục hồi để giữ nguyên file.");
					}
					entry.PostImageDeleted = true;
					WriteTransaction(text4, transaction);
					VerifyBeforeMutation(transaction, text4, entry.OriginalPath, deleting: true);
					File.Delete(entry.OriginalPath);
				}
			}
			transaction.Status = "completed";
			transaction.CompletedAtUtc = DateTime.UtcNow;
			WriteTransaction(text4, transaction);
			return new CloudRedirectRuntimeOperationResult(Succeeded: true, "Đã tạm ngắt bộ hỗ trợ NestG để Steam mở bình thường. Game, Lua, cấu hình cloud và save được giữ nguyên. Chỉ ACTIVE lại sau khi nguyên nhân lỗi được xử lý.", text3, text4);
		}
		catch (Exception ex)
		{
			IReadOnlyList<string> readOnlyList = Rollback(transaction, text4);
			transaction.Status = ((readOnlyList.Count == 0) ? "rolled_back" : "rollback_failed");
			transaction.CompletedAtUtc = DateTime.UtcNow;
			transaction.Error = ex.Message;
			transaction.RollbackErrors = readOnlyList.ToList();
			WriteTransactionBestEffort(text4, transaction);
			throw new InvalidOperationException("Chưa thể phục hồi Steam: " + ex.Message + " Backup: " + text3, ex);
		}
	}

	private static bool IsSteamRecoveryEntry(CloudRedirectInstallTransaction transaction, CloudRedirectInstallTransactionEntry entry, string steamDirectory)
	{
		if (transaction.Operation == "steam_recovery" && entry.Existed && entry.PostImageDeleted && string.IsNullOrEmpty(entry.PostImageSha256))
		{
			return SteamLoaderNames.Any((string name) => PathsEqual(entry.OriginalPath, Path.Combine(steamDirectory, name)) && CloudRedirectRuntimeProfile.IsKnownFileHash(name, entry.PreImageSha256));
		}
		return false;
	}

	internal static void EnsureRecoveryPathIsNotLinked(string path)
	{
		for (string text = Path.GetFullPath(path); text != null; text = Directory.GetParent(text)?.FullName)
		{
			try
			{
				if ((File.GetAttributes(text) & FileAttributes.ReparsePoint) != 0)
				{
					throw new InvalidDataException("Đường dẫn phục hồi chứa liên kết; launcher đã dừng để bảo vệ dữ liệu.");
				}
			}
			catch (FileNotFoundException)
			{
			}
			catch (DirectoryNotFoundException)
			{
			}
		}
	}

	internal void ResumeActivationRollback(string transactionPath, string steamDirectory, string? localAppDataOverride = null, string? roamingAppDataOverride = null)
	{
		EnsureRecoveryPathIsNotLinked(transactionPath);
		if (!TryLoadTransaction(transactionPath, out CloudRedirectInstallTransaction transaction) || !PathsEqual(transaction.SteamDirectory, steamDirectory) || transaction.Operation == "steam_recovery")
		{
			throw new InvalidDataException("Không xác minh được backup của lần ACTIVE vừa lỗi.");
		}
		ValidateRollbackTransaction(transactionPath, transaction, steamDirectory, localAppDataOverride ?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), roamingAppDataOverride ?? Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));
		foreach (CloudRedirectInstallTransactionEntry entry in transaction.Entries)
		{
			EnsureRecoveryPathIsNotLinked(entry.OriginalPath);
			if (!string.IsNullOrEmpty(entry.BackupPath))
			{
				EnsureRecoveryPathIsNotLinked(entry.BackupPath);
			}
		}
		bool flag;
		switch (transaction.Status)
		{
		case "rolled_back":
		case "rolled_back_after_health_failure":
		case "recovered_after_interruption":
			flag = true;
			break;
		default:
			flag = false;
			break;
		}
		if (!flag)
		{
			switch (transaction.Status)
			{
			case "completed":
			case "installing":
			case "rollback_failed":
			case "recovery_failed":
				flag = true;
				break;
			default:
				flag = false;
				break;
			}
			if (!flag)
			{
				throw new InvalidDataException("Trạng thái backup không cho phép rollback.");
			}
			IReadOnlyList<string> readOnlyList = Rollback(transaction, transactionPath);
			transaction.Status = ((readOnlyList.Count == 0) ? "rolled_back_after_health_failure" : "rollback_failed");
			transaction.CompletedAtUtc = DateTime.UtcNow;
			transaction.RollbackErrors = readOnlyList.ToList();
			WriteTransaction(transactionPath, transaction);
			if (readOnlyList.Count > 0)
			{
				throw new InvalidOperationException("Rollback đã dừng vì file thay đổi hoặc không thể khôi phục.");
			}
		}
	}

	internal static bool ExistingFile(string path)
	{
		try
		{
			if ((File.GetAttributes(path) & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0)
			{
				throw new InvalidDataException("Đích thao tác không phải file thường.");
			}
			return true;
		}
		catch (FileNotFoundException)
		{
			return false;
		}
		catch (DirectoryNotFoundException)
		{
			return false;
		}
	}

	internal static void ValidateTransactionScope(string path, CloudRedirectInstallTransaction transaction)
	{
		if (transaction.SchemaVersion != 2 || !Guid.TryParseExact(transaction.TransactionId, "N", out var _) || !Path.IsPathFullyQualified(transaction.SteamDirectory) || !Path.IsPathFullyQualified(transaction.LocalAppDataDirectory) || !Path.IsPathFullyQualified(transaction.RoamingAppDataDirectory) || transaction.Entries == null || transaction.Entries.Count > 8192 || transaction.ActivationFiles == null || transaction.ActivationFiles.Count > 4096)
		{
			throw new InvalidDataException("Nhật ký thiếu bằng chứng thao tác hợp lệ.");
		}
		bool flag;
		switch (transaction.Operation)
		{
		case "recover":
		case "upgrade":
		case "disable":
		case "install":
		case "enable":
		case "repair":
		case "activate":
		case "better_fallback":
		case "steam_recovery":
			flag = true;
			break;
		default:
			flag = false;
			break;
		}
		if (!flag || transaction.ActivationFiles.Any((SteamActivationFileStamp file) => (object)file == null || !IsActivationFilePath(file.InstallPath) || string.IsNullOrEmpty(file.Sha256) || !Sha256Regex().IsMatch(file.Sha256)) || transaction.ActivationFiles.Select((SteamActivationFileStamp file) => file.InstallPath).Distinct<string>(StringComparer.OrdinalIgnoreCase).Count() != transaction.ActivationFiles.Count)
		{
			throw new InvalidDataException("Phạm vi activation không hợp lệ.");
		}
		if (!IsPathWithin(path, Path.Combine(transaction.LocalAppDataDirectory, "NestGLauncher", "CloudRedirect", "Backups")) || Path.GetFileName(path) != "transaction.json")
		{
			throw new InvalidDataException("Nhật ký nằm ngoài thư mục backup.");
		}
		EnsureRecoveryPathIsNotLinked(path);
		HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		HashSet<string> hashSet2 = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (CloudRedirectInstallTransactionEntry entry in transaction.Entries)
		{
			if (entry == null || !hashSet.Add(Path.GetFullPath(entry.OriginalPath)))
			{
				throw new InvalidDataException("Nhật ký chứa file trùng hoặc entry trống.");
			}
			ValidateManagedTarget(transaction, entry.OriginalPath);
			if (entry.Existed)
			{
				if (string.IsNullOrEmpty(entry.BackupPath) || !Path.IsPathFullyQualified(entry.BackupPath) || !IsPathWithin(entry.BackupPath, Path.GetDirectoryName(path)) || !hashSet2.Add(Path.GetFullPath(entry.BackupPath)) || !Sha256Regex().IsMatch(entry.PreImageSha256))
				{
					throw new InvalidDataException("Thiếu bằng chứng backup của file có sẵn.");
				}
				EnsureRecoveryPathIsNotLinked(entry.BackupPath);
			}
			else if (!string.IsNullOrEmpty(entry.BackupPath) || !string.IsNullOrEmpty(entry.PreImageSha256) || entry.PostImageDeleted)
			{
				throw new InvalidDataException("Bằng chứng file mới không hợp lệ.");
			}
			if (!string.IsNullOrEmpty(entry.PostImageSha256) && !Sha256Regex().IsMatch(entry.PostImageSha256))
			{
				throw new InvalidDataException("Hash file mới không hợp lệ.");
			}
			string relative = Path.GetRelativePath(transaction.SteamDirectory, entry.OriginalPath).Replace('\\', '/');
			CloudRedirectRuntimeProfile cloudRedirectRuntimeProfile = CloudRedirectRuntimeProfile.Resolve(transaction.BundleId, transaction.BundleVersion);
			DesktopRuntimeBundleFileResponse value;
			string text = (cloudRedirectRuntimeProfile.RequiredFiles(transaction.BetterOnly).TryGetValue(relative, out value) ? value.Sha256 : transaction.ActivationFiles.FirstOrDefault((SteamActivationFileStamp file) => file.InstallPath == relative)?.Sha256);
			if (!string.IsNullOrEmpty(entry.PostImageSha256) && text != null && !entry.PostImageSha256.Equals(text, StringComparison.OrdinalIgnoreCase))
			{
				throw new InvalidDataException("Hash không khớp file đã xác minh của lần ACTIVE.");
			}
			if (cloudRedirectRuntimeProfile.RetiredMainDllNames.Contains<string>(relative, StringComparer.OrdinalIgnoreCase) && (!entry.Existed || !CloudRedirectRuntimeProfile.IsKnownFileHash(relative, entry.PreImageSha256)))
			{
				throw new InvalidDataException("Không đủ bằng chứng sở hữu DLL cũ.");
			}
		}
	}

	private static void ValidateManagedTarget(CloudRedirectInstallTransaction transaction, string path)
	{
		if (!Path.IsPathFullyQualified(path))
		{
			throw new InvalidDataException("Đường dẫn file phải tuyệt đối.");
		}
		string fullPath = Path.GetFullPath(path);
		EnsureRecoveryPathIsNotLinked(fullPath);
		bool flag = IsPathWithin(fullPath, transaction.SteamDirectory);
		string relative = Path.GetRelativePath(transaction.SteamDirectory, fullPath).Replace('\\', '/');
		if ((flag && relative.Split('/').Any((string part) => part.EndsWith('.') || part.EndsWith(' ') || part.Contains(':') || part.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)) || new string[8] { "steam.exe", "steamclient.dll", "steamclient64.dll", "steamui.dll", "tier0_s.dll", "tier0_s64.dll", "vstdlib_s.dll", "vstdlib_s64.dll" }.Contains<string>(Path.GetFileName(fullPath), StringComparer.OrdinalIgnoreCase))
		{
			throw new InvalidDataException("Không được thao tác trên file chính Steam hoặc đường dẫn nhập nhằng.");
		}
		CloudRedirectRuntimeProfile cloudRedirectRuntimeProfile = CloudRedirectRuntimeProfile.Resolve(transaction.BundleId, transaction.BundleVersion);
		bool flag2;
		switch (transaction.Operation)
		{
		case "steam_recovery":
			flag2 = flag && SteamLoaderNames.Contains<string>(relative, StringComparer.OrdinalIgnoreCase);
			break;
		case "better_fallback":
			flag2 = flag && relative == "opensteamtool.toml";
			break;
		case "disable":
		{
			bool flag3 = flag;
			if (flag3)
			{
				string text = relative;
				bool flag4 = ((text == "opensteamtool.toml" || text == "cloud_redirect/config.json") ? true : false);
				flag3 = flag4;
			}
			flag2 = flag3;
			break;
		}
		case "recover":
		case "upgrade":
		case "install":
		case "enable":
		case "repair":
		case "activate":
			flag2 = (flag && (cloudRedirectRuntimeProfile.RequiredFiles(transaction.BetterOnly).ContainsKey(relative) || cloudRedirectRuntimeProfile.RetiredMainDllNames.Contains<string>(relative, StringComparer.OrdinalIgnoreCase) || relative == "opensteamtool.toml" || (!transaction.BetterOnly && relative == "cloud_redirect/config.json") || (IsActivationFilePath(relative) && transaction.ActivationFiles.Any((SteamActivationFileStamp file) => file.InstallPath == relative)))) || PathsEqual(fullPath, Path.Combine(transaction.LocalAppDataDirectory, "NestGLauncher", "CloudRedirect", "state.json")) || (!transaction.BetterOnly && PathsEqual(fullPath, Path.Combine(transaction.RoamingAppDataDirectory, "CloudRedirect", "config.json")));
			break;
		default:
			flag2 = false;
			break;
		}
		if (!flag2)
		{
			throw new InvalidDataException("File không thuộc phạm vi thao tác của launcher.");
		}
	}

	private static void VerifyBeforeMutation(CloudRedirectInstallTransaction transaction, string transactionPath, string path, bool deleting = false)
	{
		CloudRedirectInstallTransaction cloudRedirectInstallTransaction = CloudRedirectTransactionJournal.Read(transactionPath, requireProtected: true);
		if (cloudRedirectInstallTransaction.TransactionId != transaction.TransactionId)
		{
			throw new InvalidDataException("Bằng chứng thao tác không thuộc transaction này.");
		}
		transaction = cloudRedirectInstallTransaction;
		ValidateTransactionScope(transactionPath, transaction);
		ValidateManagedTarget(transaction, path);
		CloudRedirectInstallTransactionEntry cloudRedirectInstallTransactionEntry = transaction.Entries.Single((CloudRedirectInstallTransactionEntry item) => PathsEqual(item.OriginalPath, path));
		string hash;
		if (!ExistingFile(path))
		{
			if (cloudRedirectInstallTransactionEntry.Existed && (!deleting || !cloudRedirectInstallTransactionEntry.PostImageDeleted))
			{
				throw new InvalidDataException("File có sẵn đã bị mất; giữ nguyên trạng thái để kiểm tra.");
			}
		}
		else if (!TryComputeSha256(path, out hash) || ((!cloudRedirectInstallTransactionEntry.Existed || !hash.Equals(cloudRedirectInstallTransactionEntry.PreImageSha256, StringComparison.OrdinalIgnoreCase)) && (deleting || !hash.Equals(cloudRedirectInstallTransactionEntry.PostImageSha256, StringComparison.OrdinalIgnoreCase))))
		{
			throw new InvalidDataException("File thay đổi ngoài transaction; đã dừng thao tác.");
		}
	}

	internal CloudRedirectRuntimeService ForRecovery(SteamRecoveryOperation operation)
	{
		SteamRecoveryPhase phase = operation.Phase;
		if ((uint)(phase - 2) <= 3u)
		{
			return this;
		}
		CloudRedirectInstallTransaction cloudRedirectInstallTransaction = ReadActivationReceipt(operation);
		CloudRedirectRuntimeProfile cloudRedirectRuntimeProfile = CloudRedirectRuntimeProfile.Resolve(cloudRedirectInstallTransaction.BundleId, cloudRedirectInstallTransaction.BundleVersion);
		return new CloudRedirectRuntimeService(storefrontApiClient, currentDesktopVersion, cloudRedirectRuntimeProfile);
	}

	internal SteamInstallationDisposition ReconcileActivationInstallation(SteamRecoveryOperation operation, string? localAppDataOverride = null, string? roamingAppDataOverride = null)
	{
		string transactionPath = operation.TransactionPath;
		CloudRedirectInstallTransaction cloudRedirectInstallTransaction = ReadActivationReceipt(operation);
		CloudRedirectRuntimeProfile profile = CloudRedirectRuntimeProfile.Resolve(cloudRedirectInstallTransaction.BundleId, cloudRedirectInstallTransaction.BundleVersion);
		return ReconcileReceipt(operation, transactionPath, cloudRedirectInstallTransaction, profile, localAppDataOverride, roamingAppDataOverride);
	}

	private static CloudRedirectInstallTransaction ReadActivationReceipt(SteamRecoveryOperation operation)
	{
		string transactionPath = operation.TransactionPath;
		if (string.IsNullOrEmpty(transactionPath))
		{
			throw new SteamRecoveryPendingException("Thiếu bằng chứng cài đặt; launcher giữ nguyên file để kiểm tra.");
		}
		EnsureRecoveryPathIsNotLinked(transactionPath);
		CloudRedirectInstallTransaction cloudRedirectInstallTransaction;
		try
		{
			cloudRedirectInstallTransaction = CloudRedirectTransactionJournal.Read(transactionPath, requireProtected: true);
		}
		catch (Exception ex) when (((ex is IOException || ex is UnauthorizedAccessException || ex is JsonException) ? 1 : 0) != 0)
		{
			throw new SteamRecoveryPendingException("Chưa đọc được transaction cài đặt; launcher giữ nguyên file.", ex);
		}
		bool flag = !PathsEqual(cloudRedirectInstallTransaction.SteamDirectory, operation.SteamDirectory) || cloudRedirectInstallTransaction.Entries == null || cloudRedirectInstallTransaction.Entries.Any((CloudRedirectInstallTransactionEntry entry) => entry == null);
		if (!flag)
		{
			bool flag2;
			switch (cloudRedirectInstallTransaction.Operation)
			{
			case "install":
			case "upgrade":
			case "repair":
			case "enable":
			case "activate":
			case "recover":
				flag2 = true;
				break;
			default:
				flag2 = false;
				break;
			}
			flag = !flag2;
		}
		if (flag)
		{
			throw new InvalidDataException("Transaction không thuộc lần cài đặt Steam này.");
		}
		return cloudRedirectInstallTransaction;
	}

	private SteamInstallationDisposition ReconcileReceipt(SteamRecoveryOperation operation, string path, CloudRedirectInstallTransaction transaction, CloudRedirectRuntimeProfile profile, string? localAppDataOverride, string? roamingAppDataOverride)
	{
		ValidateRollbackTransaction(path, transaction, operation.SteamDirectory, localAppDataOverride ?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), roamingAppDataOverride ?? Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), requireReadableBackups: true);
		foreach (CloudRedirectInstallTransactionEntry entry in transaction.Entries)
		{
			EnsureRecoveryPathIsNotLinked(entry.OriginalPath);
			if (!string.IsNullOrEmpty(entry.BackupPath))
			{
				EnsureRecoveryPathIsNotLinked(entry.BackupPath);
			}
		}
		bool flag;
		switch (transaction.Status)
		{
		case "rolled_back":
		case "rolled_back_after_health_failure":
		case "recovered_after_interruption":
			flag = true;
			break;
		default:
			flag = false;
			break;
		}
		if (flag)
		{
			return SteamInstallationDisposition.RolledBack;
		}
		switch (transaction.Status)
		{
		case "installing":
		case "rollback_failed":
		case "recovery_failed":
			flag = true;
			break;
		default:
			flag = false;
			break;
		}
		if (flag)
		{
			return SteamInstallationDisposition.Incomplete;
		}
		if (transaction.Status != "completed" || (transaction.Entries.Count == 0 && !transaction.ActivationReceipt))
		{
			throw new InvalidDataException("Transaction không có bằng chứng commit hợp lệ.");
		}
		if (operation.SteamAppId <= 0 || operation.ActivationFiles == null || operation.ActivationFiles.Count == 0)
		{
			throw new SteamRecoveryPendingException("Journal cũ thiếu danh sách file để xác minh; launcher giữ nguyên cài đặt.");
		}
		foreach (CloudRedirectInstallTransactionEntry entry2 in transaction.Entries)
		{
			if (entry2.PostImageDeleted)
			{
				try
				{
					File.GetAttributes(entry2.OriginalPath);
					throw new InvalidDataException("File đã được tạo lại sau cài đặt; launcher giữ nguyên file.");
				}
				catch (FileNotFoundException)
				{
				}
				catch (DirectoryNotFoundException)
				{
				}
			}
			else
			{
				VerifyExpectedFile(entry2.OriginalPath, entry2.PostImageSha256);
			}
		}
		VerifyInstalledRuntime(operation.SteamDirectory, profile, operation.BetterOnly);
		VerifyActivationFiles(operation.SteamDirectory, operation.SteamAppId, operation.ActivationFiles);
		if (!BetterSteamToolsConfiguration.IsReady(File.ReadAllText(Path.Combine(operation.SteamDirectory, "opensteamtool.toml")), operation.SteamDirectory, !operation.BetterOnly))
		{
			throw new InvalidDataException("Cấu hình host đã thay đổi sau cài đặt; launcher giữ nguyên file.");
		}
		operation.RequiresSteamRestart = operation.RequiresSteamRestart || operation.BetterOnly || transaction.Entries.Count > 0;
		return SteamInstallationDisposition.Committed;
	}

	private static void VerifyExpectedFile(string path, string expectedHash)
	{
		EnsureRecoveryPathIsNotLinked(path);
		if (string.IsNullOrEmpty(expectedHash) || !Sha256Regex().IsMatch(expectedHash))
		{
			throw new InvalidDataException("Thiếu hash để xác minh file đã cài.");
		}
		try
		{
			using FileStream source = File.OpenRead(path);
			if (!string.Equals(Convert.ToHexString(SHA256.HashData(source)), expectedHash, StringComparison.OrdinalIgnoreCase))
			{
				throw new InvalidDataException("File đã thay đổi: " + Path.GetFileName(path) + ". Launcher giữ nguyên file.");
			}
		}
		catch (Exception ex) when (((ex is FileNotFoundException || ex is DirectoryNotFoundException) ? 1 : 0) != 0)
		{
			throw new InvalidDataException("Thiếu file đã cài: " + Path.GetFileName(path) + ".", ex);
		}
		catch (Exception ex2) when (((ex2 is IOException || ex2 is UnauthorizedAccessException) ? 1 : 0) != 0)
		{
			throw new SteamRecoveryPendingException("Chưa đọc được file để xác minh; launcher giữ nguyên cài đặt.", ex2);
		}
	}

	private static void VerifyInstalledRuntime(string steamDirectory, CloudRedirectRuntimeProfile profile, bool betterOnly)
	{
		foreach (DesktopRuntimeBundleFileResponse value in profile.RequiredFiles(betterOnly).Values)
		{
			VerifyExpectedFile(ResolveContainedPath(steamDirectory, value.InstallPath), value.Sha256);
		}
	}

	private static void VerifyActivationFiles(string steamDirectory, int steamAppId, IReadOnlyList<SteamActivationFileStamp> files)
	{
		if (steamAppId <= 0 || files.Count == 0 || !files.Any((SteamActivationFileStamp file) => file.InstallPath.EndsWith(".lua", StringComparison.OrdinalIgnoreCase)) || !files.Any((SteamActivationFileStamp file) => file.InstallPath.EndsWith(".manifest", StringComparison.OrdinalIgnoreCase)))
		{
			throw new InvalidDataException("Thiếu Lua/manifest của lần ACTIVE.");
		}
		foreach (SteamActivationFileStamp file in files)
		{
			if (!IsActivationFilePath(file.InstallPath))
			{
				throw new InvalidDataException("Invalid activation file path.");
			}
			VerifyExpectedFile(ResolveContainedPath(steamDirectory, file.InstallPath), file.Sha256);
		}
		Regex registration = new Regex($"\\baddappid\\s*\\(\\s*{steamAppId}\\s*(?:,|\\))", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
		if (!files.Where((SteamActivationFileStamp file) => file.InstallPath.EndsWith(".lua", StringComparison.OrdinalIgnoreCase)).Any((SteamActivationFileStamp file) => File.ReadLines(ResolveContainedPath(steamDirectory, file.InstallPath)).Any((string line) => !line.TrimStart().StartsWith("--", StringComparison.Ordinal) && registration.IsMatch(line))))
		{
			throw new InvalidDataException("Lua chưa đăng ký đúng game đang ACTIVE.");
		}
	}
}
