using System;
using System.IO;

namespace UI_Desktop.Store;

internal static class StoreSupportContact
{
	internal const string PhoneNumber = "0947709029";

	internal const string ZaloUrl = "https://zalo.me/0947709029";

	private static readonly Lazy<string> QrData = new Lazy<string>(ReadQrData);

	internal static string QrDataUri => QrData.Value;

	internal static string QrAssetUrl
	{
		get
		{
			if (!File.Exists(Path.Combine(AppContext.BaseDirectory, "Assets", "zalo-qr.png")))
			{
				return string.Empty;
			}
			return "https://nestg-assets.local/zalo-qr.png";
		}
	}

	internal static bool IsCommand(string? type)
	{
		if (type == "openSupportZalo" || type == "copySupportPhone")
		{
			return true;
		}
		return false;
	}

	internal static bool IsTrustedAction(string source, string? requestedDocumentId, string? currentDocumentId)
	{
		if (source == "about:blank" && !string.IsNullOrEmpty(currentDocumentId))
		{
			return requestedDocumentId == currentDocumentId;
		}
		return false;
	}

	private static string ReadQrData()
	{
		try
		{
			string path = Path.Combine(AppContext.BaseDirectory, "Assets", "zalo-qr.png");
			return File.Exists(path) ? ("data:image/png;base64," + Convert.ToBase64String(File.ReadAllBytes(path))) : string.Empty;
		}
		catch (IOException)
		{
			return string.Empty;
		}
		catch (UnauthorizedAccessException)
		{
			return string.Empty;
		}
	}
}
