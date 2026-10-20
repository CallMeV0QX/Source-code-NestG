using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using app_desktop.Models;

namespace app_desktop.Services;

internal sealed class DesktopUpdateInstallerService
{
	private readonly HttpClient httpClient;

	private readonly string storageFolderName;

	public DesktopUpdateInstallerService(string apiBaseUrl, string storageFolderName)
	{
		this.storageFolderName = (string.IsNullOrWhiteSpace(storageFolderName) ? "NestGLauncher" : storageFolderName.Trim());
		httpClient = new HttpClient
		{
			BaseAddress = new Uri(NormalizeBaseUrl(apiBaseUrl)),
			Timeout = TimeSpan.FromMinutes(30.0)
		};
		httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("NestGLauncherDesktop/1.0");
	}

	public async Task<string> DownloadSetupAsync(DesktopAppUpdateInfo update, IProgress<string>? progress, CancellationToken cancellationToken = default(CancellationToken))
	{
		if (string.IsNullOrWhiteSpace(update.DownloadUrl))
		{
			throw new InvalidOperationException("Bản cập nhật chưa có đường dẫn tải setup.");
		}
		string path = BuildSafeSetupFileName(update);
		string text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), storageFolderName, "Updates", $"{NormalizePathSegment(update.Version)}-{Math.Max(0, update.BuildNumber)}");
		Directory.CreateDirectory(text);
		string destinationPath = Path.Combine(text, path);
		string temporaryPath = destinationPath + ".download";
		try
		{
			using HttpResponseMessage response = await httpClient.GetAsync(update.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			response.EnsureSuccessStatusCode();
			long totalBytes = response.Content.Headers.ContentLength ?? Math.Max(0L, update.Size);
			await using (Stream sourceStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false))
			{
				await using FileStream targetStream = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None, 131072, useAsync: true);
				byte[] buffer = new byte[131072];
				long readBytes = 0L;
				while (true)
				{
					int bytesRead = await sourceStream.ReadAsync(buffer, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
					if (bytesRead == 0)
					{
						break;
					}
					await targetStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
					readBytes += bytesRead;
					progress?.Report((totalBytes > 0) ? ("Đang tải bản cập nhật " + FormatSize(readBytes) + " / " + FormatSize(totalBytes)) : ("Đang tải bản cập nhật " + FormatSize(readBytes)));
				}
			}
			if (!string.IsNullOrWhiteSpace(update.ChecksumSha256))
			{
				await VerifyChecksumAsync(temporaryPath, update.ChecksumSha256, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			}
			if (File.Exists(destinationPath))
			{
				File.Delete(destinationPath);
			}
			File.Move(temporaryPath, destinationPath);
			return destinationPath;
		}
		catch
		{
			TryDeleteTemporaryFile(temporaryPath);
			throw;
		}
	}

	public static void StartSetup(string setupPath)
	{
		if (string.IsNullOrWhiteSpace(setupPath) || !File.Exists(setupPath))
		{
			throw new FileNotFoundException("Không tìm thấy file setup đã tải.", setupPath);
		}
		Process.Start(new ProcessStartInfo
		{
			FileName = setupPath,
			UseShellExecute = true,
			WorkingDirectory = Path.GetDirectoryName(setupPath)
		});
	}

	private static async Task VerifyChecksumAsync(string filePath, string expectedHash, CancellationToken cancellationToken)
	{
		string normalizedExpected = expectedHash.Trim().Replace("-", string.Empty, StringComparison.Ordinal).ToLowerInvariant();
		await using FileStream stream = File.OpenRead(filePath);
		if (!string.Equals(Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(continueOnCapturedContext: false)).ToLowerInvariant(), normalizedExpected, StringComparison.OrdinalIgnoreCase))
		{
			throw new InvalidOperationException("File setup tải về không khớp checksum. Vui lòng thử lại.");
		}
	}

	private static string BuildSafeSetupFileName(DesktopAppUpdateInfo update)
	{
		string text = (string.IsNullOrWhiteSpace(update.FileName) ? ("NestG Setup " + update.Version + ".exe") : Path.GetFileName(update.FileName.Trim()));
		if (string.IsNullOrWhiteSpace(text) || text.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
		{
			text = "NestG Setup " + update.Version + ".exe";
		}
		if (!text.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
		{
			return Path.GetFileNameWithoutExtension(text) + ".exe";
		}
		return text;
	}

	private static string NormalizePathSegment(string value)
	{
		string text = (string.IsNullOrWhiteSpace(value) ? "latest" : value.Trim());
		char[] invalidFileNameChars = Path.GetInvalidFileNameChars();
		foreach (char oldChar in invalidFileNameChars)
		{
			text = text.Replace(oldChar, '_');
		}
		return text;
	}

	private static void TryDeleteTemporaryFile(string temporaryPath)
	{
		try
		{
			if (File.Exists(temporaryPath))
			{
				File.Delete(temporaryPath);
			}
		}
		catch
		{
		}
	}

	private static string FormatSize(long bytes)
	{
		string[] array = new string[5] { "B", "KB", "MB", "GB", "TB" };
		double num = Math.Max(0L, bytes);
		int num2 = 0;
		while (num >= 1024.0 && num2 < array.Length - 1)
		{
			num /= 1024.0;
			num2++;
		}
		return $"{num:0.##} {array[num2]}";
	}

	private static string NormalizeBaseUrl(string apiBaseUrl)
	{
		if (string.IsNullOrWhiteSpace(apiBaseUrl))
		{
			return "https://localhost:7023/";
		}
		if (!apiBaseUrl.EndsWith("/", StringComparison.Ordinal))
		{
			return apiBaseUrl + "/";
		}
		return apiBaseUrl;
	}
}
