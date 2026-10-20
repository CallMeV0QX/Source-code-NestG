using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using app_desktop.Models;

namespace app_desktop.Infrastructure;

internal sealed class SecureTokenStore
{
	private static readonly byte[] AdditionalEntropy = Encoding.UTF8.GetBytes("NestGLauncher.Desktop.Auth");

	private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);

	private readonly string storagePath;

	public SecureTokenStore(string storageFolderName, string tokenFileName)
	{
		string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), storageFolderName, "Auth");
		storagePath = Path.Combine(path, tokenFileName);
	}

	public async Task<AuthSession?> LoadAsync(CancellationToken cancellationToken = default(CancellationToken))
	{
		if (!File.Exists(storagePath))
		{
			return null;
		}
		try
		{
			return JsonSerializer.Deserialize<AuthSession>(ProtectedData.Unprotect(await File.ReadAllBytesAsync(storagePath, cancellationToken), AdditionalEntropy, DataProtectionScope.CurrentUser), JsonOptions);
		}
		catch
		{
			await ClearAsync();
			return null;
		}
	}

	public async Task SaveAsync(AuthSession session, CancellationToken cancellationToken = default(CancellationToken))
	{
		Directory.CreateDirectory(Path.GetDirectoryName(storagePath));
		byte[] bytes = ProtectedData.Protect(JsonSerializer.SerializeToUtf8Bytes(session, JsonOptions), AdditionalEntropy, DataProtectionScope.CurrentUser);
		await File.WriteAllBytesAsync(storagePath, bytes, cancellationToken);
	}

	public Task ClearAsync()
	{
		try
		{
			if (File.Exists(storagePath))
			{
				File.Delete(storagePath);
			}
		}
		catch
		{
		}
		return Task.CompletedTask;
	}
}
