namespace app_desktop.Services.CloudRedirect.Patching;

internal static class SteamToolsCrypto
{
	internal static readonly byte[] AesKey = new byte[32]
	{
		49, 76, 32, 134, 21, 5, 116, 225, 92, 241,
		29, 27, 193, 113, 37, 26, 71, 8, 108, 0,
		38, 147, 85, 205, 81, 201, 58, 66, 60, 20,
		2, 148
	};
}
