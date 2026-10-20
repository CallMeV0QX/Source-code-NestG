using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace app_desktop.Services.CloudRedirect;

internal static class CloudRedirectTransactionJournal
{
	private sealed record Record(string TransactionPath, CloudRedirectInstallTransaction Transaction);

	private const int MaximumBytes = 8388608;

	private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("NestG.CloudRedirect.Transaction.v2");

	private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
	{
		WriteIndented = true
	};

	internal static string ProtectedPath(string path)
	{
		return Path.Combine(Path.GetDirectoryName(path), "transaction.dat");
	}

	internal static bool Exists(string path)
	{
		if (!CloudRedirectRuntimeService.ExistingFile(ProtectedPath(path)))
		{
			return CloudRedirectRuntimeService.ExistingFile(path);
		}
		return true;
	}

	internal static IEnumerable<string> EnumeratePaths(string root)
	{
		CloudRedirectRuntimeService.EnsureRecoveryPathIsNotLinked(root);
		return (from path in Directory.EnumerateFiles(root, "transaction.*", new EnumerationOptions
			{
				RecurseSubdirectories = true,
				AttributesToSkip = FileAttributes.ReparsePoint,
				IgnoreInaccessible = false
			}).Where(delegate(string path)
			{
				string fileName = Path.GetFileName(path);
				return (fileName == "transaction.json" || fileName == "transaction.dat") ? true : false;
			})
			select Path.Combine(Path.GetDirectoryName(path), "transaction.json")).Distinct<string>(StringComparer.OrdinalIgnoreCase);
	}

	internal static CloudRedirectInstallTransaction Read(string path, bool requireProtected = false)
	{
		path = Path.GetFullPath(path);
		CloudRedirectRuntimeService.EnsureRecoveryPathIsNotLinked(path);
		string path2 = ProtectedPath(path);
		CloudRedirectRuntimeService.EnsureRecoveryPathIsNotLinked(path2);
		if (CloudRedirectRuntimeService.ExistingFile(path2))
		{
			try
			{
				Record record = JsonSerializer.Deserialize<Record>(ProtectedData.Unprotect(ReadBounded(path2), Entropy, DataProtectionScope.CurrentUser)) ?? throw new InvalidDataException("Nhật ký bảo vệ trống.");
				if (string.Equals(record.TransactionPath, path, StringComparison.OrdinalIgnoreCase))
				{
					CloudRedirectInstallTransaction transaction = record.Transaction;
					if (transaction != null && transaction.SchemaVersion == 2 && Guid.TryParseExact(transaction.TransactionId, "N", out var _))
					{
						return transaction;
					}
				}
				throw new InvalidDataException("Nhật ký không thuộc transaction này.");
			}
			catch (Exception ex) when (((ex is CryptographicException || ex is JsonException) ? 1 : 0) != 0)
			{
				throw new InvalidDataException("Không xác minh được nhật ký transaction.", ex);
			}
		}
		if (requireProtected)
		{
			throw new SteamRecoveryPendingException("Backup cũ hoặc thiếu nhật ký bảo vệ; giữ nguyên file để kiểm tra.");
		}
		CloudRedirectInstallTransaction? obj = JsonSerializer.Deserialize<CloudRedirectInstallTransaction>(ReadBounded(path), Options) ?? throw new InvalidDataException("Transaction trống.");
		if (obj.SchemaVersion != 0)
		{
			throw new SteamRecoveryPendingException("Thiếu nhật ký bảo vệ của backup; giữ nguyên file để kiểm tra.");
		}
		return obj;
	}

	internal static void Save(string path, CloudRedirectInstallTransaction transaction)
	{
		if (transaction.SchemaVersion != 2 || !Guid.TryParseExact(transaction.TransactionId, "N", out var _))
		{
			throw new InvalidDataException("Không được tự cấp quyền cho nhật ký backup cũ.");
		}
		path = Path.GetFullPath(path);
		CloudRedirectRuntimeService.ValidateTransactionScope(path, transaction);
		string path2 = ProtectedPath(path);
		CloudRedirectRuntimeService.EnsureRecoveryPathIsNotLinked(path);
		CloudRedirectRuntimeService.EnsureRecoveryPathIsNotLinked(path2);
		if (CloudRedirectRuntimeService.ExistingFile(path2) && Read(path, requireProtected: true).TransactionId != transaction.TransactionId)
		{
			throw new InvalidDataException("Không được thay thế nhật ký của transaction khác.");
		}
		byte[] array = JsonSerializer.SerializeToUtf8Bytes(new Record(path, transaction), Options);
		if (array.Length > 8388608)
		{
			throw new InvalidDataException("Nhật ký transaction vượt giới hạn.");
		}
		byte[] array2 = ProtectedData.Protect(array, Entropy, DataProtectionScope.CurrentUser);
		if (array2.Length > 8388608)
		{
			throw new InvalidDataException("Nhật ký transaction vượt giới hạn.");
		}
		Publish(path2, array2);
		Publish(path, JsonSerializer.SerializeToUtf8Bytes(transaction, Options));
	}

	private static byte[] ReadBounded(string path)
	{
		using FileStream fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
		if (fileStream.Length > 8388608)
		{
			throw new InvalidDataException("Nhật ký transaction vượt giới hạn.");
		}
		using MemoryStream memoryStream = new MemoryStream();
		fileStream.CopyTo(memoryStream);
		if (memoryStream.Length > 8388608)
		{
			throw new InvalidDataException("Nhật ký transaction vượt giới hạn.");
		}
		return memoryStream.ToArray();
	}

	private static void Publish(string path, byte[] bytes)
	{
		Directory.CreateDirectory(Path.GetDirectoryName(path));
		string text = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
		try
		{
			using (FileStream fileStream = new FileStream(text, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.WriteThrough))
			{
				fileStream.Write(bytes);
				fileStream.Flush(flushToDisk: true);
			}
			File.Move(text, path, overwrite: true);
		}
		finally
		{
			if (File.Exists(text))
			{
				File.Delete(text);
			}
		}
	}
}
