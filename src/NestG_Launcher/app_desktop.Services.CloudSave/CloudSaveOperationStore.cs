using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace app_desktop.Services.CloudSave;

internal sealed class CloudSaveOperationStore
{
	private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("NestGLauncher.CloudSave.Operation.v1");

	private readonly string operationDirectory;

	internal CloudSaveOperationStore(string storageFolderName, string? localAppDataRoot = null)
	{
		operationDirectory = Path.Combine(CloudSaveStoragePath.GetRoot(storageFolderName, localAppDataRoot), "CloudSave", "Operations");
	}

	internal async Task<CloudSaveOperation?> FindResumableAsync(Guid nestGUserId, Guid libraryId, int steamAppId, CancellationToken cancellationToken = default(CancellationToken))
	{
		if (!Directory.Exists(operationDirectory))
		{
			return null;
		}
		CloudSaveOperation newest = null;
		foreach (string item in Directory.EnumerateFiles(operationDirectory, "*.bin", SearchOption.TopDirectoryOnly).Take(128))
		{
			cancellationToken.ThrowIfCancellationRequested();
			CloudSaveOperation cloudSaveOperation = await ProtectedJsonFile.LoadAsync<CloudSaveOperation>(item, Entropy, cancellationToken);
			if (cloudSaveOperation != null && !(cloudSaveOperation.NestGUserId != nestGUserId) && !(cloudSaveOperation.LibraryId != libraryId) && cloudSaveOperation.SteamAppId == steamAppId && cloudSaveOperation.Stage != CloudSaveOperationStage.Completed && File.Exists(cloudSaveOperation.ArchivePath) && (newest == null || cloudSaveOperation.UpdatedAtUtc > newest.UpdatedAtUtc))
			{
				newest = cloudSaveOperation;
			}
		}
		return newest;
	}

	internal Task SaveAsync(CloudSaveOperation operation, CancellationToken cancellationToken = default(CancellationToken))
	{
		ArgumentNullException.ThrowIfNull(operation, "operation");
		if (!Guid.TryParseExact(operation.OperationId, "N", out var _))
		{
			throw new InvalidOperationException("Cloud Save operation ID không hợp lệ.");
		}
		operation.UpdatedAtUtc = DateTimeOffset.UtcNow;
		return ProtectedJsonFile.SaveAsync(Path.Combine(operationDirectory, operation.OperationId + ".bin"), operation, Entropy, cancellationToken);
	}

	internal void Delete(string operationId)
	{
		if (Guid.TryParseExact(operationId, "N", out var _))
		{
			ProtectedJsonFile.TryDelete(Path.Combine(operationDirectory, operationId + ".bin"));
		}
	}
}
