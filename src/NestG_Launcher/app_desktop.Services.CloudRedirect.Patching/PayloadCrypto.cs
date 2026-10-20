using System.Security.Cryptography;

namespace app_desktop.Services.CloudRedirect.Patching;

internal static class PayloadCrypto
{
	public static byte[] AesCbcDecrypt(byte[] ct, byte[] key, byte[] iv)
	{
		using Aes aes = Aes.Create();
		aes.Key = key;
		aes.IV = iv;
		aes.Mode = CipherMode.CBC;
		aes.Padding = PaddingMode.PKCS7;
		using ICryptoTransform cryptoTransform = aes.CreateDecryptor();
		return cryptoTransform.TransformFinalBlock(ct, 0, ct.Length);
	}

	public static byte[] AesCbcEncrypt(byte[] pt, byte[] key, byte[] iv)
	{
		using Aes aes = Aes.Create();
		aes.Key = key;
		aes.IV = iv;
		aes.Mode = CipherMode.CBC;
		aes.Padding = PaddingMode.PKCS7;
		using ICryptoTransform cryptoTransform = aes.CreateEncryptor();
		return cryptoTransform.TransformFinalBlock(pt, 0, pt.Length);
	}
}
