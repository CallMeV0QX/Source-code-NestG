using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using ICSharpCode.SharpZipLib;
using UI_Desktop.Store;
using app_desktop.Models;

namespace app_desktop.Services;

internal sealed class DenuvoFixInstallService(StorefrontApiClient storefrontApiClient, Func<DesktopLibraryDetailResponse, string?>? findInstallDirectory = null, Func<CancellationToken, Task<string>>? getRealtimeConnectionId = null)
{
	private sealed class DenuvoFixLocalInstallState
	{
		public Guid AssignmentId { get; set; }

		public Guid LibraryId { get; set; }

		public Guid GameId { get; set; }

		public string GameName { get; set; } = string.Empty;

		public string VersionName { get; set; } = string.Empty;

		public string ConfigRelativePath { get; set; } = "steam_settings/configs.user.ini";

		public string? ConfigFullPath { get; set; }

		public string? MainExeRelativePath { get; set; }

		public string? SteamShortcutName { get; set; }

		public string? LaunchArguments { get; set; }

		public string? LaunchMode { get; set; }

		public string? GameExePath { get; set; }

		public string? GameInstallPath { get; set; }

		public int? LastAppliedRevisionNumber { get; set; }

		public string? PackageSha256 { get; set; }

		public string? PackageVersion { get; set; }

		public string? ClientVersion { get; set; }

		public DateTime? AdminRequestCooldownUntilUtc { get; set; }

		public DateTime UpdatedAtUtc { get; set; }
	}

	private static readonly TimeSpan AdminRequestCooldownDuration = TimeSpan.FromHours(24.0);

	private static readonly JsonSerializerOptions StateJsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
	{
		PropertyNameCaseInsensitive = true,
		WriteIndented = true
	};

	public async Task<DenuvoFixInstallResult> InstallAsync(DesktopLibraryDetailResponse detail, string accessToken, IWin32Window? owner = null, CancellationToken cancellationToken = default(CancellationToken), IProgress<DenuvoInstallProgress>? progress = null)
	{
		DesktopLibraryDenuvoFixResponse denuvo = detail.DenuvoFix;
		if (string.IsNullOrWhiteSpace(accessToken))
		{
			throw new InvalidOperationException("Bạn cần đăng nhập để nạp game Denuvo.");
		}
		if (denuvo != null && denuvo.HasDenuvoFix && !denuvo.AssignmentId.HasValue)
		{
			denuvo = await storefrontApiClient.EnsureLibraryDenuvoFixAsync(detail.LibraryId, accessToken, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			detail.DenuvoFix = denuvo;
		}
		if (denuvo == null || !denuvo.HasDenuvoFix)
		{
			throw new InvalidOperationException("Game này chưa có Denuvo Fix được gắn cho tài khoản.");
		}
		if (string.Equals(denuvo.Status, "revoked", StringComparison.OrdinalIgnoreCase))
		{
			throw new InvalidOperationException("Denuvo Fix của game này đã bị revoke.");
		}
		Guid trackingId = denuvo.AssignmentId ?? detail.LibraryId;
		string workingDirectory = CreateWorkingDirectory();
		string text = Path.Combine(workingDirectory, "downloads");
		Directory.CreateDirectory(text);
		List<string> messages = new List<string>();
		bool shouldReportInstallError = false;
		string installDirectory = null;
		try
		{
			installDirectory = ((findInstallDirectory == null) ? ResolveInstallDirectory(detail, denuvo, owner) : findInstallDirectory(detail));
			if (string.IsNullOrWhiteSpace(installDirectory))
			{
				throw new InvalidOperationException("Bạn cần tải và cài đặt game hoàn tất trên Steam trước khi yêu cầu admin nạp game.");
			}
			EnsureGameInstallDirectoryReady(installDirectory);
			if (string.IsNullOrWhiteSpace(denuvo.PackageDownloadUrl))
			{
				throw new InvalidOperationException("Game này chưa có package Denuvo active.");
			}
			progress?.Report(new DenuvoInstallProgress(DenuvoInstallStage.Downloading));
			DesktopDownloadedFileResponse package = await storefrontApiClient.DownloadLibraryDenuvoFixPackageAsync(detail.LibraryId, accessToken, text, ResolvePackageFallbackFileName(detail, denuvo), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			shouldReportInstallError = true;
			await TryPostEventAsync(detail.LibraryId, accessToken, "install_started", package.ConfigRevisionNumber ?? denuvo.ActiveConfigRevisionNumber, null, installDirectory, "Launcher bắt đầu nạp Denuvo Fix.", cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			int? appliedRevision = package.ConfigRevisionNumber ?? denuvo.ActiveConfigRevisionNumber ?? denuvo.LastAppliedRevisionNumber;
			DenuvoFixLocalInstallState state = null;
			DenuvoFileInstallResult installResult = await InstallPackageAsync(package.FullPath, installDirectory, denuvo.ConfigRelativePath, trackingId, package.ZipPassword, messages, delegate(DenuvoFileInstallResult result)
			{
				state = new DenuvoFixLocalInstallState
				{
					AssignmentId = trackingId,
					LibraryId = detail.LibraryId,
					GameId = detail.GameId,
					GameName = detail.GameName,
					VersionName = detail.VersionName,
					ConfigRelativePath = NormalizeRelativePath(denuvo.ConfigRelativePath),
					ConfigFullPath = result.ConfigFullPath,
					MainExeRelativePath = denuvo.MainExeRelativePath,
					SteamShortcutName = denuvo.SteamShortcutName,
					LaunchArguments = denuvo.LaunchArguments,
					LaunchMode = denuvo.LaunchMode,
					GameExePath = ResolveGameExePath(installDirectory, denuvo.MainExeRelativePath, denuvo.LaunchArguments, messages),
					GameInstallPath = installDirectory,
					LastAppliedRevisionNumber = appliedRevision,
					PackageSha256 = null,
					PackageVersion = denuvo.PackageVersion,
					ClientVersion = ResolveClientVersion(),
					UpdatedAtUtc = DateTime.UtcNow
				};
				return JsonSerializer.Serialize(state, StateJsonOptions);
			}, cancellationToken, progress).ConfigureAwait(continueOnCapturedContext: false);
			shouldReportInstallError = false;
			progress?.Report(new DenuvoInstallProgress(DenuvoInstallStage.RequestingToken));
			cancellationToken.ThrowIfCancellationRequested();
			string realtimeConnectionId = await ((Task<string>)((getRealtimeConnectionId == null) ? ((Task)DesktopAppContext.DenuvoActiveRealtimeClient.GetConnectionIdForRequestAsync(cancellationToken)) : ((Task)getRealtimeConnectionId(cancellationToken)))).ConfigureAwait(continueOnCapturedContext: false);
			DesktopDenuvoActivePendingItemResponse desktopDenuvoActivePendingItemResponse = await storefrontApiClient.RequestDenuvoAdminTokenAsync(detail.LibraryId, new DesktopDenuvoActiveClientStateRequest
			{
				RealtimeConnectionId = realtimeConnectionId,
				AppliedRevisionNumber = appliedRevision,
				GameInstallPath = installDirectory,
				ClientVersion = ResolveClientVersion(),
				Message = $"Khách yêu cầu admin cấp token sau khi cài {installResult.WrittenFiles.Count} file."
			}, accessToken, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			string logFilePath = WriteInstallLog(detail, denuvo, installDirectory, installResult, messages);
			return new DenuvoFixInstallResult(installDirectory, installResult.WrittenFiles, installResult.OverwrittenFiles, installResult.BackupDirectory, logFilePath, null, denuvo.PackageVersion, appliedRevision, state.ConfigRelativePath, installResult.ConfigFullPath, state.MainExeRelativePath, state.GameExePath, messages, desktopDenuvoActivePendingItemResponse.ActiveRequestId);
		}
		catch (OperationCanceledException)
		{
			throw;
		}
		catch (Exception ex2)
		{
			Exception ex3 = ex2;
			string logPath = WriteErrorLog(detail, denuvo, ex3);
			if (shouldReportInstallError)
			{
				await TryPostEventAsync(detail.LibraryId, accessToken, "error", denuvo.ActiveConfigRevisionNumber, null, installDirectory ?? denuvo.GameInstallPath, ex3.Message, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			}
			messages.Add("Log: " + logPath);
			ExceptionDispatchInfo.Capture((ex2 as Exception) ?? throw ex2).Throw();
		}
		finally
		{
			TryDeleteDirectory(workingDirectory);
		}
		throw null;
	}

	public async Task<DenuvoFixInstallResult> InstallActiveTokenPackageAsync(DesktopDenuvoActivePendingItemResponse item, string accessToken, string realtimeConnectionId, CancellationToken cancellationToken = default(CancellationToken))
	{
		if (!(item.LibraryId == Guid.Empty) && !(item.ActiveRequestId == Guid.Empty))
		{
			int? tokenRevisionNumber = item.TokenRevisionNumber;
			if (tokenRevisionNumber.HasValue && tokenRevisionNumber.GetValueOrDefault() > 0)
			{
				DateTime? tokenExpiresAt = item.TokenExpiresAt;
				if (tokenExpiresAt.HasValue)
				{
					DateTime valueOrDefault = tokenExpiresAt.GetValueOrDefault();
					if (!(valueOrDefault <= DateTime.UtcNow))
					{
						if (string.IsNullOrWhiteSpace(accessToken) || string.IsNullOrWhiteSpace(realtimeConnectionId))
						{
							throw new InvalidOperationException("Phiên nhận token không còn hợp lệ.");
						}
						string installDirectory = ResolveActiveInstallDirectory(item);
						if (string.IsNullOrWhiteSpace(installDirectory))
						{
							throw new InvalidOperationException("Chưa tìm thấy thư mục game Steam để apply Denuvo Active.");
						}
						EnsureGameInstallDirectoryReady(installDirectory);
						Guid trackingId = ((item.AssignmentId == Guid.Empty) ? item.LibraryId : item.AssignmentId);
						if (!HasInstalledDenuvoPackageMarker(installDirectory, item.ConfigRelativePath))
						{
							string workingDirectory = CreateWorkingDirectory();
							try
							{
								string text = Path.Combine(workingDirectory, "downloads");
								Directory.CreateDirectory(text);
								DesktopDownloadedFileResponse desktopDownloadedFileResponse = await storefrontApiClient.DownloadLibraryDenuvoFixPackageAsync(item.LibraryId, accessToken, text, ResolveActivePackageFallbackFileName(item), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
								await InstallPackageAsync(desktopDownloadedFileResponse.FullPath, installDirectory, item.ConfigRelativePath, trackingId, desktopDownloadedFileResponse.ZipPassword, new List<string>(), null, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
							}
							finally
							{
								TryDeleteDirectory(workingDirectory);
							}
						}
						return await InstallActiveTokenConfigAsync(item, accessToken, installDirectory, trackingId, realtimeConnectionId, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
					}
				}
				throw new InvalidOperationException("Token Denuvo Active đã hết hạn trước khi launcher apply.");
			}
		}
		throw new InvalidOperationException("Request Denuvo Active không hợp lệ.");
	}

	private async Task<DenuvoFixInstallResult> InstallActiveTokenConfigAsync(DesktopDenuvoActivePendingItemResponse item, string accessToken, string installDirectory, Guid activeTrackingId, string realtimeConnectionId, CancellationToken cancellationToken)
	{
		string configRelativePath = NormalizeRelativePath(item.ConfigRelativePath);
		string targetConfigPath = ResolveConfigFullPath(installDirectory, configRelativePath);
		if (string.IsNullOrWhiteSpace(Path.GetDirectoryName(targetConfigPath)))
		{
			throw new InvalidOperationException("Đường dẫn config Denuvo Active không hợp lệ.");
		}
		string workingDirectory = CreateWorkingDirectory();
		List<string> messages = new List<string> { "Đã phát hiện steam_settings local, chỉ tải config token từ admin." };
		try
		{
			DesktopDownloadedFileResponse downloadedConfig = await storefrontApiClient.DownloadDenuvoActiveTokenConfigAsync(item.ActiveRequestId, item.TokenRevisionNumber.Value, realtimeConnectionId, accessToken, workingDirectory, Path.GetFileName(configRelativePath), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			if (downloadedConfig.ConfigRevisionNumber != item.TokenRevisionNumber)
			{
				throw new InvalidOperationException("Server trả về sai revision token.");
			}
			int? appliedRevision = downloadedConfig.ConfigRevisionNumber ?? item.TokenRevisionNumber;
			string packageVersion = (appliedRevision.HasValue ? $"active-rev-{appliedRevision.Value}" : "active-token");
			DenuvoFixLocalInstallState state = null;
			DenuvoFileInstallResult denuvoFileInstallResult = await DenuvoFileTransaction.InstallConfigAsync(downloadedConfig.FullPath, installDirectory, configRelativePath, activeTrackingId, delegate
			{
				state = new DenuvoFixLocalInstallState
				{
					AssignmentId = activeTrackingId,
					LibraryId = item.LibraryId,
					GameId = item.GameId,
					GameName = item.GameName,
					VersionName = item.VersionName,
					ConfigRelativePath = configRelativePath,
					ConfigFullPath = targetConfigPath,
					MainExeRelativePath = item.MainExeRelativePath,
					SteamShortcutName = item.SteamShortcutName,
					LaunchArguments = item.LaunchArguments,
					LaunchMode = item.LaunchMode,
					GameExePath = ResolveGameExePath(installDirectory, item.MainExeRelativePath, item.LaunchArguments, messages),
					GameInstallPath = installDirectory,
					LastAppliedRevisionNumber = appliedRevision,
					PackageSha256 = downloadedConfig.Sha256,
					PackageVersion = packageVersion,
					ClientVersion = ResolveClientVersion(),
					UpdatedAtUtc = DateTime.UtcNow
				};
				return JsonSerializer.Serialize(state, StateJsonOptions);
			}, async delegate(CancellationToken token)
			{
				await storefrontApiClient.PostDenuvoActiveDeliveredAsync(item.ActiveRequestId, new DesktopDenuvoActiveClientStateRequest
				{
					RealtimeConnectionId = realtimeConnectionId,
					AppliedRevisionNumber = item.TokenRevisionNumber,
					GameInstallPath = installDirectory
				}, accessToken, token).ConfigureAwait(continueOnCapturedContext: false);
			}, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			messages.Add("Đã apply config token Denuvo Active.");
			return new DenuvoFixInstallResult(installDirectory, denuvoFileInstallResult.WrittenFiles, denuvoFileInstallResult.OverwrittenFiles, denuvoFileInstallResult.BackupDirectory, string.Empty, downloadedConfig.Sha256, packageVersion, appliedRevision, state.ConfigRelativePath, state.ConfigFullPath, state.MainExeRelativePath, state.GameExePath, messages, null);
		}
		finally
		{
			TryDeleteDirectory(workingDirectory);
		}
	}

	public async Task<IReadOnlyList<DenuvoFixConfigUpdateResult>> RunPendingConfigUpdatesAsync(string accessToken, CancellationToken cancellationToken = default(CancellationToken))
	{
		List<DenuvoFixLocalInstallState> states = LoadLocalStates();
		if (states.Count == 0 || string.IsNullOrWhiteSpace(accessToken))
		{
			return Array.Empty<DenuvoFixConfigUpdateResult>();
		}
		DesktopDenuvoFixLocalStateRequest[] array = (from item in states
			where item.AssignmentId != Guid.Empty && item.LibraryId != Guid.Empty
			select new DesktopDenuvoFixLocalStateRequest
			{
				LibraryId = item.LibraryId,
				AssignmentId = item.AssignmentId,
				LastAppliedRevisionNumber = item.LastAppliedRevisionNumber,
				GameInstallPath = item.GameInstallPath,
				ConfigRelativePath = item.ConfigRelativePath,
				ClientVersion = ResolveClientVersion()
			}).ToArray();
		if (array.Length == 0)
		{
			return Array.Empty<DenuvoFixConfigUpdateResult>();
		}
		DesktopDenuvoFixUpdateCheckResponse desktopDenuvoFixUpdateCheckResponse = await storefrontApiClient.CheckDenuvoFixUpdatesAsync(array, accessToken, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		if (desktopDenuvoFixUpdateCheckResponse.Updates.Count == 0)
		{
			return Array.Empty<DenuvoFixConfigUpdateResult>();
		}
		List<DenuvoFixConfigUpdateResult> results = new List<DenuvoFixConfigUpdateResult>();
		foreach (DesktopDenuvoFixUpdateItemResponse update in desktopDenuvoFixUpdateCheckResponse.Updates)
		{
			cancellationToken.ThrowIfCancellationRequested();
			DenuvoFixLocalInstallState denuvoFixLocalInstallState = states.FirstOrDefault((DenuvoFixLocalInstallState item) => item.AssignmentId == update.AssignmentId);
			if (denuvoFixLocalInstallState == null)
			{
				continue;
			}
			try
			{
				DenuvoFixConfigUpdateResult denuvoFixConfigUpdateResult = await ApplyConfigUpdateAsync(update, denuvoFixLocalInstallState, accessToken, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				if ((object)denuvoFixConfigUpdateResult != null)
				{
					results.Add(denuvoFixConfigUpdateResult);
				}
			}
			catch (Exception ex) when (!(ex is OperationCanceledException))
			{
				WriteAutoUpdateLog(update, ex);
			}
		}
		return results;
	}

	public static Task<IReadOnlyList<DenuvoFixConfigUpdateResult>> ApplyPendingConfigUpdatesAsync(StorefrontApiClient storefrontApiClient, string accessToken, CancellationToken cancellationToken = default(CancellationToken))
	{
		return new DenuvoFixInstallService(storefrontApiClient).RunPendingConfigUpdatesAsync(accessToken, cancellationToken);
	}

	public static DateTime? GetAdminRequestCooldownUntilUtc(Guid libraryId)
	{
		if (libraryId == Guid.Empty)
		{
			return null;
		}
		DateTime utcNow = DateTime.UtcNow;
		DateTime dateTime = (from item in LoadLocalStates()
			where item.LibraryId == libraryId && item.AdminRequestCooldownUntilUtc.HasValue
			select item.AdminRequestCooldownUntilUtc.Value.ToUniversalTime() into item
			where item > utcNow
			select item).DefaultIfEmpty().Max();
		if (!(dateTime == default(DateTime)))
		{
			return dateTime;
		}
		return null;
	}

	public static DenuvoFixCooldownGuideState? GetAdminRequestCooldownGuideState(Guid libraryId)
	{
		if (libraryId == Guid.Empty)
		{
			return null;
		}
		DateTime utcNow = DateTime.UtcNow;
		DenuvoFixLocalInstallState denuvoFixLocalInstallState = (from item in LoadLocalStates()
			where item.LibraryId == libraryId && item.AdminRequestCooldownUntilUtc.HasValue && item.AdminRequestCooldownUntilUtc.Value.ToUniversalTime() > utcNow
			orderby item.AdminRequestCooldownUntilUtc.Value.ToUniversalTime() descending
			select item).FirstOrDefault();
		if (denuvoFixLocalInstallState == null)
		{
			return null;
		}
		return new DenuvoFixCooldownGuideState(denuvoFixLocalInstallState.LibraryId, denuvoFixLocalInstallState.GameName, denuvoFixLocalInstallState.VersionName, denuvoFixLocalInstallState.GameExePath, denuvoFixLocalInstallState.AdminRequestCooldownUntilUtc.Value.ToUniversalTime());
	}

	public static DateTime MarkAdminRequestCooldown(Guid libraryId, Guid assignmentId, Guid gameId, string gameName, DateTime appliedAtUtc)
	{
		DateTime dateTime = appliedAtUtc.ToUniversalTime().Add(AdminRequestCooldownDuration);
		DenuvoFixLocalInstallState denuvoFixLocalInstallState = LoadLocalStates().FirstOrDefault((DenuvoFixLocalInstallState item) => (assignmentId != Guid.Empty && item.AssignmentId == assignmentId) || (libraryId != Guid.Empty && item.LibraryId == libraryId));
		if (denuvoFixLocalInstallState == null)
		{
			denuvoFixLocalInstallState = new DenuvoFixLocalInstallState
			{
				AssignmentId = ((assignmentId == Guid.Empty) ? libraryId : assignmentId),
				LibraryId = libraryId,
				GameId = gameId,
				GameName = gameName,
				UpdatedAtUtc = appliedAtUtc.ToUniversalTime()
			};
		}
		denuvoFixLocalInstallState.AdminRequestCooldownUntilUtc = dateTime;
		denuvoFixLocalInstallState.UpdatedAtUtc = appliedAtUtc.ToUniversalTime();
		SaveLocalState(denuvoFixLocalInstallState);
		return dateTime;
	}

	public static bool ClearAdminRequestCooldown(Guid libraryId)
	{
		if (libraryId == Guid.Empty)
		{
			return false;
		}
		DateTime utcNow = DateTime.UtcNow;
		bool result = false;
		foreach (DenuvoFixLocalInstallState item in from item in LoadLocalStates()
			where item.LibraryId == libraryId
			select item)
		{
			if (item.AdminRequestCooldownUntilUtc.HasValue)
			{
				item.AdminRequestCooldownUntilUtc = null;
				item.UpdatedAtUtc = utcNow;
				SaveLocalState(item);
				result = true;
			}
		}
		return result;
	}

	public static string BuildAdminRequestCooldownMessage(DateTime cooldownUntilUtc)
	{
		TimeSpan timeSpan = cooldownUntilUtc.ToUniversalTime() - DateTime.UtcNow;
		if (timeSpan <= TimeSpan.Zero)
		{
			return "Bạn có thể yêu cầu admin nạp game lại.";
		}
		int value = (int)Math.Floor(timeSpan.TotalHours);
		return $"Bạn vừa nạp game thành công. Vui lòng thử lại sau {value:00}:{timeSpan.Minutes:00}:{timeSpan.Seconds:00}.";
	}

	private async Task<DenuvoFixConfigUpdateResult?> ApplyConfigUpdateAsync(DesktopDenuvoFixUpdateItemResponse update, DenuvoFixLocalInstallState state, string accessToken, CancellationToken cancellationToken)
	{
		string configRelativePath = NormalizeRelativePath(update.ConfigRelativePath);
		string targetConfigPath = ResolveConfigFullPath(state, configRelativePath);
		if (string.IsNullOrWhiteSpace(targetConfigPath))
		{
			throw new InvalidOperationException("Không xác định được file config Denuvo Fix local.");
		}
		string directoryName = Path.GetDirectoryName(targetConfigPath);
		if (string.IsNullOrWhiteSpace(directoryName))
		{
			throw new InvalidOperationException("Đường dẫn config Denuvo Fix local không hợp lệ.");
		}
		if (!string.IsNullOrWhiteSpace(state.GameInstallPath) && !Directory.Exists(state.GameInstallPath))
		{
			throw new DirectoryNotFoundException("Không tìm thấy thư mục game đã lưu: " + state.GameInstallPath);
		}
		if (!Directory.Exists(directoryName))
		{
			throw new DirectoryNotFoundException("Không tìm thấy thư mục config Denuvo Fix: " + directoryName);
		}
		string workingDirectory = CreateWorkingDirectory();
		try
		{
			DesktopDownloadedFileResponse desktopDownloadedFileResponse = await storefrontApiClient.DownloadLibraryDenuvoFixConfigAsync(update.LibraryId, accessToken, workingDirectory, Path.GetFileName(configRelativePath), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			if (desktopDownloadedFileResponse.ConfigRevisionNumber != update.ActiveRevisionNumber)
			{
				throw new InvalidDataException("Revision config tải về không khớp bản cập nhật; giữ nguyên config hiện tại.");
			}
			if (string.IsNullOrWhiteSpace(state.GameInstallPath) || !string.Equals(Path.GetFullPath(targetConfigPath), ResolveConfigFullPath(state.GameInstallPath, configRelativePath), StringComparison.OrdinalIgnoreCase))
			{
				throw new InvalidDataException("Config Denuvo nằm ngoài thư mục game đã lưu.");
			}
			DenuvoFileInstallResult installed;
			try
			{
				installed = await DenuvoFileTransaction.InstallConfigAsync(desktopDownloadedFileResponse.FullPath, state.GameInstallPath, configRelativePath, update.AssignmentId, delegate
				{
					state.ConfigRelativePath = configRelativePath;
					state.ConfigFullPath = targetConfigPath;
					state.LastAppliedRevisionNumber = update.ActiveRevisionNumber;
					state.ClientVersion = ResolveClientVersion();
					state.UpdatedAtUtc = DateTime.UtcNow;
					return JsonSerializer.Serialize(state, StateJsonOptions);
				}, delegate
				{
					DenuvoFixLocalInstallState denuvoFixLocalInstallState = LoadLocalStates().FirstOrDefault((DenuvoFixLocalInstallState item) => item.AssignmentId == update.AssignmentId);
					if (denuvoFixLocalInstallState == null || denuvoFixLocalInstallState.LastAppliedRevisionNumber >= update.ActiveRevisionNumber)
					{
						throw new DenuvoUpdateSupersededException();
					}
					if (!string.Equals(denuvoFixLocalInstallState.GameInstallPath, state.GameInstallPath, StringComparison.OrdinalIgnoreCase) || !string.Equals(denuvoFixLocalInstallState.ConfigFullPath, state.ConfigFullPath, StringComparison.OrdinalIgnoreCase))
					{
						throw new InvalidDataException("Đường dẫn Denuvo đã thay đổi trong lúc cập nhật.");
					}
					state = denuvoFixLocalInstallState;
					return Task.CompletedTask;
				}, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			}
			catch (DenuvoUpdateSupersededException)
			{
				return null;
			}
			await TryPostEventAsync(update.LibraryId, accessToken, "config_update_applied", update.ActiveRevisionNumber, null, state.GameInstallPath, "Đã apply revision config Denuvo Fix mới.", cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			return new DenuvoFixConfigUpdateResult(update.LibraryId, update.AssignmentId, update.GameName, update.ActiveRevisionNumber, targetConfigPath, installed.BackupDirectory);
		}
		finally
		{
			TryDeleteDirectory(workingDirectory);
		}
	}

	private static async Task<DenuvoFileInstallResult> InstallPackageAsync(string packagePath, string installDirectory, string configRelativePath, Guid assignmentId, string? zipPassword, ICollection<string> messages, Func<DenuvoFileInstallResult, string>? stateFactory, CancellationToken cancellationToken, IProgress<DenuvoInstallProgress>? progress = null)
	{
		if (!IsZipFile(packagePath))
		{
			throw new InvalidOperationException("Package Denuvo Fix phải là file .zip.");
		}
		DenuvoFileInstallResult denuvoFileInstallResult;
		try
		{
			denuvoFileInstallResult = await DenuvoFileTransaction.InstallZipAsync(packagePath, installDirectory, NormalizeRelativePath(configRelativePath), assignmentId, zipPassword, stateFactory, cancellationToken, progress).ConfigureAwait(continueOnCapturedContext: false);
		}
		catch (SharpZipBaseException innerException)
		{
			throw new InvalidOperationException(BuildZipReadErrorMessage(HasZipPassword(zipPassword)), innerException);
		}
		if (denuvoFileInstallResult.ConfigFullPath == null)
		{
			messages.Add("Không tìm thấy config sau khi giải nén: " + NormalizeRelativePath(configRelativePath));
		}
		messages.Add($"Package ZIP có password: {HasZipPassword(zipPassword)}");
		messages.Add($"File đã cài: {denuvoFileInstallResult.WrittenFiles.Count}");
		messages.Add($"File đã ghi đè: {denuvoFileInstallResult.OverwrittenFiles.Count}");
		return denuvoFileInstallResult;
	}

	private static string? ResolveInstallDirectory(DesktopLibraryDetailResponse detail, DesktopLibraryDenuvoFixResponse denuvo, IWin32Window? owner)
	{
		string text = FindSteamGameInstallDirectory(denuvo.SteamAppId, denuvo.DefaultInstallFolderName, detail.GameName);
		if (!string.IsNullOrWhiteSpace(text))
		{
			return text;
		}
		return null;
	}

	public static string? FindSteamGameInstallDirectory(int? steamAppId, string? defaultInstallFolderName, string? gameName = null)
	{
		string text = SteamInstallLocator.FindSteamDirectory();
		if (string.IsNullOrWhiteSpace(text))
		{
			return null;
		}
		foreach (string item in EnumerateSteamLibraryRoots(text))
		{
			string path = Path.Combine(item, "steamapps");
			if (steamAppId.HasValue && steamAppId.GetValueOrDefault() > 0)
			{
				string text2 = Path.Combine(path, $"appmanifest_{steamAppId.Value}.acf");
				if (File.Exists(text2))
				{
					string text3 = ParseSteamManifestInstallDirectory(text2);
					if (!string.IsNullOrWhiteSpace(text3))
					{
						string text4 = Path.Combine(path, "common", text3);
						if (Directory.Exists(text4))
						{
							return text4;
						}
					}
				}
			}
			foreach (string item2 in BuildInstallFolderCandidates(defaultInstallFolderName, gameName))
			{
				string text5 = Path.Combine(path, "common", item2);
				if (Directory.Exists(text5))
				{
					return text5;
				}
			}
		}
		return null;
	}

	public static void EnsureGameInstallDirectoryReady(string installDirectory)
	{
		if (string.IsNullOrWhiteSpace(installDirectory) || !Directory.Exists(installDirectory))
		{
			throw new DirectoryNotFoundException("Không tìm thấy thư mục game Steam.");
		}
		if (CountFilesUpTo(installDirectory, 11) <= 10)
		{
			throw new InvalidOperationException("Bạn chưa tải game xong, hãy chờ tải game xong thì mới làm được bước 2.");
		}
	}

	public static bool HasInstalledDenuvoPackageMarker(string? gameInstallPath, string? configRelativePath)
	{
		if (string.IsNullOrWhiteSpace(gameInstallPath) || !Directory.Exists(gameInstallPath))
		{
			return false;
		}
		string path = Path.Combine(gameInstallPath, "steam_settings");
		if (Directory.Exists(path) || File.Exists(path))
		{
			return true;
		}
		try
		{
			string relativePath = NormalizeRelativePath(configRelativePath);
			string fullPath = Path.GetFullPath(Path.Combine(gameInstallPath, ToPlatformRelativePath(relativePath)));
			return IsPathInsideDirectory(fullPath, gameInstallPath) && File.Exists(fullPath);
		}
		catch
		{
			return false;
		}
	}

	private static int CountFilesUpTo(string directory, int limit)
	{
		int num = 0;
		EnumerationOptions enumerationOptions = new EnumerationOptions
		{
			IgnoreInaccessible = true,
			RecurseSubdirectories = true
		};
		try
		{
			foreach (string item in Directory.EnumerateFiles(directory, "*", enumerationOptions))
			{
				_ = item;
				num++;
				if (num >= limit)
				{
					break;
				}
			}
		}
		catch (Exception ex) when (((ex is IOException || ex is UnauthorizedAccessException) ? 1 : 0) != 0)
		{
			throw new InvalidOperationException("Không thể kiểm tra file trong thư mục game. Hãy đóng Steam/antivirus nếu đang khóa file và thử lại.", ex);
		}
		return num;
	}

	private static IReadOnlyList<string> EnumerateSteamLibraryRoots(string steamDirectory)
	{
		HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		AddSteamLibraryRoot(hashSet, steamDirectory);
		string path = Path.Combine(steamDirectory, "steamapps", "libraryfolders.vdf");
		if (!File.Exists(path))
		{
			return hashSet.ToArray();
		}
		try
		{
			string input = File.ReadAllText(path);
			foreach (Match item in Regex.Matches(input, "\"path\"\\s+\"(?<path>(?:\\\\.|[^\"])*)\"", RegexOptions.IgnoreCase))
			{
				AddSteamLibraryRoot(hashSet, UnescapeVdfPath(item.Groups["path"].Value));
			}
			foreach (Match item2 in Regex.Matches(input, "\"\\d+\"\\s+\"(?<path>(?:\\\\.|[^\"])*)\"", RegexOptions.IgnoreCase))
			{
				AddSteamLibraryRoot(hashSet, UnescapeVdfPath(item2.Groups["path"].Value));
			}
		}
		catch
		{
		}
		return hashSet.ToArray();
	}

	private static IReadOnlyList<string> BuildInstallFolderCandidates(string? defaultInstallFolderName, string? gameName)
	{
		List<string> list = new List<string>();
		AddInstallFolderCandidate(list, defaultInstallFolderName);
		AddInstallFolderCandidate(list, gameName);
		if (!string.IsNullOrWhiteSpace(gameName))
		{
			int num = gameName.IndexOf(" - ", StringComparison.Ordinal);
			if (num > 0)
			{
				AddInstallFolderCandidate(list, gameName.Substring(0, num));
			}
		}
		return list.Distinct<string>(StringComparer.OrdinalIgnoreCase).ToArray();
	}

	private static void AddInstallFolderCandidate(ICollection<string> candidates, string? value)
	{
		if (!string.IsNullOrWhiteSpace(value))
		{
			string text = value.Trim().Trim(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
			if (text.Length != 0 && text.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 && !text.Contains(Path.DirectorySeparatorChar) && !text.Contains(Path.AltDirectorySeparatorChar))
			{
				candidates.Add(text);
			}
		}
	}

	private static void AddSteamLibraryRoot(ISet<string> libraries, string? path)
	{
		if (string.IsNullOrWhiteSpace(path))
		{
			return;
		}
		try
		{
			string fullPath = Path.GetFullPath(path.Trim());
			if (Directory.Exists(Path.Combine(fullPath, "steamapps")))
			{
				libraries.Add(fullPath);
			}
		}
		catch
		{
		}
	}

	private static string? ParseSteamManifestInstallDirectory(string manifestPath)
	{
		try
		{
			Match match = Regex.Match(File.ReadAllText(manifestPath), "\"installdir\"\\s+\"(?<dir>[^\"]+)\"", RegexOptions.IgnoreCase);
			return match.Success ? UnescapeVdfPath(match.Groups["dir"].Value) : null;
		}
		catch
		{
			return null;
		}
	}

	private static string UnescapeVdfPath(string value)
	{
		return value.Replace("\\\\", "\\", StringComparison.Ordinal).Replace("\\/", "/", StringComparison.Ordinal);
	}

	private static string NormalizeRelativePath(string? rawPath)
	{
		string input = (string.IsNullOrWhiteSpace(rawPath) ? "steam_settings/configs.user.ini" : rawPath.Trim().Replace('\\', '/'));
		input = Regex.Replace(input, "/+", "/").Trim('/');
		if (string.IsNullOrWhiteSpace(input) || Path.IsPathRooted(input) || input.Split('/').Any((string part) => (part == "." || part == "..") ? true : false))
		{
			throw new InvalidOperationException("Đường dẫn config Denuvo Fix không hợp lệ.");
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
			return "Không thể giải nén package Denuvo Fix. Package có thể đã đặt mật khẩu nhưng server không gửi password.";
		}
		return "Không thể giải nén package Denuvo Fix. Mật khẩu ZIP có thể không đúng hoặc file zip bị lỗi.";
	}

	private static string ResolvePackageFallbackFileName(DesktopLibraryDetailResponse detail, DesktopLibraryDenuvoFixResponse denuvo)
	{
		return SanitizeFileName(string.IsNullOrWhiteSpace(denuvo.PackageVersion) ? detail.GameName : (detail.GameName + "-" + denuvo.PackageVersion)) + "-denuvo-fix.zip";
	}

	private static string? ResolveActiveInstallDirectory(DesktopDenuvoActivePendingItemResponse item)
	{
		if (!string.IsNullOrWhiteSpace(item.GameInstallPath) && Directory.Exists(item.GameInstallPath))
		{
			return item.GameInstallPath;
		}
		return FindSteamGameInstallDirectory(item.SteamAppId, item.DefaultInstallFolderName, item.GameName);
	}

	private static string ResolveActivePackageFallbackFileName(DesktopDenuvoActivePendingItemResponse item)
	{
		string text = (item.TokenRevisionNumber.HasValue ? $"-rev-{item.TokenRevisionNumber.Value}" : string.Empty);
		return SanitizeFileName(item.GameName) + text + "-denuvo-active.zip";
	}

	private static string? ResolveGameExePath(string installDirectory, string? mainExeRelativePath, string? launchArguments, ICollection<string> messages)
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
		string text2 = launchArguments?.Trim();
		if (!string.IsNullOrWhiteSpace(text2) && text2.Contains("{exe}", StringComparison.OrdinalIgnoreCase))
		{
			string text3 = SteamShortcutService.RenderLaunchArguments(text2, text);
			messages.Add("Link tạo shortcut: " + text3);
			return text3;
		}
		messages.Add("File chạy game: " + text);
		return text;
	}

	private static string ResolveClientVersion()
	{
		DesktopAppSettings settings = DesktopAppContext.Settings;
		return $"{settings.CurrentVersion}+{settings.CurrentBuildNumber}";
	}

	private static string CreateWorkingDirectory()
	{
		string text = Path.Combine(Path.GetTempPath(), "NestGLauncher", "denuvo", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(text);
		return text;
	}

	private static string ResolveStateDirectory()
	{
		return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NestGLauncher", "DenuvoFixState");
	}

	private static string ResolveLogDirectory()
	{
		return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NestGLauncher", "DenuvoFixLogs");
	}

	private static List<DenuvoFixLocalInstallState> LoadLocalStates()
	{
		string path = ResolveStateDirectory();
		if (!Directory.Exists(path))
		{
			return new List<DenuvoFixLocalInstallState>();
		}
		List<DenuvoFixLocalInstallState> list = new List<DenuvoFixLocalInstallState>();
		string[] files = Directory.GetFiles(path, "*.json");
		foreach (string path2 in files)
		{
			try
			{
				DenuvoFixLocalInstallState denuvoFixLocalInstallState = JsonSerializer.Deserialize<DenuvoFixLocalInstallState>(File.ReadAllText(path2), StateJsonOptions);
				if (denuvoFixLocalInstallState != null)
				{
					list.Add(denuvoFixLocalInstallState);
				}
			}
			catch
			{
			}
		}
		return list;
	}

	private static void SaveLocalState(DenuvoFixLocalInstallState state)
	{
		string text = ResolveStateDirectory();
		DenuvoFileTransaction.CheckPath(text);
		Directory.CreateDirectory(text);
		string text2 = Path.Combine(text, $"{state.AssignmentId:N}.json");
		DenuvoFileTransaction.CheckPath(text2);
		string text3 = text2 + "." + Guid.NewGuid().ToString("N") + ".tmp";
		bool flag = false;
		try
		{
			using (FileStream fileStream = new FileStream(text3, FileMode.CreateNew, FileAccess.Write, FileShare.None))
			{
				flag = true;
				byte[] bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(state, StateJsonOptions));
				fileStream.Write(bytes);
				fileStream.Flush(flushToDisk: true);
			}
			DenuvoFileTransaction.CheckPath(text2);
			File.Move(text3, text2, overwrite: true);
		}
		finally
		{
			if (flag && File.Exists(text3))
			{
				File.Delete(text3);
			}
		}
	}

	private static string? ResolveConfigFullPath(DenuvoFixLocalInstallState state, string configRelativePath)
	{
		if (!string.IsNullOrWhiteSpace(state.ConfigFullPath))
		{
			return state.ConfigFullPath;
		}
		if (string.IsNullOrWhiteSpace(state.GameInstallPath))
		{
			return null;
		}
		return Path.GetFullPath(Path.Combine(state.GameInstallPath, ToPlatformRelativePath(configRelativePath)));
	}

	private static string ResolveConfigFullPath(string installDirectory, string configRelativePath)
	{
		string fullPath = Path.GetFullPath(installDirectory);
		string fullPath2 = Path.GetFullPath(Path.Combine(fullPath, ToPlatformRelativePath(configRelativePath)));
		if (!IsPathInsideDirectory(fullPath2, fullPath))
		{
			throw new InvalidOperationException("Đường dẫn config Denuvo Active không hợp lệ.");
		}
		return fullPath2;
	}

	private static bool IsZipFile(string filePath)
	{
		Span<byte> buffer = stackalloc byte[4];
		using FileStream fileStream = File.OpenRead(filePath);
		return fileStream.Read(buffer) == 4 && buffer[0] == 80 && buffer[1] == 75 && buffer[2] == 3 && buffer[3] == 4;
	}

	private async Task TryPostEventAsync(Guid libraryId, string accessToken, string eventType, int? revisionNumber, string? packageSha256, string? gameInstallPath, string? message, CancellationToken cancellationToken)
	{
		try
		{
			await storefrontApiClient.PostDenuvoFixEventAsync(libraryId, new DesktopDenuvoFixEventRequest
			{
				EventType = eventType,
				ConfigRevisionNumber = revisionNumber,
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

	private static string WriteInstallLog(DesktopLibraryDetailResponse detail, DesktopLibraryDenuvoFixResponse denuvo, string installDirectory, DenuvoFileInstallResult result, IReadOnlyList<string> messages)
	{
		List<string> list = new List<string>
		{
			"Game: " + detail.GameName,
			"Version: " + detail.VersionName,
			$"Assignment: {denuvo.AssignmentId}",
			("Package: " + denuvo.PackageDisplayName + " " + denuvo.PackageVersion).Trim(),
			$"Config revision: {denuvo.ActiveConfigRevisionNumber}",
			"Install directory: " + installDirectory,
			"Config path: " + (result.ConfigFullPath ?? "-"),
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
		return WriteLogFile(SanitizeFileName(detail.GameName) + "-denuvo-install", list);
	}

	private static string WriteErrorLog(DesktopLibraryDetailResponse detail, DesktopLibraryDenuvoFixResponse? denuvo, Exception exception)
	{
		return WriteLogFile(SanitizeFileName(detail.GameName) + "-denuvo-error", new _003C_003Ez__ReadOnlyArray<string>(new string[5]
		{
			"Game: " + detail.GameName,
			"Version: " + detail.VersionName,
			$"Assignment: {denuvo?.AssignmentId}",
			$"Time UTC: {DateTime.UtcNow:O}",
			"Error: " + exception.Message
		}));
	}

	private static void WriteAutoUpdateLog(DesktopDenuvoFixUpdateItemResponse update, Exception exception)
	{
		WriteLogFile(SanitizeFileName(update.GameName) + "-denuvo-auto-update-error", new _003C_003Ez__ReadOnlyArray<string>(new string[5]
		{
			"Game: " + update.GameName,
			$"Assignment: {update.AssignmentId}",
			$"Revision: {update.ActiveRevisionNumber}",
			$"Time UTC: {DateTime.UtcNow:O}",
			"Error: " + exception.Message
		}));
	}

	private static string WriteLogFile(string name, IEnumerable<string> lines)
	{
		try
		{
			string text = ResolveLogDirectory();
			DenuvoFileTransaction.CheckPath(text);
			Directory.CreateDirectory(text);
			string text2 = Path.Combine(text, $"{name}-{DateTime.UtcNow:yyyyMMddHHmmss}.log");
			File.WriteAllLines(text2, lines);
			return text2;
		}
		catch (Exception ex) when (((ex is IOException || ex is UnauthorizedAccessException || ex is InvalidDataException) ? 1 : 0) != 0)
		{
			DesktopDiagnosticLog.Write("DenuvoInstall", "InstallLogUnavailable", "ErrorType=" + ex.GetType().Name);
			return string.Empty;
		}
	}

	private static string SanitizeFileName(string value)
	{
		string text = (string.IsNullOrWhiteSpace(value) ? "denuvo-fix" : value.Trim());
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
		return "denuvo-fix";
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
