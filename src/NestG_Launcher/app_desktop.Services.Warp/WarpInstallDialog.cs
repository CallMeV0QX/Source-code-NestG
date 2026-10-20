using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using UI_Desktop;

namespace app_desktop.Services.Warp;

internal sealed class WarpInstallDialog : Form
{
	private sealed class DialogProgress(Form owner, Action<WarpInstallProgress> updateView) : IProgress<WarpInstallProgress>
	{
		private WarpInstallPhase? lastPhase;

		private int? lastPercent;

		public void Report(WarpInstallProgress value)
		{
			if (value.Phase == WarpInstallPhase.Downloading && value.Percent.HasValue && lastPhase == value.Phase && lastPercent == value.Percent)
			{
				return;
			}
			lastPhase = value.Phase;
			lastPercent = value.Percent;
			try
			{
				if (owner.IsDisposed || !owner.IsHandleCreated)
				{
					return;
				}
				if (owner.InvokeRequired)
				{
					owner.Invoke(delegate
					{
						updateView(value);
					});
				}
				else
				{
					updateView(value);
				}
			}
			catch (InvalidOperationException) when (owner.IsDisposed || !owner.IsHandleCreated)
			{
			}
			catch (ObjectDisposedException)
			{
			}
		}
	}

	private const string OfficialHelpUrl = "https://developers.cloudflare.com/warp-client/get-started/windows/";

	private readonly WarpInstallerService installer;

	private readonly WarpClientService client;

	private readonly CoreWebView2Environment? environment;

	private readonly CancellationTokenSource cancellation = new CancellationTokenSource();

	private readonly WebView2 browser = new WebView2();

	private readonly Label loading;

	private bool viewReady;

	private bool webViewFailed;

	private bool busy;

	private bool installerStarted;

	private bool closeRequested;

	private bool needsSetup;

	private string stage = "idle";

	private string statusTitle = "Chưa cài đặt";

	private string statusMessage = "WARP chưa có trên máy. Kết nối vẫn đang tắt.";

	private int? percent;

	private bool showProgress;

	private string primaryLabel = "Tải và cài WARP";

	private bool primaryEnabled = true;

	private string closeLabel = "Để sau";

	internal WarpInstallResult? Result { get; private set; }

	internal WarpInstallDialog(WarpInstallerService installer, WarpClientService client, CoreWebView2Environment? environment = null)
	{
		this.installer = installer;
		this.client = client;
		this.environment = environment;
		Text = "NestG — Cài Cloudflare WARP";
		AppIconProvider.ApplyIcon(this);
		base.AutoScaleMode = AutoScaleMode.None;
		BackColor = Color.FromArgb(13, 20, 30);
		base.ClientSize = new Size(672, 500);
		base.FormBorderStyle = FormBorderStyle.None;
		base.StartPosition = FormStartPosition.CenterParent;
		base.MaximizeBox = false;
		base.MinimizeBox = false;
		base.ShowInTaskbar = false;
		base.KeyPreview = true;
		browser.Dock = DockStyle.Fill;
		browser.DefaultBackgroundColor = BackColor;
		browser.AllowExternalDrop = false;
		loading = new Label
		{
			Dock = DockStyle.Fill,
			Text = "Đang mở cửa sổ cài WARP…",
			TextAlign = ContentAlignment.MiddleCenter,
			ForeColor = Color.FromArgb(192, 208, 225),
			BackColor = BackColor
		};
		base.Controls.Add(browser);
		base.Controls.Add(loading);
	}

	protected override void OnLoad(EventArgs e)
	{
		base.OnLoad(e);
		FitToScreen();
		CenterToParent();
	}

	protected override void OnDpiChanged(DpiChangedEventArgs e)
	{
		base.OnDpiChanged(e);
		FitToScreen();
	}

	internal static Size CalculateClientSize(Size workingArea, uint dpi)
	{
		double num = (double)((dpi == 0) ? 96u : dpi) / 96.0;
		int num2 = (int)Math.Round(24.0 * num);
		return new Size(Math.Min((int)Math.Round(672.0 * num), Math.Max(1, workingArea.Width - num2)), Math.Min((int)Math.Round(500.0 * num), Math.Max(1, workingArea.Height - num2)));
	}

	private void FitToScreen()
	{
		Size size = Screen.FromHandle(base.Handle).WorkingArea.Size;
		uint dpiForWindow = GetDpiForWindow(base.Handle);
		base.ClientSize = (webViewFailed ? CalculateFailureClientSize(size, dpiForWindow) : CalculateClientSize(size, dpiForWindow));
	}

	internal static Size CalculateFailureClientSize(Size workingArea, uint dpi)
	{
		double num = (double)((dpi == 0) ? 96u : dpi) / 96.0;
		int num2 = (int)Math.Round(24.0 * num);
		return new Size(Math.Min((int)Math.Round(460.0 * num), Math.Max(1, workingArea.Width - num2)), Math.Min((int)Math.Round(190.0 * num), Math.Max(1, workingArea.Height - num2)));
	}

	internal Task ShowAsync(Form owner, CancellationToken cancellationToken = default(CancellationToken))
	{
		ArgumentNullException.ThrowIfNull(owner, "owner");
		cancellationToken.ThrowIfCancellationRequested();
		if (base.IsDisposed)
		{
			throw new ObjectDisposedException("WarpInstallDialog");
		}
		TaskCompletionSource completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		bool ownerWasEnabled = owner.Enabled;
		CancellationTokenRegistration cancellationRegistration = default(CancellationTokenRegistration);
		FormClosedEventHandler closed = null;
		closed = delegate
		{
			base.FormClosed -= closed;
			cancellationRegistration.Dispose();
			if (!owner.IsDisposed && !owner.Disposing)
			{
				owner.Enabled = ownerWasEnabled;
				if (ownerWasEnabled)
				{
					owner.Activate();
				}
			}
			completion.TrySetResult();
		};
		base.FormClosed += closed;
		try
		{
			owner.Enabled = false;
			Show(owner);
			Activate();
			cancellationRegistration = cancellationToken.Register(delegate
			{
				try
				{
					if (!base.IsDisposed && base.IsHandleCreated)
					{
						BeginInvoke(delegate
						{
							if (!base.IsDisposed)
							{
								Close();
							}
						});
					}
				}
				catch (InvalidOperationException) when (base.IsDisposed || base.Disposing || !base.IsHandleCreated)
				{
				}
			});
		}
		catch
		{
			base.FormClosed -= closed;
			cancellationRegistration.Dispose();
			if (!owner.IsDisposed && !owner.Disposing)
			{
				owner.Enabled = ownerWasEnabled;
			}
			throw;
		}
		return completion.Task;
	}

	protected override async void OnShown(EventArgs e)
	{
		base.OnShown(e);
		string initializationStep = "CreateWebView";
		try
		{
			await browser.EnsureCoreWebView2Async(environment).WaitAsync(TimeSpan.FromSeconds(15.0), cancellation.Token);
			if (base.IsDisposed || cancellation.IsCancellationRequested)
			{
				return;
			}
			initializationStep = "ConfigureWebView";
			CoreWebView2 core = browser.CoreWebView2;
			core.Settings.AreBrowserAcceleratorKeysEnabled = false;
			core.Settings.AreDefaultContextMenusEnabled = false;
			core.Settings.AreDefaultScriptDialogsEnabled = false;
			core.Settings.AreDevToolsEnabled = false;
			core.Settings.AreHostObjectsAllowed = false;
			core.Settings.IsStatusBarEnabled = false;
			core.Settings.IsZoomControlEnabled = false;
			core.Settings.IsWebMessageEnabled = true;
			browser.ZoomFactor = 1.0;
			string text = Path.Combine(AppContext.BaseDirectory, "Assets");
			if (Directory.Exists(text))
			{
				core.SetVirtualHostNameToFolderMapping("nestg-assets.local", text, CoreWebView2HostResourceAccessKind.DenyCors);
			}
			core.NavigationStarting += delegate(object? _, CoreWebView2NavigationStartingEventArgs args)
			{
				if (viewReady || !IsAllowedDocumentNavigation(args.Uri))
				{
					args.Cancel = true;
				}
			};
			core.NewWindowRequested += delegate(object? _, CoreWebView2NewWindowRequestedEventArgs args)
			{
				args.Handled = true;
			};
			core.WebMessageReceived += HandleMessage;
			TaskCompletionSource ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
			core.DOMContentLoaded += Ready;
			try
			{
				initializationStep = "LoadDocument";
				core.NavigateToString(WarpInstallHtmlBuilder.BuildDocument());
				await ready.Task.WaitAsync(TimeSpan.FromSeconds(10.0), cancellation.Token);
				if (base.IsDisposed || cancellation.IsCancellationRequested)
				{
					return;
				}
				initializationStep = "FocusDocument";
				loading.Visible = false;
				browser.Focus();
				viewReady = true;
				PushView();
				await browser.ExecuteScriptAsync("document.getElementById('defer')?.focus();");
			}
			finally
			{
				core.DOMContentLoaded -= Ready;
			}
			void Ready(object? sender, CoreWebView2DOMContentLoadedEventArgs args)
			{
				ready.TrySetResult();
			}
		}
		catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
		{
		}
		catch (Exception exception)
		{
			if (!base.IsDisposed && !cancellation.IsCancellationRequested)
			{
				DesktopDiagnosticLog.Write("WARP", "InstallWebViewInitializationFailed", "Step=" + initializationStep, exception);
				ShowWebViewFailure();
			}
		}
	}

	internal static bool IsAllowedDocumentNavigation(string? uri)
	{
		if (string.Equals(uri, "about:blank", StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}
		if (string.IsNullOrEmpty(uri) || !uri.StartsWith("data:text/html", StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}
		bool flag = uri.Length == "data:text/html".Length;
		if (!flag)
		{
			char c = uri["data:text/html".Length];
			bool flag2 = ((c == ',' || c == ';') ? true : false);
			flag = flag2;
		}
		return flag;
	}

	internal static bool IsTrustedAction(string? source, string token)
	{
		bool flag = string.Equals(source, "about:blank", StringComparison.OrdinalIgnoreCase);
		if (flag)
		{
			bool flag2;
			switch (token)
			{
			case "install":
			case "recheck":
			case "close":
			case "help":
			case "open":
			case "window:drag":
				flag2 = true;
				break;
			default:
				flag2 = false;
				break;
			}
			flag = flag2;
		}
		return flag;
	}

	private void HandleMessage(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
	{
		if (!viewReady || base.IsDisposed || cancellation.IsCancellationRequested)
		{
			return;
		}
		string text;
		try
		{
			text = e.TryGetWebMessageAsString();
		}
		catch
		{
			return;
		}
		if (!IsTrustedAction(e.Source, text))
		{
			return;
		}
		switch (text)
		{
		case "window:drag":
			BeginInvoke(delegate
			{
				if (!base.IsDisposed)
				{
					ReleaseCapture();
					SendMessage(base.Handle, 161, 2, IntPtr.Zero);
				}
			});
			break;
		case "close":
			BeginInvoke(delegate
			{
				if (!base.IsDisposed)
				{
					Close();
				}
			});
			break;
		case "help":
			OpenUri("https://developers.cloudflare.com/warp-client/get-started/windows/");
			break;
		case "open":
			if (needsSetup && !busy)
			{
				OpenCloudflareApp();
			}
			break;
		case "recheck":
			if (needsSetup && !busy)
			{
				RecheckAsync();
			}
			break;
		case "install":
			if (!needsSetup && !busy && primaryEnabled)
			{
				StartInstallAsync();
			}
			break;
		}
	}

	private void PushView()
	{
		if (!viewReady || base.IsDisposed || browser.CoreWebView2 == null)
		{
			return;
		}
		try
		{
			browser.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(new
			{
				stage = stage,
				title = statusTitle,
				message = statusMessage,
				percent = percent,
				showProgress = showProgress,
				busy = busy,
				canClose = (!busy || !installerStarted),
				closeLabel = closeLabel,
				primaryAction = (needsSetup ? "recheck" : "install"),
				primaryLabel = primaryLabel,
				primaryEnabled = (!busy && primaryEnabled),
				showCloudflare = needsSetup
			}));
		}
		catch (Exception exception)
		{
			DesktopDiagnosticLog.Write("WARP", "InstallWebViewUpdateFailed", null, exception);
			ShowWebViewFailure();
		}
	}

	private void ShowWebViewFailure()
	{
		webViewFailed = true;
		viewReady = false;
		browser.Visible = false;
		loading.Visible = true;
		loading.Text = ((busy && installerStarted) ? "Bộ cài Cloudflare đang chạy.\nHãy hoàn tất hoặc hủy trong cửa sổ bộ cài." : "Không mở được cửa sổ cài WARP.\nĐóng và thử lại.");
		if (!base.Controls.OfType<Button>().Any())
		{
			Button button = new Button
			{
				Text = "Đóng",
				Dock = DockStyle.Bottom,
				Height = 44,
				BackColor = Color.FromArgb(24, 41, 58),
				ForeColor = Color.White,
				FlatStyle = FlatStyle.Flat
			};
			button.Click += delegate
			{
				Close();
			};
			button.Enabled = !busy || !installerStarted;
			base.Controls.Add(button);
			button.BringToFront();
			base.CancelButton = button;
			FitToScreen();
			CenterToParent();
			button.Focus();
		}
	}

	private async Task StartInstallAsync()
	{
		if (busy)
		{
			return;
		}
		busy = true;
		installerStarted = false;
		Result = null;
		needsSetup = false;
		stage = "downloading";
		statusTitle = "Đang chuẩn bị";
		statusMessage = "Đang chuẩn bị tải Cloudflare WARP…";
		percent = null;
		showProgress = true;
		closeLabel = "Hủy tải";
		PushView();
		try
		{
			DialogProgress progress = new DialogProgress(this, delegate(WarpInstallProgress update)
			{
				if (!base.IsDisposed && busy)
				{
					stage = update.Phase switch
					{
						WarpInstallPhase.Verifying => "verifying", 
						WarpInstallPhase.Installing => "installing", 
						WarpInstallPhase.Checking => "checking", 
						_ => "downloading", 
					};
					statusTitle = update.Phase switch
					{
						WarpInstallPhase.Verifying => "Đang xác minh bộ cài", 
						WarpInstallPhase.Installing => "Bộ cài Cloudflare đang mở", 
						WarpInstallPhase.Checking => "Đang kiểm tra WARP", 
						_ => "Đang tải từ Cloudflare", 
					};
					statusMessage = update.Message;
					WarpInstallPhase phase = update.Phase;
					if ((uint)(phase - 2) <= 1u)
					{
						installerStarted = true;
						closeLabel = ((update.Phase == WarpInstallPhase.Installing) ? "Hủy trong bộ cài" : "Đang kiểm tra");
					}
					int? num = update.Percent;
					if (num.HasValue)
					{
						int valueOrDefault = num.GetValueOrDefault();
						percent = Math.Clamp(valueOrDefault, 0, 100);
					}
					PushView();
				}
			});
			Result = await installer.InstallAsync(progress, cancellation.Token);
			stage = Result.Outcome switch
			{
				WarpInstallOutcome.Ready => "ready", 
				WarpInstallOutcome.NeedsCloudflareSetup => "needsSetup", 
				WarpInstallOutcome.NeedsRestart => "needsRestart", 
				WarpInstallOutcome.Cancelled => "cancelled", 
				WarpInstallOutcome.Failed => "failed", 
				_ => "idle", 
			};
			statusTitle = Result.Outcome switch
			{
				WarpInstallOutcome.Ready => "WARP đã sẵn sàng", 
				WarpInstallOutcome.NeedsCloudflareSetup => "Cần hoàn tất Cloudflare", 
				WarpInstallOutcome.NeedsRestart => "Cần khởi động lại Windows", 
				WarpInstallOutcome.Cancelled => "Đã hủy cài đặt", 
				WarpInstallOutcome.Failed => "Chưa cài được WARP", 
				_ => "WARP đã có trên máy", 
			};
			statusMessage = Result.Message;
			needsSetup = Result.Outcome == WarpInstallOutcome.NeedsCloudflareSetup;
			primaryLabel = Result.Outcome switch
			{
				WarpInstallOutcome.Ready => "WARP đã sẵn sàng", 
				WarpInstallOutcome.NeedsCloudflareSetup => "Kiểm tra lại", 
				WarpInstallOutcome.NeedsRestart => "Cần khởi động lại", 
				WarpInstallOutcome.AlreadyInstalled => "Đã có WARP", 
				_ => "Thử lại", 
			};
			WarpInstallOutcome outcome = Result.Outcome;
			bool flag = ((outcome == WarpInstallOutcome.NeedsCloudflareSetup || (uint)(outcome - 3) <= 1u) ? true : false);
			primaryEnabled = flag;
			outcome = Result.Outcome;
			flag = (uint)outcome <= 1u;
			showProgress = flag;
			percent = (showProgress ? new int?(100) : ((int?)null));
			closeLabel = "Đóng";
		}
		catch
		{
			Result = new WarpInstallResult(WarpInstallOutcome.Failed, "Không thể hoàn tất luồng cài Cloudflare WARP. Hãy thử lại.");
			stage = "failed";
			statusTitle = "Chưa cài được WARP";
			statusMessage = Result.Message;
			showProgress = false;
			primaryLabel = "Thử lại";
			primaryEnabled = true;
			closeLabel = "Đóng";
		}
		finally
		{
			busy = false;
			PushView();
			if (!viewReady)
			{
				foreach (Button item in base.Controls.OfType<Button>())
				{
					item.Enabled = true;
				}
			}
			if (closeRequested)
			{
				Close();
			}
		}
	}

	private async Task RecheckAsync()
	{
		if (busy)
		{
			return;
		}
		busy = true;
		stage = "checking";
		statusTitle = "Đang kiểm tra WARP";
		statusMessage = "Đang kiểm tra Cloudflare WARP…";
		showProgress = true;
		percent = null;
		PushView();
		try
		{
			WarpConnectionStatus warpConnectionStatus = await client.GetStatusAsync(cancellation.Token);
			if (warpConnectionStatus.CanToggle)
			{
				Result = new WarpInstallResult(WarpInstallOutcome.Ready, "WARP đã sẵn sàng. Bấm công tắc NestG để bật kết nối.");
				stage = "ready";
				statusTitle = "WARP đã sẵn sàng";
				statusMessage = Result.Message;
				needsSetup = false;
				primaryLabel = "WARP đã sẵn sàng";
				primaryEnabled = false;
				percent = 100;
			}
			else if (warpConnectionStatus.Kind == WarpConnectionKind.NotInstalled)
			{
				Result = null;
				needsSetup = false;
				stage = "idle";
				statusTitle = "WARP chưa có trên máy";
				statusMessage = warpConnectionStatus.Detail;
				primaryLabel = "Tải và cài WARP";
				primaryEnabled = true;
				showProgress = false;
			}
			else
			{
				stage = "needsSetup";
				statusTitle = "Cần hoàn tất Cloudflare";
				statusMessage = warpConnectionStatus.Detail;
				primaryEnabled = true;
				showProgress = false;
			}
		}
		catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
		{
			Result = null;
		}
		catch
		{
			stage = "needsSetup";
			statusTitle = "Chưa kiểm tra được WARP";
			statusMessage = "Không thể kiểm tra Cloudflare lúc này. Hãy thử lại.";
			primaryEnabled = true;
			showProgress = false;
		}
		finally
		{
			busy = false;
			closeLabel = "Đóng";
			PushView();
			if (!viewReady)
			{
				foreach (Button item in base.Controls.OfType<Button>())
				{
					item.Enabled = true;
				}
			}
			if (closeRequested)
			{
				Close();
			}
		}
	}

	private void OpenCloudflareApp()
	{
		string text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Cloudflare", "Cloudflare WARP", "Cloudflare WARP.exe");
		if (!File.Exists(text))
		{
			statusMessage = "Không tìm thấy ứng dụng Cloudflare. Hãy dùng trang cài đặt chính thức.";
			PushView();
			return;
		}
		try
		{
			Process.Start(new ProcessStartInfo(text)
			{
				UseShellExecute = true
			});
		}
		catch
		{
			statusMessage = "Không thể mở ứng dụng Cloudflare lúc này.";
			PushView();
		}
	}

	private void OpenUri(string url)
	{
		try
		{
			Process.Start(new ProcessStartInfo(url)
			{
				UseShellExecute = true
			});
		}
		catch
		{
			statusMessage = "Không thể mở trang Cloudflare lúc này.";
			PushView();
		}
	}

	protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
	{
		if (keyData == Keys.Escape)
		{
			Close();
			return true;
		}
		return base.ProcessCmdKey(ref msg, keyData);
	}

	protected override void OnFormClosing(FormClosingEventArgs e)
	{
		if (busy)
		{
			e.Cancel = true;
			if (!installerStarted)
			{
				closeRequested = true;
				cancellation.Cancel();
				statusMessage = "Đang hủy tải bộ cài…";
			}
			else
			{
				statusMessage = "Bộ cài Cloudflare đang chạy. Hãy hoàn tất hoặc hủy trong cửa sổ bộ cài.";
				if (!viewReady)
				{
					loading.Text = statusMessage;
				}
			}
			PushView();
		}
		else
		{
			cancellation.Cancel();
			base.OnFormClosing(e);
		}
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing && !cancellation.IsCancellationRequested)
		{
			cancellation.Cancel();
		}
		base.Dispose(disposing);
		if (disposing)
		{
			cancellation.Dispose();
		}
	}

	[DllImport("user32.dll")]
	private static extern uint GetDpiForWindow(nint hWnd);

	[DllImport("user32.dll")]
	private static extern bool ReleaseCapture();

	[DllImport("user32.dll")]
	private static extern nint SendMessage(nint hWnd, int message, nint wParam, nint lParam);
}
