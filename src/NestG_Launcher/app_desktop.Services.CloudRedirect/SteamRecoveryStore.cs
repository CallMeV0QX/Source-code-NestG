using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace app_desktop.Services.CloudRedirect;

internal sealed class SteamRecoveryStore
{
	private const int MaximumJournalBytes = 1048576;

	private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("NestG.SteamAutoRecovery.v1");

	private readonly string localRoot;

	internal string JournalPath { get; }

	internal SteamRecoveryStore(string? localAppData = null)
	{
		localRoot = Path.GetFullPath(localAppData ?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
		JournalPath = Path.Combine(localRoot, "NestGLauncher", "CloudRedirect", "auto-recovery.dat");
	}

	internal string FallbackTransactionPath(string id)
	{
		return Path.Combine(localRoot, "NestGLauncher", "CloudRedirect", "Backups", "better-fallback-" + id, "transaction.json");
	}

	internal SteamRecoveryOperation? Load(string expectedSteamDirectory)
	{
		CloudRedirectRuntimeService.EnsureRecoveryPathIsNotLinked(JournalPath);
		if (!File.Exists(JournalPath))
		{
			return null;
		}
		if (new FileInfo(JournalPath).Length > 1048576)
		{
			throw new InvalidDataException("Recovery journal exceeds limit.");
		}
		try
		{
			SteamRecoveryOperation steamRecoveryOperation = JsonSerializer.Deserialize<SteamRecoveryOperation>(ProtectedData.Unprotect(File.ReadAllBytes(JournalPath), Entropy, DataProtectionScope.CurrentUser)) ?? throw new InvalidDataException("Recovery journal is empty.");
			Validate(steamRecoveryOperation, expectedSteamDirectory);
			return steamRecoveryOperation;
		}
		catch (Exception ex) when (((ex is CryptographicException || ex is JsonException) ? 1 : 0) != 0)
		{
			throw new InvalidDataException("Recovery journal cannot be verified.", ex);
		}
	}

	internal void Save(SteamRecoveryOperation operation)
	{
		Validate(operation, operation.SteamDirectory);
		CloudRedirectRuntimeService.EnsureRecoveryPathIsNotLinked(JournalPath);
		byte[] array = ProtectedData.Protect(JsonSerializer.SerializeToUtf8Bytes(operation), Entropy, DataProtectionScope.CurrentUser);
		if (array.Length > 1048576)
		{
			throw new InvalidDataException("Recovery journal exceeds limit.");
		}
		Directory.CreateDirectory(Path.GetDirectoryName(JournalPath));
		string text = JournalPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
		try
		{
			using (FileStream fileStream = new FileStream(text, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
			{
				fileStream.Write(array);
				fileStream.Flush(flushToDisk: true);
			}
			File.Move(text, JournalPath, overwrite: true);
		}
		finally
		{
			if (File.Exists(text))
			{
				File.Delete(text);
			}
		}
	}

	private void Validate(SteamRecoveryOperation operation, string expectedSteamDirectory)
	{
		if (operation.Schema != 1 || !Guid.TryParseExact(operation.Id, "N", out var _) || !Enum.IsDefined(operation.Phase) || !Enum.IsDefined(operation.Outcome) || !Path.IsPathFullyQualified(operation.SteamDirectory) || !string.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(operation.SteamDirectory)), Path.TrimEndingDirectorySeparator(Path.GetFullPath(expectedSteamDirectory)), StringComparison.OrdinalIgnoreCase))
		{
			throw new InvalidDataException("Recovery journal does not belong to this Steam installation.");
		}
		CloudRedirectRuntimeService.EnsureRecoveryPathIsNotLinked(operation.SteamDirectory);
		if ((operation.Phase == SteamRecoveryPhase.DisableCloud && (!operation.AllowBetterFallback || !operation.FallbackAttempted)) || (operation.Phase == SteamRecoveryPhase.VerifyBetter && (!operation.BetterOnly || (object)operation.BetterLogMarker == null)))
		{
			throw new InvalidDataException("Recovery fallback phase is inconsistent.");
		}
		if ((operation.AllowBetterFallback || operation.BetterOnly) && (operation.SteamAppId <= 0 || operation.ActivationFiles == null || operation.ActivationFiles.Count == 0 || operation.ActivationFiles.Count > 4096 || operation.ActivationFiles.Any((SteamActivationFileStamp file) => (object)file == null || !CloudRedirectRuntimeService.IsActivationFilePath(file.InstallPath) || string.IsNullOrEmpty(file.Sha256) || file.Sha256.Length != 64 || file.Sha256.Any((char c) => !Uri.IsHexDigit(c))) || operation.ActivationFiles.Select((SteamActivationFileStamp file) => file.InstallPath).Distinct<string>(StringComparer.OrdinalIgnoreCase).Count() != operation.ActivationFiles.Count))
		{
			throw new InvalidDataException("Recovery activation identity is invalid.");
		}
		if (!string.IsNullOrEmpty(operation.FallbackTransactionPath))
		{
			if (!string.Equals(Path.GetFullPath(operation.FallbackTransactionPath), FallbackTransactionPath(operation.Id), StringComparison.OrdinalIgnoreCase))
			{
				throw new InvalidDataException("Fallback transaction does not belong to this activation.");
			}
			CloudRedirectRuntimeService.EnsureRecoveryPathIsNotLinked(operation.FallbackTransactionPath);
		}
		CloudRedirectHealthMarker healthMarker = operation.HealthMarker;
		if ((object)healthMarker != null && !string.Equals(Path.GetFullPath(healthMarker.LogPath), Path.Combine(operation.SteamDirectory, "cloud_redirect.log"), StringComparison.OrdinalIgnoreCase))
		{
			throw new InvalidDataException("Recovery log path is invalid.");
		}
		if (!string.IsNullOrEmpty(operation.TransactionPath))
		{
			string value = Path.Combine(localRoot, "NestGLauncher", "CloudRedirect", "Backups") + Path.DirectorySeparatorChar;
			string fullPath = Path.GetFullPath(operation.TransactionPath);
			if (!fullPath.StartsWith(value, StringComparison.OrdinalIgnoreCase) || Path.GetFileName(fullPath) != "transaction.json")
			{
				throw new InvalidDataException("Recovery transaction is outside the backup root.");
			}
			CloudRedirectRuntimeService.EnsureRecoveryPathIsNotLinked(fullPath);
		}
	}
}
