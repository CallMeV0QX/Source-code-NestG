using System;

namespace NestG.Configuration;

internal static class ProtectedConfigurationKey
{
	internal static byte[] GetEncryptionKey()
	{
		return Convert.FromBase64String("+Ry5j92i/WOUkeHKazmyf3QanmWf96qNZXINbIRscys=");
	}

	internal static byte[] GetAuthenticationKey()
	{
		return Convert.FromBase64String("Oz+JiHUWzsv6NVASzQRD5URhjpHpVQCIszVhBi08fOE=");
	}
}
