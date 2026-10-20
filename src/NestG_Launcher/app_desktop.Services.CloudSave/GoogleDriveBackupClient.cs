using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace app_desktop.Services.CloudSave;

internal sealed class GoogleDriveBackupClient
{
	private sealed record DriveFile(string Id, long Size, string? Md5Checksum);

	private sealed record RestoreArchiveMetadata(string Name, long Size, string Md5, string ParentId, string OperationId, string SnapshotId, string Sha256);

	private sealed record UploadStatus(bool SessionExpired, long NextOffset, DriveFile? CompletedFile)
	{
		internal static UploadStatus Expired()
		{
			return new UploadStatus(SessionExpired: true, 0L, null);
		}

		internal static UploadStatus Incomplete(long nextOffset)
		{
			return new UploadStatus(SessionExpired: false, nextOffset, null);
		}

		internal static UploadStatus Completed(DriveFile file)
		{
			return new UploadStatus(SessionExpired: false, file.Size, file);
		}
	}

	private const string DriveApiBase = "https://www.googleapis.com/drive/v3";

	private const string DriveUploadBase = "https://www.googleapis.com/upload/drive/v3";

	private const string FolderMimeType = "application/vnd.google-apps.folder";

	private const int UploadChunkSize = 8388608;

	private const int MaximumAttempts = 5;

	private const long MaximumRestoreArchiveBytes = 21474836480L;

	private static readonly Regex Sha256Pattern = new Regex("^[0-9a-fA-F]{64}$", RegexOptions.CultureInvariant);

	private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);

	private readonly GoogleDriveAuthService authService;

	private readonly HttpClient httpClient;

	internal GoogleDriveBackupClient(GoogleDriveAuthService authService, HttpClient? httpClient = null)
	{
		this.authService = authService ?? throw new ArgumentNullException("authService");
		this.httpClient = httpClient ?? new HttpClient
		{
			Timeout = TimeSpan.FromMinutes(2.0)
		};
	}

	internal async Task<IReadOnlyList<CloudSaveRestoreVersion>> ListRestoreVersionsAsync(Guid nestGUserId, Guid libraryId, int steamAppId, CancellationToken cancellationToken)
	{
		string query = "trashed = false and name = 'completed.json' and appProperties has { key='nestgKind' and value='cloud-save-completed' }";
		List<CloudSaveRestoreVersion> versions = new List<CloudSaveRestoreVersion>();
		string text = null;
		for (int page = 0; page < 5; page++)
		{
			string uri = "https://www.googleapis.com/drive/v3/files?spaces=drive&pageSize=100&fields=nextPageToken,files(id,name,size,parents,appProperties)&q=" + Uri.EscapeDataString(query) + ((text == null) ? string.Empty : ("&pageToken=" + Uri.EscapeDataString(text)));
			using HttpResponseMessage response = await SendAuthorizedAsync(nestGUserId, () => new HttpRequestMessage(HttpMethod.Get, uri), cancellationToken);
			string text2 = await ReadResponsePayloadAsync(response, cancellationToken);
			EnsureSuccess(response, text2, "Không thể tải danh sách bản lưu trên Google Drive.");
			using JsonDocument document = JsonDocument.Parse(text2);
			if (!document.RootElement.TryGetProperty("files", out var value) || value.ValueKind != JsonValueKind.Array)
			{
				throw new InvalidDataException("Google Drive trả về danh sách Cloud Save không hợp lệ.");
			}
			foreach (JsonElement item in value.EnumerateArray())
			{
				cancellationToken.ThrowIfCancellationRequested();
				if (TryReadMarkerListing(item, out string markerId, out string folderId, out string operationId, out string snapshotId))
				{
					CloudSaveCompletionMarker cloudSaveCompletionMarker = await ReadCompletionMarkerAsync(nestGUserId, markerId, cancellationToken);
					if (cloudSaveCompletionMarker != null && !(cloudSaveCompletionMarker.NestGUserId != nestGUserId) && !(cloudSaveCompletionMarker.LibraryId != libraryId) && cloudSaveCompletionMarker.SteamAppId == steamAppId && !(cloudSaveCompletionMarker.OperationId != operationId) && !(cloudSaveCompletionMarker.SnapshotId != snapshotId) && cloudSaveCompletionMarker.SteamAccountId != 0)
					{
						versions.Add(new CloudSaveRestoreVersion(cloudSaveCompletionMarker.SnapshotId, cloudSaveCompletionMarker.OperationId, markerId, folderId, cloudSaveCompletionMarker.ArchiveFileId, cloudSaveCompletionMarker.NestGUserId, cloudSaveCompletionMarker.LibraryId, cloudSaveCompletionMarker.SteamAccountId, cloudSaveCompletionMarker.SteamAppId, cloudSaveCompletionMarker.FileCount, cloudSaveCompletionMarker.ArchiveSize, cloudSaveCompletionMarker.ArchiveSha256, cloudSaveCompletionMarker.CompletedAtUtc));
						markerId = null;
						folderId = null;
						operationId = null;
						snapshotId = null;
					}
				}
			}
			text = ((document.RootElement.TryGetProperty("nextPageToken", out var value2) && value2.ValueKind == JsonValueKind.String) ? value2.GetString() : null);
			if (string.IsNullOrEmpty(text))
			{
				break;
			}
			if (page == 4)
			{
				throw new InvalidOperationException("Google Drive có quá nhiều bản Cloud Save; chưa thể liệt kê an toàn.");
			}
			continue;
		}
		return versions.OrderByDescending((CloudSaveRestoreVersion item) => item.CompletedAtUtc).ToArray();
	}

	internal async Task DownloadVerifiedRestoreArchiveAsync(CloudSaveRestoreVersion version, string destinationPath, IProgress<CloudSaveProgress>? progress, CancellationToken cancellationToken)
	{
		RestoreArchiveMetadata metadata = await GetRestoreArchiveMetadataAsync(version.NestGUserId, version.ArchiveFileId, cancellationToken);
		if (metadata.Name != "save.zip" || metadata.Size != version.ArchiveSize || metadata.ParentId != version.SnapshotFolderId || metadata.OperationId != version.OperationId || metadata.SnapshotId != version.SnapshotId || !metadata.Sha256.Equals(version.ArchiveSha256, StringComparison.OrdinalIgnoreCase) || !Regex.IsMatch(metadata.Md5, "^[0-9a-fA-F]{32}$", RegexOptions.CultureInvariant))
		{
			throw new InvalidDataException("Bản lưu trên Drive không khớp dấu hoàn tất; chưa thể restore.");
		}
		string uri = "https://www.googleapis.com/drive/v3/files/" + Uri.EscapeDataString(version.ArchiveFileId) + "?alt=media";
		using HttpResponseMessage response = await SendAuthorizedAsync(version.NestGUserId, () => new HttpRequestMessage(HttpMethod.Get, uri), cancellationToken);
		if (!response.IsSuccessStatusCode)
		{
			EnsureSuccess(response, await ReadResponsePayloadAsync(response, cancellationToken), "Không thể tải bản lưu từ Google Drive.");
		}
		long? contentLength = response.Content.Headers.ContentLength;
		if (contentLength.HasValue)
		{
			long valueOrDefault = contentLength.GetValueOrDefault();
			if (valueOrDefault != version.ArchiveSize)
			{
				throw new InvalidDataException("Google Drive trả về bản lưu có kích thước không khớp.");
			}
		}
		Directory.CreateDirectory(Path.GetDirectoryName(destinationPath) ?? throw new InvalidOperationException("Đường dẫn restore staging không hợp lệ."));
		try
		{
			await using Stream source = await response.Content.ReadAsStreamAsync(cancellationToken);
			await using FileStream target = new FileStream(destinationPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 131072, FileOptions.WriteThrough | FileOptions.Asynchronous);
			using IncrementalHash sha256 = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
			using IncrementalHash md5 = IncrementalHash.CreateHash(HashAlgorithmName.MD5);
			byte[] buffer = new byte[131072];
			long received = 0L;
			while (true)
			{
				int num = await source.ReadAsync(buffer, cancellationToken);
				if (num == 0)
				{
					break;
				}
				received += num;
				if (received > version.ArchiveSize)
				{
					throw new InvalidDataException("Bản lưu tải về vượt kích thước đã công bố.");
				}
				sha256.AppendData(buffer, 0, num);
				md5.AppendData(buffer, 0, num);
				await target.WriteAsync(buffer.AsMemory(0, num), cancellationToken);
				progress?.Report(new CloudSaveProgress(CloudSaveProgressStage.Downloading, $"Đang tải bản lưu ({received / 1024 / 1024} / {version.ArchiveSize / 1024 / 1024} MB)...", 10 + (int)Math.Clamp(30.0 * (double)received / (double)version.ArchiveSize, 0.0, 30.0)));
			}
			await target.FlushAsync(cancellationToken);
			if (received != version.ArchiveSize || !Convert.ToHexString(sha256.GetHashAndReset()).Equals(version.ArchiveSha256, StringComparison.OrdinalIgnoreCase) || !Convert.ToHexString(md5.GetHashAndReset()).Equals(metadata.Md5, StringComparison.OrdinalIgnoreCase))
			{
				throw new InvalidDataException("Bản lưu tải về không vượt qua kiểm tra checksum.");
			}
		}
		catch
		{
			try
			{
				File.Delete(destinationPath);
			}
			catch
			{
			}
			throw;
		}
	}

	internal async Task<string> EnsureSnapshotFolderAsync(CloudSaveOperation operation, CancellationToken cancellationToken)
	{
		string parentId = await EnsureFolderAsync(operation.NestGUserId, "NestGLauncher", "root", "root", cancellationToken);
		string parentId2 = await EnsureFolderAsync(operation.NestGUserId, "CloudSaves", parentId, "cloud-saves", cancellationToken);
		string parentId3 = await EnsureFolderAsync(operation.NestGUserId, $"Steam-{operation.SteamAccountId}", parentId2, $"steam-{operation.SteamAccountId}", cancellationToken);
		string parentId4 = await EnsureFolderAsync(operation.NestGUserId, $"App-{operation.SteamAppId}", parentId3, $"app-{operation.SteamAccountId}-{operation.SteamAppId}", cancellationToken);
		return await EnsureFolderAsync(operation.NestGUserId, operation.SnapshotId, parentId4, "snapshot-" + operation.SnapshotId, cancellationToken);
	}

	internal async Task<string> UploadArchiveAsync(CloudSaveOperation operation, Func<CloudSaveOperation, CancellationToken, Task> saveCheckpointAsync, IProgress<CloudSaveProgress>? progress, CancellationToken cancellationToken)
	{
		if (string.IsNullOrWhiteSpace(operation.DriveSnapshotFolderId))
		{
			throw new InvalidOperationException("Google Drive snapshot folder chưa được tạo.");
		}
		if (!string.IsNullOrWhiteSpace(operation.DriveArchiveFileId) && MatchesArchive(await GetFileAsync(operation.NestGUserId, operation.DriveArchiveFileId, cancellationToken), operation))
		{
			return operation.DriveArchiveFileId;
		}
		Uri sessionUri = ValidateUploadSessionUri(operation.UploadSessionUri);
		long offset = 0L;
		if ((object)sessionUri != null)
		{
			UploadStatus uploadStatus = await QueryUploadStatusAsync(operation.NestGUserId, sessionUri, operation.ArchiveSize, cancellationToken);
			if ((object)uploadStatus.CompletedFile != null)
			{
				EnsureArchiveMatches(uploadStatus.CompletedFile, operation);
				return uploadStatus.CompletedFile.Id;
			}
			if (uploadStatus.SessionExpired)
			{
				DriveFile driveFile = await FindArchiveByOperationIdAsync(operation, cancellationToken);
				if ((object)driveFile != null)
				{
					EnsureArchiveMatches(driveFile, operation);
					return driveFile.Id;
				}
				sessionUri = null;
				operation.UploadSessionUri = null;
				operation.UploadedBytes = 0L;
			}
			else
			{
				if (uploadStatus.NextOffset < 0 || uploadStatus.NextOffset > operation.ArchiveSize)
				{
					throw new InvalidDataException("Google Drive trả về trạng thái upload vượt kích thước snapshot.");
				}
				offset = uploadStatus.NextOffset;
			}
		}
		if ((object)sessionUri == null)
		{
			sessionUri = await CreateUploadSessionAsync(operation, cancellationToken);
			operation.UploadSessionUri = sessionUri.AbsoluteUri;
			operation.UploadedBytes = 0L;
			operation.Stage = CloudSaveOperationStage.Uploading;
			await saveCheckpointAsync(operation, cancellationToken);
		}
		string id;
		await using (FileStream file = new FileStream(operation.ArchivePath, FileMode.Open, FileAccess.Read, FileShare.Read, 8388608, FileOptions.Asynchronous | FileOptions.SequentialScan))
		{
			if (file.Length != operation.ArchiveSize)
			{
				throw new InvalidOperationException("Snapshot local đã thay đổi; không thể tiếp tục upload.");
			}
			byte[] buffer = new byte[8388608];
			bool restartedExpiredSession = false;
			while (true)
			{
				if (offset < operation.ArchiveSize)
				{
					cancellationToken.ThrowIfCancellationRequested();
					file.Position = offset;
					int wanted = (int)Math.Min(buffer.Length, operation.ArchiveSize - offset);
					int num = await ReadExactlyUpToAsync(file, buffer, wanted, cancellationToken);
					if (num != wanted)
					{
						throw new EndOfStreamException("Snapshot local bị thiếu dữ liệu trong lúc upload.");
					}
					long end = offset + num - 1;
					UploadStatus uploadResult = await UploadChunkAsync(operation.NestGUserId, sessionUri, buffer, num, offset, end, operation.ArchiveSize, cancellationToken);
					if (uploadResult.SessionExpired)
					{
						DriveFile recoveredFile = await FindArchiveByOperationIdAsync(operation, cancellationToken);
						if ((object)recoveredFile != null)
						{
							EnsureArchiveMatches(recoveredFile, operation);
							operation.DriveArchiveFileId = recoveredFile.Id;
							operation.UploadedBytes = operation.ArchiveSize;
							operation.Stage = CloudSaveOperationStage.Verifying;
							await saveCheckpointAsync(operation, cancellationToken);
							id = recoveredFile.Id;
							break;
						}
						if (restartedExpiredSession)
						{
							throw new InvalidOperationException("Phiên upload Google Drive liên tục hết hạn. Hãy thử lại sau.");
						}
						restartedExpiredSession = true;
						sessionUri = await CreateUploadSessionAsync(operation, cancellationToken);
						operation.UploadSessionUri = sessionUri.AbsoluteUri;
						operation.UploadedBytes = 0L;
						offset = 0L;
						await saveCheckpointAsync(operation, cancellationToken);
						continue;
					}
					if ((object)uploadResult.CompletedFile != null)
					{
						EnsureArchiveMatches(uploadResult.CompletedFile, operation);
						operation.DriveArchiveFileId = uploadResult.CompletedFile.Id;
						operation.UploadedBytes = operation.ArchiveSize;
						operation.Stage = CloudSaveOperationStage.Verifying;
						await saveCheckpointAsync(operation, cancellationToken);
						id = uploadResult.CompletedFile.Id;
						break;
					}
					if (uploadResult.NextOffset <= offset || uploadResult.NextOffset > end + 1)
					{
						throw new InvalidDataException("Google Drive trả về vị trí upload không hợp lệ; hãy bấm CLOUD SAVE để kiểm tra và tiếp tục phiên upload.");
					}
					offset = (operation.UploadedBytes = uploadResult.NextOffset);
					int value = ((operation.ArchiveSize == 0L) ? 85 : (50 + (int)Math.Round(35.0 * (double)offset / (double)operation.ArchiveSize)));
					progress?.Report(new CloudSaveProgress(CloudSaveProgressStage.Uploading, $"Đang tải snapshot lên Google Drive ({FormatBytes(offset)} / {FormatBytes(operation.ArchiveSize)})...", Math.Clamp(value, 50, 85)));
					await saveCheckpointAsync(operation, cancellationToken);
					continue;
				}
				UploadStatus finalStatus = await QueryUploadStatusAsync(operation.NestGUserId, sessionUri, operation.ArchiveSize, cancellationToken);
				if (finalStatus.SessionExpired)
				{
					DriveFile driveFile2 = await FindArchiveByOperationIdAsync(operation, cancellationToken);
					if ((object)driveFile2 != null)
					{
						EnsureArchiveMatches(driveFile2, operation);
						id = driveFile2.Id;
						break;
					}
				}
				if ((object)finalStatus.CompletedFile == null)
				{
					throw new InvalidOperationException("Google Drive chưa xác nhận file upload hoàn chỉnh. Hãy bấm CLOUD SAVE để tiếp tục.");
				}
				EnsureArchiveMatches(finalStatus.CompletedFile, operation);
				id = finalStatus.CompletedFile.Id;
				break;
			}
		}
		return id;
	}

	internal async Task VerifyAndCompleteAsync(CloudSaveOperation operation, CancellationToken cancellationToken)
	{
		if (string.IsNullOrWhiteSpace(operation.DriveArchiveFileId) || string.IsNullOrWhiteSpace(operation.DriveSnapshotFolderId))
		{
			throw new InvalidOperationException("Google Drive upload chưa có đủ định danh để xác minh.");
		}
		EnsureArchiveMatches(await GetFileAsync(operation.NestGUserId, operation.DriveArchiveFileId, cancellationToken), operation);
		await UploadCompletionMarkerAsync(operation, cancellationToken);
	}

	private static bool TryReadMarkerListing(JsonElement file, out string markerId, out string folderId, out string operationId, out string snapshotId)
	{
		markerId = (folderId = (operationId = (snapshotId = string.Empty)));
		if (!file.TryGetProperty("id", out var value) || value.ValueKind != JsonValueKind.String || !file.TryGetProperty("parents", out var value2) || value2.ValueKind != JsonValueKind.Array || value2.GetArrayLength() != 1 || value2[0].ValueKind != JsonValueKind.String || !file.TryGetProperty("appProperties", out var value3) || value3.ValueKind != JsonValueKind.Object || !value3.TryGetProperty("operationId", out var value4) || value4.ValueKind != JsonValueKind.String || !value3.TryGetProperty("snapshotId", out var value5) || value5.ValueKind != JsonValueKind.String)
		{
			return false;
		}
		markerId = value.GetString() ?? string.Empty;
		folderId = value2[0].GetString() ?? string.Empty;
		operationId = value4.GetString() ?? string.Empty;
		snapshotId = value5.GetString() ?? string.Empty;
		int length = markerId.Length;
		if (length > 0 && length <= 256)
		{
			length = folderId.Length;
			if (length > 0 && length <= 256 && Guid.TryParseExact(operationId, "N", out var _))
			{
				return Regex.IsMatch(snapshotId, "^[0-9]{8}T[0-9]{9}Z-[0-9a-f]{8}$", RegexOptions.CultureInvariant);
			}
		}
		return false;
	}

	private async Task<CloudSaveCompletionMarker?> ReadCompletionMarkerAsync(Guid nestGUserId, string markerId, CancellationToken cancellationToken)
	{
		string uri = "https://www.googleapis.com/drive/v3/files/" + Uri.EscapeDataString(markerId) + "?alt=media";
		using HttpResponseMessage response = await SendAuthorizedAsync(nestGUserId, () => new HttpRequestMessage(HttpMethod.Get, uri), cancellationToken);
		if (!response.IsSuccessStatusCode)
		{
			EnsureSuccess(response, await ReadResponsePayloadAsync(response, cancellationToken), "Không thể đọc dấu hoàn tất Cloud Save.");
		}
		string text;
		try
		{
			text = await ReadBoundedResponsePayloadAsync(response, 16384, cancellationToken);
		}
		catch (InvalidDataException)
		{
			return null;
		}
		EnsureSuccess(response, text, "Không thể đọc dấu hoàn tất Cloud Save.");
		try
		{
			CloudSaveCompletionMarker cloudSaveCompletionMarker = JsonSerializer.Deserialize<CloudSaveCompletionMarker>(text, JsonOptions);
			object result;
			if (cloudSaveCompletionMarker != null && cloudSaveCompletionMarker.FormatVersion == 1 && cloudSaveCompletionMarker.SteamAppId > 0)
			{
				int fileCount = cloudSaveCompletionMarker.FileCount;
				if (fileCount > 0 && fileCount <= 100000)
				{
					long archiveSize = cloudSaveCompletionMarker.ArchiveSize;
					if (archiveSize > 0 && archiveSize <= 21474836480L)
					{
						fileCount = cloudSaveCompletionMarker.ArchiveFileId.Length;
						if (fileCount > 0 && fileCount <= 256 && Sha256Pattern.IsMatch(cloudSaveCompletionMarker.ArchiveSha256))
						{
							result = cloudSaveCompletionMarker;
							goto IL_0279;
						}
					}
				}
			}
			result = null;
			goto IL_0279;
			IL_0279:
			return (CloudSaveCompletionMarker?)result;
		}
		catch (JsonException)
		{
			return null;
		}
	}

	private async Task<RestoreArchiveMetadata> GetRestoreArchiveMetadataAsync(Guid nestGUserId, string archiveFileId, CancellationToken cancellationToken)
	{
		string uri = "https://www.googleapis.com/drive/v3/files/" + Uri.EscapeDataString(archiveFileId) + "?fields=id,name,size,md5Checksum,parents,appProperties";
		using HttpResponseMessage response = await SendAuthorizedAsync(nestGUserId, () => new HttpRequestMessage(HttpMethod.Get, uri), cancellationToken);
		string text = await ReadResponsePayloadAsync(response, cancellationToken);
		EnsureSuccess(response, text, "Không thể kiểm tra bản lưu trên Google Drive.");
		using JsonDocument jsonDocument = JsonDocument.Parse(text);
		JsonElement rootElement = jsonDocument.RootElement;
		if (!rootElement.TryGetProperty("name", out var value) || value.ValueKind != JsonValueKind.String || !rootElement.TryGetProperty("size", out var value2) || value2.ValueKind != JsonValueKind.String || !long.TryParse(value2.GetString(), NumberStyles.None, CultureInfo.InvariantCulture, out var result) || !rootElement.TryGetProperty("md5Checksum", out var value3) || value3.ValueKind != JsonValueKind.String || !rootElement.TryGetProperty("parents", out var value4) || value4.ValueKind != JsonValueKind.Array || value4.GetArrayLength() != 1 || value4[0].ValueKind != JsonValueKind.String || !rootElement.TryGetProperty("appProperties", out var value5) || value5.ValueKind != JsonValueKind.Object)
		{
			throw new InvalidDataException("Google Drive trả về metadata bản lưu không hợp lệ.");
		}
		JsonElement value6;
		JsonElement value7;
		JsonElement value8;
		return new RestoreArchiveMetadata(value.GetString() ?? string.Empty, result, value3.GetString() ?? string.Empty, value4[0].GetString() ?? string.Empty, (value5.TryGetProperty("operationId", out value6) && value6.ValueKind == JsonValueKind.String) ? (value6.GetString() ?? string.Empty) : string.Empty, (value5.TryGetProperty("snapshotId", out value7) && value7.ValueKind == JsonValueKind.String) ? (value7.GetString() ?? string.Empty) : string.Empty, (value5.TryGetProperty("sha256", out value8) && value8.ValueKind == JsonValueKind.String) ? (value8.GetString() ?? string.Empty) : string.Empty);
	}

	private async Task<string> EnsureFolderAsync(Guid nestGUserId, string name, string parentId, string managedKey, CancellationToken cancellationToken)
	{
		string stringToEscape = "trashed = false and mimeType = 'application/vnd.google-apps.folder' and name = '" + EscapeDriveQuery(name) + "' and '" + EscapeDriveQuery(parentId) + "' in parents and appProperties has { key='nestgKey' and value='" + EscapeDriveQuery(managedKey) + "' }";
		string uri = "https://www.googleapis.com/drive/v3/files?spaces=drive&pageSize=10&fields=files(id,name)&q=" + Uri.EscapeDataString(stringToEscape);
		using (HttpResponseMessage response = await SendAuthorizedAsync(nestGUserId, () => new HttpRequestMessage(HttpMethod.Get, uri), cancellationToken))
		{
			string text = await ReadResponsePayloadAsync(response, cancellationToken);
			EnsureSuccess(response, text, "Không thể kiểm tra thư mục Cloud Save trên Google Drive.");
			using JsonDocument jsonDocument = JsonDocument.Parse(text);
			if (jsonDocument.RootElement.TryGetProperty("files", out var value) && value.ValueKind == JsonValueKind.Array)
			{
				foreach (JsonElement item in value.EnumerateArray())
				{
					if (item.TryGetProperty("id", out var value2) && value2.ValueKind == JsonValueKind.String)
					{
						return value2.GetString();
					}
				}
			}
		}
		string metadata = JsonSerializer.Serialize(new
		{
			name = name,
			mimeType = "application/vnd.google-apps.folder",
			parents = new string[1] { parentId },
			appProperties = new Dictionary<string, string>
			{
				["nestgManaged"] = "true",
				["nestgKey"] = managedKey
			}
		}, JsonOptions);
		using HttpResponseMessage createResponse = await SendAuthorizedAsync(nestGUserId, () => new HttpRequestMessage(HttpMethod.Post, "https://www.googleapis.com/drive/v3/files?fields=id,name")
		{
			Content = new StringContent(metadata, Encoding.UTF8, "application/json")
		}, cancellationToken, allowSessionExpired: false, allowAutomaticRetry: false);
		string text2 = await ReadResponsePayloadAsync(createResponse, cancellationToken);
		EnsureSuccess(createResponse, text2, "Không thể tạo thư mục Cloud Save trên Google Drive.");
		using JsonDocument jsonDocument2 = JsonDocument.Parse(text2);
		if (!jsonDocument2.RootElement.TryGetProperty("id", out var value3) || value3.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value3.GetString()))
		{
			throw new InvalidDataException("Google Drive không trả về folder ID hợp lệ.");
		}
		return value3.GetString();
	}

	private async Task<Uri> CreateUploadSessionAsync(CloudSaveOperation operation, CancellationToken cancellationToken)
	{
		string metadata = JsonSerializer.Serialize(new
		{
			name = "save.zip",
			parents = new string[1] { operation.DriveSnapshotFolderId },
			appProperties = new Dictionary<string, string>
			{
				["nestgManaged"] = "true",
				["nestgKind"] = "cloud-save-archive",
				["snapshotId"] = operation.SnapshotId,
				["operationId"] = operation.OperationId,
				["sha256"] = operation.ArchiveSha256
			}
		}, JsonOptions);
		using HttpResponseMessage response = await SendAuthorizedAsync(operation.NestGUserId, delegate
		{
			HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, "https://www.googleapis.com/upload/drive/v3/files?uploadType=resumable&fields=id,name,size,md5Checksum,appProperties");
			httpRequestMessage.Content = new StringContent(metadata, Encoding.UTF8, "application/json");
			httpRequestMessage.Headers.TryAddWithoutValidation("X-Upload-Content-Type", "application/zip");
			httpRequestMessage.Headers.TryAddWithoutValidation("X-Upload-Content-Length", operation.ArchiveSize.ToString(CultureInfo.InvariantCulture));
			return httpRequestMessage;
		}, cancellationToken, allowSessionExpired: false, allowAutomaticRetry: false);
		EnsureSuccess(response, await ReadResponsePayloadAsync(response, cancellationToken), "Không thể khởi tạo phiên upload Google Drive.");
		Uri location = response.Headers.Location;
		if ((object)location != null && location.IsAbsoluteUri)
		{
			Uri uri = ValidateUploadSessionUri(location.AbsoluteUri);
			if ((object)uri != null)
			{
				return uri;
			}
		}
		throw new InvalidDataException("Google Drive không trả về upload session an toàn.");
	}

	private async Task<UploadStatus> UploadChunkAsync(Guid nestGUserId, Uri sessionUri, byte[] buffer, int count, long start, long end, long total, CancellationToken cancellationToken)
	{
		using HttpResponseMessage response = await SendAuthorizedAsync(nestGUserId, delegate
		{
			HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Put, sessionUri);
			ByteArrayContent byteArrayContent = new ByteArrayContent(buffer, 0, count);
			byteArrayContent.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
			byteArrayContent.Headers.TryAddWithoutValidation("Content-Range", $"bytes {start}-{end}/{total}");
			httpRequestMessage.Content = byteArrayContent;
			return httpRequestMessage;
		}, cancellationToken, allowSessionExpired: true);
		HttpStatusCode statusCode = response.StatusCode;
		if ((statusCode == HttpStatusCode.NotFound || statusCode == HttpStatusCode.Gone) ? true : false)
		{
			return UploadStatus.Expired();
		}
		if (response.StatusCode == HttpStatusCode.PermanentRedirect)
		{
			return UploadStatus.Incomplete(ReadNextOffset(response));
		}
		string payload = await ReadResponsePayloadAsync(response, cancellationToken);
		EnsureSuccess(response, payload, "Google Drive không thể nhận một phần dữ liệu upload.");
		return UploadStatus.Completed(ParseDriveFile(payload));
	}

	private async Task<UploadStatus> QueryUploadStatusAsync(Guid nestGUserId, Uri sessionUri, long total, CancellationToken cancellationToken)
	{
		using HttpResponseMessage response = await SendAuthorizedAsync(nestGUserId, delegate
		{
			HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Put, sessionUri);
			ByteArrayContent byteArrayContent = new ByteArrayContent(Array.Empty<byte>());
			byteArrayContent.Headers.TryAddWithoutValidation("Content-Range", $"bytes */{total}");
			httpRequestMessage.Content = byteArrayContent;
			return httpRequestMessage;
		}, cancellationToken, allowSessionExpired: true);
		HttpStatusCode statusCode = response.StatusCode;
		if ((statusCode == HttpStatusCode.NotFound || statusCode == HttpStatusCode.Gone) ? true : false)
		{
			return UploadStatus.Expired();
		}
		if (response.StatusCode == HttpStatusCode.PermanentRedirect)
		{
			return UploadStatus.Incomplete(ReadNextOffset(response));
		}
		string payload = await ReadResponsePayloadAsync(response, cancellationToken);
		EnsureSuccess(response, payload, "Không thể kiểm tra trạng thái upload Google Drive.");
		return UploadStatus.Completed(ParseDriveFile(payload));
	}

	private async Task<DriveFile> GetFileAsync(Guid nestGUserId, string fileId, CancellationToken cancellationToken)
	{
		string uri = "https://www.googleapis.com/drive/v3/files/" + Uri.EscapeDataString(fileId) + "?fields=id,name,size,md5Checksum,appProperties";
		using HttpResponseMessage response = await SendAuthorizedAsync(nestGUserId, () => new HttpRequestMessage(HttpMethod.Get, uri), cancellationToken);
		string payload = await ReadResponsePayloadAsync(response, cancellationToken);
		EnsureSuccess(response, payload, "Không thể xác minh snapshot trên Google Drive.");
		return ParseDriveFile(payload);
	}

	private async Task<DriveFile?> FindArchiveByOperationIdAsync(CloudSaveOperation operation, CancellationToken cancellationToken)
	{
		if (string.IsNullOrWhiteSpace(operation.DriveSnapshotFolderId))
		{
			return null;
		}
		string stringToEscape = "trashed = false and name = 'save.zip' and '" + EscapeDriveQuery(operation.DriveSnapshotFolderId) + "' in parents and appProperties has { key='operationId' and value='" + EscapeDriveQuery(operation.OperationId) + "' }";
		string uri = "https://www.googleapis.com/drive/v3/files?spaces=drive&pageSize=10&fields=files(id,name,size,md5Checksum,appProperties)&q=" + Uri.EscapeDataString(stringToEscape);
		using HttpResponseMessage response = await SendAuthorizedAsync(operation.NestGUserId, () => new HttpRequestMessage(HttpMethod.Get, uri), cancellationToken);
		string text = await ReadResponsePayloadAsync(response, cancellationToken);
		EnsureSuccess(response, text, "Không thể phục hồi trạng thái upload Google Drive.");
		using JsonDocument jsonDocument = JsonDocument.Parse(text);
		if (!jsonDocument.RootElement.TryGetProperty("files", out var value) || value.ValueKind != JsonValueKind.Array || value.GetArrayLength() == 0)
		{
			return null;
		}
		foreach (JsonElement item in value.EnumerateArray())
		{
			DriveFile driveFile = ParseDriveFile(item.GetRawText());
			if (MatchesArchive(driveFile, operation))
			{
				return driveFile;
			}
		}
		throw new InvalidOperationException("Google Drive có file cùng phiên backup nhưng không khớp checksum; đã dừng để tránh tạo bản trùng hoặc sai dữ liệu.");
	}

	private async Task UploadCompletionMarkerAsync(CloudSaveOperation operation, CancellationToken cancellationToken)
	{
		if (await CompletionMarkerExistsAsync(operation, cancellationToken))
		{
			return;
		}
		byte[] marker = JsonSerializer.SerializeToUtf8Bytes(new
		{
			formatVersion = 1,
			SnapshotId = operation.SnapshotId,
			OperationId = operation.OperationId,
			NestGUserId = operation.NestGUserId,
			LibraryId = operation.LibraryId,
			SteamAccountId = operation.SteamAccountId,
			SteamAppId = operation.SteamAppId,
			archiveFileId = operation.DriveArchiveFileId,
			archiveSize = operation.ArchiveSize,
			archiveSha256 = operation.ArchiveSha256,
			fileCount = operation.FileCount,
			completedAtUtc = DateTimeOffset.UtcNow
		}, JsonOptions);
		string metadata = JsonSerializer.Serialize(new
		{
			name = "completed.json",
			parents = new string[1] { operation.DriveSnapshotFolderId },
			appProperties = new Dictionary<string, string>
			{
				["nestgManaged"] = "true",
				["nestgKind"] = "cloud-save-completed",
				["snapshotId"] = operation.SnapshotId,
				["operationId"] = operation.OperationId
			}
		}, JsonOptions);
		using HttpResponseMessage response = await SendAuthorizedAsync(operation.NestGUserId, delegate
		{
			string boundary = "nestg_" + Guid.NewGuid().ToString("N");
			MultipartContent multipartContent = new MultipartContent("related", boundary)
			{
				new StringContent(metadata, Encoding.UTF8, "application/json")
			};
			ByteArrayContent byteArrayContent = new ByteArrayContent(marker);
			byteArrayContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");
			multipartContent.Add(byteArrayContent);
			return new HttpRequestMessage(HttpMethod.Post, "https://www.googleapis.com/upload/drive/v3/files?uploadType=multipart&fields=id")
			{
				Content = multipartContent
			};
		}, cancellationToken, allowSessionExpired: false, allowAutomaticRetry: false);
		EnsureSuccess(response, await ReadResponsePayloadAsync(response, cancellationToken), "Snapshot đã upload nhưng chưa tạo được dấu hoàn tất trên Google Drive.");
	}

	private async Task<bool> CompletionMarkerExistsAsync(CloudSaveOperation operation, CancellationToken cancellationToken)
	{
		string stringToEscape = "trashed = false and name = 'completed.json' and '" + EscapeDriveQuery(operation.DriveSnapshotFolderId) + "' in parents and appProperties has { key='operationId' and value='" + EscapeDriveQuery(operation.OperationId) + "' }";
		string uri = "https://www.googleapis.com/drive/v3/files?spaces=drive&pageSize=1&fields=files(id)&q=" + Uri.EscapeDataString(stringToEscape);
		using HttpResponseMessage response = await SendAuthorizedAsync(operation.NestGUserId, () => new HttpRequestMessage(HttpMethod.Get, uri), cancellationToken);
		string text = await ReadResponsePayloadAsync(response, cancellationToken);
		EnsureSuccess(response, text, "Không thể kiểm tra dấu hoàn tất Cloud Save trên Google Drive.");
		using JsonDocument jsonDocument = JsonDocument.Parse(text);
		JsonElement value;
		return jsonDocument.RootElement.TryGetProperty("files", out value) && value.ValueKind == JsonValueKind.Array && value.GetArrayLength() > 0;
	}

	private async Task<HttpResponseMessage> SendAuthorizedAsync(Guid nestGUserId, Func<HttpRequestMessage> requestFactory, CancellationToken cancellationToken, bool allowSessionExpired = false, bool allowAutomaticRetry = true)
	{
		HttpResponseMessage response = null;
		for (int attempt = 0; attempt < 5; attempt++)
		{
			cancellationToken.ThrowIfCancellationRequested();
			response?.Dispose();
			using HttpRequestMessage request = requestFactory();
			string parameter = await authService.GetAccessTokenAsync(nestGUserId, allowInteractive: false, forceRefresh: false, cancellationToken);
			request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", parameter);
			try
			{
				response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
			}
			catch (HttpRequestException) when (allowAutomaticRetry && attempt < 4)
			{
				await Task.Delay(RetryDelay(attempt, null), cancellationToken);
				goto end_IL_00d7;
			}
			if (response.StatusCode == HttpStatusCode.Unauthorized && attempt == 0)
			{
				await authService.GetAccessTokenAsync(nestGUserId, allowInteractive: false, forceRefresh: true, cancellationToken);
				continue;
			}
			bool flag = allowSessionExpired;
			if (flag)
			{
				HttpStatusCode statusCode = response.StatusCode;
				bool flag2 = ((statusCode == HttpStatusCode.NotFound || statusCode == HttpStatusCode.Gone) ? true : false);
				flag = flag2;
			}
			if (flag)
			{
				return response;
			}
			if (!allowAutomaticRetry || !IsRetryable(response.StatusCode) || attempt == 4)
			{
				return response;
			}
			await Task.Delay(RetryDelay(attempt, response), cancellationToken);
			end_IL_00d7:;
		}
		return response ?? throw new InvalidOperationException("Không nhận được phản hồi từ Google Drive.");
	}

	private static TimeSpan RetryDelay(int attempt, HttpResponseMessage? response)
	{
		TimeSpan? timeSpan = response?.Headers.RetryAfter?.Delta;
		if (timeSpan.HasValue)
		{
			TimeSpan valueOrDefault = timeSpan.GetValueOrDefault();
			if (valueOrDefault <= TimeSpan.FromSeconds(30.0))
			{
				return valueOrDefault;
			}
		}
		return TimeSpan.FromMilliseconds(Math.Min(8000.0, 500.0 * Math.Pow(2.0, attempt)));
	}

	private static bool IsRetryable(HttpStatusCode statusCode)
	{
		bool flag = ((statusCode == HttpStatusCode.RequestTimeout || statusCode == HttpStatusCode.TooManyRequests) ? true : false);
		bool flag2 = flag;
		if (!flag2)
		{
			bool flag3 = ((statusCode == HttpStatusCode.InternalServerError || (uint)(statusCode - 502) <= 2u) ? true : false);
			flag2 = flag3;
		}
		return flag2;
	}

	private static async Task<int> ReadExactlyUpToAsync(Stream stream, byte[] buffer, int count, CancellationToken cancellationToken)
	{
		int total;
		int num;
		for (total = 0; total < count; total += num)
		{
			num = await stream.ReadAsync(buffer.AsMemory(total, count - total), cancellationToken);
			if (num == 0)
			{
				break;
			}
		}
		return total;
	}

	private static long ReadNextOffset(HttpResponseMessage response)
	{
		if (!response.Headers.TryGetValues("Range", out IEnumerable<string> values))
		{
			return 0L;
		}
		string text = values.FirstOrDefault();
		int num = text?.LastIndexOf('-') ?? (-1);
		if (num < 0 || !long.TryParse(text.Substring(num + 1), NumberStyles.None, CultureInfo.InvariantCulture, out var result))
		{
			return 0L;
		}
		return result + 1;
	}

	private static Uri? ValidateUploadSessionUri(string? value)
	{
		if (!Uri.TryCreate(value, UriKind.Absolute, out Uri result) || result.Scheme != Uri.UriSchemeHttps || (!result.Host.Equals("www.googleapis.com", StringComparison.OrdinalIgnoreCase) && !result.Host.EndsWith(".googleapis.com", StringComparison.OrdinalIgnoreCase)))
		{
			return null;
		}
		return result;
	}

	private static string EscapeDriveQuery(string value)
	{
		return value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("'", "\\'", StringComparison.Ordinal);
	}

	private static async Task<string> ReadResponsePayloadAsync(HttpResponseMessage response, CancellationToken cancellationToken)
	{
		return await ReadBoundedResponsePayloadAsync(response, 2097152, cancellationToken);
	}

	private static async Task<string> ReadBoundedResponsePayloadAsync(HttpResponseMessage response, int maximumBytes, CancellationToken cancellationToken)
	{
		if (response.Content == null)
		{
			return string.Empty;
		}
		long? contentLength = response.Content.Headers.ContentLength;
		if (contentLength.HasValue)
		{
			long valueOrDefault = contentLength.GetValueOrDefault();
			if (valueOrDefault > maximumBytes)
			{
				throw new InvalidDataException("Google Drive response vượt giới hạn an toàn.");
			}
		}
		string @string;
		await using (Stream source = await response.Content.ReadAsStreamAsync(cancellationToken))
		{
			using MemoryStream output = new MemoryStream();
			byte[] buffer = new byte[32768];
			while (true)
			{
				int num = await source.ReadAsync(buffer, cancellationToken);
				if (num == 0)
				{
					break;
				}
				if (output.Length > maximumBytes - num)
				{
					throw new InvalidDataException("Google Drive response vượt giới hạn an toàn.");
				}
				output.Write(buffer, 0, num);
			}
			@string = Encoding.UTF8.GetString(output.GetBuffer(), 0, (int)output.Length);
		}
		return @string;
	}

	private static void EnsureSuccess(HttpResponseMessage response, string payload, string fallback)
	{
		if (response.IsSuccessStatusCode)
		{
			return;
		}
		if (response.StatusCode == HttpStatusCode.NotFound)
		{
			throw new GoogleDriveResourceNotFoundException("Thư mục hoặc file Cloud Save trên Google Drive không còn tồn tại.");
		}
		throw new InvalidOperationException(response.StatusCode switch
		{
			HttpStatusCode.Unauthorized => "Phiên Google Drive đã hết hạn. Hãy kết nối lại.", 
			HttpStatusCode.Forbidden => "Google Drive từ chối thao tác hoặc tài khoản đã hết dung lượng.", 
			HttpStatusCode.TooManyRequests => "Google Drive đang giới hạn yêu cầu. Hãy chờ một lát rồi thử lại.", 
			HttpStatusCode.RequestTimeout => "Kết nối Google Drive quá thời gian. Hãy thử lại.", 
			_ => (response.StatusCode < HttpStatusCode.InternalServerError) ? fallback : "Google Drive đang tạm thời không khả dụng. Hãy thử lại sau.", 
		});
	}

	private static DriveFile ParseDriveFile(string payload)
	{
		using JsonDocument jsonDocument = JsonDocument.Parse(payload);
		JsonElement rootElement = jsonDocument.RootElement;
		if (!rootElement.TryGetProperty("id", out var value) || value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString()))
		{
			throw new InvalidDataException("Google Drive response thiếu file ID.");
		}
		JsonElement value2;
		long result;
		long size = ((rootElement.TryGetProperty("size", out value2) && long.TryParse(value2.GetString(), NumberStyles.None, CultureInfo.InvariantCulture, out result)) ? result : (-1));
		JsonElement value3;
		string md5Checksum = ((rootElement.TryGetProperty("md5Checksum", out value3) && value3.ValueKind == JsonValueKind.String) ? value3.GetString() : null);
		return new DriveFile(value.GetString(), size, md5Checksum);
	}

	private static bool MatchesArchive(DriveFile file, CloudSaveOperation operation)
	{
		if (file.Size == operation.ArchiveSize && !string.IsNullOrWhiteSpace(file.Md5Checksum))
		{
			return file.Md5Checksum.Equals(operation.ArchiveMd5, StringComparison.OrdinalIgnoreCase);
		}
		return false;
	}

	private static void EnsureArchiveMatches(DriveFile file, CloudSaveOperation operation)
	{
		if (!MatchesArchive(file, operation))
		{
			throw new InvalidOperationException("Snapshot trên Google Drive không khớp kích thước/checksum local; chưa đánh dấu hoàn tất.");
		}
	}

	private static string FormatBytes(long bytes)
	{
		string[] array = new string[4] { "B", "KB", "MB", "GB" };
		double num = Math.Max(0L, bytes);
		int num2 = 0;
		while (num >= 1024.0 && num2 < array.Length - 1)
		{
			num /= 1024.0;
			num2++;
		}
		return $"{num:0.##} {array[num2]}";
	}
}
