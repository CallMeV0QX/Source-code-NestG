using System;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace app_desktop.Services.CloudSave;

internal static class ProtectedJsonFile
{
	private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);

	internal static async Task<T?> LoadAsync<T>(string path, byte[] entropy, CancellationToken cancellationToken, bool deleteOnError = true)
	{
		if (!File.Exists(path))
		{
			return default(T);
		}
		try
		{
			byte[] array = await File.ReadAllBytesAsync(path, cancellationToken);
			int num = array.Length;
			if ((num > 1048576 || num == 0) ? true : false)
			{
				throw new InvalidDataException("Protected Cloud Save state has an invalid size.");
			}
			return JsonSerializer.Deserialize<T>(ProtectedData.Unprotect(array, entropy, DataProtectionScope.CurrentUser), JsonOptions);
		}
		catch (OperationCanceledException)
		{
			throw;
		}
		catch (Exception innerException)
		{
			if (!deleteOnError)
			{
				throw new InvalidDataException("Protected Cloud Save restore journal is damaged.", innerException);
			}
			TryDelete(path);
			return default(T);
		}
	}

	internal static async Task SaveAsync<T>(string path, T value, byte[] entropy, CancellationToken cancellationToken)
	{
		Directory.CreateDirectory(Path.GetDirectoryName(path) ?? throw new InvalidOperationException("Cloud Save storage path không hợp lệ."));
		byte[] array = ProtectedData.Protect(JsonSerializer.SerializeToUtf8Bytes(value, JsonOptions), entropy, DataProtectionScope.CurrentUser);
		string temporaryPath = path + $".tmp.{Guid.NewGuid():N}";
		try
		{
			await using (FileStream stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, FileOptions.WriteThrough | FileOptions.Asynchronous))
			{
				await stream.WriteAsync(array, cancellationToken);
				await stream.FlushAsync(cancellationToken);
			}
			File.Move(temporaryPath, path, overwrite: true);
		}
		finally
		{
			TryDelete(temporaryPath);
		}
	}

	internal static void TryDelete(string path)
	{
		try
		{
			if (File.Exists(path))
			{
				File.Delete(path);
			}
		}
		catch
		{
		}
	}
}
