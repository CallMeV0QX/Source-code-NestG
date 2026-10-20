using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Threading;
using System.Windows.Forms;

namespace app_desktop.Services.SteamExclusions;

internal static class SteamExclusionStartup
{
	private static int started;

	internal static async void Start(CancellationToken cancellationToken)
	{
		if (Interlocked.Exchange(ref started, 1) != 0)
		{
			return;
		}
		Mutex mutex = null;
		bool acquired = false;
		try
		{
			using WindowsIdentity identity = WindowsIdentity.GetCurrent();
			string text = identity.User?.Value ?? Environment.UserName;
			mutex = new Mutex(initiallyOwned: false, "Local\\NestG.SteamExclusions." + text);
			try
			{
				acquired = mutex.WaitOne(0);
			}
			catch (AbandonedMutexException)
			{
				acquired = true;
			}
			if (!acquired)
			{
				return;
			}
			string directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NestGLauncher", "SteamExclusions");
			SteamExclusionOutcome outcome = await new SteamExclusionService(new SteamGameFolderDiscovery(), new WindowsDefenderExclusionClient(new DefenderPowerShellRunner()), new SteamExclusionConsentStore(directory), Confirm).RunOnceAsync(cancellationToken);
			cancellationToken.ThrowIfCancellationRequested();
			WriteStatus(directory, outcome);
			string text2 = OutcomeMessage(outcome);
			if (text2 != null)
			{
				ShowNotification(text2);
			}
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
		}
		catch
		{
		}
		finally
		{
			try
			{
				if (acquired)
				{
					mutex?.ReleaseMutex();
				}
			}
			catch (ApplicationException)
			{
			}
			mutex?.Dispose();
		}
	}

	internal static string BuildConsentMessage(IReadOnlyList<string> paths)
	{
		return "NestG muốn thêm các thư mục game Steam sau vào Exclusions của Microsoft Defender:\n\n" + string.Join("\n", paths) + "\n\nThao tác này làm giảm bảo vệ: Defender sẽ bỏ qua quét nội dung trong các thư mục này. Không nên đồng ý nếu bạn không tin cậy các file game.\n\nNếu đồng ý, NestG ghi nhớ đúng các đường dẫn này và kiểm tra lại mỗi lần mở launcher; Windows có thể hỏi quyền quản trị để bổ sung khi còn thiếu. Thư viện mới sẽ được hỏi riêng. Chọn Không để bỏ qua và không hỏi lại trong 7 ngày; launcher vẫn hoạt động bình thường.\n\nBạn có đồng ý không?";
	}

	private static bool Confirm(IReadOnlyList<string> paths)
	{
		Form form2 = Form.ActiveForm ?? ((IEnumerable)Application.OpenForms).Cast<Form>().FirstOrDefault((Form form) => form.Visible);
		if (form2 == null || form2.IsDisposed)
		{
			return false;
		}
		using SteamExclusionConsentDialog steamExclusionConsentDialog = new SteamExclusionConsentDialog(paths, null);
		return steamExclusionConsentDialog.ShowDialog(form2) == DialogResult.Yes;
	}

	internal static string? OutcomeMessage(SteamExclusionOutcome outcome)
	{
		return outcome switch
		{
			SteamExclusionOutcome.Added => "Đã xác nhận các thư mục game Steam có trong Defender Exclusions.", 
			SteamExclusionOutcome.Unknown => "Không đọc được Exclusions. NestG chưa thay đổi Defender; bạn có thể kiểm tra trong Windows Security.", 
			SteamExclusionOutcome.Blocked => "Chính sách Windows không cho NestG quản lý Exclusions. Hãy liên hệ quản trị viên nếu cần.", 
			SteamExclusionOutcome.Failed => "Không thể hoàn tất Exclusions. Có thể chỉ một phần đã được thêm; hãy kiểm tra Windows Security.", 
			SteamExclusionOutcome.TimedOut => "Kiểm tra Exclusions quá thời gian. Launcher vẫn hoạt động; hãy kiểm tra Windows Security nếu cần.", 
			_ => null, 
		};
	}

	private static void WriteStatus(string directory, SteamExclusionOutcome outcome)
	{
		try
		{
			Directory.CreateDirectory(directory);
			File.WriteAllText(Path.Combine(directory, "last-status.log"), $"{DateTime.UtcNow:O} {outcome}");
		}
		catch
		{
		}
	}

	private static void ShowNotification(string message)
	{
		NotifyIcon tray = new NotifyIcon
		{
			Icon = SystemIcons.Information,
			Text = "NestG — Steam Exclusions",
			Visible = true,
			BalloonTipTitle = "NestG — Steam Exclusions",
			BalloonTipText = message,
			BalloonTipIcon = ToolTipIcon.Info
		};
		System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer
		{
			Interval = 12000
		};
		timer.Tick += Cleanup;
		Application.ApplicationExit += Cleanup;
		tray.ShowBalloonTip(8000);
		timer.Start();
		void Cleanup(object? sender, EventArgs e)
		{
			timer.Stop();
			Application.ApplicationExit -= Cleanup;
			tray.Dispose();
			timer.Dispose();
		}
	}
}
