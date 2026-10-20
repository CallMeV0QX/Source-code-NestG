using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace app_desktop.Services.CloudSave;

internal sealed class CloudSaveTokenStore
{
	private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("NestGLauncher.CloudSave.GoogleDrive.v1");

	private readonly string tokenDirectory;

	internal CloudSaveTokenStore(string storageFolderName, string? localAppDataRoot = null)
	{
		tokenDirectory = Path.Combine(CloudSaveStoragePath.GetRoot(storageFolderName, localAppDataRoot), "CloudSave", "Tokens");
	}

	internal bool HasCredential(Guid nestGUserId)
	{
		if (nestGUserId != Guid.Empty)
		{
			return File.Exists(GetPath(nestGUserId));
		}
		return false;
	}

	internal Task<GoogleDriveCredential?> LoadAsync(Guid nestGUserId, CancellationToken cancellationToken = default(CancellationToken))
	{
		return ProtectedJsonFile.LoadAsync<GoogleDriveCredential>(GetPath(nestGUserId), Entropy, cancellationToken);
	}

	internal Task SaveAsync(GoogleDriveCredential credential, CancellationToken cancellationToken = default(CancellationToken))
	{
		ArgumentNullException.ThrowIfNull(credential, "credential");
		if (credential.NestGUserId == Guid.Empty)
		{
			throw new ArgumentException("NestG user không hợp lệ.", "credential");
		}
		return ProtectedJsonFile.SaveAsync(GetPath(credential.NestGUserId), credential, Entropy, cancellationToken);
	}

	internal Task ClearAsync(Guid nestGUserId)
	{
		ProtectedJsonFile.TryDelete(GetPath(nestGUserId));
		return Task.CompletedTask;
	}

	private string GetPath(Guid nestGUserId)
	{
		if (nestGUserId == Guid.Empty)
		{
			throw new ArgumentException("NestG user không hợp lệ.", "nestGUserId");
		}
		return Path.Combine(tokenDirectory, $"{nestGUserId:N}.bin");
	}
}
