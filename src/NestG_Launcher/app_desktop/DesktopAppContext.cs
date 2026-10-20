using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using UI_Desktop.Auth;
using UI_Desktop.Store;
using app_desktop.Infrastructure;
using app_desktop.Models;
using app_desktop.Services;

namespace app_desktop;

internal static class DesktopAppContext
{
	private static int denuvoUpdateCheckStarted;

	public static DesktopAppSettings Settings { get; private set; } = new DesktopAppSettings();

	public static AuthSessionManager AuthSessionManager { get; private set; } = null;

	public static DesktopPaymentRealtimeClient PaymentRealtimeClient { get; private set; } = null;

	public static DesktopSupportRealtimeClient SupportRealtimeClient { get; private set; } = null;

	public static DesktopDenuvoActiveRealtimeClient DenuvoActiveRealtimeClient { get; private set; } = null;

	public static async Task<bool> InitializeAsync(CancellationToken cancellationToken = default(CancellationToken))
	{
		Settings = await DesktopAppSettings.LoadForRuntimeAsync(AppContext.BaseDirectory, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		Task.Run(async delegate
		{
			try
			{
				await DenuvoFileTransaction.RecoverAllAsync().ConfigureAwait(continueOnCapturedContext: false);
			}
			catch (Exception ex)
			{
				DesktopDiagnosticLog.Write("DenuvoRecovery", "StartupRecoveryPending", "ErrorType=" + ex.GetType().Name);
			}
		});
		await LegacyFileGameCleanupService.CleanupAsync(Settings.StorageFolderName, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		SecureTokenStore tokenStore = new SecureTokenStore(Settings.StorageFolderName, Settings.TokenFileName);
		AuthSessionManager = new AuthSessionManager(new DesktopAuthApiClient(Settings.ApiBaseUrl), tokenStore, Settings);
		PaymentRealtimeClient = new DesktopPaymentRealtimeClient(Settings.ApiBaseUrl, AuthSessionManager);
		SupportRealtimeClient = new DesktopSupportRealtimeClient(Settings.ApiBaseUrl, AuthSessionManager);
		DenuvoActiveRealtimeClient = new DesktopDenuvoActiveRealtimeClient(Settings.ApiBaseUrl, AuthSessionManager);
		return await AuthSessionManager.TryRestoreSessionAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
	}

	public static Form CreateStartupForm(bool hasStoredSession)
	{
		string log = Path.Combine(AppContext.BaseDirectory, "startup.log");
		File.AppendAllText(log, $"[{DateTime.Now}] CreateStartupForm(hasStoredSession={hasStoredSession})\n");
		if (!hasStoredSession)
		{
			File.AppendAllText(log, $"[{DateTime.Now}] Calling CreateLoginForm()...\n");
			return CreateLoginForm();
		}
		File.AppendAllText(log, $"[{DateTime.Now}] Calling CreateStorefrontForm()...\n");
		return CreateStorefrontForm();
	}

	public static Form CreateLoginForm()
	{
		string log = Path.Combine(AppContext.BaseDirectory, "startup.log");
		File.AppendAllText(log, $"[{DateTime.Now}] new LoginWebViewForm()...\n");
		var form = new LoginWebViewForm();
		File.AppendAllText(log, $"[{DateTime.Now}] new LoginWebViewForm() DONE\n");
		return form;
	}

	public static Form CreateStorefrontForm()
	{
		string log = Path.Combine(AppContext.BaseDirectory, "startup.log");
		try
		{
			File.AppendAllText(log, $"[{DateTime.Now}] StartDenuvoConfigUpdateCheck...\n");
			StartDenuvoConfigUpdateCheck();
			File.AppendAllText(log, $"[{DateTime.Now}] StartDenuvoActiveRealtime...\n");
			StartDenuvoActiveRealtime();
			File.AppendAllText(log, $"[{DateTime.Now}] new StoreWebHomeForm()...\n");
			var form = new StoreWebHomeForm();
			File.AppendAllText(log, $"[{DateTime.Now}] new StoreWebHomeForm() DONE\n");
			return form;
		}
		catch (Exception ex)
		{
			File.AppendAllText(log, $"[{DateTime.Now}] EXCEPTION in CreateStorefrontForm: {ex}\n");
			throw;
		}
	}

	private static void StartDenuvoActiveRealtime()
	{
		Task.Run(async delegate
		{
			try
			{
				await DenuvoActiveRealtimeClient.StartAsync().ConfigureAwait(continueOnCapturedContext: false);
			}
			catch (Exception error)
			{
				WriteDenuvoActiveStartupLog(error);
			}
		});
	}

	private static void StartDenuvoConfigUpdateCheck()
	{
		if (Interlocked.Exchange(ref denuvoUpdateCheckStarted, 1) == 1)
		{
			return;
		}
		Task.Run(async delegate
		{
			_ = 1;
			try
			{
				string text = await AuthSessionManager.GetAccessTokenAsync().ConfigureAwait(continueOnCapturedContext: false);
				if (!string.IsNullOrWhiteSpace(text))
				{
					await DenuvoFixInstallService.ApplyPendingConfigUpdatesAsync(new StorefrontApiClient(Settings.ApiBaseUrl), text).ConfigureAwait(continueOnCapturedContext: false);
				}
			}
			catch
			{
				WriteDenuvoUpdateStartupLog();
			}
		});
	}

	private static void WriteDenuvoUpdateStartupLog()
	{
		try
		{
			string text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NestGLauncher", "DenuvoFixLogs");
			Directory.CreateDirectory(text);
			File.WriteAllLines(Path.Combine(text, $"denuvo-startup-update-{DateTime.UtcNow:yyyyMMddHHmmss}.log"), new string[2]
			{
				$"Time UTC: {DateTime.UtcNow:O}",
				"Denuvo config update check failed during launcher startup."
			});
		}
		catch
		{
		}
	}

	private static void WriteDenuvoActiveStartupLog(Exception error)
	{
		try
		{
			string text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NestGLauncher", "DenuvoFixLogs");
			Directory.CreateDirectory(text);
			File.WriteAllLines(Path.Combine(text, $"denuvo-active-startup-{DateTime.UtcNow:yyyyMMddHHmmss}.log"), new string[3]
			{
				$"Time UTC: {DateTime.UtcNow:O}",
				"Denuvo Active realtime initial attempt failed; supervised recovery remains active.",
				$"ErrorType: {error.GetType().Name}; HResult: {error.HResult}"
			});
		}
		catch
		{
		}
	}
}
