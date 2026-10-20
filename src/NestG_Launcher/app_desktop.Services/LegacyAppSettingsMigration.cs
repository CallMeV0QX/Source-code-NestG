using System;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using app_desktop.Models;

namespace app_desktop.Services;

internal static class LegacyAppSettingsMigration
{
	private static readonly JsonDocumentOptions DocumentOptions = new JsonDocumentOptions
	{
		CommentHandling = JsonCommentHandling.Skip,
		AllowTrailingCommas = true
	};

	internal static DesktopAppSettings ApplyAndRemove(string applicationDirectory, string protectedStoreDirectory, DesktopAppSettings currentSettings)
	{
		string text = Path.Combine(applicationDirectory, "appsettings.json");
		string text2 = Path.Combine(protectedStoreDirectory, "legacy-appsettings-dpapi.bin");
		if (File.Exists(text))
		{
			long length = new FileInfo(text).Length;
			if (length == 0L || length > 65536)
			{
				throw new InvalidDataException("Legacy launcher configuration has an invalid size.");
			}
			byte[] array = File.ReadAllBytes(text);
			try
			{
				ValidateLegacyJson(array);
				Directory.CreateDirectory(protectedStoreDirectory);
				byte[] array2 = ProtectedData.Protect(array, null, DataProtectionScope.CurrentUser);
				string text3 = text2 + ".tmp-" + Guid.NewGuid().ToString("N");
				try
				{
					File.WriteAllBytes(text3, array2);
					byte[] array3 = ProtectedData.Unprotect(File.ReadAllBytes(text3), null, DataProtectionScope.CurrentUser);
					try
					{
						if (!CryptographicOperations.FixedTimeEquals(array, array3))
						{
							throw new InvalidDataException("Legacy configuration backup failed validation.");
						}
					}
					finally
					{
						CryptographicOperations.ZeroMemory(array3);
					}
					File.Move(text3, text2, overwrite: true);
					File.Delete(text);
				}
				finally
				{
					if (File.Exists(text3))
					{
						File.Delete(text3);
					}
					CryptographicOperations.ZeroMemory(array2);
				}
			}
			finally
			{
				CryptographicOperations.ZeroMemory(array);
			}
		}
		if (!File.Exists(text2))
		{
			return currentSettings;
		}
		long length2 = new FileInfo(text2).Length;
		if (length2 == 0L || length2 > 131072)
		{
			throw new InvalidDataException("Protected legacy configuration has an invalid size.");
		}
		byte[] array4 = ProtectedData.Unprotect(File.ReadAllBytes(text2), null, DataProtectionScope.CurrentUser);
		try
		{
			using JsonDocument jsonDocument = JsonDocument.Parse(array4, DocumentOptions);
			if (!jsonDocument.RootElement.TryGetProperty("auth", out var value) || value.ValueKind != JsonValueKind.Object)
			{
				return currentSettings;
			}
			string text4 = ReadNonblank(value, "googleDriveClientId");
			string text5 = ReadNonblank(value, "googleDriveClientSecret");
			if ((!string.IsNullOrWhiteSpace(currentSettings.GoogleDriveClientId) || text4 == null) && (!string.IsNullOrWhiteSpace(currentSettings.GoogleDriveClientSecret) || text5 == null))
			{
				return currentSettings;
			}
			return currentSettings.WithOptionalOAuth(string.IsNullOrWhiteSpace(currentSettings.GoogleDriveClientId) ? (text4 ?? currentSettings.GoogleDriveClientId) : currentSettings.GoogleDriveClientId, string.IsNullOrWhiteSpace(currentSettings.GoogleDriveClientSecret) ? (text5 ?? currentSettings.GoogleDriveClientSecret) : currentSettings.GoogleDriveClientSecret);
		}
		finally
		{
			CryptographicOperations.ZeroMemory(array4);
		}
	}

	private static void ValidateLegacyJson(byte[] bytes)
	{
		if (bytes.Length == 0 || bytes.Length > 65536)
		{
			throw new InvalidDataException("Legacy launcher configuration has an invalid size.");
		}
		using JsonDocument jsonDocument = JsonDocument.Parse(bytes, DocumentOptions);
		if (!jsonDocument.RootElement.TryGetProperty("auth", out var value) || value.ValueKind != JsonValueKind.Object)
		{
			throw new InvalidDataException("Legacy launcher configuration is missing its auth section.");
		}
	}

	private static string? ReadNonblank(JsonElement section, string propertyName)
	{
		if (!section.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString()))
		{
			return null;
		}
		return value.GetString().Trim();
	}
}
