using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using app_desktop;
using app_desktop.Models;
using app_desktop.Services;

namespace UI_Desktop.Auth;

internal sealed class LoginWebViewForm : Form
{
	private sealed class LoginWebMessage
	{
		public string Type { get; set; } = string.Empty;

		public string? Email { get; set; }

		public string? Password { get; set; }

		public bool Remember { get; set; }
	}

	private const int WmNcButtonDown = 161;

	private const int WmNcHitTest = 132;

	private const int HtClient = 1;

	private const int HtCaption = 2;

	private const int HtLeft = 10;

	private const int HtRight = 11;

	private const int HtTop = 12;

	private const int HtTopLeft = 13;

	private const int HtTopRight = 14;

	private const int HtBottom = 15;

	private const int HtBottomLeft = 16;

	private const int HtBottomRight = 17;

	private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
	{
		PropertyNameCaseInsensitive = true
	};

	private readonly WebView2 loginWebView = new WebView2();

	private readonly CancellationTokenSource disposalCancellation = new CancellationTokenSource();

	private readonly DeferredWebViewMessageDispatcher webMessageDispatcher;

	private Task? webViewInitializationTask;

	private bool isSubmitting;

	private bool isNavigating;

	public LoginWebViewForm()
	{
		webMessageDispatcher = new DeferredWebViewMessageDispatcher(this, (Exception exception) => SetLoginStateAsync(isBusy: false, DesktopUserMessage.FromException(exception, "Không thể hoàn tất thao tác. Vui lòng thử lại."), isError: true), disposalCancellation.Token);
		InitializeShell();
	}

	protected override async void OnShown(EventArgs e)
	{
		base.OnShown(e);
		await LoadLoginPageWithRecoveryAsync();
	}

	protected override void OnFormClosed(FormClosedEventArgs e)
	{
		disposalCancellation.Cancel();
		disposalCancellation.Dispose();
		loginWebView.Dispose();
		base.OnFormClosed(e);
	}

	private void InitializeShell()
	{
		AuthUiHelper.ApplyWindowIcon(this);
		base.AutoScaleMode = AutoScaleMode.None;
		BackColor = Color.FromArgb(7, 13, 20);
		Rectangle workingArea = Screen.FromPoint(System.Windows.Forms.Cursor.Position).WorkingArea;
		base.ClientSize = new Size(Math.Min(1180, workingArea.Width), Math.Min(740, workingArea.Height));
		MinimumSize = new Size(Math.Min(760, workingArea.Width), Math.Min(620, workingArea.Height));
		base.MaximizedBounds = workingArea;
		base.FormBorderStyle = FormBorderStyle.None;
		base.StartPosition = FormStartPosition.CenterScreen;
		Text = "NestG Launcher";
		loginWebView.Dock = DockStyle.Fill;
		loginWebView.Margin = Padding.Empty;
		loginWebView.DefaultBackgroundColor = Color.FromArgb(7, 13, 20);
		base.Controls.Add(loginWebView);
	}

	private Task EnsureWebViewAsync()
	{
		if (webViewInitializationTask == null)
		{
			webViewInitializationTask = EnsureWebViewCoreAsync();
		}
		return webViewInitializationTask;
	}

	private async Task EnsureWebViewCoreAsync()
	{
		await loginWebView.EnsureCoreWebView2Async();
		if (!loginWebView.IsDisposed && loginWebView.CoreWebView2 != null)
		{
			CoreWebView2Settings settings = loginWebView.CoreWebView2.Settings;
			settings.AreDefaultContextMenusEnabled = false;
			settings.AreDevToolsEnabled = false;
			settings.AreBrowserAcceleratorKeysEnabled = true;
			settings.IsStatusBarEnabled = false;
			settings.IsZoomControlEnabled = false;
			loginWebView.ZoomFactor = 1.0;
			string text = Path.Combine(AppContext.BaseDirectory, "Assets");
			if (Directory.Exists(text))
			{
				loginWebView.CoreWebView2.SetVirtualHostNameToFolderMapping("nestg-assets.local", text, CoreWebView2HostResourceAccessKind.Allow);
			}
			loginWebView.CoreWebView2.WebMessageReceived -= LoginWebView_WebMessageReceived;
			loginWebView.CoreWebView2.WebMessageReceived += LoginWebView_WebMessageReceived;
			loginWebView.CoreWebView2.NavigationStarting -= LoginWebView_NavigationStarting;
			loginWebView.CoreWebView2.NavigationStarting += LoginWebView_NavigationStarting;
		}
	}

	private async Task LoadLoginPageWithRecoveryAsync()
	{
		while (!base.IsDisposed && !disposalCancellation.IsCancellationRequested)
		{
			try
			{
				await EnsureWebViewAsync();
				if (!loginWebView.IsDisposed)
				{
					loginWebView.NavigateToString(LoginWebViewHtmlBuilder.BuildDocument(DesktopAppContext.Settings.RememberSessionByDefault));
				}
				break;
			}
			catch (Exception exception)
			{
				webViewInitializationTask = null;
				if (await LauncherMessageBox.ShowAsync(this, DesktopUserMessage.FromException(exception, "Không thể mở giao diện đăng nhập. Hãy kiểm tra hoặc cài lại Microsoft Edge WebView2 Runtime, sau đó thử lại."), "Không thể mở WebView2", MessageBoxButtons.RetryCancel, MessageBoxIcon.Exclamation) != DialogResult.Retry)
				{
					Close();
					break;
				}
			}
		}
	}

	private void LoginWebView_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
	{
		LoginWebMessage message;
		try
		{
			message = JsonSerializer.Deserialize<LoginWebMessage>(e.WebMessageAsJson, JsonOptions);
		}
		catch
		{
			return;
		}
		if (message != null && !string.IsNullOrWhiteSpace(message.Type))
		{
			webMessageDispatcher.TryDispatch(() => ProcessWebMessageAsync(message));
		}
	}

	private async Task ProcessWebMessageAsync(LoginWebMessage message)
	{
		string type = message.Type;
		if (type == null)
		{
			return;
		}
		switch (type.Length)
		{
		case 8:
			switch (type[1])
			{
			case 'i':
				if (type == "minimize")
				{
					base.WindowState = FormWindowState.Minimized;
				}
				break;
			case 'a':
				if (type == "maximize")
				{
					base.WindowState = ((base.WindowState != FormWindowState.Maximized) ? FormWindowState.Maximized : FormWindowState.Normal);
				}
				break;
			case 'e':
				if (type == "register" && !isSubmitting)
				{
					NavigateTo(new RegisterWebViewForm());
				}
				break;
			}
			break;
		case 5:
			switch (type[0])
			{
			case 'c':
				if (type == "close")
				{
					Close();
				}
				break;
			case 'l':
				if (type == "login")
				{
					await LoginAsync(message);
				}
				break;
			}
			break;
		case 10:
			if (type == "dragWindow")
			{
				BeginWindowDrag();
			}
			break;
		case 14:
			if (type == "forgotPassword" && !isSubmitting)
			{
				NavigateTo(new PasswordRecoveryWebViewForm());
			}
			break;
		}
	}

	private void LoginWebView_NavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
	{
		if (e.IsUserInitiated && !e.Uri.StartsWith("about:blank", StringComparison.OrdinalIgnoreCase) && !e.Uri.StartsWith("data:text/html", StringComparison.OrdinalIgnoreCase))
		{
			e.Cancel = true;
		}
	}

	private async Task LoginAsync(LoginWebMessage message)
	{
		if (isSubmitting || isNavigating)
		{
			return;
		}
		string text = message.Email?.Trim() ?? string.Empty;
		string text2 = message.Password ?? string.Empty;
		if (!string.IsNullOrWhiteSpace(text) && !string.IsNullOrWhiteSpace(text2))
		{
			isSubmitting = true;
			try
			{
				try
				{
					AuthActionResult result = await DesktopAppContext.AuthSessionManager.LoginAsync(text, text2, message.Remember, disposalCancellation.Token);
					if (result.HasSession)
					{
						NavigateTo(DesktopAppContext.CreateStorefrontForm());
						return;
					}
					if (result.RequiresChallenge && result.Challenge != null)
					{
						await SetLoginStateAsync(isBusy: false);
						OtpVerificationDialog dialog = new OtpVerificationDialog(result.Challenge, message.Remember);
						try
						{
							if (await DeferredModalDialog.ShowAsync(this, () => dialog.ShowDialog(this)) == DialogResult.OK && !base.IsDisposed)
							{
								NavigateTo(DesktopAppContext.CreateStorefrontForm());
							}
							return;
						}
						finally
						{
							if (dialog != null)
							{
								((IDisposable)dialog).Dispose();
							}
						}
					}
					await SetLoginStateAsync(isBusy: false, string.IsNullOrWhiteSpace(result.Message) ? "Không thể đăng nhập." : result.Message, isError: true);
				}
				catch (OperationCanceledException) when (disposalCancellation.IsCancellationRequested)
				{
				}
				catch (Exception exception)
				{
					await SetLoginStateAsync(isBusy: false, DesktopUserMessage.FromException(exception, "Không thể đăng nhập lúc này. Vui lòng thử lại."), isError: true);
				}
				return;
			}
			finally
			{
				isSubmitting = false;
			}
		}
		await SetLoginStateAsync(isBusy: false, "Vui lòng nhập email và mật khẩu.", isError: true);
	}

	private void NavigateTo(Form next)
	{
		if (isNavigating)
		{
			next.Dispose();
			return;
		}
		isNavigating = true;
		AuthUiHelper.Navigate(this, next);
	}

	private Task SetLoginStateAsync(bool isBusy, string message = "", bool isError = false)
	{
		string value = JsonSerializer.Serialize(message, JsonOptions);
		string value2 = (isBusy ? "true" : "false");
		string value3 = (isError ? "true" : "false");
		return ExecuteScriptAsync($"window.__nestgLogin?.setState({value2}, {value}, {value3});");
	}

	private async Task ExecuteScriptAsync(string script)
	{
		try
		{
			if (!loginWebView.IsDisposed && loginWebView.CoreWebView2 != null)
			{
				await loginWebView.CoreWebView2.ExecuteScriptAsync(script);
			}
		}
		catch (ObjectDisposedException)
		{
		}
		catch (InvalidOperationException)
		{
		}
	}

	protected override void WndProc(ref Message message)
	{
		base.WndProc(ref message);
		if (message.Msg == 132 && base.WindowState == FormWindowState.Normal && ((IntPtr)message.Result).ToInt32() == 1)
		{
			long num = ((IntPtr)message.LParam).ToInt64();
			Point p = new Point((short)(num & 0xFFFF), (short)((num >> 16) & 0xFFFF));
			Point point = PointToClient(p);
			int num2 = Math.Max(6, 8 * base.DeviceDpi / 96);
			bool num3 = point.X <= num2;
			bool flag = point.X >= base.ClientSize.Width - num2;
			bool flag2 = point.Y <= num2;
			bool flag3 = point.Y >= base.ClientSize.Height - num2;
			nint result = (num3 ? (flag2 ? 13 : ((!flag3) ? 10 : 16)) : (flag ? (flag2 ? 14 : ((!flag3) ? 11 : 17)) : (flag2 ? 12 : ((!flag3) ? message.Result : 15))));
			message.Result = result;
		}
	}

	private void BeginWindowDrag()
	{
		if (base.WindowState == FormWindowState.Maximized)
		{
			base.WindowState = FormWindowState.Normal;
		}
		ReleaseCapture();
		SendMessage(base.Handle, 161, 2, 0);
	}

	[DllImport("user32.dll")]
	private static extern bool ReleaseCapture();

	[DllImport("user32.dll")]
	private static extern nint SendMessage(nint hWnd, int message, int wParam, int lParam);
}
