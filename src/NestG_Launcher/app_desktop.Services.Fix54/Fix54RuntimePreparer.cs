using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace app_desktop.Services.Fix54;

internal sealed class Fix54RuntimePreparer : IFix54RuntimePreparer
{
	internal const string ScriptUrl = "https://raw.githubusercontent.com/Peron4TheWin/ps1collection/refs/heads/main/steamless.ps1";

	internal const string ScriptSha256 = "71801501EACCE3E06BEBE3DE175ED0CD09D0A7F5B89B4FA9584DEF3C27C344B2";

	internal const int ScriptSize = 3009;

	internal const string ToolUrl = "https://github.com/atom0s/Steamless/releases/download/v3.1.0.5/Steamless.v3.1.0.5.-.by.atom0s.zip";

	internal const string ToolSha256 = "E3E2D22E098FF3FB359B2876AA2BED9596F0501E6FF588CBFFAE90A76D2DC4F5";

	internal const int ToolSize = 610646;

	public async Task<Fix54PreparedRuntime> PrepareAsync(string operationDirectory, CancellationToken cancellationToken)
	{
		byte[] script = await DownloadAsync(new Uri("https://raw.githubusercontent.com/Peron4TheWin/ps1collection/refs/heads/main/steamless.ps1"), 3009, cancellationToken);
		Verify(script, 3009, "71801501EACCE3E06BEBE3DE175ED0CD09D0A7F5B89B4FA9584DEF3C27C344B2");
		byte[] tool = await DownloadAsync(new Uri("https://github.com/atom0s/Steamless/releases/download/v3.1.0.5/Steamless.v3.1.0.5.-.by.atom0s.zip"), 610646, cancellationToken);
		Verify(tool, 610646, "E3E2D22E098FF3FB359B2876AA2BED9596F0501E6FF588CBFFAE90A76D2DC4F5");
		string scriptPath = Path.Combine(operationDirectory, "steamless.ps1");
		await File.WriteAllBytesAsync(scriptPath, script, cancellationToken);
		string toolRoot = Path.Combine(operationDirectory, "Temp", "steamless");
		Directory.CreateDirectory(toolRoot);
		ExtractVerifiedTool(tool, toolRoot);
		Fix54Paths.ValidatePortableExecutable(Path.Combine(toolRoot, "Steamless.CLI.exe"));
		List<FileStream> leases = new List<FileStream>();
		try
		{
			foreach (string item in Directory.EnumerateFiles(toolRoot, "*", SearchOption.AllDirectories).Prepend(scriptPath))
			{
				leases.Add(new FileStream(item, FileMode.Open, FileAccess.Read, FileShare.Read));
			}
			Verify(await File.ReadAllBytesAsync(scriptPath, cancellationToken), 3009, "71801501EACCE3E06BEBE3DE175ED0CD09D0A7F5B89B4FA9584DEF3C27C344B2");
			using ZipArchive archive = new ZipArchive(new MemoryStream(tool), ZipArchiveMode.Read);
			foreach (ZipArchiveEntry entry2 in archive.Entries.Where((ZipArchiveEntry entry) => !entry.FullName.EndsWith('/')))
			{
				using MemoryStream entryBytes = new MemoryStream();
				using Stream entryStream = entry2.Open();
				await entryStream.CopyToAsync(entryBytes, cancellationToken);
				Verify(await File.ReadAllBytesAsync(Path.Combine(toolRoot, entry2.FullName.Replace('/', Path.DirectorySeparatorChar)), cancellationToken), checked((int)entry2.Length), Convert.ToHexString(SHA256.HashData(entryBytes.ToArray())));
			}
			return new Fix54PreparedRuntime(scriptPath, leases);
		}
		catch
		{
			foreach (FileStream item2 in leases)
			{
				item2.Dispose();
			}
			throw;
		}
	}

	internal static void Verify(byte[] bytes, int expectedSize, string expectedHash)
	{
		if (bytes.Length != expectedSize || !Convert.ToHexString(SHA256.HashData(bytes)).Equals(expectedHash, StringComparison.OrdinalIgnoreCase))
		{
			throw new Fix54Exception("Nguồn FIX54 đã thay đổi hoặc tải chưa đầy đủ. Launcher không chạy nội dung chưa được xác minh.");
		}
	}

	private static async Task<byte[]> DownloadAsync(Uri uri, int maximumBytes, CancellationToken cancellationToken)
	{
		using HttpClientHandler handler = new HttpClientHandler
		{
			AllowAutoRedirect = false,
			UseCookies = false,
			UseProxy = false
		};
		using HttpClient client = new HttpClient(handler)
		{
			Timeout = Timeout.InfiniteTimeSpan
		};
		client.DefaultRequestHeaders.UserAgent.ParseAdd("NestGLauncher/FIX54");
		using CancellationTokenSource deadline = new CancellationTokenSource(TimeSpan.FromSeconds(90.0));
		using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
		Uri current = uri;
		for (int redirects = 0; redirects <= 3; redirects++)
		{
			using (HttpResponseMessage response = await client.GetAsync(current, HttpCompletionOption.ResponseHeadersRead, linked.Token))
			{
				int statusCode = (int)response.StatusCode;
				if (statusCode >= 300 && statusCode < 400)
				{
					Uri location = response.Headers.Location;
					Uri uri2 = (((object)location != null) ? new Uri(current, location) : null);
					if (redirects == 3 || (object)uri2 == null || uri2.Scheme != Uri.UriSchemeHttps || uri2.UserInfo.Length != 0 || uri2.Port != 443 || (uri2.Host != "release-assets.githubusercontent.com" && uri2.Host != "github.com"))
					{
						throw new Fix54Exception("Nguồn tải FIX54 chuyển hướng ngoài phạm vi cho phép.");
					}
					current = uri2;
					continue;
				}
				if (response.StatusCode != HttpStatusCode.OK || response.Content.Headers.ContentLength > maximumBytes)
				{
					throw new Fix54Exception("Không tải được nguồn FIX54 hợp lệ.");
				}
				byte[] result;
				await using (Stream input = await response.Content.ReadAsStreamAsync(linked.Token))
				{
					using MemoryStream output = new MemoryStream();
					byte[] buffer = new byte[32768];
					int num;
					while ((num = await input.ReadAsync(buffer, linked.Token)) != 0)
					{
						if (output.Length + num > maximumBytes)
						{
							throw new Fix54Exception("Nguồn tải FIX54 vượt giới hạn.");
						}
						output.Write(buffer, 0, num);
					}
					result = output.ToArray();
				}
				return result;
			}
			IL_04f6:;
		}
		throw new Fix54Exception("Không tải được nguồn FIX54.");
	}

	internal static void ExtractVerifiedTool(byte[] bytes, string root)
	{
		using MemoryStream stream = new MemoryStream(bytes);
		using ZipArchive zipArchive = new ZipArchive(stream, ZipArchiveMode.Read);
		if (zipArchive.Entries.Count > 64 || zipArchive.Entries.Sum((ZipArchiveEntry entry) => entry.Length) > 16777216)
		{
			throw new Fix54Exception("Gói công cụ FIX54 vượt giới hạn.");
		}
		HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (ZipArchiveEntry entry in zipArchive.Entries)
		{
			string text = entry.FullName.Replace('/', Path.DirectorySeparatorChar);
			if (Path.IsPathRooted(text) || text.Split('\\', '/').Any((string part) => (part == "." || part == "..") ? true : false) || entry.FullName.Contains(':') || ((entry.ExternalAttributes >> 16) & 0xF000) == 40960)
			{
				throw new Fix54Exception("Gói công cụ FIX54 có đường dẫn không hợp lệ.");
			}
			string fullPath = Path.GetFullPath(Path.Combine(root, text));
			Fix54Paths.EnsureInside(fullPath, root);
			if (entry.FullName.EndsWith('/'))
			{
				Directory.CreateDirectory(fullPath);
				continue;
			}
			if (!hashSet.Add(fullPath))
			{
				throw new Fix54Exception("Gói công cụ FIX54 có tệp trùng.");
			}
			Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
			entry.ExtractToFile(fullPath, overwrite: false);
		}
		if (!File.Exists(Path.Combine(root, "Steamless.CLI.exe")))
		{
			throw new Fix54Exception("Gói FIX54 thiếu công cụ CLI đã xác minh.");
		}
	}
}
