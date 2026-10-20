using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using app_desktop.Services.CloudRedirect;

namespace app_desktop.Services.CloudSave;

internal sealed class CloudSaveFolderRestoreService
{
	private sealed record RestoreTarget(string CacheAppRoot, string ProviderAppRoot, string CacheManifestPath, string CacheCnPath, string ProviderStatePath, string ProviderCnPath);

	private sealed record CurrentFile(string Sha1, long Size, JsonObject Metadata);

	private sealed class RestoreJournal
	{
		public string TransactionId { get; set; } = string.Empty;

		public Guid NestGUserId { get; set; }

		public uint SteamAccountId { get; set; }

		public int SteamAppId { get; set; }

		public string CacheAppRoot { get; set; } = string.Empty;

		public string ProviderAppRoot { get; set; } = string.Empty;

		public List<string> OldFileNames { get; set; } = new List<string>();

		public List<string> NewFileNames { get; set; } = new List<string>();

		public string OldStateSha256 { get; set; } = string.Empty;

		public string NewStateSha256 { get; set; } = string.Empty;

		public string Stage { get; set; } = string.Empty;
	}

	private static readonly byte[] JournalEntropy = Encoding.UTF8.GetBytes("NestGLauncher.CloudSave.Restore.v1");

	private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);

	private readonly string backupRoot;

	private readonly Func<string?> findSteamDirectory;

	private readonly Action ensureSteamClosed;

	private readonly Action<string>? faultInjection;

	private readonly string roamingAppDataRoot;

	internal CloudSaveFolderRestoreService(string storageFolderName, string? localAppDataRoot = null, string? roamingAppDataRoot = null, Func<string?>? findSteamDirectory = null, Action? ensureSteamClosed = null, Action<string>? faultInjection = null)
	{
		backupRoot = Path.Combine(CloudSaveStoragePath.GetRoot(storageFolderName, localAppDataRoot), "CloudSave", "RestoreBackups");
		this.roamingAppDataRoot = roamingAppDataRoot ?? Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
		this.findSteamDirectory = findSteamDirectory ?? ((Func<string>)(() => SteamInstallLocator.FindSteamDirectory(allowDriveScan: false)));
		this.ensureSteamClosed = ensureSteamClosed ?? new Action(EnsureSteamClosed);
		this.faultInjection = faultInjection;
	}

	internal void EnsureSupportedProvider()
	{
		try
		{
			ReadFolderProviderSyncRoot();
		}
		catch (Exception ex)
		{
			bool flag = ((ex is InvalidOperationException || ex is InvalidDataException) ? true : false);
			throw new CloudSaveRestorePreflightException(flag ? ex.Message : "Không đọc được cấu hình Folder provider để chuẩn bị restore.", ex);
		}
	}

	internal void EnsureTargetIdentityBeforeShutdown(CloudSaveRestoreVersion version)
	{
		try
		{
			string steamDirectory = findSteamDirectory() ?? throw new InvalidOperationException("Không tìm thấy Steam để restore Cloud Save.");
			CloudSaveSteamShutdownService.RequireInstalledGameRoot(steamDirectory, version.SteamAppId);
			if (CloudRedirectSnapshotService.ResolveCurrentSteamAccountId(steamDirectory) != version.SteamAccountId)
			{
				throw new InvalidOperationException("Bản lưu thuộc Steam account khác. Hãy đăng nhập đúng account trong Steam trước khi restore.");
			}
		}
		catch (Exception ex)
		{
			bool flag = ((ex is InvalidOperationException || ex is InvalidDataException) ? true : false);
			throw new CloudSaveRestoreTargetException(flag ? ex.Message : "Không xác định được bản cài game và Steam account để restore.", ex);
		}
	}

	internal async Task<string> ApplyAsync(CloudSaveRestoreVersion version, CloudSaveSnapshotManifest snapshot, string archivePath, IProgress<CloudSaveProgress>? progress, CancellationToken cancellationToken)
	{
		RestoreTarget target = ResolveTarget(version);
		await RecoverInterruptedAsync(target, cancellationToken);
		JsonObject jsonObject = ReadState(target.ProviderStatePath);
		Dictionary<string, CurrentFile> dictionary = ReadStateFiles(jsonObject);
		Dictionary<string, CurrentFile> localFiles = ReadLocalManifest(target.CacheManifestPath);
		EnsureCurrentCacheMatchesProvider(dictionary, localFiles);
		ulong num = ReadChangeNumber(jsonObject);
		if (ReadCnFile(target.CacheCnPath, num == 0) != num || ReadCnFile(target.ProviderCnPath, num == 0) != num)
		{
			throw new InvalidOperationException("CloudRedirect change number trong cache/provider chưa đồng bộ; hãy mở Steam rồi thử lại.");
		}
		if (num >= 9007199254740991L)
		{
			throw new InvalidOperationException("CloudRedirect change number không thể tăng an toàn.");
		}
		Dictionary<string, CloudSaveSnapshotFile> newFiles = new Dictionary<string, CloudSaveSnapshotFile>(StringComparer.OrdinalIgnoreCase);
		foreach (CloudSaveSnapshotFile file in snapshot.Files)
		{
			string key = CloudSaveRestoreArchiveVerifier.SafeRelativePath(file.RelativePath);
			if (!newFiles.TryAdd(key, file))
			{
				throw new InvalidDataException("Snapshot chứa đường dẫn save trùng nhau.");
			}
		}
		ulong newCn = num + 1;
		byte[] newStateBytes = BuildRestoredState(jsonObject, dictionary, newFiles, newCn);
		byte[] newManifestBytes = BuildLocalManifest(newFiles);
		if (newStateBytes.Length > 16777216 || newManifestBytes.Length > 16777216)
		{
			throw new InvalidDataException("Bản restore vượt giới hạn metadata 16 MB của CloudRedirect.");
		}
		byte[] newCnBytes = Encoding.ASCII.GetBytes(newCn.ToString(CultureInfo.InvariantCulture));
		string text = Guid.NewGuid().ToString("N");
		string backupDirectory = Path.Combine(backupRoot, text);
		RestoreJournal journal = new RestoreJournal
		{
			TransactionId = text,
			NestGUserId = version.NestGUserId,
			SteamAccountId = version.SteamAccountId,
			SteamAppId = version.SteamAppId,
			CacheAppRoot = target.CacheAppRoot,
			ProviderAppRoot = target.ProviderAppRoot,
			OldFileNames = localFiles.Keys.ToList(),
			NewFileNames = newFiles.Keys.ToList(),
			OldStateSha256 = Sha256(File.ReadAllBytes(target.ProviderStatePath)),
			NewStateSha256 = Sha256(newStateBytes),
			Stage = "prepared"
		};
		EnsureNoReparse(backupRoot, backupRoot);
		Directory.CreateDirectory(backupDirectory);
		try
		{
			progress?.Report(new CloudSaveProgress(CloudSaveProgressStage.Restoring, "Đang bảo vệ bản save hiện tại...", 60));
			await BackupCurrentAsync(target, localFiles, backupDirectory, cancellationToken);
			await SaveJournalAsync(backupDirectory, journal, cancellationToken);
			await StageSnapshotFilesAsync(archivePath, newFiles, backupDirectory, cancellationToken);
			progress?.Report(new CloudSaveProgress(CloudSaveProgressStage.Restoring, "Đang đưa bản save đã chọn vào CloudRedirect...", 75));
			foreach (CloudSaveSnapshotFile value in newFiles.Values)
			{
				cancellationToken.ThrowIfCancellationRequested();
				string stagedPath = ResolveChild(Path.Combine(backupDirectory, "staged"), value.RelativePath);
				string blobPath = ResolveChild(Path.Combine(target.ProviderAppRoot, "blobs"), value.RelativePath + "/" + value.Sha1.ToLowerInvariant());
				await EnsureCasBlobAsync(stagedPath, blobPath, value.Sha1, cancellationToken);
			}
			foreach (CloudSaveSnapshotFile value2 in newFiles.Values)
			{
				cancellationToken.ThrowIfCancellationRequested();
				string source = ResolveChild(Path.Combine(backupDirectory, "staged"), value2.RelativePath);
				string destination = ResolveChild(target.CacheAppRoot, value2.RelativePath);
				await AtomicCopyAsync(source, destination, cancellationToken);
			}
			foreach (string item in localFiles.Keys.Except<string>(newFiles.Keys, StringComparer.OrdinalIgnoreCase))
			{
				string path = ResolveChild(target.CacheAppRoot, item);
				EnsureNoReparse(target.CacheAppRoot, path);
				if (File.Exists(path))
				{
					File.Delete(path);
				}
			}
			await AtomicWriteAsync(target.CacheManifestPath, newManifestBytes, cancellationToken);
			await AtomicWriteAsync(target.CacheCnPath, newCnBytes, cancellationToken);
			faultInjection?.Invoke("after-cache-update");
			EnsureStateUnchanged(target.ProviderStatePath, journal.OldStateSha256);
			await AtomicWriteAsync(target.ProviderStatePath, newStateBytes, cancellationToken);
			journal.Stage = "provider-published";
			await SaveJournalAsync(backupDirectory, journal, cancellationToken);
			await AtomicWriteAsync(target.ProviderCnPath, newCnBytes, cancellationToken);
			faultInjection?.Invoke("after-provider-publication");
			await VerifyAppliedAsync(target, newFiles, journal.NewStateSha256, cancellationToken);
			journal.Stage = "completed";
			await SaveJournalAsync(backupDirectory, journal, cancellationToken);
			progress?.Report(new CloudSaveProgress(CloudSaveProgressStage.Completed, "Đã áp dụng bản save; hãy mở game để xác nhận tiến độ.", 100));
			return backupDirectory;
		}
		catch (Exception ex)
		{
			Exception exception = ex;
			if (File.Exists(Path.Combine(backupDirectory, "journal.bin")))
			{
				try
				{
					await RollbackAsync(target, backupDirectory, journal, CancellationToken.None);
				}
				catch (Exception ex2)
				{
					throw new CloudSaveRestoreRollbackException(backupDirectory, new AggregateException(exception, ex2));
				}
			}
			ExceptionDispatchInfo.Capture((ex as Exception) ?? throw ex).Throw();
		}
		throw null;
	}

	private RestoreTarget ResolveTarget(CloudSaveRestoreVersion version)
	{
		string text = findSteamDirectory() ?? throw new InvalidOperationException("Không tìm thấy Steam để restore Cloud Save.");
		CloudSaveSteamShutdownService.RequireInstalledGameRoot(text, version.SteamAppId);
		uint num = CloudRedirectSnapshotService.ResolveCurrentSteamAccountId(text);
		if (num != version.SteamAccountId)
		{
			throw new InvalidOperationException("Bản lưu thuộc Steam account khác. Hãy đăng nhập đúng account trong Steam trước khi restore.");
		}
		ensureSteamClosed();
		string text2 = ReadFolderProviderSyncRoot();
		string path = num.ToString(CultureInfo.InvariantCulture);
		string path2 = version.SteamAppId.ToString(CultureInfo.InvariantCulture);
		string text3 = Path.Combine(text, "cloud_redirect", "storage");
		string fullPath = Path.GetFullPath(Path.Combine(text3, path, path2));
		string fullPath2 = Path.GetFullPath(Path.Combine(text2, path, path2));
		EnsureNoReparse(text3, fullPath);
		EnsureNoReparse(text2, fullPath2);
		if (!Directory.Exists(fullPath) || !Directory.Exists(fullPath2))
		{
			throw new InvalidOperationException("CloudRedirect chưa có cache/provider cho game này.");
		}
		if (CloudRedirectSnapshotService.InspectSessionStatus(fullPath) != 0)
		{
			throw new InvalidOperationException("CloudRedirect còn phiên đồng bộ; chưa thể restore an toàn.");
		}
		return new RestoreTarget(fullPath, fullPath2, Path.Combine(fullPath, "manifest.cloudredirect"), Path.Combine(fullPath, "cn.cloudredirect"), Path.Combine(fullPath2, "state.cloudredirect"), Path.Combine(fullPath2, "cn.cloudredirect"));
	}

	private string ReadFolderProviderSyncRoot()
	{
		string path = Path.Combine(roamingAppDataRoot, "CloudRedirect", "config.json");
		if (!File.Exists(path))
		{
			throw new InvalidOperationException("CloudRedirect provider chưa được cấu hình.");
		}
		EnsureNoReparse(Path.GetDirectoryName(path), path);
		CloudRedirectProviderStatus cloudRedirectProviderStatus = CloudRedirectConfiguration.InspectProvider(File.ReadAllText(path));
		if (!cloudRedirectProviderStatus.IsReady || !cloudRedirectProviderStatus.Provider.Equals("folder", StringComparison.OrdinalIgnoreCase))
		{
			throw new InvalidOperationException("Restore hiện cần Folder provider đã cấu hình hợp lệ; launcher sẽ giữ nguyên provider hiện tại.");
		}
		string fullPath = Path.GetFullPath(cloudRedirectProviderStatus.SyncPath);
		if (!Path.IsPathFullyQualified(fullPath) || !Directory.Exists(fullPath) || fullPath.StartsWith("\\\\", StringComparison.Ordinal) || new DirectoryInfo(fullPath).Attributes.HasFlag(FileAttributes.ReparsePoint))
		{
			throw new InvalidOperationException("Folder sync path không phải thư mục cục bộ an toàn để restore.");
		}
		EnsureNoReparse(fullPath, fullPath);
		return fullPath;
	}

	private static JsonObject ReadState(string path)
	{
		EnsureNoReparse(Path.GetDirectoryName(path), path);
		FileInfo fileInfo = new FileInfo(path);
		bool flag = !fileInfo.Exists;
		if (!flag)
		{
			long length = fileInfo.Length;
			bool flag2 = ((length <= 0 || length > 16777216) ? true : false);
			flag = flag2;
		}
		if (flag)
		{
			throw new InvalidDataException("Folder provider không có state.cloudredirect hợp lệ.");
		}
		JsonObject jsonObject;
		try
		{
			jsonObject = (JsonNode.Parse(File.ReadAllText(path), null) as JsonObject) ?? throw new JsonException();
		}
		catch (JsonException innerException)
		{
			throw new InvalidDataException("Folder provider state.cloudredirect bị hỏng.", innerException);
		}
		int num;
		try
		{
			num = jsonObject["v"]?.GetValue<int>() ?? (-1);
		}
		catch (Exception ex) when (((ex is FormatException || ex is InvalidOperationException) ? 1 : 0) != 0)
		{
			throw new InvalidDataException("Folder provider state version không hợp lệ.", ex);
		}
		if (num != 2 || !(jsonObject["files"] is JsonObject) || jsonObject["session"] != null)
		{
			throw new InvalidDataException("Folder provider state không tương thích hoặc còn session hoạt động.");
		}
		return jsonObject;
	}

	private static ulong ReadChangeNumber(JsonObject state)
	{
		try
		{
			return (state["cn"] ?? throw new JsonException()).GetValue<ulong>();
		}
		catch (Exception ex) when (((ex is JsonException || ex is InvalidOperationException || ex is FormatException) ? 1 : 0) != 0)
		{
			throw new InvalidDataException("CloudRedirect change number không hợp lệ.", ex);
		}
	}

	private static ulong ReadCnFile(string path, bool allowMissingZero = false)
	{
		EnsureNoReparse(Path.GetDirectoryName(path), path);
		if (!File.Exists(path) && allowMissingZero)
		{
			return 0uL;
		}
		bool flag = !File.Exists(path);
		if (!flag)
		{
			long length = new FileInfo(path).Length;
			bool flag2 = ((length <= 0 || length > 128) ? true : false);
			flag = flag2;
		}
		if (flag || !ulong.TryParse(File.ReadAllText(path).Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var result))
		{
			throw new InvalidDataException("CloudRedirect cn.cloudredirect không hợp lệ.");
		}
		return result;
	}

	private static Dictionary<string, CurrentFile> ReadStateFiles(JsonObject state)
	{
		Dictionary<string, CurrentFile> dictionary = new Dictionary<string, CurrentFile>(StringComparer.OrdinalIgnoreCase);
		JsonObject obj = (JsonObject)state["files"];
		if (obj.Count > 100000)
		{
			throw new InvalidDataException("Folder provider có quá nhiều file save.");
		}
		foreach (KeyValuePair<string, JsonNode> item in obj)
		{
			string key = CloudSaveRestoreArchiveVerifier.SafeRelativePath(item.Key);
			if (!(item.Value is JsonObject value) || !dictionary.TryAdd(key, ReadCurrentFile(value)))
			{
				throw new InvalidDataException("Folder provider state chứa file save không hợp lệ.");
			}
		}
		return dictionary;
	}

	private static Dictionary<string, CurrentFile> ReadLocalManifest(string path)
	{
		Dictionary<string, CurrentFile> dictionary = new Dictionary<string, CurrentFile>(StringComparer.OrdinalIgnoreCase);
		if (!File.Exists(path))
		{
			return dictionary;
		}
		EnsureNoReparse(Path.GetDirectoryName(path), path);
		long length = new FileInfo(path).Length;
		if ((length <= 0 || length > 16777216) ? true : false)
		{
			throw new InvalidDataException("CloudRedirect cache manifest có kích thước không hợp lệ.");
		}
		JsonObject jsonObject;
		try
		{
			jsonObject = (JsonNode.Parse(File.ReadAllText(path), null) as JsonObject) ?? throw new JsonException();
		}
		catch (JsonException innerException)
		{
			throw new InvalidDataException("CloudRedirect cache manifest bị hỏng.", innerException);
		}
		if (jsonObject.Count > 100000)
		{
			throw new InvalidDataException("CloudRedirect cache có quá nhiều file save.");
		}
		foreach (KeyValuePair<string, JsonNode> item in jsonObject)
		{
			string key = CloudSaveRestoreArchiveVerifier.SafeRelativePath(item.Key);
			if (!(item.Value is JsonObject value) || !dictionary.TryAdd(key, ReadCurrentFile(value)))
			{
				throw new InvalidDataException("CloudRedirect cache manifest chứa file không hợp lệ.");
			}
		}
		return dictionary;
	}

	private static CurrentFile ReadCurrentFile(JsonObject value)
	{
		try
		{
			string text = value["sha"]?.GetValue<string>() ?? string.Empty;
			long num = value["size"]?.GetValue<long>() ?? (-1);
			if (text.Length != 40 || text.Any((char character) => !Uri.IsHexDigit(character)) || num < 0)
			{
				throw new JsonException();
			}
			return new CurrentFile(text.ToLowerInvariant(), num, value);
		}
		catch (Exception ex) when (((ex is JsonException || ex is InvalidOperationException || ex is FormatException) ? 1 : 0) != 0)
		{
			throw new InvalidDataException("CloudRedirect file metadata không hợp lệ.", ex);
		}
	}

	private static void EnsureCurrentCacheMatchesProvider(Dictionary<string, CurrentFile> providerFiles, Dictionary<string, CurrentFile> cacheFiles)
	{
		if (providerFiles.Count != cacheFiles.Count || providerFiles.Any<KeyValuePair<string, CurrentFile>>((KeyValuePair<string, CurrentFile> item) => !cacheFiles.TryGetValue(item.Key, out CurrentFile value) || value.Sha1 != item.Value.Sha1 || value.Size != item.Value.Size))
		{
			throw new InvalidOperationException("Save trên Folder provider và cache Steam chưa đồng bộ. Hãy mở Steam để đồng bộ rồi thử restore lại.");
		}
	}

	private static byte[] BuildRestoredState(JsonObject previous, Dictionary<string, CurrentFile> currentFiles, Dictionary<string, CloudSaveSnapshotFile> snapshotFiles, ulong newCn)
	{
		JsonObject jsonObject = (JsonObject)previous.DeepClone();
		jsonObject["cn"] = JsonValue.Create(newCn, (JsonNodeOptions?)null);
		JsonObject jsonObject2 = new JsonObject((JsonNodeOptions?)null);
		foreach (KeyValuePair<string, CloudSaveSnapshotFile> item in snapshotFiles.OrderBy<KeyValuePair<string, CloudSaveSnapshotFile>, string>((KeyValuePair<string, CloudSaveSnapshotFile> item) => item.Key, StringComparer.Ordinal))
		{
			CloudSaveSnapshotFile value = item.Value;
			CurrentFile value2;
			JsonObject jsonObject3 = (currentFiles.TryGetValue(item.Key, out value2) ? ((JsonObject)value2.Metadata.DeepClone()) : new JsonObject((JsonNodeOptions?)null));
			jsonObject3["sha"] = value.Sha1.ToLowerInvariant();
			jsonObject3["size"] = value.Size;
			jsonObject3["ts"] = Math.Max(0L, value.LastWriteTimeUtc.ToUnixTimeSeconds());
			jsonObject2[item.Key] = jsonObject3;
		}
		jsonObject["files"] = jsonObject2;
		return JsonSerializer.SerializeToUtf8Bytes(jsonObject, JsonOptions);
	}

	private static byte[] BuildLocalManifest(Dictionary<string, CloudSaveSnapshotFile> files)
	{
		JsonObject jsonObject = new JsonObject((JsonNodeOptions?)null);
		foreach (KeyValuePair<string, CloudSaveSnapshotFile> item in files.OrderBy<KeyValuePair<string, CloudSaveSnapshotFile>, string>((KeyValuePair<string, CloudSaveSnapshotFile> item) => item.Key, StringComparer.Ordinal))
		{
			jsonObject[item.Key] = new JsonObject((JsonNodeOptions?)null)
			{
				["sha"] = item.Value.Sha1.ToLowerInvariant(),
				["ts"] = Math.Max(0L, item.Value.LastWriteTimeUtc.ToUnixTimeSeconds()),
				["size"] = item.Value.Size
			};
		}
		return JsonSerializer.SerializeToUtf8Bytes(jsonObject, JsonOptions);
	}

	private static async Task BackupCurrentAsync(RestoreTarget target, Dictionary<string, CurrentFile> files, string backupDirectory, CancellationToken cancellationToken)
	{
		string text = Path.Combine(backupDirectory, "before", "metadata");
		Directory.CreateDirectory(text);
		EnsureNoReparse(target.ProviderAppRoot, target.ProviderStatePath);
		File.Copy(target.ProviderStatePath, Path.Combine(text, "provider-state"));
		CopyIfExists(target.ProviderCnPath, Path.Combine(text, "provider-cn"));
		CopyIfExists(target.CacheManifestPath, Path.Combine(text, "cache-manifest"));
		CopyIfExists(target.CacheCnPath, Path.Combine(text, "cache-cn"));
		string fileRoot = Path.Combine(backupDirectory, "before", "files");
		foreach (KeyValuePair<string, CurrentFile> item in files)
		{
			cancellationToken.ThrowIfCancellationRequested();
			string source = ResolveChild(target.CacheAppRoot, item.Key);
			EnsureNoReparse(target.CacheAppRoot, source);
			FileInfo fileInfo = new FileInfo(source);
			bool flag = !fileInfo.Exists || fileInfo.Length != item.Value.Size;
			if (!flag)
			{
				flag = !string.Equals(await HashFileAsync(source, HashAlgorithmName.SHA1, cancellationToken), item.Value.Sha1, StringComparison.OrdinalIgnoreCase);
			}
			if (flag)
			{
				throw new InvalidOperationException("Save hiện tại không khớp manifest; chưa thể tạo bản dự phòng.");
			}
			string text2 = ResolveChild(fileRoot, item.Key);
			Directory.CreateDirectory(Path.GetDirectoryName(text2));
			File.Copy(source, text2);
			if (!string.Equals(await HashFileAsync(text2, HashAlgorithmName.SHA1, cancellationToken), item.Value.Sha1, StringComparison.OrdinalIgnoreCase))
			{
				throw new InvalidDataException("Bản dự phòng save hiện tại không vượt qua kiểm tra hash.");
			}
		}
	}

	private static async Task StageSnapshotFilesAsync(string archivePath, Dictionary<string, CloudSaveSnapshotFile> files, string backupDirectory, CancellationToken cancellationToken)
	{
		string stagedRoot = Path.Combine(backupDirectory, "staged");
		using ZipArchive archive = ZipFile.OpenRead(archivePath);
		foreach (KeyValuePair<string, CloudSaveSnapshotFile> item in files)
		{
			cancellationToken.ThrowIfCancellationRequested();
			ZipArchiveEntry zipArchiveEntry = archive.GetEntry("files/" + item.Key) ?? throw new InvalidDataException("Bản lưu thiếu file save đã xác minh.");
			string destination = ResolveChild(stagedRoot, item.Key);
			Directory.CreateDirectory(Path.GetDirectoryName(destination));
			await using Stream source = zipArchiveEntry.Open();
			await using (FileStream target = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, 131072, FileOptions.WriteThrough | FileOptions.Asynchronous))
			{
				await source.CopyToAsync(target, 131072, cancellationToken);
				await target.FlushAsync(cancellationToken);
			}
			bool flag = new FileInfo(destination).Length != item.Value.Size;
			if (!flag)
			{
				flag = !string.Equals(await HashFileAsync(destination, HashAlgorithmName.SHA256, cancellationToken), item.Value.Sha256, StringComparison.OrdinalIgnoreCase);
			}
			if (flag)
			{
				throw new InvalidDataException("File save staging không khớp snapshot đã chọn.");
			}
		}
	}

	private static async Task EnsureCasBlobAsync(string stagedPath, string blobPath, string expectedSha1, CancellationToken cancellationToken)
	{
		EnsureNoReparse(Path.GetDirectoryName(blobPath), blobPath);
		if (File.Exists(blobPath))
		{
			if (!string.Equals(await HashFileAsync(blobPath, HashAlgorithmName.SHA1, cancellationToken), expectedSha1, StringComparison.OrdinalIgnoreCase))
			{
				throw new InvalidDataException("Folder provider có blob cùng tên nhưng khác hash.");
			}
			return;
		}
		try
		{
			await AtomicCopyAsync(stagedPath, blobPath, cancellationToken, overwrite: false);
		}
		catch (IOException) when (File.Exists(blobPath))
		{
			if (!string.Equals(await HashFileAsync(blobPath, HashAlgorithmName.SHA1, cancellationToken), expectedSha1, StringComparison.OrdinalIgnoreCase))
			{
				throw new InvalidDataException("Folder provider blob thay đổi trong lúc restore.");
			}
		}
		if (string.Equals(await HashFileAsync(blobPath, HashAlgorithmName.SHA1, cancellationToken), expectedSha1, StringComparison.OrdinalIgnoreCase))
		{
			return;
		}
		throw new InvalidDataException("Folder provider blob không khớp file restore.");
	}

	private static async Task AtomicCopyAsync(string source, string destination, CancellationToken cancellationToken, bool overwrite = true)
	{
		string? obj = Path.GetDirectoryName(destination) ?? throw new InvalidDataException("Đường dẫn save đích không hợp lệ.");
		Directory.CreateDirectory(obj);
		EnsureNoReparse(obj, destination);
		string temporary = destination + ".restore-" + Guid.NewGuid().ToString("N");
		try
		{
			await using (FileStream input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read, 131072, FileOptions.Asynchronous))
			{
				await using FileStream output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 131072, FileOptions.WriteThrough | FileOptions.Asynchronous);
				await input.CopyToAsync(output, 131072, cancellationToken);
				await output.FlushAsync(cancellationToken);
			}
			File.Move(temporary, destination, overwrite);
		}
		finally
		{
			try
			{
				File.Delete(temporary);
			}
			catch
			{
			}
		}
	}

	private static async Task AtomicWriteAsync(string destination, byte[] data, CancellationToken cancellationToken)
	{
		string? obj = Path.GetDirectoryName(destination) ?? throw new InvalidDataException("Đường dẫn metadata đích không hợp lệ.");
		Directory.CreateDirectory(obj);
		EnsureNoReparse(obj, destination);
		string temporary = destination + ".restore-" + Guid.NewGuid().ToString("N");
		try
		{
			await using (FileStream output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, FileOptions.WriteThrough | FileOptions.Asynchronous))
			{
				await output.WriteAsync(data, cancellationToken);
				await output.FlushAsync(cancellationToken);
			}
			File.Move(temporary, destination, overwrite: true);
		}
		finally
		{
			try
			{
				File.Delete(temporary);
			}
			catch
			{
			}
		}
	}

	private static async Task VerifyAppliedAsync(RestoreTarget target, Dictionary<string, CloudSaveSnapshotFile> files, string stateSha256, CancellationToken cancellationToken)
	{
		EnsureStateUnchanged(target.ProviderStatePath, stateSha256);
		ulong num = ReadChangeNumber(ReadState(target.ProviderStatePath));
		if (ReadCnFile(target.CacheCnPath) != num || ReadCnFile(target.ProviderCnPath) != num)
		{
			throw new InvalidDataException("CloudRedirect change number sau restore không khớp state mới.");
		}
		Dictionary<string, CurrentFile> cache = ReadLocalManifest(target.CacheManifestPath);
		if (cache.Count != files.Count)
		{
			throw new InvalidDataException("CloudRedirect cache manifest sau restore không đủ file.");
		}
		foreach (KeyValuePair<string, CloudSaveSnapshotFile> item in files)
		{
			cancellationToken.ThrowIfCancellationRequested();
			if (!cache.TryGetValue(item.Key, out CurrentFile value) || !value.Sha1.Equals(item.Value.Sha1, StringComparison.OrdinalIgnoreCase) || value.Size != item.Value.Size)
			{
				throw new InvalidDataException("CloudRedirect cache manifest sau restore không khớp snapshot.");
			}
			string text = ResolveChild(target.CacheAppRoot, item.Key);
			bool flag = new FileInfo(text).Length != item.Value.Size;
			if (!flag)
			{
				flag = !string.Equals(await HashFileAsync(text, HashAlgorithmName.SHA1, cancellationToken), item.Value.Sha1, StringComparison.OrdinalIgnoreCase);
			}
			if (flag)
			{
				throw new InvalidDataException("File save trong Steam cache sau restore không khớp snapshot.");
			}
			string text2 = ResolveChild(Path.Combine(target.ProviderAppRoot, "blobs"), item.Key + "/" + item.Value.Sha1.ToLowerInvariant());
			flag = new FileInfo(text2).Length != item.Value.Size;
			if (!flag)
			{
				flag = !string.Equals(await HashFileAsync(text2, HashAlgorithmName.SHA1, cancellationToken), item.Value.Sha1, StringComparison.OrdinalIgnoreCase);
			}
			if (flag)
			{
				throw new InvalidDataException("Folder provider blob sau restore không khớp snapshot.");
			}
		}
	}

	private static async Task<string> HashFileAsync(string path, HashAlgorithmName algorithm, CancellationToken cancellationToken)
	{
		string result;
		await using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 131072, FileOptions.Asynchronous | FileOptions.SequentialScan))
		{
			using IncrementalHash hash = IncrementalHash.CreateHash(algorithm);
			byte[] buffer = new byte[131072];
			while (true)
			{
				int num = await stream.ReadAsync(buffer, cancellationToken);
				if (num == 0)
				{
					break;
				}
				hash.AppendData(buffer, 0, num);
			}
			result = Convert.ToHexString(hash.GetHashAndReset());
		}
		return result;
	}

	private async Task RecoverInterruptedAsync(RestoreTarget target, CancellationToken cancellationToken)
	{
		if (!Directory.Exists(backupRoot))
		{
			return;
		}
		EnsureNoReparse(backupRoot, backupRoot);
		foreach (string directory in Directory.EnumerateDirectories(backupRoot, "*", SearchOption.TopDirectoryOnly))
		{
			cancellationToken.ThrowIfCancellationRequested();
			if (!Guid.TryParseExact(Path.GetFileName(directory), "N", out var _))
			{
				continue;
			}
			string path = Path.Combine(directory, "journal.bin");
			if (File.Exists(path))
			{
				EnsureNoReparse(directory, path);
				RestoreJournal restoreJournal = await ProtectedJsonFile.LoadAsync<RestoreJournal>(path, JournalEntropy, cancellationToken, deleteOnError: false);
				if (restoreJournal == null)
				{
					throw new InvalidDataException("Cloud Save restore journal rỗng; chưa thể khôi phục giao dịch dở dang.");
				}
				bool flag;
				switch (restoreJournal.Stage)
				{
				case "completed":
				case "rolled-back":
				case "provider-changed":
					flag = true;
					break;
				default:
					flag = false;
					break;
				}
				if (!flag && !(restoreJournal.CacheAppRoot != target.CacheAppRoot) && !(restoreJournal.ProviderAppRoot != target.ProviderAppRoot))
				{
					await RollbackAsync(target, directory, restoreJournal, cancellationToken);
				}
			}
		}
	}

	private static async Task RollbackAsync(RestoreTarget target, string backupDirectory, RestoreJournal journal, CancellationToken cancellationToken)
	{
		if (journal.CacheAppRoot != target.CacheAppRoot || journal.ProviderAppRoot != target.ProviderAppRoot || journal.TransactionId != Path.GetFileName(backupDirectory) || journal.OldFileNames == null || journal.NewFileNames == null || !Guid.TryParseExact(journal.TransactionId, "N", out var _))
		{
			throw new InvalidDataException("Cloud Save restore journal không khớp thư mục backup.");
		}
		string beforeRoot = Path.Combine(backupDirectory, "before");
		string metadataRoot = Path.Combine(beforeRoot, "metadata");
		string providerBefore = Path.Combine(metadataRoot, "provider-state");
		if (!File.Exists(providerBefore) || Sha256(File.ReadAllBytes(providerBefore)) != journal.OldStateSha256)
		{
			throw new InvalidDataException("Bản dự phòng provider state không còn nguyên vẹn.");
		}
		Dictionary<string, CurrentFile> originalFiles = ReadStateFiles(ReadState(providerBefore));
		if (originalFiles.Count != journal.OldFileNames.Count || journal.OldFileNames.Any((string name) => !originalFiles.ContainsKey(name)))
		{
			throw new InvalidDataException("Bản dự phòng file save không khớp provider state cũ.");
		}
		foreach (var (relativePath, file) in originalFiles)
		{
			cancellationToken.ThrowIfCancellationRequested();
			string text2 = ResolveChild(Path.Combine(beforeRoot, "files"), relativePath);
			bool flag = !File.Exists(text2) || new FileInfo(text2).Length != file.Size;
			if (!flag)
			{
				flag = !string.Equals(await HashFileAsync(text2, HashAlgorithmName.SHA1, cancellationToken), file.Sha1, StringComparison.OrdinalIgnoreCase);
			}
			if (flag)
			{
				throw new InvalidDataException("Bản dự phòng file save không còn nguyên vẹn.");
			}
		}
		string currentStateSha = (File.Exists(target.ProviderStatePath) ? Sha256(File.ReadAllBytes(target.ProviderStatePath)) : string.Empty);
		bool providerChanged = currentStateSha != journal.NewStateSha256 && currentStateSha != journal.OldStateSha256;
		foreach (string oldFileName in journal.OldFileNames)
		{
			cancellationToken.ThrowIfCancellationRequested();
			string text3 = ResolveChild(Path.Combine(beforeRoot, "files"), oldFileName);
			if (!File.Exists(text3))
			{
				throw new InvalidDataException("Bản dự phòng save hiện tại thiếu file.");
			}
			await AtomicCopyAsync(text3, ResolveChild(target.CacheAppRoot, oldFileName), cancellationToken);
		}
		foreach (string item in journal.NewFileNames.Except<string>(journal.OldFileNames, StringComparer.OrdinalIgnoreCase))
		{
			string path = ResolveChild(target.CacheAppRoot, item);
			if (File.Exists(path))
			{
				File.Delete(path);
			}
		}
		await RestoreOptionalMetadataAsync(Path.Combine(metadataRoot, "cache-manifest"), target.CacheManifestPath, cancellationToken);
		await RestoreOptionalMetadataAsync(Path.Combine(metadataRoot, "cache-cn"), target.CacheCnPath, cancellationToken);
		if (providerChanged)
		{
			journal.Stage = "provider-changed";
			await SaveJournalAsync(backupDirectory, journal, cancellationToken);
			throw new InvalidOperationException("Save trong cache đã trở về bản cũ, nhưng Folder provider đã đổi từ máy khác. Launcher giữ nguyên provider mới; cần kiểm tra trước khi mở Steam.");
		}
		if (currentStateSha == journal.NewStateSha256)
		{
			await AtomicCopyAsync(providerBefore, target.ProviderStatePath, cancellationToken);
		}
		await RestoreOptionalMetadataAsync(Path.Combine(metadataRoot, "provider-cn"), target.ProviderCnPath, cancellationToken);
		journal.Stage = "rolled-back";
		await SaveJournalAsync(backupDirectory, journal, cancellationToken);
	}

	private static async Task RestoreOptionalMetadataAsync(string backupPath, string targetPath, CancellationToken cancellationToken)
	{
		EnsureNoReparse(Path.GetDirectoryName(targetPath), targetPath);
		if (File.Exists(backupPath))
		{
			await AtomicCopyAsync(backupPath, targetPath, cancellationToken);
		}
		else if (File.Exists(targetPath))
		{
			File.Delete(targetPath);
		}
	}

	private static Task SaveJournalAsync(string backupDirectory, RestoreJournal journal, CancellationToken cancellationToken)
	{
		return ProtectedJsonFile.SaveAsync(Path.Combine(backupDirectory, "journal.bin"), journal, JournalEntropy, cancellationToken);
	}

	private static void CopyIfExists(string source, string destination)
	{
		EnsureNoReparse(Path.GetDirectoryName(source), source);
		if (File.Exists(source))
		{
			File.Copy(source, destination);
		}
	}

	private static void EnsureStateUnchanged(string path, string expectedSha256)
	{
		EnsureNoReparse(Path.GetDirectoryName(path), path);
		if (!File.Exists(path) || Sha256(File.ReadAllBytes(path)) != expectedSha256)
		{
			throw new InvalidOperationException("Folder provider state đã thay đổi trong lúc restore; chưa thể ghi đè save mới.");
		}
	}

	private static string Sha256(byte[] bytes)
	{
		return Convert.ToHexString(SHA256.HashData(bytes));
	}

	private static string ResolveChild(string root, string relativePath)
	{
		string text = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);
		string text2 = CloudSaveRestoreArchiveVerifier.SafeRelativePath(relativePath);
		string fullPath = Path.GetFullPath(Path.Combine(text, text2.Replace('/', Path.DirectorySeparatorChar)));
		if (!fullPath.StartsWith(text + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
		{
			throw new InvalidDataException("Save path thoát khỏi thư mục CloudRedirect.");
		}
		EnsureNoReparse(text, fullPath);
		return fullPath;
	}

	private static void EnsureNoReparse(string root, string path)
	{
		string text = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);
		string fullPath = Path.GetFullPath(path);
		if (!fullPath.Equals(text, StringComparison.OrdinalIgnoreCase) && !fullPath.StartsWith(text + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
		{
			throw new InvalidDataException("CloudRedirect path nằm ngoài vùng restore.");
		}
		if (Directory.Exists(fullPath) && new DirectoryInfo(fullPath).Attributes.HasFlag(FileAttributes.ReparsePoint))
		{
			throw new InvalidDataException("Restore không đi qua junction hoặc symbolic link.");
		}
		for (DirectoryInfo directoryInfo = new DirectoryInfo(Path.GetDirectoryName(fullPath) ?? fullPath); directoryInfo != null; directoryInfo = directoryInfo.Parent)
		{
			if (directoryInfo.Exists && directoryInfo.Attributes.HasFlag(FileAttributes.ReparsePoint))
			{
				throw new InvalidDataException("Restore không đi qua junction hoặc symbolic link.");
			}
			if (directoryInfo.FullName.TrimEnd(Path.DirectorySeparatorChar).Equals(text, StringComparison.OrdinalIgnoreCase))
			{
				break;
			}
		}
		if (File.Exists(fullPath) && File.GetAttributes(fullPath).HasFlag(FileAttributes.ReparsePoint))
		{
			throw new InvalidDataException("Restore không ghi vào symbolic link.");
		}
	}

	private static void EnsureSteamClosed()
	{
		Process[] processesByName = Process.GetProcessesByName("steam");
		try
		{
			if (processesByName.Any((Process process) => !process.HasExited))
			{
				throw new InvalidOperationException("Steam phải đóng hoàn toàn trước khi restore save.");
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
	}
}
