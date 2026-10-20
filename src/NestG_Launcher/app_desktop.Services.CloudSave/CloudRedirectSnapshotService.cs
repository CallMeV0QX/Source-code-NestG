using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace app_desktop.Services.CloudSave;

internal sealed class CloudRedirectSnapshotService
{
	private sealed record SteamLoginAccounts(HashSet<uint> All, HashSet<uint> MostRecent);

	private sealed record ManifestEntry(string RelativePath, string Sha1, long Size, long Timestamp);

	private sealed record SourceFile(string RelativePath, string FullPath, string Sha1, long Size, DateTime LastWriteTimeUtc, long ManifestTimestamp);

	private const ulong SteamId64Base = 76561197960265728uL;

	private const int MaxManifestBytes = 16777216;

	private const int MaxManifestEntries = 100000;

	private const long MaxSnapshotBytes = 21474836480L;

	private const int CopyBufferSize = 131072;

	private static readonly Regex LoginUserPattern = new Regex("\\\"(?<id>[0-9]{17,20})\\\"\\s*\\{(?<body>.*?)\\}", RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.CultureInvariant);

	private static readonly Regex Sha1Pattern = new Regex("^[0-9a-fA-F]{40}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

	private static readonly string[] MetadataNames = new string[2] { "manifest.cloudredirect", "cn.cloudredirect" };

	private static readonly string[] LegacyMetadataNames = new string[2] { "manifest.dat", "cn.dat" };

	private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
	{
		WriteIndented = true
	};

	private readonly string stagingRoot;

	private readonly Func<string?> findSteamDirectory;

	private readonly Action ensureSteamClosed;

	internal CloudRedirectSnapshotService(string storageFolderName, string? localAppDataRoot = null, Func<string?>? findSteamDirectory = null, Action? ensureSteamClosed = null)
	{
		stagingRoot = Path.Combine(CloudSaveStoragePath.GetRoot(storageFolderName, localAppDataRoot), "CloudSave", "Staging");
		this.findSteamDirectory = findSteamDirectory ?? ((Func<string>)(() => SteamInstallLocator.FindSteamDirectory(allowDriveScan: false)));
		this.ensureSteamClosed = ensureSteamClosed ?? new Action(EnsureSteamClosed);
	}

	internal Task<CloudSaveSnapshotPackage> CreateAsync(CloudSaveBackupRequest request, IProgress<CloudSaveProgress>? progress, CancellationToken cancellationToken = default(CancellationToken))
	{
		ArgumentNullException.ThrowIfNull(request, "request");
		if (request.SteamAppId <= 0)
		{
			throw new InvalidOperationException("Game chưa có Steam App ID hợp lệ để tạo Cloud Save.");
		}
		cancellationToken.ThrowIfCancellationRequested();
		string text = findSteamDirectory();
		if (string.IsNullOrWhiteSpace(text))
		{
			throw new InvalidOperationException("Không tìm thấy Steam. Hãy mở Steam ít nhất một lần, đóng Steam rồi thử CLOUD SAVE lại.");
		}
		string storageRoot = Path.Combine(text, "cloud_redirect", "storage");
		uint steamAccountId = ResolveSteamAccountId(text, storageRoot, request.SteamAppId);
		return CreateFromStorageAsync(request, storageRoot, steamAccountId, progress, cancellationToken);
	}

	internal async Task<CloudSaveSnapshotPackage> CreateFromStorageAsync(CloudSaveBackupRequest request, string storageRoot, uint steamAccountId, IProgress<CloudSaveProgress>? progress, CancellationToken cancellationToken = default(CancellationToken))
	{
		string appRoot = Path.GetFullPath(Path.Combine(storageRoot, steamAccountId.ToString(CultureInfo.InvariantCulture), request.SteamAppId.ToString(CultureInfo.InvariantCulture)));
		EnsureContainedPath(storageRoot, appRoot);
		EnsureNoReparsePoints(storageRoot, appRoot);
		if (!Directory.Exists(appRoot))
		{
			throw new CloudSaveNoDataException();
		}
		progress?.Report(new CloudSaveProgress(CloudSaveProgressStage.Inspecting, "Đang kiểm tra dữ liệu CloudRedirect...", 20));
		EnsureNoActiveOrPendingSession(appRoot);
		string manifestPath = ResolveMetadataPath(appRoot, "manifest.cloudredirect", "manifest.dat", required: true);
		string cnPath = ResolveMetadataPath(appRoot, "cn.cloudredirect", "cn.dat", required: false);
		EnsureNoReparsePoints(appRoot, manifestPath);
		if (cnPath != null)
		{
			EnsureNoReparsePoints(appRoot, cnPath);
		}
		byte[] manifestBytesBefore = await ReadBoundedFileAsync(manifestPath, 16777216, cancellationToken);
		byte[] array = ((cnPath != null) ? (await ReadBoundedFileAsync(cnPath, 128, cancellationToken)) : Array.Empty<byte>());
		byte[] cnBytesBefore = array;
		ulong changeNumber = ParseChangeNumber(cnBytesBefore);
		List<ManifestEntry> list = ParseManifest(manifestBytesBefore);
		if (list.Count == 0)
		{
			throw new CloudSaveNoDataException();
		}
		ensureSteamClosed();
		List<SourceFile> sourceFiles = ResolveAndValidateSourceFiles(appRoot, list);
		if (sourceFiles.Sum((SourceFile item) => item.Size) > 21474836480L)
		{
			throw new InvalidOperationException("Dữ liệu save vượt giới hạn an toàn 20 GB của một snapshot.");
		}
		IReadOnlyDictionary<string, byte[]> metadata = await ReadMetadataAsync(appRoot, cancellationToken);
		string operationId = Guid.NewGuid().ToString("N");
		DateTimeOffset createdAt = DateTimeOffset.UtcNow;
		string snapshotId = $"{createdAt:yyyyMMdd'T'HHmmssfff'Z'}-{operationId.Substring(0, 8)}";
		string operationDirectory = Path.Combine(stagingRoot, operationId);
		string archivePath = Path.Combine(operationDirectory, "save.zip");
		Directory.CreateDirectory(operationDirectory);
		try
		{
			progress?.Report(new CloudSaveProgress(CloudSaveProgressStage.Snapshotting, $"Đang tạo snapshot {sourceFiles.Count} file save...", 35));
			CloudSaveSnapshotManifest snapshotManifest = new CloudSaveSnapshotManifest
			{
				SnapshotId = snapshotId,
				OperationId = operationId,
				NestGUserId = request.NestGUserId,
				LibraryId = request.LibraryId,
				GameName = request.GameName,
				SteamAccountId = steamAccountId,
				SteamAppId = request.SteamAppId,
				CloudRedirectChangeNumber = changeNumber,
				CreatedAtUtc = createdAt
			};
			await CreateArchiveAsync(archivePath, sourceFiles, metadata, snapshotManifest, cancellationToken);
			await EnsureSourceUnchangedAsync(manifestPath, manifestBytesBefore, cnPath, cnBytesBefore, sourceFiles, cancellationToken);
			await VerifyArchiveAsync(archivePath, snapshotManifest, cancellationToken);
			var (archiveSha, archiveMd, archiveSize) = await HashArchiveAsync(archivePath, cancellationToken);
			return new CloudSaveSnapshotPackage(operationId, snapshotId, steamAccountId, request.SteamAppId, archivePath, archiveSize, archiveSha, archiveMd, sourceFiles.Count, createdAt);
		}
		catch
		{
			TryDeleteOwnedDirectory(operationDirectory);
			throw;
		}
	}

	internal static uint ResolveSteamAccountId(string steamDirectory, string storageRoot, int steamAppId)
	{
		if (!Directory.Exists(storageRoot))
		{
			throw new CloudSaveNoDataException();
		}
		List<uint> list = new List<uint>();
		foreach (string item in Directory.EnumerateDirectories(storageRoot, "*", SearchOption.TopDirectoryOnly))
		{
			if (uint.TryParse(Path.GetFileName(item), NumberStyles.None, CultureInfo.InvariantCulture, out var result))
			{
				string path = Path.Combine(item, steamAppId.ToString(CultureInfo.InvariantCulture));
				if (File.Exists(Path.Combine(path, "manifest.cloudredirect")) || File.Exists(Path.Combine(path, "manifest.dat")))
				{
					list.Add(result);
				}
			}
		}
		if (list.Count == 0)
		{
			throw new CloudSaveNoDataException();
		}
		if (list.Count == 1)
		{
			return list[0];
		}
		HashSet<uint> hashSet = ReadMostRecentSteamAccountIds(steamDirectory);
		uint[] array = list.Where(hashSet.Contains).Distinct().ToArray();
		if (array.Length == 1)
		{
			return array[0];
		}
		throw new InvalidOperationException("Có nhiều Steam account cùng lưu game này và không xác định được account hiện tại. Hãy đăng nhập đúng account trong Steam, đóng Steam rồi thử lại.");
	}

	private static HashSet<uint> ReadMostRecentSteamAccountIds(string steamDirectory)
	{
		return ReadSteamLoginAccounts(steamDirectory).MostRecent;
	}

	private static SteamLoginAccounts ReadSteamLoginAccounts(string steamDirectory)
	{
		HashSet<uint> hashSet = new HashSet<uint>();
		HashSet<uint> hashSet2 = new HashSet<uint>();
		string text = Path.Combine(steamDirectory, "config", "loginusers.vdf");
		if (!File.Exists(text))
		{
			return new SteamLoginAccounts(hashSet, hashSet2);
		}
		string input;
		try
		{
			long length = new FileInfo(text).Length;
			if ((length <= 0 || length > 1048576) ? true : false)
			{
				return new SteamLoginAccounts(hashSet, hashSet2);
			}
			input = File.ReadAllText(text, Encoding.UTF8);
		}
		catch
		{
			return new SteamLoginAccounts(hashSet, hashSet2);
		}
		foreach (Match item2 in LoginUserPattern.Matches(input))
		{
			if (ulong.TryParse(item2.Groups["id"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var result) && result >= 76561197960265728L && result - 76561197960265728L <= uint.MaxValue)
			{
				uint item = (uint)(result - 76561197960265728L);
				hashSet.Add(item);
				if (Regex.IsMatch(item2.Groups["body"].Value, "\\\"MostRecent\\\"\\s+\\\"1\\\"", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
				{
					hashSet2.Add(item);
				}
			}
		}
		return new SteamLoginAccounts(hashSet, hashSet2);
	}

	internal static uint ResolveCurrentSteamAccountId(string steamDirectory)
	{
		SteamLoginAccounts steamLoginAccounts = ReadSteamLoginAccounts(steamDirectory);
		if (steamLoginAccounts.MostRecent.Count == 1)
		{
			return steamLoginAccounts.MostRecent.Single();
		}
		if (steamLoginAccounts.MostRecent.Count == 0 && steamLoginAccounts.All.Count == 1)
		{
			return steamLoginAccounts.All.Single();
		}
		throw new InvalidOperationException("Không xác định được duy nhất Steam account đang dùng. Hãy đăng nhập đúng account trong Steam rồi thử restore lại.");
	}

	private static List<ManifestEntry> ParseManifest(byte[] bytes)
	{
		try
		{
			using JsonDocument jsonDocument = JsonDocument.Parse(bytes, new JsonDocumentOptions
			{
				AllowTrailingCommas = false,
				CommentHandling = JsonCommentHandling.Disallow,
				MaxDepth = 32
			});
			if (jsonDocument.RootElement.ValueKind != JsonValueKind.Object)
			{
				throw new InvalidDataException();
			}
			List<ManifestEntry> list = new List<ManifestEntry>();
			foreach (JsonProperty item in jsonDocument.RootElement.EnumerateObject())
			{
				if (list.Count >= 100000 || item.Value.ValueKind != JsonValueKind.Object)
				{
					throw new InvalidDataException();
				}
				JsonElement value = item.Value;
				if (!value.TryGetProperty("sha", out var value2) || value2.ValueKind != JsonValueKind.String || !Sha1Pattern.IsMatch(value2.GetString() ?? string.Empty) || !value.TryGetProperty("size", out var value3) || !value3.TryGetInt64(out var value4) || value4 < 0)
				{
					throw new InvalidDataException();
				}
				JsonElement value5;
				long value6;
				long timestamp = ((value.TryGetProperty("ts", out value5) && value5.TryGetInt64(out value6) && value6 >= 0) ? value6 : 0);
				list.Add(new ManifestEntry(item.Name, value2.GetString().ToLowerInvariant(), value4, timestamp));
			}
			return list;
		}
		catch (Exception ex) when (((ex is JsonException || ex is InvalidDataException) ? 1 : 0) != 0)
		{
			throw new InvalidOperationException("Manifest CloudRedirect bị hỏng hoặc không đúng định dạng; đã dừng để tránh backup sai save.", ex);
		}
	}

	private static List<SourceFile> ResolveAndValidateSourceFiles(string appRoot, IReadOnlyList<ManifestEntry> entries)
	{
		List<SourceFile> list = new List<SourceFile>(entries.Count);
		HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (ManifestEntry item in entries.OrderBy<ManifestEntry, string>((ManifestEntry item) => item.RelativePath, StringComparer.Ordinal))
		{
			string text = NormalizeRelativePath(item.RelativePath);
			if (!hashSet.Add(text))
			{
				throw new InvalidOperationException("Manifest CloudRedirect chứa đường dẫn save trùng nhau.");
			}
			string fullPath = Path.GetFullPath(Path.Combine(appRoot, text.Replace('/', Path.DirectorySeparatorChar)));
			EnsureContainedPath(appRoot, fullPath);
			EnsureNoReparsePoints(appRoot, fullPath);
			if (!File.Exists(fullPath))
			{
				throw new InvalidOperationException("Thiếu file save trong cache CloudRedirect: " + text);
			}
			FileInfo fileInfo = new FileInfo(fullPath);
			if (fileInfo.Length != item.Size)
			{
				throw new InvalidOperationException("Kích thước file save không khớp manifest: " + text);
			}
			list.Add(new SourceFile(text, fullPath, item.Sha1, item.Size, fileInfo.LastWriteTimeUtc, item.Timestamp));
		}
		return list;
	}

	private static async Task CreateArchiveAsync(string archivePath, IReadOnlyList<SourceFile> sourceFiles, IReadOnlyDictionary<string, byte[]> metadata, CloudSaveSnapshotManifest manifest, CancellationToken cancellationToken)
	{
		await using FileStream archiveStream = new FileStream(archivePath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 131072, FileOptions.Asynchronous | FileOptions.SequentialScan);
		using ZipArchive archive = new ZipArchive(archiveStream, ZipArchiveMode.Create, leaveOpen: true, Encoding.UTF8);
		byte[] buffer = new byte[131072];
		foreach (SourceFile source in sourceFiles)
		{
			cancellationToken.ThrowIfCancellationRequested();
			ZipArchiveEntry zipArchiveEntry = archive.CreateEntry("files/" + source.RelativePath, CompressionLevel.Optimal);
			zipArchiveEntry.LastWriteTime = ClampZipTimestamp(source.LastWriteTimeUtc);
			await using FileStream input = new FileStream(source.FullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 131072, FileOptions.Asynchronous | FileOptions.SequentialScan);
			await using Stream output = zipArchiveEntry.Open();
			using IncrementalHash sha1 = IncrementalHash.CreateHash(HashAlgorithmName.SHA1);
			using IncrementalHash sha256 = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
			long copied = 0L;
			while (true)
			{
				int read = await input.ReadAsync(buffer, cancellationToken);
				if (read == 0)
				{
					break;
				}
				sha1.AppendData(buffer, 0, read);
				sha256.AppendData(buffer, 0, read);
				await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
				copied += read;
			}
			string text = Convert.ToHexString(sha1.GetHashAndReset()).ToLowerInvariant();
			if (copied != source.Size || !text.Equals(source.Sha1, StringComparison.OrdinalIgnoreCase))
			{
				throw new InvalidOperationException("File save thay đổi trong lúc tạo snapshot: " + source.RelativePath + ". Hãy đóng game và Steam rồi thử lại.");
			}
			manifest.Files.Add(new CloudSaveSnapshotFile
			{
				RelativePath = source.RelativePath,
				Size = copied,
				Sha1 = text,
				Sha256 = Convert.ToHexString(sha256.GetHashAndReset()).ToLowerInvariant(),
				LastWriteTimeUtc = new DateTimeOffset(source.LastWriteTimeUtc, TimeSpan.Zero)
			});
		}
		foreach (KeyValuePair<string, byte[]> item2 in metadata.OrderBy<KeyValuePair<string, byte[]>, string>((KeyValuePair<string, byte[]> item) => item.Key, StringComparer.Ordinal))
		{
			ZipArchiveEntry zipArchiveEntry2 = archive.CreateEntry("cloudredirect/metadata/" + item2.Key, CompressionLevel.Optimal);
			await using Stream metadataOutput = zipArchiveEntry2.Open();
			await metadataOutput.WriteAsync(item2.Value, cancellationToken);
			manifest.CloudRedirectMetadata.Add(new CloudSaveSnapshotMetadataFile
			{
				Name = item2.Key,
				Size = item2.Value.LongLength,
				Sha256 = Convert.ToHexString(SHA256.HashData(item2.Value)).ToLowerInvariant()
			});
		}
		ZipArchiveEntry zipArchiveEntry3 = archive.CreateEntry("snapshot.json", CompressionLevel.Optimal);
		await using Stream snapshotOutput = zipArchiveEntry3.Open();
		byte[] array = JsonSerializer.SerializeToUtf8Bytes(manifest, JsonOptions);
		await snapshotOutput.WriteAsync(array, cancellationToken);
	}

	private static async Task VerifyArchiveAsync(string archivePath, CloudSaveSnapshotManifest manifest, CancellationToken cancellationToken)
	{
		await using FileStream stream = new FileStream(archivePath, FileMode.Open, FileAccess.Read, FileShare.Read, 131072, FileOptions.Asynchronous | FileOptions.SequentialScan);
		using ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true, Encoding.UTF8);
		if (archive.GetEntry("snapshot.json") == null)
		{
			throw new InvalidDataException("Snapshot archive thiếu snapshot.json.");
		}
		HashSet<string> hashSet = new HashSet<string>(StringComparer.Ordinal);
		foreach (ZipArchiveEntry entry2 in archive.Entries)
		{
			if (!hashSet.Add(entry2.FullName) || NormalizeArchiveEntry(entry2.FullName) != entry2.FullName)
			{
				throw new InvalidDataException("Snapshot archive chứa đường dẫn không an toàn hoặc trùng nhau.");
			}
		}
		foreach (CloudSaveSnapshotFile expected in manifest.Files)
		{
			cancellationToken.ThrowIfCancellationRequested();
			ZipArchiveEntry entry = archive.GetEntry("files/" + expected.RelativePath) ?? throw new InvalidDataException("Snapshot archive thiếu file " + expected.RelativePath + ".");
			await using Stream content = entry.Open();
			byte[] inArray = await SHA256.HashDataAsync(content, cancellationToken);
			if (entry.Length != expected.Size || !Convert.ToHexString(inArray).Equals(expected.Sha256, StringComparison.OrdinalIgnoreCase))
			{
				throw new InvalidDataException("Snapshot archive không vượt qua kiểm tra hash: " + expected.RelativePath + ".");
			}
		}
	}

	private static async Task EnsureSourceUnchangedAsync(string manifestPath, byte[] manifestBefore, string? cnPath, byte[] cnBefore, IReadOnlyList<SourceFile> sourceFiles, CancellationToken cancellationToken)
	{
		byte[] manifestAfter = await ReadBoundedFileAsync(manifestPath, 16777216, cancellationToken);
		byte[] array = ((cnPath != null) ? (await ReadBoundedFileAsync(cnPath, 128, cancellationToken)) : Array.Empty<byte>());
		byte[] array2 = array;
		if (!manifestBefore.AsSpan().SequenceEqual(manifestAfter) || !cnBefore.AsSpan().SequenceEqual(array2))
		{
			throw new InvalidOperationException("CloudRedirect thay đổi manifest trong lúc tạo snapshot. Hãy đóng game và Steam rồi thử lại.");
		}
		foreach (SourceFile sourceFile in sourceFiles)
		{
			FileInfo fileInfo = new FileInfo(sourceFile.FullPath);
			if (!fileInfo.Exists || fileInfo.Length != sourceFile.Size || fileInfo.LastWriteTimeUtc != sourceFile.LastWriteTimeUtc)
			{
				throw new InvalidOperationException("File save thay đổi trong lúc tạo snapshot: " + sourceFile.RelativePath + ". Hãy thử lại sau khi đóng Steam.");
			}
		}
	}

	private static async Task<IReadOnlyDictionary<string, byte[]>> ReadMetadataAsync(string appRoot, CancellationToken cancellationToken)
	{
		Dictionary<string, byte[]> result = new Dictionary<string, byte[]>(StringComparer.Ordinal);
		for (int index = 0; index < MetadataNames.Length; index++)
		{
			string path = ResolveMetadataPath(appRoot, MetadataNames[index], LegacyMetadataNames[index], required: false);
			if (path != null)
			{
				EnsureNoReparsePoints(appRoot, path);
				byte[] value = await ReadBoundedFileAsync(path, 16777216, cancellationToken);
				result[Path.GetFileName(path)] = value;
			}
		}
		return result;
	}

	private static void EnsureNoActiveOrPendingSession(string appRoot)
	{
		switch (InspectSessionStatus(appRoot))
		{
		case CloudSaveSessionStatus.Active:
			throw new InvalidOperationException("Game vẫn có phiên CloudRedirect đang hoạt động. Hãy thoát game và đóng Steam hoàn toàn rồi thử lại.");
		case CloudSaveSessionStatus.Pending:
			throw new InvalidOperationException("CloudRedirect còn dữ liệu upload đang xử lý. Hãy mở Steam để hoàn tất đồng bộ, sau đó thử lại.");
		}
	}

	internal static CloudSaveSessionStatus InspectSessionStatus(string appRoot)
	{
		string text = Path.Combine(appRoot, "pending_ops.cloudredirect");
		if (!File.Exists(text))
		{
			return CloudSaveSessionStatus.Idle;
		}
		try
		{
			EnsureNoReparsePoints(appRoot, text);
			long length = new FileInfo(text).Length;
			if ((length <= 0 || length > 1048576) ? true : false)
			{
				throw new InvalidDataException();
			}
			using JsonDocument jsonDocument = JsonDocument.Parse(File.ReadAllBytes(text));
			JsonElement rootElement = jsonDocument.RootElement;
			if (rootElement.ValueKind != JsonValueKind.Object)
			{
				throw new InvalidDataException();
			}
			if (rootElement.TryGetProperty("current_session", out var value) && ReadOperation(value) == 1)
			{
				return CloudSaveSessionStatus.Active;
			}
			if (rootElement.TryGetProperty("pending_remote_operations", out var value2) && value2.ValueKind == JsonValueKind.Array && value2.EnumerateArray().Any(delegate(JsonElement item)
			{
				int num = ReadOperation(item);
				return (uint)(num - 2) <= 1u;
			}))
			{
				return CloudSaveSessionStatus.Pending;
			}
			return CloudSaveSessionStatus.Idle;
		}
		catch (InvalidOperationException)
		{
			throw;
		}
		catch (Exception ex2) when (((ex2 is IOException || ex2 is UnauthorizedAccessException || ex2 is JsonException || ex2 is InvalidDataException) ? 1 : 0) != 0)
		{
			throw new InvalidOperationException("Không đọc được trạng thái phiên CloudRedirect; đã dừng để tránh backup snapshot không hoàn chỉnh.", ex2);
		}
	}

	private static int ReadOperation(JsonElement element)
	{
		if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty("operation", out var value) || !value.TryGetInt32(out var value2))
		{
			return 0;
		}
		return value2;
	}

	private static void EnsureSteamClosed()
	{
		Process[] array;
		try
		{
			array = Process.GetProcessesByName("steam");
		}
		catch
		{
			array = Array.Empty<Process>();
		}
		try
		{
			if (array.Any((Process process) => !process.HasExited))
			{
				throw new InvalidOperationException("Hãy thoát game và đóng Steam hoàn toàn trước khi bấm CLOUD SAVE để snapshot không bị thiếu dữ liệu.");
			}
		}
		finally
		{
			Process[] array2 = array;
			for (int i = 0; i < array2.Length; i++)
			{
				array2[i].Dispose();
			}
		}
	}

	private static string? ResolveMetadataPath(string appRoot, string canonicalName, string legacyName, bool required)
	{
		string text = Path.Combine(appRoot, canonicalName);
		if (File.Exists(text))
		{
			return text;
		}
		string text2 = Path.Combine(appRoot, legacyName);
		if (File.Exists(text2))
		{
			return text2;
		}
		if (required)
		{
			throw new CloudSaveNoDataException();
		}
		return null;
	}

	private static ulong ParseChangeNumber(byte[] bytes)
	{
		if (bytes.Length == 0)
		{
			return 0uL;
		}
		if (!ulong.TryParse(Encoding.ASCII.GetString(bytes).Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var result))
		{
			throw new InvalidOperationException("Change number của CloudRedirect bị hỏng; đã dừng để tránh backup sai phiên bản.");
		}
		return result;
	}

	private static async Task<byte[]> ReadBoundedFileAsync(string path, int maximumBytes, CancellationToken cancellationToken)
	{
		FileInfo fileInfo = new FileInfo(path);
		if (!fileInfo.Exists || fileInfo.Length < 0 || fileInfo.Length > maximumBytes)
		{
			throw new InvalidDataException("CloudRedirect metadata có kích thước không hợp lệ: " + fileInfo.Name);
		}
		return await File.ReadAllBytesAsync(path, cancellationToken);
	}

	private static string NormalizeRelativePath(string value)
	{
		if (string.IsNullOrWhiteSpace(value) || Path.IsPathRooted(value) || value.Contains(':', StringComparison.Ordinal) || value.Contains('\0', StringComparison.Ordinal))
		{
			throw new InvalidOperationException("Manifest CloudRedirect chứa đường dẫn save không an toàn.");
		}
		string[] array = value.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
		if (array.Length == 0 || array.Any((string segment) => ((segment == "." || segment == "..") ? true : false) || segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0))
		{
			throw new InvalidOperationException("Manifest CloudRedirect chứa đường dẫn save không an toàn.");
		}
		return string.Join('/', array);
	}

	private static string NormalizeArchiveEntry(string value)
	{
		return NormalizeRelativePath(value);
	}

	private static void EnsureContainedPath(string root, string path)
	{
		string value = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
		if (!Path.GetFullPath(path).StartsWith(value, StringComparison.OrdinalIgnoreCase))
		{
			throw new InvalidOperationException("Cloud Save path nằm ngoài vùng dữ liệu được phép.");
		}
	}

	private static void EnsureNoReparsePoints(string root, string path)
	{
		EnsureContainedPath(root, path);
		DirectoryInfo directoryInfo = (new FileInfo(path).Exists ? new FileInfo(path).Directory : new DirectoryInfo(path));
		string value = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);
		while (directoryInfo != null)
		{
			if (directoryInfo.Exists && directoryInfo.Attributes.HasFlag(FileAttributes.ReparsePoint))
			{
				throw new InvalidOperationException("Cloud Save không đọc dữ liệu qua junction hoặc symbolic link.");
			}
			if (Path.GetFullPath(directoryInfo.FullName).TrimEnd(Path.DirectorySeparatorChar).Equals(value, StringComparison.OrdinalIgnoreCase))
			{
				break;
			}
			directoryInfo = directoryInfo.Parent;
		}
		if (File.Exists(path) && File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint))
		{
			throw new InvalidOperationException("Cloud Save không đọc file qua symbolic link.");
		}
	}

	private static DateTimeOffset ClampZipTimestamp(DateTime utc)
	{
		DateTime dateTime = DateTime.SpecifyKind(utc, DateTimeKind.Utc);
		if (dateTime.Year < 1980)
		{
			dateTime = new DateTime(1980, 1, 1, 0, 0, 0, DateTimeKind.Utc);
		}
		else if (dateTime.Year > 2107)
		{
			dateTime = new DateTime(2107, 12, 31, 23, 59, 58, DateTimeKind.Utc);
		}
		return new DateTimeOffset(dateTime);
	}

	private static async Task<(string Sha256, string Md5, long Size)> HashArchiveAsync(string path, CancellationToken cancellationToken)
	{
		(string Sha256, string Md5, long Size) result;
		await using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 131072, FileOptions.Asynchronous | FileOptions.SequentialScan))
		{
			using IncrementalHash sha256 = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
			using IncrementalHash md5 = IncrementalHash.CreateHash(HashAlgorithmName.MD5);
			byte[] buffer = new byte[131072];
			long size = 0L;
			while (true)
			{
				int num = await stream.ReadAsync(buffer, cancellationToken);
				if (num == 0)
				{
					break;
				}
				sha256.AppendData(buffer, 0, num);
				md5.AppendData(buffer, 0, num);
				size += num;
			}
			result = (Sha256: Convert.ToHexString(sha256.GetHashAndReset()).ToLowerInvariant(), Md5: Convert.ToHexString(md5.GetHashAndReset()).ToLowerInvariant(), Size: size);
		}
		return result;
	}

	private static void TryDeleteOwnedDirectory(string path)
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
}
