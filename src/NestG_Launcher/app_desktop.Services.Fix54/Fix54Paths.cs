using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace app_desktop.Services.Fix54;

internal static class Fix54Paths
{
	internal static string SafePath(string raw)
	{
		if (string.IsNullOrWhiteSpace(raw) || raw.Length > 1024 || !Path.IsPathFullyQualified(raw) || raw.StartsWith("\\\\", StringComparison.Ordinal) || raw.Any(char.IsControl) || raw.IndexOfAny(new char[6] { '*', '?', '"', '<', '>', '|' }) >= 0 || raw.Split('\\', '/').Any(delegate(string part)
		{
			bool flag = ((part == "." || part == "..") ? true : false);
			return flag || part.EndsWith(' ') || part.EndsWith('.');
		}) || raw.AsSpan(2).Contains(':'))
		{
			throw new Fix54Exception("Đường dẫn game hoặc thư mục FIX54 chưa an toàn.");
		}
		return Path.TrimEndingDirectorySeparator(Path.GetFullPath(raw));
	}

	internal static bool IsInside(string child, string parent)
	{
		return child.StartsWith(Path.TrimEndingDirectorySeparator(parent) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
	}

	internal static void EnsureInside(string child, string parent)
	{
		if (!IsInside(SafePath(child), SafePath(parent)))
		{
			throw new Fix54Exception("FIX54 từ chối đường dẫn nằm ngoài thư mục đã xác minh.");
		}
	}

	internal static void EnsureNoReparse(string path)
	{
		string text = SafePath(path);
		while (!string.IsNullOrEmpty(text))
		{
			if ((File.Exists(text) || Directory.Exists(text)) && (File.GetAttributes(text) & FileAttributes.ReparsePoint) != 0)
			{
				throw new Fix54Exception("FIX54 không xử lý đường dẫn liên kết hoặc junction.");
			}
			text = Path.GetDirectoryName(text);
		}
	}

	internal static async Task<string> HashAsync(string path, CancellationToken cancellationToken)
	{
		EnsureNoReparse(path);
		string result;
		await using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 131072, FileOptions.Asynchronous | FileOptions.SequentialScan))
		{
			result = Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken));
		}
		return result;
	}

	internal static void ValidatePortableExecutable(string path)
	{
		EnsureNoReparse(path);
		using FileStream fileStream = File.OpenRead(path);
		if (fileStream.Length < 128)
		{
			throw new Fix54Exception("Tệp EXE sau xử lý chưa hợp lệ; giữ nguyên game.");
		}
		using BinaryReader binaryReader = new BinaryReader(fileStream);
		if (binaryReader.ReadUInt16() != 23117)
		{
			throw new Fix54Exception("Tệp EXE sau xử lý chưa hợp lệ; giữ nguyên game.");
		}
		fileStream.Position = 60L;
		int num = binaryReader.ReadInt32();
		if (num < 64 || num > fileStream.Length - 26)
		{
			throw new Fix54Exception("Tệp EXE sau xử lý chưa hợp lệ; giữ nguyên game.");
		}
		fileStream.Position = num;
		if (binaryReader.ReadUInt32() != 17744)
		{
			throw new Fix54Exception("Tệp EXE sau xử lý chưa hợp lệ; giữ nguyên game.");
		}
		fileStream.Position = num + 24;
		ushort num2 = binaryReader.ReadUInt16();
		if ((num2 != 267 && num2 != 523) || 1 == 0)
		{
			throw new Fix54Exception("Tệp EXE sau xử lý chưa hợp lệ; giữ nguyên game.");
		}
	}
}
