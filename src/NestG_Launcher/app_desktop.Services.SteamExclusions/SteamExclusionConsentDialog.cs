using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using UI_Desktop;

namespace app_desktop.Services.SteamExclusions;

internal sealed class SteamExclusionConsentDialog : Form
{
	private readonly WebView2 browser = new WebView2();

	private readonly CancellationTokenSource lifetime = new CancellationTokenSource();

	private readonly string document;

	private readonly Label loading;

	private bool responding;

	internal SteamExclusionConsentDialog(IReadOnlyList<string> paths, bool? enable = null)
	{
		document = SteamExclusionConsentHtmlBuilder.BuildDocument(paths, enable != false);
		Text = "NestG — Ngoại lệ game";
		AppIconProvider.ApplyIcon(this);
		base.AutoScaleMode = AutoScaleMode.None;
		BackColor = Color.FromArgb(11, 19, 29);
		base.ClientSize = new Size(620, 580);
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
			Text = "Đang mở…",
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
		return new Size(Math.Min((int)Math.Round(620.0 * num), Math.Max(1, workingArea.Width - num2)), Math.Min((int)Math.Round(580.0 * num), Math.Max(1, workingArea.Height - num2)));
	}

	private void FitToScreen()
	{
		base.ClientSize = CalculateClientSize(Screen.FromHandle(base.Handle).WorkingArea.Size, GetDpiForWindow(base.Handle));
	}

	internal Task<DialogResult> ShowAsync(Form owner, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(owner, "owner");
		cancellationToken.ThrowIfCancellationRequested();
		if (base.IsDisposed)
		{
			throw new ObjectDisposedException("SteamExclusionConsentDialog");
		}
		TaskCompletionSource<DialogResult> completion = new TaskCompletionSource<DialogResult>(TaskCreationOptions.RunContinuationsAsynchronously);
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
			completion.TrySetResult((base.DialogResult == DialogResult.None) ? DialogResult.No : base.DialogResult);
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
		try
		{
			await browser.EnsureCoreWebView2Async().WaitAsync(TimeSpan.FromSeconds(15.0), lifetime.Token);
			if (base.IsDisposed || lifetime.IsCancellationRequested)
			{
				return;
			}
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
				if (!IsAllowedDocumentNavigation(args.Uri))
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
				core.NavigateToString(document);
				await ready.Task.WaitAsync(TimeSpan.FromSeconds(10.0), lifetime.Token);
				loading.Visible = false;
				browser.Focus();
				await browser.ExecuteScriptAsync("document.getElementById('decline')?.focus();");
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
		catch (OperationCanceledException) when (lifetime.IsCancellationRequested)
		{
		}
		catch (Exception exception)
		{
			if (!base.IsDisposed && !lifetime.IsCancellationRequested)
			{
				DesktopDiagnosticLog.Write("SteamExclusionConsent", "WebViewInitializationFailed", null, exception);
				browser.Visible = false;
				loading.Visible = true;
				loading.Text = "Không mở được cửa sổ xác nhận.\nĐóng và thử lại.";
				Button button = new Button
				{
					Text = "Đóng",
					Dock = DockStyle.Bottom,
					Height = 44,
					DialogResult = DialogResult.No,
					BackColor = Color.FromArgb(24, 41, 58),
					ForeColor = Color.White,
					FlatStyle = FlatStyle.Flat
				};
				base.Controls.Add(button);
				button.BringToFront();
				base.AcceptButton = button;
				base.CancelButton = button;
				button.Focus();
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

	internal static DialogResult? ResolveResponse(string source, string token)
	{
		if (!string.Equals(source, "about:blank", StringComparison.OrdinalIgnoreCase))
		{
			return null;
		}
		DialogResult? result;
		switch (token)
		{
		case "yes":
			result = DialogResult.Yes;
			break;
		case "no":
		case "window:close":
			result = DialogResult.No;
			break;
		default:
			result = null;
			break;
		}
		return result;
	}

	private void HandleMessage(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
	{
		if (responding || lifetime.IsCancellationRequested)
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
		if (!string.Equals(e.Source, "about:blank", StringComparison.OrdinalIgnoreCase))
		{
			return;
		}
		if (text == "window:drag")
		{
			BeginInvoke(delegate
			{
				if (!base.IsDisposed)
				{
					ReleaseCapture();
					SendMessage(base.Handle, 161, 2, IntPtr.Zero);
				}
			});
			return;
		}
		DialogResult? dialogResult = ResolveResponse(e.Source, text);
		if (!dialogResult.HasValue)
		{
			return;
		}
		DialogResult result = dialogResult.GetValueOrDefault();
		responding = true;
		BeginInvoke(delegate
		{
			if (!base.IsDisposed)
			{
				base.DialogResult = result;
				Close();
			}
		});
	}

	protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
	{
		if (keyData == Keys.Escape)
		{
			base.DialogResult = DialogResult.No;
			Close();
			return true;
		}
		return base.ProcessCmdKey(ref msg, keyData);
	}

	protected override void OnFormClosing(FormClosingEventArgs e)
	{
		if (base.DialogResult == DialogResult.None)
		{
			base.DialogResult = DialogResult.No;
		}
		lifetime.Cancel();
		base.OnFormClosing(e);
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing && !lifetime.IsCancellationRequested)
		{
			lifetime.Cancel();
		}
		base.Dispose(disposing);
		if (disposing)
		{
			lifetime.Dispose();
		}
	}

	[DllImport("user32.dll")]
	private static extern uint GetDpiForWindow(nint hWnd);

	[DllImport("user32.dll")]
	private static extern bool ReleaseCapture();

	[DllImport("user32.dll")]
	private static extern nint SendMessage(nint hWnd, int message, nint wParam, nint lParam);
}
