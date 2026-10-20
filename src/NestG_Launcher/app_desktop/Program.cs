using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using UI_Desktop;
using UI_Desktop.Store;
using app_desktop.Services;
using app_desktop.Services.SteamConnection;

namespace app_desktop;

internal static class Program
{
	[STAThread]
	private static void Main()
	{
		using Mutex singleInstanceMutex = new Mutex(true, @"Local\NestGLauncher_Singleton_App", out bool isNewInstance);
		if (!isNewInstance)
		{
			MessageBox.Show("NestG Launcher hiện đang chạy rồi!\nBạn vui lòng kiểm tra dưới thanh Taskbar hoặc khay hệ thống.", "NestG Launcher", MessageBoxButtons.OK, MessageBoxIcon.Information);
			return;
		}

		string logPath = Path.Combine(AppContext.BaseDirectory, "startup.log");
		AppDomain.CurrentDomain.UnhandledException += (s, e) =>
		{
			try { File.AppendAllText(logPath, $"[{DateTime.Now}] UNHANDLED APPDOMAIN EXCEPTION: {e.ExceptionObject}\n"); } catch { }
		};
		Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
		Application.ThreadException += (s, e) =>
		{
			try { File.AppendAllText(logPath, $"[{DateTime.Now}] THREAD EXCEPTION: {e.Exception}\n"); } catch { }
		};

		try
		{
			File.WriteAllText(logPath, $"[{DateTime.Now}] Starting NestG Launcher...\n");
			ApplicationConfiguration.Initialize();
			File.AppendAllText(logPath, $"[{DateTime.Now}] ApplicationConfiguration.Initialize done\n");
			Application.AddMessageFilter(StoreMouseWheelMessageFilter.Instance);
			File.AppendAllText(logPath, $"[{DateTime.Now}] MessageFilter added\n");
			bool restored = DesktopAppContext.InitializeAsync().GetAwaiter().GetResult();
			File.AppendAllText(logPath, $"[{DateTime.Now}] InitializeAsync done (restored={restored})\n");
			Form form = DesktopAppContext.CreateStartupForm(restored);
			File.AppendAllText(logPath, $"[{DateTime.Now}] CreateStartupForm done: {form.GetType().FullName}\n");
			Application.Run(form);
		}
		catch (Exception exception)
		{
			try
			{
				File.AppendAllText(logPath, $"[{DateTime.Now}] FATAL ERROR: {exception}\n");
			}
			catch { }
			MessageBox.Show("Fatal error starting NestG Launcher:\n\n" + exception, "Lỗi khởi động", MessageBoxButtons.OK, MessageBoxIcon.Hand);
		}
		finally
		{
			if (SteamConnectionCoordinator.IsCreated)
			{
				try
				{
					SteamConnectionCoordinator.Shared.ShutdownAsync().WaitAsync(TimeSpan.FromSeconds(25.0)).GetAwaiter()
						.GetResult();
				}
				catch
				{
				}
			}
		}
	}
}
