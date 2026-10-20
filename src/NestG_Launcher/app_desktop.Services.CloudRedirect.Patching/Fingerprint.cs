using System;
using System.IO;
using System.IO.Compression;
using System.Runtime.Intrinsics.X86;
using System.Security.Cryptography;
using System.Text;

namespace app_desktop.Services.CloudRedirect.Patching;

internal static class Fingerprint
{
	public static string? FindCachePath(string steamPath, bool verbose = true, Action<string>? log = null)
	{
		string text = Path.Combine(steamPath, "appcache", "httpcache", "3b");
		if (!Directory.Exists(text))
		{
			return null;
		}
		try
		{
			string text2 = Compute();
			string text3 = Path.Combine(text, text2);
			if (File.Exists(text3))
			{
				if (ValidatePayloadCache(text3))
				{
					if (verbose)
					{
						log?.Invoke("Cache: " + text3);
					}
					return text3;
				}
				if (verbose)
				{
					log?.Invoke("Cache at " + text3 + " failed validation, scanning..");
				}
			}
			else if (verbose)
			{
				log?.Invoke("Fingerprint " + text2 + " computed but no cache file there");
			}
		}
		catch (Exception ex)
		{
			if (verbose)
			{
				log?.Invoke("Fingerprint computation failed (" + ex.Message + "), scanning..");
			}
		}
		string[] files = Directory.GetFiles(text);
		foreach (string text4 in files)
		{
			string fileName = Path.GetFileName(text4);
			FileInfo fileInfo = new FileInfo(text4);
			if (fileName.Length != 16 || fileInfo.Length <= 500000 || fileInfo.Length >= 5000000)
			{
				continue;
			}
			if (!ValidatePayloadCache(text4))
			{
				if (verbose)
				{
					log?.Invoke("Cache candidate " + fileName + " failed validation, skipping");
				}
				continue;
			}
			if (verbose)
			{
				log?.Invoke("Cache (found by scan): " + text4);
			}
			return text4;
		}
		return null;
	}

	public static string GetExpectedCachePath(string steamPath)
	{
		return Path.Combine(Path.Combine(steamPath, "appcache", "httpcache", "3b"), Compute());
	}

	public static string ComputeFingerprint()
	{
		return Compute();
	}

	public static bool ValidatePayloadFile(string path)
	{
		return ValidatePayloadCache(path);
	}

	private static bool ValidatePayloadCache(string path)
	{
		try
		{
			byte[] array;
			using (FileStream fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
			{
				array = new byte[fileStream.Length];
				fileStream.ReadExactly(array);
			}
			if (array.Length < 32)
			{
				return false;
			}
			byte[] iv = array.AsSpan(0, 16).ToArray();
			byte[] array2 = PayloadCrypto.AesCbcDecrypt(array.AsSpan(16).ToArray(), SteamToolsCrypto.AesKey, iv);
			if (array2.Length < 6)
			{
				return false;
			}
			using ZLibStream zLibStream = new ZLibStream(new MemoryStream(array2, 4, array2.Length - 4), CompressionMode.Decompress);
			byte[] array3 = new byte[2];
			return zLibStream.ReadAtLeast(array3, 2, throwOnEndOfStream: false) >= 2 && array3[0] == 77 && array3[1] == 90;
		}
		catch
		{
			return false;
		}
	}

	private static string Compute()
	{
		if (!X86Base.IsSupported)
		{
			throw new PlatformNotSupportedException("CPU fingerprint requires x86/x64 CPUID support.");
		}
		(int, int, int, int) tuple = X86Base.CpuId(0, 0);
		byte[] array = new byte[12];
		BitConverter.TryWriteBytes(array.AsSpan(0, 4), (uint)tuple.Item2);
		BitConverter.TryWriteBytes(array.AsSpan(4, 4), (uint)tuple.Item4);
		BitConverter.TryWriteBytes(array.AsSpan(8, 4), (uint)tuple.Item3);
		string @string = Encoding.ASCII.GetString(array);
		(int Eax, int Ebx, int Ecx, int Edx) tuple2 = X86Base.CpuId(1, 0);
		int value = (tuple2.Eax >> 8) & 0xF;
		int value2 = (tuple2.Eax >> 4) & 0xF;
		int value3 = Environment.ProcessorCount & 0xFF;
		byte[] bytes = Encoding.ASCII.GetBytes($"V{@string}_F{value:X}_M{value2:X}_C{value3:X}");
		byte[] bytes2 = Encoding.ASCII.GetBytes("version");
		byte[] array2 = new byte[bytes.Length];
		for (int i = 0; i < bytes.Length; i++)
		{
			array2[i] = (byte)(bytes[i] ^ bytes2[i % bytes2.Length]);
		}
		byte[] bytes3 = Encoding.ASCII.GetBytes(Convert.ToHexString(MD5.HashData(array2)).ToLowerInvariant());
		ulong num = ulong.MaxValue;
		byte[] array3 = bytes3;
		foreach (byte b in array3)
		{
			num ^= b;
			for (int k = 0; k < 8; k++)
			{
				if ((num & 1) != 0L)
				{
					num ^= 0x85E1C3D753D46D27uL;
				}
				num >>= 1;
			}
		}
		return (num ^ 0xFFFFFFFFFFFFFFFFuL).ToString("X16");
	}
}
