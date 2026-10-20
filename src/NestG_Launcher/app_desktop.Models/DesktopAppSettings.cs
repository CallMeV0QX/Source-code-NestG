using System;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NestG.Configuration;
using app_desktop.Services;

namespace app_desktop.Models;

internal sealed class DesktopAppSettings
{
	private static readonly JsonDocumentOptions DocumentOptions = new JsonDocumentOptions
	{
		CommentHandling = JsonCommentHandling.Skip,
		AllowTrailingCommas = true
	};

	public string ApiBaseUrl { get; init; } = "https://localhost:7023/";

	public bool RememberSessionByDefault { get; init; } = true;

	public int RefreshThresholdSeconds { get; init; } = 60;

	public string StorageFolderName { get; init; } = "NestGLauncher";

	public string TokenFileName { get; init; } = "desktop-auth-session.bin";

	public string Platform { get; init; } = "windows";

	public string ReleaseChannel { get; init; } = "stable";

	public string CurrentVersion { get; init; } = "2.2.21";

	public int CurrentBuildNumber { get; init; } = 1;

	public bool UseNewWebHomeForm { get; init; } = true;

	public string GoogleDriveClientId { get; init; } = string.Empty;

	public string? GoogleDriveClientSecret { get; init; }

	public TimeSpan RefreshThreshold => TimeSpan.FromSeconds(Math.Clamp(RefreshThresholdSeconds, 15, 300));

	public static Task<DesktopAppSettings> LoadForRuntimeAsync(string applicationDirectory, CancellationToken cancellationToken = default(CancellationToken))
	{
		cancellationToken.ThrowIfCancellationRequested();
		using FileStream encryptedStream = File.OpenRead(Path.Combine(applicationDirectory, "appsettings.enc"));
		byte[] array = ProtectedJsonConfiguration.Decrypt(encryptedStream);
		try
		{
			using JsonDocument document = JsonDocument.Parse(array, DocumentOptions);
			DesktopAppSettings currentSettings = ParseDocument(document, allowDefaults: false);
			string protectedStoreDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NestGLauncher", "Config");
			return Task.FromResult(LegacyAppSettingsMigration.ApplyAndRemove(applicationDirectory, protectedStoreDirectory, currentSettings));
		}
		finally
		{
			CryptographicOperations.ZeroMemory(array);
		}
	}

	public static async Task<DesktopAppSettings> LoadAsync(string configurationPath, CancellationToken cancellationToken = default(CancellationToken))
	{
		if (!File.Exists(configurationPath))
		{
			return CreateDefaults();
		}
		try
		{
			DesktopAppSettings result;
			await using (FileStream stream = File.OpenRead(configurationPath))
			{
				using JsonDocument document = await JsonDocument.ParseAsync(stream, DocumentOptions, cancellationToken);
				result = ParseDocument(document, allowDefaults: true);
			}
			return result;
		}
		catch
		{
			return CreateDefaults();
		}
	}

	private static DesktopAppSettings ParseDocument(JsonDocument document, bool allowDefaults)
	{
		if (!document.RootElement.TryGetProperty("auth", out var value) || value.ValueKind != JsonValueKind.Object)
		{
			if (allowDefaults)
			{
				return CreateDefaults();
			}
			throw new InvalidDataException("Protected launcher configuration is missing its auth section.");
		}
		if (!allowDefaults)
		{
			string text = ReadString(value, "apiBaseUrl");
			string value2 = ReadString(value, "currentVersion");
			int? num = ReadInt(value, "currentBuildNumber");
			Uri result;
			bool flag = string.IsNullOrWhiteSpace(text) || !Uri.TryCreate(text, UriKind.Absolute, out result) || (result.Scheme != Uri.UriSchemeHttp && result.Scheme != Uri.UriSchemeHttps) || string.IsNullOrWhiteSpace(value2);
			if (!flag)
			{
				bool flag2 = ((!num.HasValue || num.GetValueOrDefault() <= 0) ? true : false);
				flag = flag2;
			}
			if (flag)
			{
				throw new InvalidDataException("Protected launcher configuration is missing required release settings.");
			}
		}
		return new DesktopAppSettings
		{
			ApiBaseUrl = (ReadString(value, "apiBaseUrl") ?? "https://localhost:7023/"),
			RememberSessionByDefault = (ReadBool(value, "rememberSessionByDefault") ?? true),
			RefreshThresholdSeconds = (ReadInt(value, "refreshThresholdSeconds") ?? 60),
			StorageFolderName = (ReadString(value, "storageFolderName") ?? "NestGLauncher"),
			TokenFileName = (ReadString(value, "tokenFileName") ?? "desktop-auth-session.bin"),
			Platform = (ReadString(value, "platform") ?? "windows"),
			ReleaseChannel = (ReadString(value, "releaseChannel") ?? "stable"),
			CurrentVersion = (ReadString(value, "currentVersion") ?? "2.2.21"),
			CurrentBuildNumber = (ReadInt(value, "currentBuildNumber") ?? 1),
			UseNewWebHomeForm = (ReadBool(value, "useNewWebHomeForm") ?? true),
			GoogleDriveClientId = ResolveGoogleDriveClientId(value),
			GoogleDriveClientSecret = ResolveGoogleDriveClientSecret(value)
		};
	}

	private static DesktopAppSettings CreateDefaults()
	{
		return new DesktopAppSettings
		{
			GoogleDriveClientId = ReadGoogleDriveClientIdFromEnvironment(),
			GoogleDriveClientSecret = ReadGoogleDriveClientSecretFromEnvironment()
		};
	}

	internal DesktopAppSettings WithOptionalOAuth(string clientId, string? clientSecret)
	{
		return new DesktopAppSettings
		{
			ApiBaseUrl = ApiBaseUrl,
			RememberSessionByDefault = RememberSessionByDefault,
			RefreshThresholdSeconds = RefreshThresholdSeconds,
			StorageFolderName = StorageFolderName,
			TokenFileName = TokenFileName,
			Platform = Platform,
			ReleaseChannel = ReleaseChannel,
			CurrentVersion = CurrentVersion,
			CurrentBuildNumber = CurrentBuildNumber,
			UseNewWebHomeForm = UseNewWebHomeForm,
			GoogleDriveClientId = clientId,
			GoogleDriveClientSecret = clientSecret
		};
	}

	private static string? ReadString(JsonElement element, string propertyName)
	{
		if (!element.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.String)
		{
			return null;
		}
		return value.GetString();
	}

	private static bool? ReadBool(JsonElement element, string propertyName)
	{
		JsonElement value;
		bool flag = element.TryGetProperty(propertyName, out value);
		if (flag)
		{
			JsonValueKind valueKind = value.ValueKind;
			bool flag2 = valueKind - 5 <= JsonValueKind.Object;
			flag = flag2;
		}
		if (!flag)
		{
			return null;
		}
		return value.GetBoolean();
	}

	private static string ResolveGoogleDriveClientId(JsonElement authSection)
	{
		string text = ReadString(authSection, "googleDriveClientId");
		if (!string.IsNullOrWhiteSpace(text))
		{
			return text.Trim();
		}
		return ReadGoogleDriveClientIdFromEnvironment();
	}

	private static string ReadGoogleDriveClientIdFromEnvironment()
	{
		return Environment.GetEnvironmentVariable("NESTG_GOOGLE_DRIVE_CLIENT_ID")?.Trim() ?? string.Empty;
	}

	private static string? ReadGoogleDriveClientSecretFromEnvironment()
	{
		string environmentVariable = Environment.GetEnvironmentVariable("NESTG_GOOGLE_DRIVE_CLIENT_SECRET");
		if (!string.IsNullOrWhiteSpace(environmentVariable))
		{
			return environmentVariable.Trim();
		}
		return null;
	}

	private static string? ResolveGoogleDriveClientSecret(JsonElement authSection)
	{
		string text = ReadString(authSection, "googleDriveClientSecret");
		if (!string.IsNullOrWhiteSpace(text))
		{
			return text.Trim();
		}
		return ReadGoogleDriveClientSecretFromEnvironment();
	}

	private static int? ReadInt(JsonElement element, string propertyName)
	{
		if (!element.TryGetProperty(propertyName, out var value) || !value.TryGetInt32(out var value2))
		{
			return null;
		}
		return value2;
	}
}
