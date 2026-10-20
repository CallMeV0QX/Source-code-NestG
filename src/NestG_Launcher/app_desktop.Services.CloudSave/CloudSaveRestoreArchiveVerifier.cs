using System;
using System.Collections.Generic;
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

internal static class CloudSaveRestoreArchiveVerifier
{
	private const long MaximumSnapshotBytes = 21474836480L;

	private const int MaximumSnapshotEntries = 100000;

	private const long MaximumManifestBytes = 33554432L;

	private static readonly Regex Sha1Pattern = new Regex("^[0-9a-fA-F]{40}$", RegexOptions.CultureInvariant);

	private static readonly Regex Sha256Pattern = new Regex("^[0-9a-fA-F]{64}$", RegexOptions.CultureInvariant);

	private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
	{
		MaxDepth = 32
	};

	internal static async Task<CloudSaveSnapshotManifest> VerifyAsync(string archivePath, CloudSaveRestoreVersion version, CancellationToken cancellationToken = default(CancellationToken))
	{
		FileInfo fileInfo = new FileInfo(archivePath);
		bool flag = !fileInfo.Exists || fileInfo.Length != version.ArchiveSize;
		if (!flag)
		{
			long length = fileInfo.Length;
			bool flag2 = ((length <= 0 || length > 21474836480L) ? true : false);
			flag = flag2;
		}
		if (flag)
		{
			throw new InvalidDataException("Bản lưu cục bộ không khớp kích thước đã xác minh trên Drive.");
		}
		CloudSaveSnapshotManifest result;
		await using (FileStream stream = new FileStream(archivePath, FileMode.Open, FileAccess.Read, FileShare.Read, 131072, FileOptions.Asynchronous | FileOptions.SequentialScan))
		{
			using ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true, Encoding.UTF8);
			int count = archive.Entries.Count;
			if ((count < 2 || count > 100004) ? true : false)
			{
				throw new InvalidDataException("Bản lưu có số lượng file ZIP không hợp lệ.");
			}
			Dictionary<string, ZipArchiveEntry> entries = new Dictionary<string, ZipArchiveEntry>(StringComparer.OrdinalIgnoreCase);
			long num = 0L;
			foreach (ZipArchiveEntry entry in archive.Entries)
			{
				cancellationToken.ThrowIfCancellationRequested();
				if (entry.FullName.EndsWith("/", StringComparison.Ordinal) || SafeRelativePath(entry.FullName) != entry.FullName || !entries.TryAdd(entry.FullName, entry) || ((entry.ExternalAttributes >> 16) & 0xF000) == 40960 || entry.Length < 0 || num > 21474836480L - entry.Length)
				{
					throw new InvalidDataException("Bản lưu chứa file hoặc đường dẫn ZIP không an toàn.");
				}
				num += entry.Length;
			}
			flag = !entries.TryGetValue("snapshot.json", out ZipArchiveEntry manifestEntry);
			if (!flag)
			{
				long length = manifestEntry.Length;
				bool flag2 = ((length <= 0 || length > 33554432) ? true : false);
				flag = flag2;
			}
			if (flag)
			{
				throw new InvalidDataException("Bản lưu thiếu snapshot.json hợp lệ.");
			}
			CloudSaveSnapshotManifest manifest;
			await using (Stream manifestStream = manifestEntry.Open())
			{
				using MemoryStream boundedManifest = new MemoryStream();
				byte[] buffer = new byte[65536];
				while (true)
				{
					int num2 = await manifestStream.ReadAsync(buffer, cancellationToken);
					if (num2 == 0)
					{
						break;
					}
					if (boundedManifest.Length > 33554432L - (long)num2)
					{
						throw new InvalidDataException("snapshot.json vượt giới hạn kích thước an toàn.");
					}
					boundedManifest.Write(buffer, 0, num2);
				}
				if (boundedManifest.Length != manifestEntry.Length)
				{
					throw new InvalidDataException("snapshot.json không khớp kích thước ZIP.");
				}
				boundedManifest.Position = 0L;
				try
				{
					manifest = (await JsonSerializer.DeserializeAsync<CloudSaveSnapshotManifest>(boundedManifest, JsonOptions, cancellationToken)) ?? throw new InvalidDataException("snapshot.json không hợp lệ.");
				}
				catch (JsonException innerException)
				{
					throw new InvalidDataException("snapshot.json bị hỏng.", innerException);
				}
			}
			flag = manifest.FormatVersion != 1 || manifest.CloudRedirectVersion != "2.6.5" || manifest.NestGUserId != version.NestGUserId || manifest.LibraryId != version.LibraryId || manifest.SteamAccountId != version.SteamAccountId || manifest.SteamAppId != version.SteamAppId || manifest.SnapshotId != version.SnapshotId || manifest.OperationId != version.OperationId || manifest.Files == null || manifest.Files.Count != version.FileCount;
			if (!flag)
			{
				count = manifest.Files.Count;
				bool flag2 = ((count < 1 || count > 100000) ? true : false);
				flag = flag2;
			}
			if (flag || manifest.CloudRedirectMetadata == null)
			{
				throw new InvalidDataException("Bản lưu không thuộc đúng NestG user, game hoặc Steam account.");
			}
			HashSet<string> expectedEntries = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "snapshot.json" };
			long saveBytes = 0L;
			foreach (CloudSaveSnapshotFile file in manifest.Files)
			{
				cancellationToken.ThrowIfCancellationRequested();
				string text = SafeRelativePath(file.RelativePath);
				if (text != file.RelativePath || file.Size < 0 || saveBytes > 21474836480L - file.Size || !Sha1Pattern.IsMatch(file.Sha1 ?? string.Empty) || !Sha256Pattern.IsMatch(file.Sha256 ?? string.Empty))
				{
					throw new InvalidDataException("snapshot.json chứa file save không hợp lệ.");
				}
				saveBytes += file.Size;
				string text2 = "files/" + text;
				if (!expectedEntries.Add(text2) || !entries.TryGetValue(text2, out ZipArchiveEntry value) || value.Length != file.Size)
				{
					throw new InvalidDataException("Bản lưu thiếu file save hoặc chứa tên file trùng.");
				}
				await VerifyEntryAsync(value, file.Size, file.Sha1, file.Sha256, cancellationToken);
			}
			foreach (CloudSaveSnapshotMetadataFile cloudRedirectMetadatum in manifest.CloudRedirectMetadata)
			{
				cancellationToken.ThrowIfCancellationRequested();
				switch (cloudRedirectMetadatum.Name)
				{
				case "manifest.cloudredirect":
				case "manifest.dat":
				case "cn.cloudredirect":
				case "cn.dat":
					flag = true;
					break;
				default:
					flag = false;
					break;
				}
				bool flag2 = !flag;
				if (!flag2)
				{
					long length = cloudRedirectMetadatum.Size;
					bool flag3 = ((length < 0 || length > 16777216) ? true : false);
					flag2 = flag3;
				}
				if (flag2 || !Sha256Pattern.IsMatch(cloudRedirectMetadatum.Sha256 ?? string.Empty))
				{
					throw new InvalidDataException("Bản lưu chứa metadata CloudRedirect không hợp lệ.");
				}
				string text3 = "cloudredirect/metadata/" + cloudRedirectMetadatum.Name;
				if (!expectedEntries.Add(text3) || !entries.TryGetValue(text3, out ZipArchiveEntry value2) || value2.Length != cloudRedirectMetadatum.Size)
				{
					throw new InvalidDataException("Bản lưu thiếu metadata CloudRedirect hoặc chứa tên trùng.");
				}
				await VerifyEntryAsync(value2, cloudRedirectMetadatum.Size, null, cloudRedirectMetadatum.Sha256, cancellationToken);
			}
			await VerifyCloudRedirectMetadataAsync(manifest, entries, cancellationToken);
			if (expectedEntries.Count != entries.Count)
			{
				throw new InvalidDataException("Bản lưu chứa dữ liệu ngoài danh sách snapshot.json.");
			}
			result = manifest;
		}
		return result;
	}

	private static async Task VerifyCloudRedirectMetadataAsync(CloudSaveSnapshotManifest snapshot, Dictionary<string, ZipArchiveEntry> entries, CancellationToken cancellationToken)
	{
		CloudSaveSnapshotMetadataFile[] array = snapshot.CloudRedirectMetadata.Where(delegate(CloudSaveSnapshotMetadataFile item)
		{
			string name = item.Name;
			return (name == "manifest.cloudredirect" || name == "manifest.dat") ? true : false;
		}).ToArray();
		CloudSaveSnapshotMetadataFile[] cnNames = snapshot.CloudRedirectMetadata.Where(delegate(CloudSaveSnapshotMetadataFile item)
		{
			string name2 = item.Name;
			return (name2 == "cn.cloudredirect" || name2 == "cn.dat") ? true : false;
		}).ToArray();
		if (array.Length != 1 || cnNames.Length > 1)
		{
			throw new InvalidDataException("Snapshot thiếu hoặc trùng metadata CloudRedirect.");
		}
		byte[] array2 = await ReadBoundedEntryAsync(entries["cloudredirect/metadata/" + array[0].Name], 16777216, cancellationToken);
		try
		{
			using JsonDocument jsonDocument = JsonDocument.Parse(array2, new JsonDocumentOptions
			{
				MaxDepth = 32,
				AllowTrailingCommas = false,
				CommentHandling = JsonCommentHandling.Disallow
			});
			if (jsonDocument.RootElement.ValueKind != JsonValueKind.Object || jsonDocument.RootElement.EnumerateObject().Count() != snapshot.Files.Count)
			{
				throw new InvalidDataException("CloudRedirect manifest trong ZIP không khớp số file save.");
			}
			Dictionary<string, CloudSaveSnapshotFile> dictionary = snapshot.Files.ToDictionary<CloudSaveSnapshotFile, string>((CloudSaveSnapshotFile file) => file.RelativePath, StringComparer.OrdinalIgnoreCase);
			HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach (JsonProperty item in jsonDocument.RootElement.EnumerateObject())
			{
				if (SafeRelativePath(item.Name) != item.Name || !hashSet.Add(item.Name) || !dictionary.TryGetValue(item.Name, out var value) || item.Value.ValueKind != JsonValueKind.Object || !item.Value.TryGetProperty("sha", out var value2) || value2.ValueKind != JsonValueKind.String || !item.Value.TryGetProperty("size", out var value3) || !value3.TryGetInt64(out var value4) || value4 != value.Size || !string.Equals(value2.GetString(), value.Sha1, StringComparison.OrdinalIgnoreCase))
				{
					throw new InvalidDataException("CloudRedirect manifest trong ZIP không khớp file save.");
				}
			}
			if (hashSet.Count != dictionary.Count)
			{
				throw new InvalidDataException("CloudRedirect manifest trong ZIP thiếu file save.");
			}
		}
		catch (JsonException innerException)
		{
			throw new InvalidDataException("CloudRedirect manifest trong ZIP bị hỏng.", innerException);
		}
		if (cnNames.Length == 0)
		{
			if (snapshot.CloudRedirectChangeNumber != 0L)
			{
				throw new InvalidDataException("Snapshot change number không khớp metadata CloudRedirect.");
			}
			return;
		}
		byte[] bytes = await ReadBoundedEntryAsync(entries["cloudredirect/metadata/" + cnNames[0].Name], 128, cancellationToken);
		if (ulong.TryParse(Encoding.ASCII.GetString(bytes).Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var result) && result == snapshot.CloudRedirectChangeNumber)
		{
			return;
		}
		throw new InvalidDataException("Snapshot change number không khớp metadata CloudRedirect.");
	}

	private static async Task<byte[]> ReadBoundedEntryAsync(ZipArchiveEntry entry, int maximumBytes, CancellationToken cancellationToken)
	{
		long length = entry.Length;
		bool flag = ((length < 0 || length > int.MaxValue) ? true : false);
		if (flag || entry.Length > maximumBytes)
		{
			throw new InvalidDataException("Metadata CloudRedirect vượt giới hạn kích thước.");
		}
		byte[] result;
		await using (Stream source = entry.Open())
		{
			using MemoryStream output = new MemoryStream((int)entry.Length);
			byte[] buffer = new byte[65536];
			while (true)
			{
				int num = await source.ReadAsync(buffer, cancellationToken);
				if (num == 0)
				{
					break;
				}
				if (output.Length > maximumBytes - num)
				{
					throw new InvalidDataException("Metadata CloudRedirect vượt giới hạn kích thước.");
				}
				output.Write(buffer, 0, num);
			}
			if (output.Length != entry.Length)
			{
				throw new InvalidDataException("Metadata CloudRedirect không khớp kích thước ZIP.");
			}
			result = output.ToArray();
		}
		return result;
	}

	internal static string SafeRelativePath(string? value)
	{
		if (string.IsNullOrWhiteSpace(value) || Path.IsPathRooted(value) || value.Contains(':', StringComparison.Ordinal) || value.Contains('\0', StringComparison.Ordinal))
		{
			throw new InvalidDataException("Đường dẫn save không an toàn.");
		}
		string[] array = value.Replace('\\', '/').Split('/');
		if (array.Length == 0 || array.Any(delegate(string part)
		{
			bool flag = part.Length == 0;
			if (!flag)
			{
				bool flag2 = ((part == "." || part == "..") ? true : false);
				flag = flag2;
			}
			return flag || part.EndsWith(' ') || part.EndsWith('.') || part.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || IsWindowsDeviceName(part);
		}))
		{
			throw new InvalidDataException("Đường dẫn save không an toàn.");
		}
		return string.Join('/', array);
	}

	private static bool IsWindowsDeviceName(string part)
	{
		string text = part.Split('.', 2)[0].ToUpperInvariant();
		bool flag;
		switch (text)
		{
		case "CON":
		case "PRN":
		case "AUX":
		case "NUL":
			flag = true;
			break;
		default:
			flag = false;
			break;
		}
		bool flag2 = flag;
		if (!flag2)
		{
			bool flag3 = text.Length == 4;
			if (flag3)
			{
				string text2 = text.Substring(0, 3);
				bool flag4 = ((text2 == "COM" || text2 == "LPT") ? true : false);
				flag3 = flag4;
			}
			int num;
			if (flag3)
			{
				char c = text[3];
				num = ((c >= '1' && c <= '9') ? 1 : 0);
			}
			else
			{
				num = 0;
			}
			flag2 = (byte)num != 0;
		}
		return flag2;
	}

	private static async Task VerifyEntryAsync(ZipArchiveEntry entry, long expectedSize, string? expectedSha1, string expectedSha256, CancellationToken cancellationToken)
	{
		await using Stream source = entry.Open();
		using IncrementalHash sha256 = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
		using IncrementalHash sha1 = ((expectedSha1 == null) ? null : IncrementalHash.CreateHash(HashAlgorithmName.SHA1));
		byte[] buffer = new byte[131072];
		long readBytes = 0L;
		while (true)
		{
			int num = await source.ReadAsync(buffer, cancellationToken);
			if (num == 0)
			{
				break;
			}
			readBytes += num;
			if (readBytes > expectedSize)
			{
				throw new InvalidDataException("File trong bản lưu vượt kích thước đã công bố.");
			}
			sha1?.AppendData(buffer, 0, num);
			sha256.AppendData(buffer, 0, num);
		}
		if (readBytes != expectedSize || !Convert.ToHexString(sha256.GetHashAndReset()).Equals(expectedSha256, StringComparison.OrdinalIgnoreCase) || (sha1 != null && !Convert.ToHexString(sha1.GetHashAndReset()).Equals(expectedSha1, StringComparison.OrdinalIgnoreCase)))
		{
			throw new InvalidDataException("File trong bản lưu không khớp checksum.");
		}
	}
}
