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

namespace app_desktop.Services.WindowsUpdate;

internal sealed class WubSetupDialog : Form
{
	private readonly WubPackageStore package;

	private readonly IWubPackageProvisioner downloadService;

	private readonly CoreWebView2Environment? environment;

	private readonly bool autoDownload;

	private readonly bool autoCloseOnReady;

	private readonly CancellationTokenSource lifetime = new CancellationTokenSource();

	private CancellationTokenSource? currentDownload;

	private readonly WebView2 browser = new WebView2();

	private readonly Label loading;

	private bool viewReady;

	private bool webViewFailed;

	private bool busy;

	private bool downloading;

	private bool closeRequested;

	private int? percent;

	private string stage = "missing";

	private string statusTitle = "WUB chưa được nhập";

	private string statusMessage = "NestG sẽ tải và kiểm tra WUB v1.8 từ Sordum.";

	internal bool Imported { get; private set; }

	internal WubSetupDialog(WubPackageStore package, CoreWebView2Environment? environment = null, IWubPackageProvisioner? downloadService = null, bool autoDownload = true, bool autoCloseOnReady = false)
	{
		this.package = package;
		this.environment = environment;
		this.downloadService = downloadService ?? new WubPackageDownloadService(package);
		this.autoDownload = autoDownload;
		this.autoCloseOnReady = autoCloseOnReady;
		Text = "NestG — Windows Update Blocker";
		AppIconProvider.ApplyIcon(this);
		base.AutoScaleMode = AutoScaleMode.None;
		BackColor = Color.FromArgb(13, 20, 30);
		base.ClientSize = new Size(620, 500);
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
			Text = "Đang mở cửa sổ WUB…",
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
		return new Size(Math.Min((int)Math.Round(620.0 * num), Math.Max(1, workingArea.Width - num2)), Math.Min((int)Math.Round(500.0 * num), Math.Max(1, workingArea.Height - num2)));
	}

	internal static Size CalculateFailureClientSize(Size workingArea, uint dpi)
	{
		double num = (double)((dpi == 0) ? 96u : dpi) / 96.0;
		int num2 = (int)Math.Round(24.0 * num);
		return new Size(Math.Min((int)Math.Round(460.0 * num), Math.Max(1, workingArea.Width - num2)), Math.Min((int)Math.Round(190.0 * num), Math.Max(1, workingArea.Height - num2)));
	}

	private void FitToScreen()
	{
		Size size = Screen.FromHandle(base.Handle).WorkingArea.Size;
		uint dpiForWindow = GetDpiForWindow(base.Handle);
		base.ClientSize = (webViewFailed ? CalculateFailureClientSize(size, dpiForWindow) : CalculateClientSize(size, dpiForWindow));
	}

	internal Task ShowAsync(Form owner, CancellationToken cancellationToken = default(CancellationToken))
	{
		ArgumentNullException.ThrowIfNull(owner, "owner");
		cancellationToken.ThrowIfCancellationRequested();
		if (base.IsDisposed)
		{
			throw new ObjectDisposedException("WubSetupDialog");
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
								if (busy)
								{
									closeRequested = true;
									currentDownload?.Cancel();
								}
								else
								{
									Close();
								}
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
			await browser.EnsureCoreWebView2Async(environment).WaitAsync(TimeSpan.FromSeconds(15.0));
			if (base.IsDisposed)
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
				core.NavigateToString(WubSetupHtmlBuilder.BuildDocument());
				await ready.Task.WaitAsync(TimeSpan.FromSeconds(10.0));
				if (base.IsDisposed)
				{
					return;
				}
				initializationStep = "FocusDocument";
				loading.Visible = false;
				browser.Focus();
				viewReady = true;
				PushView();
				await browser.ExecuteScriptAsync("document.getElementById('defer')?.focus();");
				if (autoDownload && !Imported)
				{
					DownloadAsync();
				}
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
		catch (Exception exception)
		{
			if (!base.IsDisposed)
			{
				DesktopDiagnosticLog.Write("WUB", "SetupWebViewInitializationFailed", "Step=" + initializationStep, exception);
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
			case "cancel":
			case "choose":
			case "download":
			case "official":
			case "close":
			case "license":
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
		if (!viewReady || base.IsDisposed)
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
		if (!IsTrustedAction(e.Source, text) || text == null)
		{
			return;
		}
		switch (text.Length)
		{
		case 8:
			switch (text[0])
			{
			case 'o':
				if (text == "official" && !busy)
				{
					OpenOfficialPage("https://www.sordum.org/9470/windows-update-blocker-v1-8/");
				}
				break;
			case 'd':
				if (text == "download" && !busy && !Imported)
				{
					DownloadAsync();
				}
				break;
			}
			break;
		case 6:
			switch (text[1])
			{
			case 'h':
				if (text == "choose" && !busy && !Imported)
				{
					ChooseArchiveAsync();
				}
				break;
			case 'a':
				if (text == "cancel" && busy && downloading)
				{
					currentDownload?.Cancel();
				}
				break;
			}
			break;
		case 11:
			if (!(text == "window:drag") || busy)
			{
				break;
			}
			BeginInvoke(delegate
			{
				if (!base.IsDisposed)
				{
					ReleaseCapture();
					SendMessage(base.Handle, 161, 2, IntPtr.Zero);
				}
			});
			break;
		case 5:
			if (text == "close" && !busy)
			{
				Close();
			}
			break;
		case 7:
			if (text == "license" && !busy)
			{
				OpenOfficialPage("https://www.sordum.org/eula/");
			}
			break;
		case 9:
		case 10:
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
				busy = busy,
				canClose = !busy,
				canChoose = (!busy && !Imported),
				canDownload = (!busy && !Imported),
				canCancel = (busy && downloading),
				percent = percent,
				closeLabel = (Imported ? "Đóng" : "Để sau")
			}));
		}
		catch (Exception exception)
		{
			DesktopDiagnosticLog.Write("WUB", "SetupWebViewUpdateFailed", null, exception);
			ShowWebViewFailure();
		}
	}

	private void ShowWebViewFailure()
	{
		webViewFailed = true;
		viewReady = false;
		browser.Visible = false;
		loading.Visible = true;
		loading.Text = (busy ? "Đang tải hoặc xác minh gói WUB…" : "Không mở được cửa sổ WUB.\nĐóng và thử lại.");
		if (!base.Controls.OfType<Button>().Any())
		{
			Button button = new Button
			{
				Text = "Đóng",
				Dock = DockStyle.Bottom,
				Height = 44,
				BackColor = Color.FromArgb(24, 41, 58),
				ForeColor = Color.White,
				FlatStyle = FlatStyle.Flat,
				Enabled = !busy
			};
			button.Click += delegate
			{
				Close();
			};
			base.Controls.Add(button);
			button.BringToFront();
			base.CancelButton = button;
			FitToScreen();
			CenterToParent();
			button.Focus();
		}
	}

	private async Task ChooseArchiveAsync()
	{
		if (busy || Imported)
		{
			return;
		}
		using OpenFileDialog picker = new OpenFileDialog
		{
			Title = "Chọn gói Windows Update Blocker v1.8 tải từ Sordum",
			Filter = "Gói ZIP (*.zip)|*.zip",
			CheckFileExists = true,
			Multiselect = false
		};
		if (picker.ShowDialog(this) != DialogResult.OK)
		{
			PushView();
			return;
		}
		string archivePath = picker.FileName;
		busy = true;
		percent = null;
		stage = "verifying";
		statusTitle = "Đang xác minh gói WUB";
		statusMessage = "NestG đang kiểm tra ZIP và các file WUB v1.8 trước khi nhập.";
		PushView();
		try
		{
			await Task.Run(delegate
			{
				package.Import(archivePath);
			});
			if (!base.IsDisposed)
			{
				Imported = true;
				stage = "ready";
				statusTitle = "WUB đã sẵn sàng";
				statusMessage = "NestG sẽ tiếp tục đổi trạng thái Windows Update bằng WUB.";
			}
		}
		catch (Exception ex) when (((ex is IOException || ex is UnauthorizedAccessException || ex is InvalidDataException) ? 1 : 0) != 0)
		{
			if (!base.IsDisposed)
			{
				stage = "failed";
				statusTitle = "Chưa nhập được WUB";
				statusMessage = ((ex is InvalidDataException) ? ex.Message : "Không thể nhập gói WUB. Kiểm tra quyền truy cập rồi thử lại.");
			}
		}
		catch (Exception exception)
		{
			if (!base.IsDisposed)
			{
				DesktopDiagnosticLog.Write("WUB", "SetupImportFailed", null, exception);
				stage = "failed";
				statusTitle = "Chưa nhập được WUB";
				statusMessage = "Không thể nhập gói WUB lúc này. Hãy thử lại.";
			}
		}
		finally
		{
			busy = false;
			if (!base.IsDisposed)
			{
				if (closeRequested || (autoCloseOnReady && Imported))
				{
					Close();
				}
				else
				{
					foreach (Button item in base.Controls.OfType<Button>())
					{
						item.Enabled = true;
					}
					PushView();
				}
			}
		}
	}

	private async Task DownloadAsync()
	{
		if (busy || Imported || lifetime.IsCancellationRequested)
		{
			return;
		}
		currentDownload = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
		busy = true;
		downloading = true;
		percent = null;
		stage = "downloading";
		statusTitle = "Đang tải WUB v1.8";
		statusMessage = "NestG đang tải ZIP từ Sordum. Bạn có thể hủy tải hoặc dùng ZIP đã tải trước đó.";
		PushView();
		try
		{
			Progress<WubPackageProgress> progress = new Progress<WubPackageProgress>(delegate(WubPackageProgress update)
			{
				if (!base.IsDisposed && busy)
				{
					downloading = update.Phase == WubPackagePhase.Downloading;
					stage = (downloading ? "downloading" : "verifying");
					statusTitle = (downloading ? "Đang tải WUB v1.8" : "Đang xác minh gói WUB");
					statusMessage = update.Message;
					percent = update.Percent;
					PushView();
				}
			});
			WubPackageResult wubPackageResult = await downloadService.DownloadAndImportAsync(progress, currentDownload.Token);
			if (!base.IsDisposed)
			{
				WubPackageOutcome outcome = wubPackageResult.Outcome;
				bool imported = (uint)outcome <= 1u;
				Imported = imported;
				string text;
				switch (wubPackageResult.Outcome)
				{
				case WubPackageOutcome.Ready:
				case WubPackageOutcome.AlreadyReady:
					text = "ready";
					break;
				case WubPackageOutcome.Cancelled:
					text = "missing";
					break;
				default:
					text = "failed";
					break;
				}
				stage = text;
				switch (wubPackageResult.Outcome)
				{
				case WubPackageOutcome.Ready:
				case WubPackageOutcome.AlreadyReady:
					text = "WUB đã sẵn sàng";
					break;
				case WubPackageOutcome.Cancelled:
					text = "Đã hủy tải WUB";
					break;
				default:
					text = "Chưa tải được WUB";
					break;
				}
				statusTitle = text;
				statusMessage = (Imported ? "NestG sẽ tiếp tục đổi trạng thái Windows Update bằng WUB." : wubPackageResult.Message);
			}
		}
		catch (Exception exception)
		{
			if (!base.IsDisposed)
			{
				DesktopDiagnosticLog.Write("WUB", "SetupDownloadFailed", null, exception);
				stage = "failed";
				statusTitle = "Chưa tải được WUB";
				statusMessage = "Không tải được WUB lúc này. Hãy thử lại hoặc chọn ZIP v1.8 từ Sordum.";
			}
		}
		finally
		{
			currentDownload?.Dispose();
			currentDownload = null;
			busy = false;
			downloading = false;
			percent = null;
			if (!base.IsDisposed)
			{
				if (closeRequested || (autoCloseOnReady && Imported))
				{
					Close();
				}
				else
				{
					PushView();
				}
			}
		}
	}

	private void OpenOfficialPage(string url)
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
			stage = "failed";
			statusTitle = "Không mở được trình duyệt";
			statusMessage = "Hãy truy cập trang Sordum bằng trình duyệt của bạn.";
			PushView();
		}
	}

	protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
	{
		if (keyData == Keys.Escape)
		{
			if (!busy)
			{
				Close();
			}
			return true;
		}
		return base.ProcessCmdKey(ref msg, keyData);
	}

	protected override void OnFormClosing(FormClosingEventArgs e)
	{
		if (busy)
		{
			closeRequested = true;
			currentDownload?.Cancel();
			e.Cancel = true;
		}
		else
		{
			lifetime.Cancel();
			base.OnFormClosing(e);
		}
	}

	protected override void OnFormClosed(FormClosedEventArgs e)
	{
		lifetime.Dispose();
		base.OnFormClosed(e);
	}

	[DllImport("user32.dll")]
	private static extern uint GetDpiForWindow(nint hWnd);

	[DllImport("user32.dll")]
	private static extern bool ReleaseCapture();

	[DllImport("user32.dll")]
	private static extern nint SendMessage(nint hWnd, int message, nint wParam, nint lParam);
}
