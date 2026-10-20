using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

namespace app_desktop.Services;

internal sealed class PypassDirectDownloadService : IDisposable
{
	private sealed record DownloadProbe(Uri DownloadUri, long? TotalBytes, bool SupportsRanges, string? ETag, DateTimeOffset? LastModified);

	private sealed class PypassDownloadState
	{
		public Guid PackageId { get; set; }

		public string CacheValidationToken { get; set; } = string.Empty;

		public long TotalBytes { get; set; }

		public string? ETag { get; set; }

		public DateTime? LastModifiedUtc { get; set; }

		public DateTime UpdatedAtUtc { get; set; }

		public List<PypassDownloadSegment> Segments { get; set; } = new List<PypassDownloadSegment>();

		public PypassDownloadState Clone()
		{
			return new PypassDownloadState
			{
				PackageId = PackageId,
				CacheValidationToken = CacheValidationToken,
				TotalBytes = TotalBytes,
				ETag = ETag,
				LastModifiedUtc = LastModifiedUtc,
				UpdatedAtUtc = UpdatedAtUtc,
				Segments = Segments.Select((PypassDownloadSegment segment) => segment.Clone()).ToList()
			};
		}
	}

	private sealed class PypassDownloadSegment
	{
		public long StartOffset { get; set; }

		public long EndOffset { get; set; }

		public long DownloadedBytes { get; set; }

		public long Length => EndOffset - StartOffset + 1;

		public PypassDownloadSegment Clone()
		{
			return new PypassDownloadSegment
			{
				StartOffset = StartOffset,
				EndOffset = EndOffset,
				DownloadedBytes = DownloadedBytes
			};
		}
	}

	private sealed class PypassDownloadThrottledException : IOException
	{
	}

	private sealed class PypassRangeNotSupportedException : IOException
	{
	}

	internal const long HardMaxPackageSize = 11811160064L;

	private const int MaxAttemptsPerSegment = 3;

	private const int MaxThrottleRounds = 4;

	private const int ProgressIntervalMilliseconds = 250;

	private const int MetadataIntervalMilliseconds = 1000;

	private const int BufferSize = 1048576;

	private const long ParallelDownloadThresholdBytes = 33554432L;

	private const long MinimumFreeSpaceReserveBytes = 536870912L;

	private const string PackageFileName = "package.zip";

	private const string PartFileName = "package.zip.part";

	private const string MetadataFileName = "package.zip.meta";

	private static readonly JsonSerializerOptions MetadataJsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
	{
		PropertyNameCaseInsensitive = true,
		WriteIndented = true
	};

	private readonly HttpClient httpClient;

	private readonly string cacheRoot;

	private readonly long parallelDownloadThresholdBytes;

	private readonly long minimumFreeSpaceReserveBytes;

	public PypassDirectDownloadService()
		: this(CreateDefaultHandler(), ResolveDefaultCacheRoot(), 33554432L, 536870912L)
	{
	}

	internal PypassDirectDownloadService(HttpMessageHandler handler, string cacheRoot, long parallelDownloadThresholdBytes = 33554432L, long minimumFreeSpaceReserveBytes = 536870912L)
	{
		httpClient = new HttpClient(handler)
		{
			Timeout = Timeout.InfiniteTimeSpan
		};
		httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("NestGLauncherPypassDirect/2.0");
		this.cacheRoot = Path.GetFullPath(cacheRoot);
		this.parallelDownloadThresholdBytes = Math.Max(1L, parallelDownloadThresholdBytes);
		this.minimumFreeSpaceReserveBytes = Math.Max(0L, minimumFreeSpaceReserveBytes);
	}

	public async Task<PypassDirectDownloadResult> DownloadAsync(Guid packageId, string? cacheValidationToken, string downloadUrl, long? expectedSizeBytes, long maxPackageSizeBytes, int preferredConnectionCount, bool resumeEnabled, int cacheRetentionHours, IProgress<PypassDirectDownloadProgress>? progress, CancellationToken cancellationToken)
	{
		if (packageId == Guid.Empty)
		{
			throw new InvalidOperationException("Package Pypass chưa có ID hợp lệ.");
		}
		long maxPackageSize = NormalizeMaxPackageSize(maxPackageSizeBytes);
		long expectedSize = expectedSizeBytes.GetValueOrDefault();
		if (expectedSize <= 0)
		{
			throw new InvalidOperationException("Package Pypass chưa có dung lượng đã validate.");
		}
		if (expectedSize > maxPackageSize)
		{
			throw new InvalidOperationException("Package Pypass vượt quá giới hạn tải trực tiếp " + FormatBytes(maxPackageSize) + ".");
		}
		int connectionCount = Math.Clamp(preferredConnectionCount, 1, 8);
		int retentionHours = Math.Clamp(cacheRetentionHours, 1, 720);
		CleanupExpiredCache(retentionHours, packageId);
		string packageDirectory = Path.Combine(cacheRoot, packageId.ToString("N"));
		string partPath = Path.Combine(packageDirectory, "package.zip.part");
		string metadataPath = Path.Combine(packageDirectory, "package.zip.meta");
		string completedPath = Path.Combine(packageDirectory, "package.zip");
		Directory.CreateDirectory(packageDirectory);
		Uri originalUri = NormalizeGoogleDriveDownloadUri(downloadUrl);
		progress?.Report(new PypassDirectDownloadProgress(0L, expectedSizeBytes, 0, 0L, null, 1, IsResumed: false, "Đang kiểm tra máy chủ tải và khả năng tiếp tục file..."));
		DownloadProbe probe = await ProbeDownloadAsync(originalUri, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		long totalBytes = ResolveTotalBytes(probe.TotalBytes, expectedSize, maxPackageSize);
		string normalizedCacheValidationToken = NormalizeCacheValidationToken(cacheValidationToken, packageId, totalBytes);
		PypassDownloadState pypassDownloadState = ((!resumeEnabled) ? null : (await TryLoadStateAsync(metadataPath, cancellationToken).ConfigureAwait(continueOnCapturedContext: false)));
		PypassDownloadState state = pypassDownloadState;
		bool canResume = state != null && IsStateCompatible(state, packageId, normalizedCacheValidationToken, totalBytes, probe) && File.Exists(partPath) && new FileInfo(partPath).Length == totalBytes;
		if (state != null && IsStateCompatible(state, packageId, normalizedCacheValidationToken, totalBytes, probe) && state.Segments.Count > 0 && state.Segments.All((PypassDownloadSegment segment) => segment.DownloadedBytes == segment.Length) && File.Exists(completedPath) && new FileInfo(completedPath).Length == totalBytes)
		{
			EnsureZipSignature(completedPath);
			TouchDirectory(packageDirectory);
			progress?.Report(BuildProgress(totalBytes, totalBytes, 0L, null, 0, resumed: true, "Đã dùng lại package ZIP đã tải hoàn tất."));
			return new PypassDirectDownloadResult(completedPath, totalBytes, WasResumed: true);
		}
		if (!canResume)
		{
			state = CreateState(packageId, normalizedCacheValidationToken, totalBytes, probe, (!probe.SupportsRanges || totalBytes < parallelDownloadThresholdBytes) ? 1 : connectionCount);
			PrepareFreshPartFile(packageDirectory, partPath, completedPath, metadataPath, totalBytes);
			await SaveStateAsync(metadataPath, state, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
		else
		{
			TryDeleteFile(completedPath);
		}
		long num = state.Segments.Sum((PypassDownloadSegment segment) => segment.DownloadedBytes);
		bool resumed = num > 0;
		if (canResume)
		{
			EnsureFreeSpace(packageDirectory, minimumFreeSpaceReserveBytes);
		}
		if (resumed)
		{
			progress?.Report(BuildProgress(num, totalBytes, 0L, null, Math.Min(connectionCount, state.Segments.Count), resumed: true, "Đang tiếp tục từ " + FormatBytes(num) + " đã tải trước đó..."));
		}
		await DownloadSegmentsAsync(originalUri, probe, state, partPath, metadataPath, connectionCount, resumed, progress, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		if (new FileInfo(partPath).Length != totalBytes || state.Segments.Any((PypassDownloadSegment segment) => segment.DownloadedBytes != segment.Length))
		{
			throw new IOException("Package Pypass tải chưa đầy đủ dung lượng.");
		}
		EnsureZipSignature(partPath);
		File.Move(partPath, completedPath, overwrite: true);
		state.UpdatedAtUtc = DateTime.UtcNow;
		await SaveStateAsync(metadataPath, state, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		TouchDirectory(packageDirectory);
		progress?.Report(BuildProgress(totalBytes, totalBytes, 0L, TimeSpan.Zero, 0, resumed, "Đã tải xong và kiểm tra package ZIP từ Google Drive."));
		return new PypassDirectDownloadResult(completedPath, totalBytes, resumed);
	}

	private async Task DownloadSegmentsAsync(Uri originalUri, DownloadProbe probe, PypassDownloadState state, string partPath, string metadataPath, int preferredConnectionCount, bool resumed, IProgress<PypassDirectDownloadProgress>? progress, CancellationToken cancellationToken)
	{
		int activeConnections = ((!probe.SupportsRanges) ? 1 : Math.Min(preferredConnectionCount, state.Segments.Count));
		int throttleRounds = 0;
		while (state.Segments.Any((PypassDownloadSegment segment) => segment.DownloadedBytes < segment.Length))
		{
			cancellationToken.ThrowIfCancellationRequested();
			try
			{
				await DownloadRoundAsync(probe, state, partPath, metadataPath, activeConnections, resumed, progress, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				break;
			}
			catch (PypassRangeNotSupportedException) when (probe.SupportsRanges)
			{
				probe = await ProbeDownloadAsync(originalUri, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				if (probe.SupportsRanges)
				{
					throw;
				}
				ResetForSequentialDownload(state, partPath);
				await SaveStateAsync(metadataPath, state, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				activeConnections = 1;
				progress?.Report(BuildProgress(0L, state.TotalBytes, 0L, null, 1, resumed: false, "Máy chủ không hỗ trợ chia luồng. Launcher chuyển sang một kết nối an toàn."));
			}
			catch (PypassDownloadThrottledException) when (throttleRounds < 4)
			{
				throttleRounds++;
				activeConnections = Math.Max(1, activeConnections / 2);
				progress?.Report(BuildProgress(state.Segments.Sum((PypassDownloadSegment segment) => segment.DownloadedBytes), state.TotalBytes, 0L, null, activeConnections, resumed, $"Google Drive đang giới hạn tốc độ. Đã giảm còn {activeConnections} kết nối và sẽ thử lại..."));
				await Task.Delay(TimeSpan.FromSeconds(Math.Min(8, throttleRounds * 2)), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			}
		}
	}

	private async Task DownloadRoundAsync(DownloadProbe probe, PypassDownloadState state, string partPath, string metadataPath, int connectionCount, bool resumed, IProgress<PypassDirectDownloadProgress>? progress, CancellationToken cancellationToken)
	{
		PypassDownloadSegment[] array = state.Segments.Where((PypassDownloadSegment segment) => segment.DownloadedBytes < segment.Length).ToArray();
		if (array.Length == 0)
		{
			return;
		}
		SafeFileHandle fileHandle = File.OpenHandle(partPath, FileMode.Open, FileAccess.Write, FileShare.Read, FileOptions.Asynchronous | FileOptions.RandomAccess, 0L);
		SemaphoreSlim metadataGate;
		object stateLock;
		Stopwatch stopwatch;
		long startingBytes;
		long totalAtStart;
		long lastProgressAt;
		long lastMetadataAt;
		try
		{
			SemaphoreSlim concurrencyGate = new SemaphoreSlim(Math.Max(1, connectionCount));
			try
			{
				metadataGate = new SemaphoreSlim(1, 1);
				try
				{
					stateLock = new object();
					stopwatch = Stopwatch.StartNew();
					startingBytes = array.Sum((PypassDownloadSegment segment) => segment.DownloadedBytes);
					totalAtStart = state.Segments.Sum((PypassDownloadSegment segment) => segment.DownloadedBytes);
					lastProgressAt = 0L;
					lastMetadataAt = 0L;
					Task[] tasks = ((IEnumerable<PypassDownloadSegment>)array).Select((Func<PypassDownloadSegment, Task>)async delegate(PypassDownloadSegment segment)
					{
						await concurrencyGate.WaitAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
						try
						{
							await DownloadSegmentWithRetryAsync(probe, state.TotalBytes, segment, fileHandle, OnBytesWrittenAsync, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
						}
						finally
						{
							concurrencyGate.Release();
						}
					}).ToArray();
					Task allTask = Task.WhenAll(tasks);
					try
					{
						await allTask.ConfigureAwait(continueOnCapturedContext: false);
					}
					catch
					{
						await PersistStateSnapshotAsync(metadataPath, state, stateLock, metadataGate, CancellationToken.None).ConfigureAwait(continueOnCapturedContext: false);
						IEnumerable<Exception> source;
						if (allTask.Exception != null)
						{
							source = allTask.Exception.Flatten().InnerExceptions.AsEnumerable();
						}
						else
						{
							IEnumerable<Exception> enumerable = Enumerable.Empty<Exception>();
							source = enumerable;
						}
						if (source.Any((Exception error) => error is PypassDownloadThrottledException))
						{
							throw new PypassDownloadThrottledException();
						}
						if (source.Any((Exception error) => error is PypassRangeNotSupportedException))
						{
							throw new PypassRangeNotSupportedException();
						}
						throw;
					}
					await PersistStateSnapshotAsync(metadataPath, state, stateLock, metadataGate, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				}
				finally
				{
					if (metadataGate != null)
					{
						((IDisposable)metadataGate).Dispose();
					}
				}
			}
			finally
			{
				if (concurrencyGate != null)
				{
					((IDisposable)concurrencyGate).Dispose();
				}
			}
		}
		finally
		{
			if (fileHandle != null)
			{
				((IDisposable)fileHandle).Dispose();
			}
		}
		async Task OnBytesWrittenAsync(PypassDownloadSegment segment, int count)
		{
			long tickCount = Environment.TickCount64;
			bool flag = false;
			bool flag2 = false;
			long num;
			lock (stateLock)
			{
				segment.DownloadedBytes += count;
				state.UpdatedAtUtc = DateTime.UtcNow;
				num = state.Segments.Sum((PypassDownloadSegment item) => item.DownloadedBytes);
				if (tickCount - lastProgressAt >= 250)
				{
					lastProgressAt = tickCount;
					flag = true;
				}
				if (tickCount - lastMetadataAt >= 1000)
				{
					lastMetadataAt = tickCount;
					flag2 = true;
				}
			}
			if (flag)
			{
				long num2 = Math.Max(0L, num - totalAtStart);
				long num3 = ((stopwatch.Elapsed.TotalSeconds > 0.2) ? ((long)((double)num2 / stopwatch.Elapsed.TotalSeconds)) : 0);
				TimeSpan? estimatedRemaining = ((num3 > 0) ? new TimeSpan?(TimeSpan.FromSeconds((double)(state.TotalBytes - num) / (double)num3)) : ((TimeSpan?)null));
				progress?.Report(BuildProgress(num, state.TotalBytes, num3, estimatedRemaining, connectionCount, resumed || startingBytes > 0, BuildDownloadMessage(num3, estimatedRemaining, connectionCount, resumed || startingBytes > 0)));
			}
			if (flag2)
			{
				await PersistStateSnapshotAsync(metadataPath, state, stateLock, metadataGate, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			}
		}
	}

	private async Task DownloadSegmentWithRetryAsync(DownloadProbe probe, long totalBytes, PypassDownloadSegment segment, SafeFileHandle fileHandle, Func<PypassDownloadSegment, int, Task> onBytesWritten, CancellationToken cancellationToken)
	{
		Exception lastException = null;
		for (int attempt = 1; attempt <= 3; attempt++)
		{
			cancellationToken.ThrowIfCancellationRequested();
			try
			{
				await DownloadSegmentAsync(probe, totalBytes, segment, fileHandle, onBytesWritten, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				return;
			}
			catch (PypassDownloadThrottledException)
			{
				throw;
			}
			catch (PypassRangeNotSupportedException)
			{
				throw;
			}
			catch (Exception ex3) when (((ex3 is HttpRequestException || ex3 is IOException) ? true : false) && attempt < 3 && !cancellationToken.IsCancellationRequested)
			{
				lastException = ex3;
				if (!probe.SupportsRanges)
				{
					segment.DownloadedBytes = 0L;
				}
				await Task.Delay(TimeSpan.FromMilliseconds(600 * attempt), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			}
		}
		throw new IOException("Kết nối tải package Pypass bị gián đoạn quá nhiều lần.", lastException);
	}

	private async Task DownloadSegmentAsync(DownloadProbe probe, long totalBytes, PypassDownloadSegment segment, SafeFileHandle fileHandle, Func<PypassDownloadSegment, int, Task> onBytesWritten, CancellationToken cancellationToken)
	{
		long offset = segment.StartOffset + segment.DownloadedBytes;
		if (offset > segment.EndOffset)
		{
			return;
		}
		using HttpRequestMessage request = BuildDownloadRequest(probe.DownloadUri, probe.SupportsRanges ? new long?(offset) : ((long?)null), probe.SupportsRanges ? new long?(segment.EndOffset) : ((long?)null), probe);
		using HttpResponseMessage response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		if (response.StatusCode == HttpStatusCode.TooManyRequests)
		{
			throw new PypassDownloadThrottledException();
		}
		int statusCode = (int)response.StatusCode;
		if ((statusCode >= 500 || statusCode == 408) ? true : false)
		{
			throw new HttpRequestException("Google Drive tạm thời không sẵn sàng.", null, response.StatusCode);
		}
		if (probe.SupportsRanges && response.StatusCode != HttpStatusCode.PartialContent)
		{
			if (response.StatusCode == HttpStatusCode.OK)
			{
				throw new PypassRangeNotSupportedException();
			}
			throw new HttpRequestException($"Google Drive từ chối đoạn tải (HTTP {response.StatusCode}).", null, response.StatusCode);
		}
		if (!probe.SupportsRanges && !response.IsSuccessStatusCode)
		{
			throw new HttpRequestException($"Google Drive từ chối tải package (HTTP {response.StatusCode}).", null, response.StatusCode);
		}
		ValidateResponseValidator(response, probe);
		if (probe.SupportsRanges)
		{
			ContentRangeHeaderValue contentRange = response.Content.Headers.ContentRange;
			if (contentRange == null || contentRange.From != offset || !contentRange.To.HasValue || contentRange.To > segment.EndOffset)
			{
				goto IL_0377;
			}
			long? length = contentRange.Length;
			if (length.HasValue)
			{
				long valueOrDefault = length.GetValueOrDefault();
				if (valueOrDefault != totalBytes)
				{
					goto IL_0377;
				}
			}
		}
		await using (Stream input = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false))
		{
			byte[] buffer = new byte[1048576];
			long writeOffset = offset;
			while (writeOffset <= segment.EndOffset)
			{
				long val = segment.EndOffset - writeOffset + 1;
				int length2 = (int)Math.Min(buffer.Length, val);
				int bytesRead = await input.ReadAsync(buffer.AsMemory(0, length2), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				if (bytesRead == 0)
				{
					break;
				}
				await RandomAccess.WriteAsync(fileHandle, buffer.AsMemory(0, bytesRead), writeOffset, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				writeOffset += bytesRead;
				await onBytesWritten(segment, bytesRead).ConfigureAwait(continueOnCapturedContext: false);
			}
			if (segment.DownloadedBytes != segment.Length)
			{
				throw new IOException("Đoạn package Pypass tải chưa đầy đủ.");
			}
			return;
		}
		IL_0377:
		throw new IOException("Google Drive trả về đoạn dữ liệu không đúng yêu cầu.");
	}

	private async Task<DownloadProbe> ProbeDownloadAsync(Uri initialUri, CancellationToken cancellationToken)
	{
		using HttpResponseMessage resolvedResponse = await ResolveGoogleDriveDownloadResponseAsync(initialUri, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		EnsureDownloadResponse(resolvedResponse);
		Uri resolvedUri = resolvedResponse.RequestMessage?.RequestUri ?? initialUri;
		long? initialLength = resolvedResponse.Content.Headers.ContentLength;
		string initialETag = resolvedResponse.Headers.ETag?.ToString();
		DateTimeOffset? initialLastModified = resolvedResponse.Content.Headers.LastModified;
		using HttpRequestMessage probeRequest = BuildDownloadRequest(resolvedUri, 0L, 0L, new DownloadProbe(resolvedUri, initialLength, SupportsRanges: false, initialETag, initialLastModified));
		using HttpResponseMessage httpResponseMessage = await httpClient.SendAsync(probeRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		if (httpResponseMessage.StatusCode == HttpStatusCode.TooManyRequests)
		{
			throw new InvalidOperationException("Google Drive đang giới hạn yêu cầu. Vui lòng thử lại sau.");
		}
		if (!httpResponseMessage.IsSuccessStatusCode)
		{
			throw new InvalidOperationException($"Không thể kiểm tra package Google Drive (HTTP {httpResponseMessage.StatusCode}).");
		}
		int num;
		if (httpResponseMessage.StatusCode == HttpStatusCode.PartialContent)
		{
			ContentRangeHeaderValue? contentRange = httpResponseMessage.Content.Headers.ContentRange;
			if (contentRange != null && contentRange.From == 0)
			{
				ContentRangeHeaderValue? contentRange2 = httpResponseMessage.Content.Headers.ContentRange;
				num = ((contentRange2 != null && contentRange2.To == 0) ? 1 : 0);
				goto IL_02ba;
			}
		}
		num = 0;
		goto IL_02ba;
		IL_02ba:
		bool flag = (byte)num != 0;
		long? totalBytes = ((!flag) ? (httpResponseMessage.Content.Headers.ContentLength ?? initialLength) : httpResponseMessage.Content.Headers.ContentRange?.Length);
		return new DownloadProbe(httpResponseMessage.RequestMessage?.RequestUri ?? resolvedUri, totalBytes, flag, httpResponseMessage.Headers.ETag?.ToString() ?? initialETag, httpResponseMessage.Content.Headers.LastModified ?? initialLastModified);
	}

	private async Task<HttpResponseMessage> ResolveGoogleDriveDownloadResponseAsync(Uri initialUri, CancellationToken cancellationToken)
	{
		Uri currentUri = initialUri;
		for (int step = 0; step < 3; step++)
		{
			HttpResponseMessage response = await SendInitialRequestAsync(currentUri, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			if (!response.IsSuccessStatusCode || !IsHtmlResponse(response))
			{
				return response;
			}
			long? contentLength = response.Content.Headers.ContentLength;
			if (contentLength.HasValue && contentLength.GetValueOrDefault() > 2097152)
			{
				response.Dispose();
				throw new InvalidOperationException("Google Drive trả về trang xác nhận không hợp lệ.");
			}
			Uri responseUri = response.RequestMessage?.RequestUri ?? currentUri;
			Uri uri = ExtractGoogleDriveConfirmUri(responseUri, await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false));
			response.Dispose();
			if ((object)uri == null)
			{
				throw new InvalidOperationException("Google Drive không trả về file ZIP. Hãy kiểm tra quyền Anyone with the link.");
			}
			currentUri = uri;
		}
		throw new InvalidOperationException("Google Drive yêu cầu quá nhiều bước xác nhận.");
	}

	private async Task<HttpResponseMessage> SendInitialRequestAsync(Uri uri, CancellationToken cancellationToken)
	{
		using HttpRequestMessage request = BuildDownloadRequest(uri, null, null, null);
		return await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
	}

	private static HttpRequestMessage BuildDownloadRequest(Uri uri, long? rangeStart, long? rangeEnd, DownloadProbe? probe)
	{
		HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, uri);
		httpRequestMessage.Headers.Accept.ParseAdd("application/zip");
		httpRequestMessage.Headers.Accept.ParseAdd("application/octet-stream");
		httpRequestMessage.Headers.Accept.ParseAdd("*/*");
		if (rangeStart.HasValue)
		{
			httpRequestMessage.Headers.Range = new RangeHeaderValue(rangeStart, rangeEnd);
			if (!string.IsNullOrWhiteSpace(probe?.ETag))
			{
				httpRequestMessage.Headers.TryAddWithoutValidation("If-Range", probe.ETag);
			}
			else
			{
				DateTimeOffset? dateTimeOffset = probe?.LastModified;
				if (dateTimeOffset.HasValue)
				{
					DateTimeOffset valueOrDefault = dateTimeOffset.GetValueOrDefault();
					httpRequestMessage.Headers.IfRange = new RangeConditionHeaderValue(valueOrDefault);
				}
			}
		}
		return httpRequestMessage;
	}

	private static void EnsureDownloadResponse(HttpResponseMessage response)
	{
		int statusCode = (int)response.StatusCode;
		if ((statusCode >= 500 || statusCode == 408 || statusCode == 429) ? true : false)
		{
			throw new HttpRequestException("Google Drive tạm thời không sẵn sàng.", null, response.StatusCode);
		}
		if (!response.IsSuccessStatusCode)
		{
			throw new InvalidOperationException($"Google Drive từ chối tải package (HTTP {response.StatusCode}).");
		}
		if (IsHtmlResponse(response))
		{
			throw new InvalidOperationException("Google Drive không trả về file ZIP. Hãy kiểm tra quyền Anyone with the link.");
		}
	}

	private static long ResolveTotalBytes(long? probedSize, long expectedSize, long maxPackageSize)
	{
		long num = probedSize ?? expectedSize;
		if (num <= 0)
		{
			throw new InvalidOperationException("Không xác định được dung lượng package Pypass.");
		}
		if (num > maxPackageSize)
		{
			throw new InvalidOperationException("Package Pypass vượt quá giới hạn tải trực tiếp " + FormatBytes(maxPackageSize) + ".");
		}
		if (num != expectedSize)
		{
			throw new InvalidOperationException("File Google Drive đã thay đổi sau lần admin validate. Hãy yêu cầu admin validate lại package.");
		}
		return num;
	}

	private static PypassDownloadState CreateState(Guid packageId, string cacheValidationToken, long totalBytes, DownloadProbe probe, int segmentCount)
	{
		List<PypassDownloadSegment> segments = SplitSegments(totalBytes, Math.Clamp(segmentCount, 1, 8));
		return new PypassDownloadState
		{
			PackageId = packageId,
			CacheValidationToken = cacheValidationToken,
			TotalBytes = totalBytes,
			ETag = probe.ETag,
			LastModifiedUtc = probe.LastModified?.UtcDateTime,
			UpdatedAtUtc = DateTime.UtcNow,
			Segments = segments
		};
	}

	private static List<PypassDownloadSegment> SplitSegments(long totalBytes, int count)
	{
		count = (int)Math.Min(count, totalBytes);
		List<PypassDownloadSegment> list = new List<PypassDownloadSegment>(count);
		long num = totalBytes / count;
		long num2 = totalBytes % count;
		long num3 = 0L;
		for (int i = 0; i < count; i++)
		{
			long num4 = num + ((i < num2) ? 1 : 0);
			list.Add(new PypassDownloadSegment
			{
				StartOffset = num3,
				EndOffset = num3 + num4 - 1,
				DownloadedBytes = 0L
			});
			num3 += num4;
		}
		return list;
	}

	private static bool IsStateCompatible(PypassDownloadState state, Guid packageId, string cacheValidationToken, long totalBytes, DownloadProbe probe)
	{
		if (state.PackageId != packageId || !string.Equals(state.CacheValidationToken, cacheValidationToken, StringComparison.Ordinal) || state.TotalBytes != totalBytes || state.Segments.Count == 0 || !ValidatorsMatch(state, probe))
		{
			return false;
		}
		long num = 0L;
		foreach (PypassDownloadSegment item in state.Segments.OrderBy((PypassDownloadSegment segment) => segment.StartOffset))
		{
			if (item.StartOffset != num || item.EndOffset < item.StartOffset || item.EndOffset >= totalBytes || item.DownloadedBytes < 0 || item.DownloadedBytes > item.Length)
			{
				return false;
			}
			num = item.EndOffset + 1;
		}
		return num == totalBytes;
	}

	private static bool ValidatorsMatch(PypassDownloadState state, DownloadProbe probe)
	{
		if (!string.IsNullOrWhiteSpace(state.ETag) && !string.IsNullOrWhiteSpace(probe.ETag) && !string.Equals(state.ETag, probe.ETag, StringComparison.Ordinal))
		{
			return false;
		}
		if (state.LastModifiedUtc.HasValue && probe.LastModified.HasValue)
		{
			return state.LastModifiedUtc.Value == probe.LastModified.Value.UtcDateTime;
		}
		return true;
	}

	private static void ResetForSequentialDownload(PypassDownloadState state, string partPath)
	{
		int num = 1;
		List<PypassDownloadSegment> list = new List<PypassDownloadSegment>(num);
		CollectionsMarshal.SetCount(list, num);
		CollectionsMarshal.AsSpan(list)[0] = new PypassDownloadSegment
		{
			StartOffset = 0L,
			EndOffset = state.TotalBytes - 1,
			DownloadedBytes = 0L
		};
		state.Segments = list;
		state.UpdatedAtUtc = DateTime.UtcNow;
		using FileStream fileStream = new FileStream(partPath, FileMode.Open, FileAccess.Write, FileShare.Read);
		fileStream.SetLength(state.TotalBytes);
	}

	private void PrepareFreshPartFile(string packageDirectory, string partPath, string completedPath, string metadataPath, long totalBytes)
	{
		TryDeleteFile(partPath);
		TryDeleteFile(completedPath);
		TryDeleteFile(metadataPath);
		EnsureFreeSpace(packageDirectory, totalBytes + minimumFreeSpaceReserveBytes);
		using FileStream fileStream = new FileStream(partPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read, 4096, FileOptions.RandomAccess);
		fileStream.SetLength(totalBytes);
	}

	private static async Task<PypassDownloadState?> TryLoadStateAsync(string metadataPath, CancellationToken cancellationToken)
	{
		_ = 1;
		try
		{
			if (!File.Exists(metadataPath))
			{
				return null;
			}
			PypassDownloadState result;
			await using (FileStream stream = new FileStream(metadataPath, FileMode.Open, FileAccess.Read, FileShare.Read, 16384, useAsync: true))
			{
				result = await JsonSerializer.DeserializeAsync<PypassDownloadState>(stream, MetadataJsonOptions, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			}
			return result;
		}
		catch (Exception ex) when (((ex is IOException || ex is UnauthorizedAccessException || ex is JsonException) ? 1 : 0) != 0)
		{
			return null;
		}
	}

	private static async Task PersistStateSnapshotAsync(string metadataPath, PypassDownloadState state, object stateLock, SemaphoreSlim metadataGate, CancellationToken cancellationToken)
	{
		await metadataGate.WaitAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		try
		{
			PypassDownloadState state2;
			lock (stateLock)
			{
				state2 = state.Clone();
			}
			await SaveStateAsync(metadataPath, state2, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
		finally
		{
			metadataGate.Release();
		}
	}

	private static async Task SaveStateAsync(string metadataPath, PypassDownloadState state, CancellationToken cancellationToken)
	{
		string tempPath = metadataPath + ".tmp";
		try
		{
			await using (FileStream stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 16384, useAsync: true))
			{
				await JsonSerializer.SerializeAsync(stream, state, MetadataJsonOptions, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				await stream.FlushAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			}
			File.Move(tempPath, metadataPath, overwrite: true);
		}
		finally
		{
			TryDeleteFile(tempPath);
		}
	}

	private static void ValidateResponseValidator(HttpResponseMessage response, DownloadProbe probe)
	{
		string text = response.Headers.ETag?.ToString();
		if (!string.IsNullOrWhiteSpace(probe.ETag) && !string.IsNullOrWhiteSpace(text) && !string.Equals(probe.ETag, text, StringComparison.Ordinal))
		{
			throw new InvalidOperationException("File Google Drive đã thay đổi trong lúc tải. Hãy chạy Active-2 lại.");
		}
		DateTimeOffset? lastModified = response.Content.Headers.LastModified;
		if (!probe.LastModified.HasValue || !lastModified.HasValue || probe.LastModified.Value.UtcDateTime == lastModified.Value.UtcDateTime)
		{
			return;
		}
		throw new InvalidOperationException("File Google Drive đã thay đổi trong lúc tải. Hãy chạy Active-2 lại.");
	}

	private static void EnsureZipSignature(string path)
	{
		Span<byte> buffer = stackalloc byte[4];
		using FileStream fileStream = File.OpenRead(path);
		bool flag = fileStream.Read(buffer) != buffer.Length || buffer[0] != 80 || buffer[1] != 75;
		if (!flag)
		{
			bool flag2;
			switch (buffer[2])
			{
			case 3:
			case 5:
			case 7:
				flag2 = true;
				break;
			default:
				flag2 = false;
				break;
			}
			flag = !flag2;
		}
		bool flag3 = flag;
		if (!flag3)
		{
			bool flag2;
			switch (buffer[3])
			{
			case 4:
			case 6:
			case 8:
				flag2 = true;
				break;
			default:
				flag2 = false;
				break;
			}
			flag3 = !flag2;
		}
		if (flag3)
		{
			throw new InvalidDataException("File tải về không phải package ZIP hợp lệ.");
		}
	}

	private static void EnsureFreeSpace(string directory, long requiredBytes)
	{
		try
		{
			string pathRoot = Path.GetPathRoot(Path.GetFullPath(directory));
			if (!string.IsNullOrWhiteSpace(pathRoot))
			{
				DriveInfo driveInfo = new DriveInfo(pathRoot);
				if (driveInfo.IsReady && driveInfo.AvailableFreeSpace < requiredBytes)
				{
					throw new InvalidOperationException($"Ổ đĩa không đủ chỗ trống để tải package. Cần ít nhất {FormatBytes(requiredBytes)}, hiện còn {FormatBytes(driveInfo.AvailableFreeSpace)}.");
				}
			}
		}
		catch (IOException)
		{
		}
	}

	private void CleanupExpiredCache(int retentionHours, Guid activePackageId)
	{
		try
		{
			if (!Directory.Exists(cacheRoot))
			{
				return;
			}
			DateTime dateTime = DateTime.UtcNow.AddHours(-retentionHours);
			foreach (string item in Directory.EnumerateDirectories(cacheRoot))
			{
				if (!string.Equals(Path.GetFileName(item), activePackageId.ToString("N"), StringComparison.OrdinalIgnoreCase) && Directory.GetLastWriteTimeUtc(item) < dateTime)
				{
					TryDeleteDirectory(item);
				}
			}
		}
		catch (Exception ex) when (((ex is IOException || ex is UnauthorizedAccessException) ? 1 : 0) != 0)
		{
		}
	}

	private static PypassDirectDownloadProgress BuildProgress(long bytesReceived, long totalBytes, long speedBytesPerSecond, TimeSpan? estimatedRemaining, int connectionCount, bool resumed, string message)
	{
		int value = (int)((totalBytes > 0) ? Math.Clamp(bytesReceived * 100 / totalBytes, 0L, 100L) : 0);
		return new PypassDirectDownloadProgress(bytesReceived, totalBytes, value, speedBytesPerSecond, estimatedRemaining, connectionCount, resumed, message);
	}

	private static string BuildDownloadMessage(long speedBytesPerSecond, TimeSpan? estimatedRemaining, int connectionCount, bool resumed)
	{
		string value = (resumed ? "Đang tiếp tục" : "Đang tải");
		string value2 = ((speedBytesPerSecond > 0) ? (" • " + FormatBytes(speedBytesPerSecond) + "/s") : string.Empty);
		string value3 = (estimatedRemaining.HasValue ? (" • còn khoảng " + FormatDuration(estimatedRemaining.Value)) : string.Empty);
		return $"{value} bằng {Math.Max(1, connectionCount)} kết nối{value2}{value3}.";
	}

	private static string FormatDuration(TimeSpan duration)
	{
		if (!(duration.TotalHours >= 1.0))
		{
			if (!(duration.TotalMinutes >= 1.0))
			{
				return $"{Math.Max(1, (int)duration.TotalSeconds)} giây";
			}
			return $"{Math.Max(1, (int)duration.TotalMinutes)} phút";
		}
		return $"{(int)duration.TotalHours} giờ {duration.Minutes} phút";
	}

	private static string FormatBytes(long bytes)
	{
		string[] array = new string[4] { "B", "KB", "MB", "GB" };
		double num = Math.Max(0L, bytes);
		int num2 = 0;
		while (num >= 1024.0 && num2 < array.Length - 1)
		{
			num /= 1024.0;
			num2++;
		}
		return $"{num:0.##} {array[num2]}";
	}

	private static long NormalizeMaxPackageSize(long configuredValue)
	{
		if (configuredValue <= 0)
		{
			return 11811160064L;
		}
		return Math.Min(configuredValue, 11811160064L);
	}

	private static string NormalizeCacheValidationToken(string? value, Guid packageId, long totalBytes)
	{
		if (!string.IsNullOrWhiteSpace(value))
		{
			return value.Trim();
		}
		return $"{packageId:N}:{totalBytes}";
	}

	private static HttpClientHandler CreateDefaultHandler()
	{
		return new HttpClientHandler
		{
			AllowAutoRedirect = true,
			MaxAutomaticRedirections = 10,
			UseCookies = true,
			CookieContainer = new CookieContainer(),
			AutomaticDecompression = DecompressionMethods.None
		};
	}

	private static string ResolveDefaultCacheRoot()
	{
		return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NestGLauncher", "PypassDownloads");
	}

	private static Uri NormalizeGoogleDriveDownloadUri(string value)
	{
		if (!Uri.TryCreate(value?.Trim(), UriKind.Absolute, out Uri result) || !string.Equals(result.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) || !IsGoogleDriveHost(result.Host))
		{
			throw new InvalidOperationException("Link tải trực tiếp Pypass không phải Google Drive hợp lệ.");
		}
		string queryParameter = GetQueryParameter(result, "resourcekey");
		string[] array = result.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
		if (array.Length >= 3 && string.Equals(array[0], "file", StringComparison.OrdinalIgnoreCase) && string.Equals(array[1], "d", StringComparison.OrdinalIgnoreCase))
		{
			return BuildGoogleDriveDownloadUri(array[2], queryParameter);
		}
		string queryParameter2 = GetQueryParameter(result, "id");
		if (!string.IsNullOrWhiteSpace(queryParameter2))
		{
			return BuildGoogleDriveDownloadUri(queryParameter2, queryParameter);
		}
		return result;
	}

	private static Uri BuildGoogleDriveDownloadUri(string fileId, string? resourceKey)
	{
		string text = "export=download&id=" + Uri.EscapeDataString(fileId);
		if (!string.IsNullOrWhiteSpace(resourceKey))
		{
			text = text + "&resourcekey=" + Uri.EscapeDataString(resourceKey);
		}
		return new UriBuilder(Uri.UriSchemeHttps, "drive.google.com")
		{
			Path = "uc",
			Query = text
		}.Uri;
	}

	private static Uri? ExtractGoogleDriveConfirmUri(Uri baseUri, string html)
	{
		Match match = Regex.Match(html, "href=\"(?<href>[^\"]*(?:confirm=|download)[^\"]*)\"", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
		if (match.Success)
		{
			string relativeUri = WebUtility.HtmlDecode(match.Groups["href"].Value);
			if (Uri.TryCreate(baseUri, relativeUri, out Uri result))
			{
				return result;
			}
		}
		Match match2 = Regex.Match(html, "<form[^>]*id=\"download-form\"[^>]*action=\"(?<action>[^\"]+)\"[^>]*>(?<body>.*?)</form>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant);
		if (!match2.Success)
		{
			return null;
		}
		string relativeUri2 = WebUtility.HtmlDecode(match2.Groups["action"].Value);
		if (!Uri.TryCreate(baseUri, relativeUri2, out Uri result2))
		{
			return null;
		}
		List<string> list = new List<string>();
		foreach (Match item in Regex.Matches(match2.Groups["body"].Value, "<input[^>]*name=\"(?<name>[^\"]+)\"[^>]*value=\"(?<value>[^\"]*)\"[^>]*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
		{
			string stringToEscape = WebUtility.HtmlDecode(item.Groups["name"].Value);
			string stringToEscape2 = WebUtility.HtmlDecode(item.Groups["value"].Value);
			list.Add(Uri.EscapeDataString(stringToEscape) + "=" + Uri.EscapeDataString(stringToEscape2));
		}
		if (list.Count == 0)
		{
			return null;
		}
		return new UriBuilder(result2)
		{
			Query = string.Join("&", list)
		}.Uri;
	}

	private static string? GetQueryParameter(Uri uri, string name)
	{
		string[] array = uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries);
		for (int i = 0; i < array.Length; i++)
		{
			string[] array2 = array[i].Split('=', 2);
			if (array2.Length != 0 && string.Equals(Uri.UnescapeDataString(array2[0]), name, StringComparison.OrdinalIgnoreCase))
			{
				if (array2.Length != 1)
				{
					return Uri.UnescapeDataString(array2[1].Replace('+', ' '));
				}
				return string.Empty;
			}
		}
		return null;
	}

	private static bool IsGoogleDriveHost(string host)
	{
		if (!string.Equals(host, "drive.google.com", StringComparison.OrdinalIgnoreCase))
		{
			return string.Equals(host, "drive.usercontent.google.com", StringComparison.OrdinalIgnoreCase);
		}
		return true;
	}

	private static bool IsHtmlResponse(HttpResponseMessage response)
	{
		MediaTypeHeaderValue? contentType = response.Content.Headers.ContentType;
		if (contentType == null)
		{
			return false;
		}
		return contentType.MediaType?.Contains("html", StringComparison.OrdinalIgnoreCase) == true;
	}

	private static void TouchDirectory(string path)
	{
		try
		{
			Directory.SetLastWriteTimeUtc(path, DateTime.UtcNow);
		}
		catch
		{
		}
	}

	private static void TryDeleteFile(string path)
	{
		try
		{
			if (File.Exists(path))
			{
				File.Delete(path);
			}
		}
		catch
		{
		}
	}

	private static void TryDeleteDirectory(string path)
	{
		try
		{
			if (Directory.Exists(path))
			{
				Directory.Delete(path, recursive: true);
			}
		}
		catch
		{
		}
	}

	public void Dispose()
	{
		httpClient.Dispose();
	}
}
