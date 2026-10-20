using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace app_desktop.Services.Warp;

internal sealed class CloudflareWarpPackageDownloader : IWarpPackageDownloader
{
	internal static readonly Uri OfficialDownloadUri = new Uri("https://downloads.cloudflareclient.com/v1/download/windows/ga");

	private const long MaximumPackageBytes = 209715200L;

	private static readonly HttpClient Http = new HttpClient(new HttpClientHandler
	{
		AllowAutoRedirect = false
	})
	{
		Timeout = TimeSpan.FromMinutes(5.0)
	};

	public async Task DownloadAsync(string destinationPath, IProgress<WarpInstallProgress>? progress, CancellationToken cancellationToken)
	{
		Uri uri = OfficialDownloadUri;
		for (int redirect = 0; redirect <= 4; redirect++)
		{
			using HttpResponseMessage response = await Http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			int statusCode = (int)response.StatusCode;
			if (statusCode >= 300 && statusCode < 400)
			{
				Uri uri2 = response.Headers.Location ?? throw new InvalidDataException("Cloudflare không trả địa chỉ bộ cài.");
				uri = (uri2.IsAbsoluteUri ? uri2 : new Uri(uri, uri2));
				if (!IsOfficialDownloadUri(uri))
				{
					throw new InvalidDataException("Địa chỉ tải WARP không thuộc Cloudflare.");
				}
				continue;
			}
			response.EnsureSuccessStatusCode();
			if (!IsOfficialDownloadUri(uri))
			{
				throw new InvalidDataException("Địa chỉ tải WARP không thuộc Cloudflare.");
			}
			long? contentLength = response.Content.Headers.ContentLength;
			if (contentLength.HasValue && contentLength.GetValueOrDefault() > 209715200)
			{
				throw new InvalidDataException("Bộ cài WARP vượt giới hạn dung lượng.");
			}
			long? expected = response.Content.Headers.ContentLength;
			await using FileStream destination = new FileStream(destinationPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, FileOptions.Asynchronous);
			await using (Stream source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false))
			{
				byte[] buffer = new byte[65536];
				long received = 0L;
				while (true)
				{
					int num = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
					if (num == 0)
					{
						break;
					}
					received += num;
					if (received > 209715200)
					{
						throw new InvalidDataException("Bộ cài WARP vượt giới hạn dung lượng.");
					}
					await destination.WriteAsync(buffer.AsMemory(0, num), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
					if (expected.HasValue && expected.GetValueOrDefault() > 0)
					{
						progress?.Report(new WarpInstallProgress("Đang tải Cloudflare WARP…", (int)Math.Min(99L, received * 100 / expected.Value)));
					}
				}
				if (received == 0L || (expected.HasValue && expected.GetValueOrDefault() > 0 && received != expected.Value))
				{
					throw new InvalidDataException("Bộ cài WARP tải chưa đầy đủ.");
				}
			}
			return;
		}
		throw new InvalidDataException("Cloudflare chuyển hướng tải quá nhiều lần.");
	}

	internal static bool IsOfficialDownloadUri(Uri uri)
	{
		if (uri.Scheme == Uri.UriSchemeHttps && uri.Host.Equals("downloads.cloudflareclient.com", StringComparison.OrdinalIgnoreCase))
		{
			return uri.AbsolutePath.StartsWith("/v1/download/windows/", StringComparison.Ordinal);
		}
		return false;
	}
}
