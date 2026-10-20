using System;
using System.Collections.Concurrent;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace UI_Desktop.Store;

internal static class StoreRemoteImageCache
{
	private sealed record CachedImageEntry(byte[] Bytes, Image SharedImage);

	private static readonly HttpClient HttpClient = CreateHttpClient();

	private static readonly ConcurrentDictionary<string, CachedImageEntry> Cache = new ConcurrentDictionary<string, CachedImageEntry>(StringComparer.OrdinalIgnoreCase);

	private static readonly ConcurrentDictionary<string, Task<CachedImageEntry?>> Pending = new ConcurrentDictionary<string, Task<CachedImageEntry>>(StringComparer.OrdinalIgnoreCase);

	private static readonly ConcurrentDictionary<string, Image> PreparedCache = new ConcurrentDictionary<string, Image>(StringComparer.OrdinalIgnoreCase);

	public static bool TryGet(string? imageUrl, out Image? image)
	{
		image = null;
		if (string.IsNullOrWhiteSpace(imageUrl))
		{
			return false;
		}
		if (!Cache.TryGetValue(imageUrl, out CachedImageEntry value))
		{
			return false;
		}
		image = value.SharedImage;
		return true;
	}

	public static Task<Image?> GetAsync(string? imageUrl)
	{
		if (string.IsNullOrWhiteSpace(imageUrl))
		{
			return Task.FromResult<Image>(null);
		}
		if (Cache.TryGetValue(imageUrl, out CachedImageEntry value))
		{
			return Task.FromResult(value.SharedImage);
		}
		Task<CachedImageEntry> orAdd = Pending.GetOrAdd(imageUrl, DownloadImageAsync);
		return AwaitAndCacheAsync(imageUrl, orAdd);
	}

	public static bool TryGetPrepared(string? imageUrl, Size targetSize, StoreImageResizeMode resizeMode, out Image? image, string objectPosition = "center center")
	{
		image = null;
		if (string.IsNullOrWhiteSpace(imageUrl) || targetSize.Width <= 0 || targetSize.Height <= 0)
		{
			return false;
		}
		return PreparedCache.TryGetValue(BuildPreparedKey(imageUrl, targetSize, resizeMode, objectPosition), out image);
	}

	public static async Task<Image?> GetPreparedAsync(string? imageUrl, Size targetSize, StoreImageResizeMode resizeMode, string objectPosition = "center center")
	{
		if (string.IsNullOrWhiteSpace(imageUrl) || targetSize.Width <= 0 || targetSize.Height <= 0)
		{
			return null;
		}
		string key = BuildPreparedKey(imageUrl, targetSize, resizeMode, objectPosition);
		if (PreparedCache.TryGetValue(key, out Image value))
		{
			return value;
		}
		if (!Cache.TryGetValue(imageUrl, out CachedImageEntry value2) && (await GetAsync(imageUrl).ConfigureAwait(continueOnCapturedContext: false) == null || !Cache.TryGetValue(imageUrl, out value2)))
		{
			return null;
		}
		using Image image = CreateImageFromBytes(value2.Bytes);
		if (image == null)
		{
			return null;
		}
		Bitmap bitmap = StoreImageRenderHelper.RenderToBitmap(image, targetSize, resizeMode, objectPosition, null);
		Image orAdd = PreparedCache.GetOrAdd(key, bitmap);
		if (orAdd != bitmap)
		{
			bitmap.Dispose();
		}
		return orAdd;
	}

	private static async Task<Image?> AwaitAndCacheAsync(string imageUrl, Task<CachedImageEntry?> task)
	{
		try
		{
			CachedImageEntry cachedImageEntry = await task.ConfigureAwait(continueOnCapturedContext: false);
			if ((object)cachedImageEntry != null)
			{
				Cache.TryAdd(imageUrl, cachedImageEntry);
			}
			return cachedImageEntry?.SharedImage;
		}
		finally
		{
			Pending.TryRemove(imageUrl, out Task<CachedImageEntry> _);
		}
	}

	private static async Task<CachedImageEntry?> DownloadImageAsync(string imageUrl)
	{
		_ = 3;
		try
		{
			using (HttpResponseMessage response = await HttpClient.GetAsync(imageUrl).ConfigureAwait(continueOnCapturedContext: false))
			{
				if (!response.IsSuccessStatusCode)
				{
					return null;
				}
				CachedImageEntry result;
				await using (Stream networkStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(continueOnCapturedContext: false))
				{
					using MemoryStream memoryStream = new MemoryStream();
					await networkStream.CopyToAsync(memoryStream).ConfigureAwait(continueOnCapturedContext: false);
					byte[] array = memoryStream.ToArray();
					using MemoryStream stream = new MemoryStream(array, writable: false);
					using Image original = Image.FromStream(stream, useEmbeddedColorManagement: true, validateImageData: true);
					result = new CachedImageEntry(array, new Bitmap(original));
				}
				return result;
			}
			IL_031a:;
		}
		catch
		{
			return null;
		}
		throw null;
	}

	private static HttpClient CreateHttpClient()
	{
		HttpClient httpClient = new HttpClient();
		httpClient.Timeout = TimeSpan.FromSeconds(20.0);
		httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("NestGLauncherDesktop/1.0");
		return httpClient;
	}

	private static string BuildPreparedKey(string imageUrl, Size targetSize, StoreImageResizeMode resizeMode, string objectPosition)
	{
		return string.Format(CultureInfo.InvariantCulture, "{0}|{1}x{2}|{3}|{4}", imageUrl, targetSize.Width, targetSize.Height, resizeMode, objectPosition.Trim().ToLowerInvariant());
	}

	private static Image? CreateImageFromBytes(byte[] bytes)
	{
		try
		{
			using MemoryStream stream = new MemoryStream(bytes, writable: false);
			using Image original = Image.FromStream(stream, useEmbeddedColorManagement: true, validateImageData: true);
			return new Bitmap(original);
		}
		catch
		{
			return null;
		}
	}
}
