using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace app_desktop.Services.WindowsUpdate;

internal sealed class WubPackageDownloadService : IWubPackageProvisioner
{
	private readonly WubPackageStore package;

	private readonly IWubPackageDownloader downloader;

	internal WubPackageDownloadService(WubPackageStore package, IWubPackageDownloader? downloader = null)
	{
		this.package = package;
		this.downloader = downloader ?? new SordumWubPackageDownloader();
	}

	public async Task<WubPackageResult> DownloadAndImportAsync(IProgress<WubPackageProgress>? progress, CancellationToken cancellationToken)
	{
		if (package.IsReady)
		{
			return new WubPackageResult(WubPackageOutcome.AlreadyReady, "WUB v1.8 đã sẵn sàng.");
		}
		string directory = Path.Combine(Path.GetTempPath(), "NestGWub-" + Guid.NewGuid().ToString("N"));
		string archive = Path.Combine(directory, "Wub.zip");
		Directory.CreateDirectory(directory);
		try
		{
			progress?.Report(new WubPackageProgress("Đang tải WUB v1.8 từ Sordum…", null));
			await downloader.DownloadAsync(archive, progress, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			cancellationToken.ThrowIfCancellationRequested();
			progress?.Report(new WubPackageProgress("Đang xác minh ZIP và các file WUB v1.8…", 100, WubPackagePhase.Verifying));
			await Task.Run(delegate
			{
				cancellationToken.ThrowIfCancellationRequested();
				package.Import(archive);
			}).ConfigureAwait(continueOnCapturedContext: false);
			return new WubPackageResult(WubPackageOutcome.Ready, "WUB đã sẵn sàng. NestG sẽ tiếp tục đổi trạng thái Windows Update bằng WUB.");
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			return new WubPackageResult(WubPackageOutcome.Cancelled, "Đã hủy tải WUB. Windows Update không thay đổi.");
		}
		catch (OperationCanceledException)
		{
			return new WubPackageResult(WubPackageOutcome.Failed, "Nguồn tải WUB phản hồi quá lâu. Hãy thử lại hoặc chọn ZIP v1.8 từ Sordum.");
		}
		catch (InvalidDataException)
		{
			return new WubPackageResult(WubPackageOutcome.Failed, "ZIP tải về không khớp WUB v1.8 chính thức NestG hỗ trợ. Hãy thử lại hoặc chọn ZIP v1.8 từ Sordum.");
		}
		catch (Exception ex4) when (((ex4 is HttpRequestException || ex4 is IOException || ex4 is UnauthorizedAccessException) ? 1 : 0) != 0)
		{
			return new WubPackageResult(WubPackageOutcome.Failed, "Không tải hoặc nhập được WUB. Kiểm tra mạng và thử lại, hoặc chọn ZIP đã tải từ Sordum.");
		}
		finally
		{
			try
			{
				if (File.Exists(archive))
				{
					File.Delete(archive);
				}
				if (Directory.Exists(directory))
				{
					Directory.Delete(directory);
				}
			}
			catch (IOException)
			{
			}
			catch (UnauthorizedAccessException)
			{
			}
		}
	}
}
