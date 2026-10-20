using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace NestG.Configuration;

internal static class ProtectedJsonConfiguration
{
	private const int HeaderLength = 21;

	private const int AuthenticationLength = 32;

	private const int MaximumFileLength = 65605;

	internal static byte[] Decrypt(Stream encryptedStream)
	{
		using MemoryStream memoryStream = new MemoryStream();
		byte[] array = new byte[8192];
		int num;
		while ((num = encryptedStream.Read(array, 0, array.Length)) > 0)
		{
			if (memoryStream.Length + num > 65605)
			{
				throw new InvalidDataException("Protected configuration has an invalid size.");
			}
			memoryStream.Write(array, 0, num);
		}
		if (memoryStream.Length < 69 || memoryStream.Length > 65605)
		{
			throw new InvalidDataException("Protected configuration has an invalid size.");
		}
		byte[] array2 = memoryStream.ToArray();
		if (!array2.AsSpan(0, 4).SequenceEqual(Encoding.ASCII.GetBytes("NGCF")) || array2[4] != 1)
		{
			throw new InvalidDataException("Protected configuration has an invalid format.");
		}
		int num2 = array2.Length - 21 - 32;
		if (num2 % 16 != 0)
		{
			throw new InvalidDataException("Protected configuration has an invalid block length.");
		}
		byte[] encryptionKey = ProtectedConfigurationKey.GetEncryptionKey();
		byte[] authenticationKey = ProtectedConfigurationKey.GetAuthenticationKey();
		try
		{
			using (HMACSHA256 hMACSHA = new HMACSHA256(authenticationKey))
			{
				if (!CryptographicOperations.FixedTimeEquals(hMACSHA.ComputeHash(array2, 0, array2.Length - 32), array2.AsSpan(array2.Length - 32, 32)))
				{
					throw new InvalidDataException("Protected configuration failed validation.");
				}
			}
			using Aes aes = Aes.Create();
			aes.Key = encryptionKey;
			aes.IV = array2.AsSpan(5, 16).ToArray();
			aes.Mode = CipherMode.CBC;
			aes.Padding = PaddingMode.PKCS7;
			using ICryptoTransform cryptoTransform = aes.CreateDecryptor();
			return cryptoTransform.TransformFinalBlock(array2, 21, num2);
		}
		catch (CryptographicException)
		{
			throw new InvalidDataException("Protected configuration failed validation.");
		}
		finally
		{
			CryptographicOperations.ZeroMemory(encryptionKey);
			CryptographicOperations.ZeroMemory(authenticationKey);
			CryptographicOperations.ZeroMemory(array2);
		}
	}
}
