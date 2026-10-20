using System;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace app_desktop.Services.Warp;

internal sealed class WarpInstallerService
{
	private readonly WarpClientService client;

	private readonly IWarpPackageDownloader downloader;

	private readonly IWarpPackageVerifier verifier;

	private readonly IWarpPackageRunner runner;

	internal WarpInstallerService(WarpClientService client, IWarpPackageDownloader? downloader = null, IWarpPackageVerifier? verifier = null, IWarpPackageRunner? runner = null)
	{
		this.client = client;
		this.downloader = downloader ?? new CloudflareWarpPackageDownloader();
		this.verifier = verifier ?? new CloudflareWarpPackageVerifier();
		this.runner = runner ?? new CloudflareWarpPackageRunner();
	}

	internal async Task<WarpInstallResult> InstallAsync(IProgress<WarpInstallProgress>? progress, CancellationToken cancellationToken = default(CancellationToken))
	{
		if ((await client.GetStatusAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false)).Kind != 0)
		{
			return new WarpInstallResult(WarpInstallOutcome.AlreadyInstalled, "Cloudflare WARP đã có trên máy. NestG sẽ đọc lại trạng thái kết nối.");
		}
		string tempDirectory = Path.Combine(Path.GetTempPath(), "NestGWarp-" + Guid.NewGuid().ToString("N"));
		string installerPath = Path.Combine(tempDirectory, "Cloudflare_WARP.msi");
		Directory.CreateDirectory(tempDirectory);
		WarpInstallResult result;
		try
		{
			try
			{
				progress?.Report(new WarpInstallProgress("Đang tải Cloudflare WARP từ Cloudflare…", null));
				await downloader.DownloadAsync(installerPath, progress, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				cancellationToken.ThrowIfCancellationRequested();
				progress?.Report(new WarpInstallProgress("Đang kiểm tra chữ ký số của bộ cài…", 100, WarpInstallPhase.Verifying));
				if (!verifier.IsOfficialInstaller(installerPath))
				{
					result = new WarpInstallResult(WarpInstallOutcome.Failed, "Bộ cài không xác minh được là bản chính thức của Cloudflare. NestG đã dừng cài đặt.");
				}
				else
				{
					cancellationToken.ThrowIfCancellationRequested();
					progress?.Report(new WarpInstallProgress("Đã mở bộ cài. Hãy xác nhận quyền Windows và làm theo hướng dẫn Cloudflare.", 100, WarpInstallPhase.Installing));
					int num2;
					try
					{
						num2 = await runner.RunAsync(installerPath).ConfigureAwait(continueOnCapturedContext: false);
					}
					catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
					{
						result = new WarpInstallResult(WarpInstallOutcome.Cancelled, "Bạn đã hủy yêu cầu quyền Windows. WARP chưa được cài.");
						goto end_IL_0131;
					}
					bool flag;
					switch (num2)
					{
					case 1602:
						result = new WarpInstallResult(WarpInstallOutcome.Cancelled, "Bạn đã hủy bộ cài Cloudflare. Có thể thử lại từ công tắc WARP.");
						goto end_IL_0131;
					case 1641:
					case 3010:
						flag = true;
						break;
					default:
						flag = false;
						break;
					}
					if (flag)
					{
						result = new WarpInstallResult(WarpInstallOutcome.NeedsRestart, "Cloudflare yêu cầu khởi động lại Windows để hoàn tất cài WARP.");
					}
					else if (num2 != 0)
					{
						result = new WarpInstallResult(WarpInstallOutcome.Failed, "Bộ cài Cloudflare chưa hoàn tất. Hãy thử lại hoặc cài từ trang chính thức.");
					}
					else
					{
						progress?.Report(new WarpInstallProgress("Đang kiểm tra dịch vụ và chế độ WARP…", 100, WarpInstallPhase.Checking));
						using CancellationTokenSource readinessDeadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
						readinessDeadline.CancelAfter(TimeSpan.FromSeconds(45.0));
						try
						{
							int attempt = 0;
							while (attempt < 20)
							{
								WarpConnectionStatus warpConnectionStatus = await client.GetStatusAsync(readinessDeadline.Token).ConfigureAwait(continueOnCapturedContext: false);
								if (!warpConnectionStatus.CanToggle)
								{
									WarpConnectionKind kind = warpConnectionStatus.Kind;
									if ((uint)(kind - 4) > 1u)
									{
										if (attempt < 19)
										{
											await Task.Delay(1000, readinessDeadline.Token).ConfigureAwait(continueOnCapturedContext: false);
										}
										attempt++;
										continue;
									}
									break;
								}
								result = new WarpInstallResult(WarpInstallOutcome.Ready, "WARP đã sẵn sàng. Bấm công tắc NestG để bật kết nối.");
								goto end_IL_03b3;
							}
						}
						catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
						{
						}
						result = new WarpInstallResult(WarpInstallOutcome.NeedsCloudflareSetup, "Cloudflare đã được cài nhưng WARP chưa sẵn sàng. Mở ứng dụng Cloudflare để hoàn tất thiết lập hoặc chuyển sang chế độ WARP.");
						end_IL_03b3:;
					}
				}
				end_IL_0131:;
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				result = new WarpInstallResult(WarpInstallOutcome.Cancelled, "Đã hủy tải WARP. Kết nối trên máy không thay đổi.");
			}
			catch (Exception)
			{
				result = new WarpInstallResult(WarpInstallOutcome.Failed, "Không thể tải hoặc mở bộ cài Cloudflare. Hãy kiểm tra mạng rồi thử lại.");
			}
		}
		finally
		{
			await CleanupTemporaryPackageAsync(installerPath, tempDirectory).ConfigureAwait(continueOnCapturedContext: false);
		}
		return result;
	}

	private static async Task CleanupTemporaryPackageAsync(string installerPath, string directory)
	{
		for (int attempt = 0; attempt < 5; attempt++)
		{
			try
			{
				if (File.Exists(installerPath))
				{
					File.Delete(installerPath);
				}
				if (Directory.Exists(directory))
				{
					Directory.Delete(directory);
				}
				break;
			}
			catch (IOException)
			{
			}
			catch (UnauthorizedAccessException)
			{
				break;
			}
			if (attempt < 4)
			{
				await Task.Delay(250).ConfigureAwait(continueOnCapturedContext: false);
			}
		}
	}
}
