using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;

namespace app_desktop.Services.WindowsUpdate;

internal sealed class WubPackageStore
{
	internal const string OfficialPage = "https://www.sordum.org/9470/windows-update-blocker-v1-8/";

	internal const string LicensePage = "https://www.sordum.org/eula/";

	internal const string PackageSha256 = "C209BC42E43E3D654925B943E2BABA819A864897F1C33005AFC3751305661EE6";

	internal const string ExecutableSha256 = "187EEEE9E518011DE1B87CFB0ED03E12EA551E9011F0C8DEFDD0E4535E672DA2";

	internal const string IniSha256 = "067E8D757952C5E717A3E8CEF4D339DF8806802D85929B4B23EB2D624A70A48E";

	private const long MaximumPackageBytes = 3145728L;

	private readonly string root;

	internal string ExecutablePath => Path.Combine(root, "Wub_x64.exe");

	internal string IniPath => Path.Combine(root, "Wub.ini");

	internal bool IsReady
	{
		get
		{
			if (HasHash(ExecutablePath, "187EEEE9E518011DE1B87CFB0ED03E12EA551E9011F0C8DEFDD0E4535E672DA2", 961600L))
			{
				return HasHash(IniPath, "067E8D757952C5E717A3E8CEF4D339DF8806802D85929B4B23EB2D624A70A48E", 106548L);
			}
			return false;
		}
	}

	internal WubPackageStore(string? root = null)
	{
		this.root = root ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NestGLauncher", "WindowsUpdate", "Wub-v1.8");
	}

	internal void Import(string archivePath)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(archivePath, "archivePath");
		FileInfo fileInfo = new FileInfo(archivePath);
		bool flag = !fileInfo.Exists;
		if (!flag)
		{
			long length = fileInfo.Length;
			bool flag2 = ((length <= 0 || length > 3145728) ? true : false);
			flag = flag2;
		}
		if (flag || !HasHash(fileInfo.FullName, "C209BC42E43E3D654925B943E2BABA819A864897F1C33005AFC3751305661EE6", null))
		{
			throw new InvalidDataException("Gói WUB không khớp bản v1.8 chính thức được NestG hỗ trợ.");
		}
		using ZipArchive zipArchive = ZipFile.OpenRead(fileInfo.FullName);
		ZipArchiveEntry entry = zipArchive.GetEntry("Wub/Wub_x64.exe");
		ZipArchiveEntry entry2 = zipArchive.GetEntry("Wub/Wub.ini");
		if (entry == null || entry2 == null || entry.Length != 961600 || entry2.Length != 106548)
		{
			throw new InvalidDataException("Gói WUB thiếu file hoặc có kích thước không hợp lệ.");
		}
		byte[] bytes = ReadPinnedEntry(entry, "187EEEE9E518011DE1B87CFB0ED03E12EA551E9011F0C8DEFDD0E4535E672DA2");
		byte[] bytes2 = ReadPinnedEntry(entry2, "067E8D757952C5E717A3E8CEF4D339DF8806802D85929B4B23EB2D624A70A48E");
		Directory.CreateDirectory(root);
		WriteAtomically(ExecutablePath, bytes);
		WriteAtomically(IniPath, bytes2);
		if (!IsReady)
		{
			throw new InvalidDataException("Không xác minh được WUB sau khi nhập gói.");
		}
	}

	private static byte[] ReadPinnedEntry(ZipArchiveEntry entry, string expectedHash)
	{
		using Stream stream = entry.Open();
		using MemoryStream memoryStream = new MemoryStream((int)entry.Length);
		stream.CopyTo(memoryStream);
		if (memoryStream.Length != entry.Length || !Convert.ToHexString(SHA256.HashData(memoryStream.GetBuffer().AsSpan(0, (int)memoryStream.Length))).Equals(expectedHash, StringComparison.OrdinalIgnoreCase))
		{
			throw new InvalidDataException("Nội dung file WUB không khớp bản chính thức.");
		}
		return memoryStream.ToArray();
	}

	private static void WriteAtomically(string path, byte[] bytes)
	{
		string text = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
		try
		{
			using (FileStream fileStream = new FileStream(text, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
			{
				fileStream.Write(bytes);
				fileStream.Flush(flushToDisk: true);
			}
			File.Move(text, path, overwrite: true);
		}
		finally
		{
			if (File.Exists(text))
			{
				File.Delete(text);
			}
		}
	}

	internal static bool HasHash(string path, string expectedHash, long? expectedLength = null)
	{
		try
		{
			using FileStream fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
			if (expectedLength.HasValue)
			{
				long valueOrDefault = expectedLength.GetValueOrDefault();
				if (fileStream.Length != valueOrDefault)
				{
					return false;
				}
			}
			return Convert.ToHexString(SHA256.HashData(fileStream)).Equals(expectedHash, StringComparison.OrdinalIgnoreCase);
		}
		catch (IOException)
		{
			return false;
		}
		catch (UnauthorizedAccessException)
		{
			return false;
		}
	}
}
