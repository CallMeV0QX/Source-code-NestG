using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace app_desktop.Services.WindowsUpdate;

internal sealed class SordumWubPackageDownloader : IWubPackageDownloader
{
	internal static readonly Uri OfficialDownloadUri = new Uri("https://www.sordum.org/files/downloads.php?st-windows-update-blocker");

	private const long MaximumPackageBytes = 3145728L;

	private static readonly HttpClient SharedHttp = new HttpClient(new HttpClientHandler
	{
		AllowAutoRedirect = false
	})
	{
		Timeout = TimeSpan.FromMinutes(2.0)
	};

	private readonly HttpClient http;

	internal SordumWubPackageDownloader(HttpClient? http = null)
	{
		this.http = http ?? SharedHttp;
	}

	public async Task DownloadAsync(string destinationPath, IProgress<WubPackageProgress>? progress, CancellationToken cancellationToken)
	{
		using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		deadline.CancelAfter(TimeSpan.FromMinutes(2.0));
		CancellationToken downloadToken = deadline.Token;
		Uri uri = OfficialDownloadUri;
		string driveFileId = null;
		for (int redirect = 0; redirect <= 6; redirect++)
		{
			using (HttpResponseMessage response = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, downloadToken).ConfigureAwait(continueOnCapturedContext: false))
			{
				int statusCode = (int)response.StatusCode;
				if (statusCode >= 300 && statusCode < 400)
				{
					Uri uri2 = response.Headers.Location ?? throw new InvalidDataException("Nguồn tải WUB không trả địa chỉ chuyển hướng.");
					Uri uri3 = (uri2.IsAbsoluteUri ? uri2 : new Uri(uri, uri2));
					if (!IsAllowedRedirect(uri, uri3, ref driveFileId))
					{
						throw new InvalidDataException("Nguồn tải WUB chuyển hướng ngoài Sordum/Google Drive.");
					}
					uri = uri3;
					continue;
				}
				response.EnsureSuccessStatusCode();
				if (!IsAllowedPayloadUri(uri, driveFileId) || response.StatusCode != HttpStatusCode.OK)
				{
					throw new InvalidDataException("Nguồn tải WUB không hợp lệ.");
				}
				long? expected = response.Content.Headers.ContentLength;
				bool flag;
				if (expected.HasValue)
				{
					long valueOrDefault = expected.GetValueOrDefault();
					if (valueOrDefault <= 0 || valueOrDefault > 3145728)
					{
						flag = true;
						goto IL_021d;
					}
				}
				flag = false;
				goto IL_021d;
				IL_021d:
				if (flag)
				{
					throw new InvalidDataException("Gói WUB có kích thước không hợp lệ.");
				}
				await using FileStream destination = new FileStream(destinationPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, FileOptions.Asynchronous);
				await using (Stream source = await response.Content.ReadAsStreamAsync(downloadToken).ConfigureAwait(continueOnCapturedContext: false))
				{
					byte[] buffer = new byte[65536];
					long received = 0L;
					while (true)
					{
						int num = await source.ReadAsync(buffer, downloadToken).ConfigureAwait(continueOnCapturedContext: false);
						if (num == 0)
						{
							break;
						}
						received += num;
						if (received > 3145728)
						{
							throw new InvalidDataException("Gói WUB vượt giới hạn dung lượng.");
						}
						await destination.WriteAsync(buffer.AsMemory(0, num), downloadToken).ConfigureAwait(continueOnCapturedContext: false);
						if (expected.HasValue && expected.GetValueOrDefault() > 0)
						{
							progress?.Report(new WubPackageProgress("Đang tải WUB v1.8 từ Sordum…", (int)Math.Min(99L, received * 100 / expected.Value)));
						}
					}
					if (received == 0L || (expected.HasValue && expected.GetValueOrDefault() > 0 && received != expected.Value))
					{
						throw new InvalidDataException("Gói WUB tải chưa đầy đủ.");
					}
				}
				return;
			}
			IL_06a6:;
		}
		throw new InvalidDataException("Nguồn tải WUB chuyển hướng quá nhiều lần.");
	}

	internal static bool IsAllowedRedirect(Uri current, Uri next, ref string? driveFileId)
	{
		if (IsSordumEndpoint(current) && IsSordumEndpoint(next))
		{
			return true;
		}
		if (IsSordumEndpoint(current) && IsGoogleDriveDownload(next))
		{
			driveFileId = GetDriveFileId(next);
			return driveFileId != null;
		}
		if (driveFileId != null && IsGoogleDriveDownload(current) && IsGoogleDriveDownload(next))
		{
			return string.Equals(GetDriveFileId(next), driveFileId, StringComparison.Ordinal);
		}
		return false;
	}

	private static bool IsAllowedPayloadUri(Uri uri, string? driveFileId)
	{
		if (!IsSordumEndpoint(uri))
		{
			if (driveFileId != null && IsGoogleDriveDownload(uri))
			{
				return string.Equals(GetDriveFileId(uri), driveFileId, StringComparison.Ordinal);
			}
			return false;
		}
		return true;
	}

	private static bool IsSordumEndpoint(Uri uri)
	{
		if (uri.Scheme == Uri.UriSchemeHttps && uri.IsDefaultPort && uri.Host.Equals("www.sordum.org", StringComparison.OrdinalIgnoreCase) && uri.AbsolutePath.Equals("/files/downloads.php", StringComparison.Ordinal))
		{
			return uri.Query.Equals("?st-windows-update-blocker", StringComparison.Ordinal);
		}
		return false;
	}

	private static bool IsGoogleDriveDownload(Uri uri)
	{
		bool flag = uri.Scheme == Uri.UriSchemeHttps && uri.IsDefaultPort;
		if (flag)
		{
			bool flag2 = uri.Host.Equals("drive.google.com", StringComparison.OrdinalIgnoreCase);
			if (flag2)
			{
				string absolutePath = uri.AbsolutePath;
				bool flag3 = ((absolutePath == "/u/0/uc" || absolutePath == "/uc") ? true : false);
				flag2 = flag3;
			}
			flag = flag2 || (uri.Host.Equals("drive.usercontent.google.com", StringComparison.OrdinalIgnoreCase) && uri.AbsolutePath == "/download");
		}
		if (flag && uri.Query.TrimStart('?').Split('&').Contains<string>("export=download", StringComparer.Ordinal))
		{
			return GetDriveFileId(uri) != null;
		}
		return false;
	}

	private static string? GetDriveFileId(Uri uri)
	{
		string[] array = uri.Query.TrimStart('?').Split('&');
		foreach (string text in array)
		{
			if (text.StartsWith("id=", StringComparison.Ordinal))
			{
				string text2 = text.Substring(3);
				int length = text2.Length;
				bool flag = ((length < 10 || length > 100) ? true : false);
				if (flag || text2.Any((char c) => !char.IsAsciiLetterOrDigit(c) && c != '_' && c != '-'))
				{
					return null;
				}
				return text2;
			}
		}
		return null;
	}
}
