using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace app_desktop.Services.CloudRedirect;

internal static class FileUtils
{
	private static void WriteFlushAndPublish(string path, Action<FileStream> writer)
	{
		string text = path + ".tmp";
		try
		{
			using (FileStream fileStream = new FileStream(text, FileMode.Create, FileAccess.Write, FileShare.None, 81920, FileOptions.None))
			{
				writer(fileStream);
				fileStream.Flush(flushToDisk: true);
			}
			File.Move(text, path, overwrite: true);
		}
		catch
		{
			try
			{
				File.Delete(text);
			}
			catch
			{
			}
			throw;
		}
	}

	public static void AtomicWriteAllBytes(string path, byte[] data)
	{
		WriteFlushAndPublish(path, delegate(FileStream fs)
		{
			fs.Write(data, 0, data.Length);
		});
	}

	public static void AtomicWriteAllText(string path, string content)
	{
		byte[] bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(content);
		WriteFlushAndPublish(path, delegate(FileStream fs)
		{
			fs.Write(bytes, 0, bytes.Length);
		});
	}

	public static void AtomicWriteAllLines(string path, IEnumerable<string> lines)
	{
		WriteFlushAndPublish(path, delegate(FileStream fs)
		{
			using StreamWriter streamWriter = new StreamWriter(fs, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), 1024, leaveOpen: true);
			foreach (string line in lines)
			{
				streamWriter.WriteLine(line);
			}
			streamWriter.Flush();
		});
	}

	public static void AtomicCopy(string sourcePath, string destPath)
	{
		WriteFlushAndPublish(destPath, delegate(FileStream fs)
		{
			using FileStream fileStream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.SequentialScan);
			fileStream.CopyTo(fs);
		});
	}

	public static string FormatSize(long bytes)
	{
		if (bytes >= 1024)
		{
			if (bytes >= 1048576)
			{
				if (bytes >= 1073741824)
				{
					return $"{(double)bytes / 1073741824.0:F1} GB";
				}
				return $"{(double)bytes / 1048576.0:F1} MB";
			}
			return $"{(double)bytes / 1024.0:F1} KB";
		}
		return $"{bytes} B";
	}
}
