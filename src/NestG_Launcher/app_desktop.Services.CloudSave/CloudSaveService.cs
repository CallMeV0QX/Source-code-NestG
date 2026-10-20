using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using app_desktop.Services.CloudRedirect;

namespace app_desktop.Services.CloudSave;

internal sealed class CloudSaveService
{
	private sealed class PreparationProgress(Action<CloudSaveProgress> callback) : IProgress<CloudSaveProgress>
	{
		public void Report(CloudSaveProgress value)
		{
			callback(value);
		}
	}

	private const int HashBufferSize = 131072;

	private readonly CloudRedirectSnapshotService snapshotService;

	private readonly CloudSaveOperationStore operationStore;

	private readonly GoogleDriveAuthService authService;

	private readonly GoogleDriveBackupClient driveClient;

	private readonly CloudSaveSteamShutdownService steamShutdownService = new CloudSaveSteamShutdownService();

	private readonly CloudSaveFolderRestoreService folderRestoreService;

	private readonly string stagingRoot;

	private readonly string restoreStagingRoot;

	private readonly SemaphoreSlim gate = new SemaphoreSlim(1, 1);

	internal bool IsConfigured => authService.IsConfigured;

	internal CloudSaveService(string storageFolderName, string googleDriveClientId, HttpClient? authHttpClient = null, HttpClient? driveHttpClient = null, string? localAppDataRoot = null, string? googleDriveClientSecret = null)
	{
		CloudSaveTokenStore tokenStore = new CloudSaveTokenStore(storageFolderName, localAppDataRoot);
		snapshotService = new CloudRedirectSnapshotService(storageFolderName, localAppDataRoot);
		operationStore = new CloudSaveOperationStore(storageFolderName, localAppDataRoot);
		authService = new GoogleDriveAuthService(googleDriveClientId, tokenStore, authHttpClient, null, null, googleDriveClientSecret, null, null);
		driveClient = new GoogleDriveBackupClient(authService, driveHttpClient);
		folderRestoreService = new CloudSaveFolderRestoreService(storageFolderName, localAppDataRoot);
		stagingRoot = Path.Combine(CloudSaveStoragePath.GetRoot(storageFolderName, localAppDataRoot), "CloudSave", "Staging");
		restoreStagingRoot = Path.Combine(CloudSaveStoragePath.GetRoot(storageFolderName, localAppDataRoot), "CloudSave", "RestoreStaging");
	}

	internal bool HasStoredCredential(Guid nestGUserId)
	{
		return authService.HasStoredCredential(nestGUserId);
	}

	internal Task DisconnectAsync(Guid nestGUserId)
	{
		return authService.DisconnectAsync(nestGUserId);
	}

	internal async Task<IReadOnlyList<CloudSaveRestoreVersion>> ListRestoreVersionsAsync(CloudSaveBackupRequest request, CancellationToken cancellationToken = default(CancellationToken))
	{
		ValidateRequest(request);
		await gate.WaitAsync(cancellationToken);
		CloudSaveProgressStage stage = CloudSaveProgressStage.Connecting;
		try
		{
			await authService.GetAccessTokenAsync(request.NestGUserId, allowInteractive: true, forceRefresh: false, cancellationToken);
			stage = CloudSaveProgressStage.Listing;
			return await driveClient.ListRestoreVersionsAsync(request.NestGUserId, request.LibraryId, request.SteamAppId, cancellationToken);
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			throw;
		}
		catch (Exception cause)
		{
			throw new CloudSaveFailureException(stage, cause, isRestore: true);
		}
		finally
		{
			gate.Release();
		}
	}

	internal async Task<CloudSaveRestoreResult> RestoreAsync(CloudSaveBackupRequest request, CloudSaveRestoreVersion selectedVersion, IProgress<CloudSaveProgress>? progress = null, CancellationToken cancellationToken = default(CancellationToken))
	{
		ValidateRequest(request);
		if (selectedVersion.NestGUserId != request.NestGUserId || selectedVersion.LibraryId != request.LibraryId || selectedVersion.SteamAppId != request.SteamAppId)
		{
			throw new InvalidOperationException("Bản lưu được chọn không thuộc đúng game trong thư viện.");
		}
		await gate.WaitAsync(cancellationToken);
		CloudSaveProgressStage stage = CloudSaveProgressStage.Connecting;
		string path = Guid.NewGuid().ToString("N");
		string restoreDirectory = Path.Combine(restoreStagingRoot, path);
		string archivePath = Path.Combine(restoreDirectory, "save.zip");
		try
		{
			stage = CloudSaveProgressStage.Restoring;
			folderRestoreService.EnsureSupportedProvider();
			folderRestoreService.EnsureTargetIdentityBeforeShutdown(selectedVersion);
			stage = CloudSaveProgressStage.Connecting;
			progress?.Report(new CloudSaveProgress(CloudSaveProgressStage.Connecting, "Đang xác minh kết nối Google Drive...", 5));
			await authService.GetAccessTokenAsync(request.NestGUserId, allowInteractive: true, forceRefresh: false, cancellationToken);
			stage = CloudSaveProgressStage.Listing;
			CloudSaveRestoreVersion version = (await driveClient.ListRestoreVersionsAsync(request.NestGUserId, request.LibraryId, request.SteamAppId, cancellationToken)).SingleOrDefault((CloudSaveRestoreVersion item) => item.SnapshotId == selectedVersion.SnapshotId && item.OperationId == selectedVersion.OperationId && item.ArchiveFileId == selectedVersion.ArchiveFileId);
			if ((object)version == null)
			{
				throw new InvalidOperationException("Bản lưu trên Google Drive đã thay đổi; hãy mở danh sách và chọn lại.");
			}
			stage = CloudSaveProgressStage.Downloading;
			progress?.Report(new CloudSaveProgress(stage, "Đang tải bản lưu đã chọn...", 10));
			PrepareRestoreStaging(restoreDirectory, archivePath);
			await driveClient.DownloadVerifiedRestoreArchiveAsync(version, archivePath, progress, cancellationToken);
			stage = CloudSaveProgressStage.Inspecting;
			progress?.Report(new CloudSaveProgress(stage, "Đang kiểm tra từng file save...", 42));
			CloudSaveSnapshotManifest snapshot = await CloudSaveRestoreArchiveVerifier.VerifyAsync(archivePath, version, cancellationToken);
			string safetyBackupPath;
			using (await CloudRedirectOperationCoordinator.AcquireAsync(cancellationToken))
			{
				stage = CloudSaveProgressStage.ClosingGame;
				await steamShutdownService.PrepareForSnapshotAsync(request, new PreparationProgress(delegate(CloudSaveProgress update)
				{
					stage = update.Stage;
					int percentage = update.Stage switch
					{
						CloudSaveProgressStage.ClosingGame => 46, 
						CloudSaveProgressStage.WaitingForSync => 50, 
						CloudSaveProgressStage.ClosingSteam => 55, 
						_ => update.Percentage, 
					};
					progress?.Report(update with
					{
						Percentage = percentage
					});
				}), cancellationToken, forRestore: true);
				stage = CloudSaveProgressStage.Restoring;
				safetyBackupPath = await folderRestoreService.ApplyAsync(version, snapshot, archivePath, progress, cancellationToken);
			}
			return new CloudSaveRestoreResult(version.SnapshotId, version.SteamAccountId, version.SteamAppId, version.FileCount, safetyBackupPath, DateTimeOffset.UtcNow);
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			throw;
		}
		catch (Exception cause)
		{
			throw new CloudSaveFailureException(stage, cause, isRestore: true);
		}
		finally
		{
			try
			{
				if (Directory.Exists(restoreDirectory) && Path.GetFullPath(restoreDirectory).StartsWith(Path.GetFullPath(restoreStagingRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
				{
					EnsureNoStagingReparse(restoreStagingRoot, restoreDirectory);
					EnsureNoStagingReparse(restoreDirectory, archivePath);
					if (File.Exists(archivePath))
					{
						File.Delete(archivePath);
					}
					Directory.Delete(restoreDirectory, recursive: false);
				}
			}
			catch
			{
			}
			gate.Release();
		}
	}

	private void PrepareRestoreStaging(string restoreDirectory, string archivePath)
	{
		EnsureNoStagingReparse(restoreStagingRoot, restoreStagingRoot);
		Directory.CreateDirectory(restoreDirectory);
		EnsureNoStagingReparse(restoreStagingRoot, restoreDirectory);
		EnsureNoStagingReparse(restoreDirectory, archivePath);
		if (File.Exists(archivePath))
		{
			throw new InvalidDataException("Restore staging đã có file bất thường.");
		}
	}

	private static void EnsureNoStagingReparse(string root, string path)
	{
		string text = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);
		string fullPath = Path.GetFullPath(path);
		if (!fullPath.Equals(text, StringComparison.OrdinalIgnoreCase) && !fullPath.StartsWith(text + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
		{
			throw new InvalidDataException("Restore staging nằm ngoài vùng launcher quản lý.");
		}
		for (DirectoryInfo directoryInfo = new DirectoryInfo(Directory.Exists(fullPath) ? fullPath : Path.GetDirectoryName(fullPath)); directoryInfo != null; directoryInfo = directoryInfo.Parent)
		{
			if (directoryInfo.Exists && directoryInfo.Attributes.HasFlag(FileAttributes.ReparsePoint))
			{
				throw new InvalidDataException("Restore staging không đi qua junction hoặc symbolic link.");
			}
			if (directoryInfo.FullName.TrimEnd(Path.DirectorySeparatorChar).Equals(text, StringComparison.OrdinalIgnoreCase))
			{
				break;
			}
		}
		if (File.Exists(fullPath) && File.GetAttributes(fullPath).HasFlag(FileAttributes.ReparsePoint))
		{
			throw new InvalidDataException("Restore staging không dùng symbolic link.");
		}
	}

	private static void ValidateRequest(CloudSaveBackupRequest request)
	{
		if (request.NestGUserId == Guid.Empty || request.LibraryId == Guid.Empty || request.SteamAppId <= 0)
		{
			throw new InvalidOperationException("Phiên NestG hoặc game trong thư viện không hợp lệ.");
		}
	}

	internal async Task<CloudSaveBackupResult> BackupAsync(CloudSaveBackupRequest request, IProgress<CloudSaveProgress>? progress = null, CancellationToken cancellationToken = default(CancellationToken))
	{
		if (request.NestGUserId == Guid.Empty)
		{
			throw new InvalidOperationException("Phiên NestG không có user ID hợp lệ. Hãy đăng nhập lại.");
		}
		if (request.LibraryId == Guid.Empty || request.SteamAppId <= 0)
		{
			throw new InvalidOperationException("Game trong thư viện chưa có định danh Cloud Save hợp lệ.");
		}
		await gate.WaitAsync(cancellationToken);
		CloudSaveProgressStage stage = CloudSaveProgressStage.Connecting;
		try
		{
			progress?.Report(new CloudSaveProgress(CloudSaveProgressStage.Connecting, HasStoredCredential(request.NestGUserId) ? "Đang xác minh kết nối Google Drive..." : "Đang mở Google để kết nối Drive...", 8));
			await authService.GetAccessTokenAsync(request.NestGUserId, allowInteractive: true, forceRefresh: false, cancellationToken);
			string connectionId = await authService.GetConnectionIdAsync(request.NestGUserId, cancellationToken);
			stage = CloudSaveProgressStage.Inspecting;
			CloudSaveOperation operation = await operationStore.FindResumableAsync(request.NestGUserId, request.LibraryId, request.SteamAppId, cancellationToken);
			if (operation != null)
			{
				if (!IsOwnedArchivePath(operation))
				{
					operationStore.Delete(operation.OperationId);
					operation = null;
				}
				else
				{
					try
					{
						await VerifyLocalArchiveAsync(operation, cancellationToken);
					}
					catch (OperationCanceledException)
					{
						throw;
					}
					catch
					{
						CleanupOwnedOperation(operation);
						operation = null;
					}
				}
			}
			if (operation == null)
			{
				stage = CloudSaveProgressStage.Snapshotting;
				CloudSaveSnapshotPackage cloudSaveSnapshotPackage;
				using (await CloudRedirectOperationCoordinator.AcquireAsync(cancellationToken))
				{
					stage = CloudSaveProgressStage.ClosingGame;
					await steamShutdownService.PrepareForSnapshotAsync(request, new PreparationProgress(delegate(CloudSaveProgress update)
					{
						stage = update.Stage;
						progress?.Report(update);
					}), cancellationToken);
					stage = CloudSaveProgressStage.Snapshotting;
					cloudSaveSnapshotPackage = await snapshotService.CreateAsync(request, progress, cancellationToken);
				}
				operation = new CloudSaveOperation
				{
					OperationId = cloudSaveSnapshotPackage.OperationId,
					SnapshotId = cloudSaveSnapshotPackage.SnapshotId,
					NestGUserId = request.NestGUserId,
					LibraryId = request.LibraryId,
					GameName = request.GameName,
					SteamAccountId = cloudSaveSnapshotPackage.SteamAccountId,
					SteamAppId = cloudSaveSnapshotPackage.SteamAppId,
					ArchivePath = cloudSaveSnapshotPackage.ArchivePath,
					ArchiveSize = cloudSaveSnapshotPackage.ArchiveSize,
					ArchiveSha256 = cloudSaveSnapshotPackage.ArchiveSha256,
					ArchiveMd5 = cloudSaveSnapshotPackage.ArchiveMd5,
					FileCount = cloudSaveSnapshotPackage.FileCount,
					Stage = CloudSaveOperationStage.SnapshotReady,
					DriveConnectionId = connectionId,
					CreatedAtUtc = cloudSaveSnapshotPackage.CreatedAtUtc,
					UpdatedAtUtc = cloudSaveSnapshotPackage.CreatedAtUtc
				};
				await operationStore.SaveAsync(operation, cancellationToken);
			}
			else
			{
				progress?.Report(new CloudSaveProgress(CloudSaveProgressStage.Snapshotting, "Đã tìm thấy snapshot chưa hoàn tất; đang tiếp tục upload...", 40));
			}
			ResetRemoteCheckpointForConnection(operation, connectionId);
			stage = CloudSaveProgressStage.Uploading;
			if (string.IsNullOrWhiteSpace(operation.DriveSnapshotFolderId))
			{
				operation.Stage = CloudSaveOperationStage.PreparingDrive;
				await operationStore.SaveAsync(operation, cancellationToken);
				CloudSaveOperation cloudSaveOperation = operation;
				cloudSaveOperation.DriveSnapshotFolderId = await driveClient.EnsureSnapshotFolderAsync(operation, cancellationToken);
				await operationStore.SaveAsync(operation, cancellationToken);
			}
			progress?.Report(new CloudSaveProgress(CloudSaveProgressStage.Uploading, "Đang tải snapshot lên Google Drive...", 50));
			try
			{
				CloudSaveOperation cloudSaveOperation = operation;
				cloudSaveOperation.DriveArchiveFileId = await driveClient.UploadArchiveAsync(operation, operationStore.SaveAsync, progress, cancellationToken);
			}
			catch (GoogleDriveResourceNotFoundException)
			{
				operation.DriveSnapshotFolderId = null;
				operation.DriveArchiveFileId = null;
				operation.UploadSessionUri = null;
				operation.UploadedBytes = 0L;
				operation.Stage = CloudSaveOperationStage.PreparingDrive;
				await operationStore.SaveAsync(operation, cancellationToken);
				CloudSaveOperation cloudSaveOperation = operation;
				cloudSaveOperation.DriveSnapshotFolderId = await driveClient.EnsureSnapshotFolderAsync(operation, cancellationToken);
				await operationStore.SaveAsync(operation, cancellationToken);
				cloudSaveOperation = operation;
				cloudSaveOperation.DriveArchiveFileId = await driveClient.UploadArchiveAsync(operation, operationStore.SaveAsync, progress, cancellationToken);
			}
			operation.Stage = CloudSaveOperationStage.Verifying;
			stage = CloudSaveProgressStage.Verifying;
			operation.UploadedBytes = operation.ArchiveSize;
			await operationStore.SaveAsync(operation, cancellationToken);
			progress?.Report(new CloudSaveProgress(CloudSaveProgressStage.Verifying, "Đang xác minh checksum và hoàn tất phiên bản backup...", 92));
			await driveClient.VerifyAndCompleteAsync(operation, cancellationToken);
			operation.Stage = CloudSaveOperationStage.Completed;
			await operationStore.SaveAsync(operation, cancellationToken);
			CloudSaveBackupResult result = new CloudSaveBackupResult(operation.SnapshotId, operation.SteamAccountId, operation.SteamAppId, operation.FileCount, operation.ArchiveSize, operation.DriveArchiveFileId, DateTimeOffset.UtcNow);
			progress?.Report(new CloudSaveProgress(CloudSaveProgressStage.Completed, "Cloud Save đã được lưu thành công trên Google Drive.", 100));
			CleanupOwnedOperation(operation);
			return result;
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			throw;
		}
		catch (Exception cause)
		{
			throw new CloudSaveFailureException(stage, cause);
		}
		finally
		{
			gate.Release();
		}
	}

	internal static void ResetRemoteCheckpointForConnection(CloudSaveOperation operation, string? connectionId)
	{
		if (string.IsNullOrEmpty(connectionId) || !(operation.DriveConnectionId == connectionId))
		{
			operation.DriveConnectionId = connectionId;
			operation.DriveSnapshotFolderId = null;
			operation.DriveArchiveFileId = null;
			operation.UploadSessionUri = null;
			operation.UploadedBytes = 0L;
			operation.Stage = CloudSaveOperationStage.SnapshotReady;
		}
	}

	private static async Task VerifyLocalArchiveAsync(CloudSaveOperation operation, CancellationToken cancellationToken)
	{
		FileInfo fileInfo = new FileInfo(operation.ArchivePath);
		if (!fileInfo.Exists || fileInfo.Length != operation.ArchiveSize)
		{
			throw new InvalidOperationException("Snapshot đang chờ upload không còn nguyên vẹn. Hãy xóa dữ liệu staging và tạo Cloud Save mới.");
		}
		await using FileStream stream = new FileStream(operation.ArchivePath, FileMode.Open, FileAccess.Read, FileShare.Read, 131072, FileOptions.Asynchronous | FileOptions.SequentialScan);
		using IncrementalHash sha256 = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
		using IncrementalHash md5 = IncrementalHash.CreateHash(HashAlgorithmName.MD5);
		byte[] buffer = new byte[131072];
		while (true)
		{
			int num = await stream.ReadAsync(buffer, cancellationToken);
			if (num == 0)
			{
				break;
			}
			sha256.AppendData(buffer, 0, num);
			md5.AppendData(buffer, 0, num);
		}
		string text = Convert.ToHexString(sha256.GetHashAndReset());
		string text2 = Convert.ToHexString(md5.GetHashAndReset());
		if (!text.Equals(operation.ArchiveSha256, StringComparison.OrdinalIgnoreCase) || !text2.Equals(operation.ArchiveMd5, StringComparison.OrdinalIgnoreCase))
		{
			throw new InvalidOperationException("Snapshot đang chờ upload không khớp checksum; đã dừng để tránh tải dữ liệu hỏng.");
		}
	}

	private void CleanupOwnedOperation(CloudSaveOperation operation)
	{
		operationStore.Delete(operation.OperationId);
		if (!IsOwnedArchivePath(operation))
		{
			return;
		}
		try
		{
			string directoryName = Path.GetDirectoryName(operation.ArchivePath);
			if (directoryName != null && Directory.Exists(directoryName))
			{
				Directory.Delete(directoryName, recursive: true);
			}
		}
		catch
		{
		}
	}

	private bool IsOwnedArchivePath(CloudSaveOperation operation)
	{
		if (!Guid.TryParseExact(operation.OperationId, "N", out var _))
		{
			return false;
		}
		try
		{
			string fullPath = Path.GetFullPath(Path.Combine(stagingRoot, operation.OperationId));
			string value = Path.Combine(fullPath, "save.zip");
			return Path.GetFullPath(operation.ArchivePath).Equals(value, StringComparison.OrdinalIgnoreCase) && fullPath.StartsWith(Path.GetFullPath(stagingRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
		}
		catch
		{
			return false;
		}
	}
}
