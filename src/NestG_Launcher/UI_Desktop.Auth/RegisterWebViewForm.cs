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

internal sealed class RegisterWebViewForm : Form
{
	private sealed class RegisterWebMessage
	{
		public string Type { get; set; } = string.Empty;

		public string? DisplayName { get; set; }

		public string? Phone { get; set; }

		public string? Email { get; set; }

		public string? Password { get; set; }

		public bool Remember { get; set; }
	}

	private const int WmNcButtonDown = 161;

	private const int HtCaption = 2;

	private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
	{
		PropertyNameCaseInsensitive = true
	};

	private readonly WebView2 registerWebView = new WebView2();

	private readonly CancellationTokenSource disposalCancellation = new CancellationTokenSource();

	private readonly DeferredWebViewMessageDispatcher webMessageDispatcher;

	private Task? webViewInitializationTask;

	private bool isSubmitting;

	private bool isNavigating;

	public RegisterWebViewForm()
	{
		webMessageDispatcher = new DeferredWebViewMessageDispatcher(this, (Exception exception) => SetRegisterStateAsync(isBusy: false, DesktopUserMessage.FromException(exception, "Không thể hoàn tất thao tác. Vui lòng thử lại."), isError: true), disposalCancellation.Token);
		InitializeShell();
	}

	protected override async void OnShown(EventArgs e)
	{
		base.OnShown(e);
		try
		{
			await EnsureWebViewAsync();
			if (!registerWebView.IsDisposed)
			{
				registerWebView.NavigateToString(RegisterWebViewHtmlBuilder.BuildDocument(DesktopAppContext.Settings.RememberSessionByDefault));
			}
		}
		catch (Exception ex)
		{
			await LauncherMessageBox.ShowAsync(this, "Không thể khởi tạo giao diện tạo tài khoản.\n\n" + ex.Message, "Lỗi WebView2", MessageBoxButtons.OK, MessageBoxIcon.Hand);
		}
	}

	protected override void OnFormClosed(FormClosedEventArgs e)
	{
		disposalCancellation.Cancel();
		disposalCancellation.Dispose();
		registerWebView.Dispose();
		base.OnFormClosed(e);
	}

	private void InitializeShell()
	{
		AuthUiHelper.ApplyWindowIcon(this);
		base.AutoScaleMode = AutoScaleMode.None;
		BackColor = Color.FromArgb(7, 13, 20);
		base.ClientSize = new Size(1180, 740);
		MinimumSize = new Size(760, 620);
		base.FormBorderStyle = FormBorderStyle.None;
		base.StartPosition = FormStartPosition.CenterScreen;
		Text = "NestG Launcher";
		registerWebView.Dock = DockStyle.Fill;
		registerWebView.Margin = Padding.Empty;
		registerWebView.DefaultBackgroundColor = Color.FromArgb(7, 13, 20);
		base.Controls.Add(registerWebView);
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
		await registerWebView.EnsureCoreWebView2Async();
		if (!registerWebView.IsDisposed && registerWebView.CoreWebView2 != null)
		{
			CoreWebView2Settings settings = registerWebView.CoreWebView2.Settings;
			settings.AreDefaultContextMenusEnabled = false;
			settings.AreDevToolsEnabled = false;
			settings.AreBrowserAcceleratorKeysEnabled = false;
			settings.IsStatusBarEnabled = false;
			settings.IsZoomControlEnabled = false;
			string text = Path.Combine(AppContext.BaseDirectory, "Assets");
			if (Directory.Exists(text))
			{
				registerWebView.CoreWebView2.SetVirtualHostNameToFolderMapping("nestg-assets.local", text, CoreWebView2HostResourceAccessKind.Allow);
			}
			registerWebView.CoreWebView2.WebMessageReceived -= RegisterWebView_WebMessageReceived;
			registerWebView.CoreWebView2.WebMessageReceived += RegisterWebView_WebMessageReceived;
			registerWebView.CoreWebView2.NavigationStarting -= RegisterWebView_NavigationStarting;
			registerWebView.CoreWebView2.NavigationStarting += RegisterWebView_NavigationStarting;
		}
	}

	private void RegisterWebView_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
	{
		RegisterWebMessage message;
		try
		{
			message = JsonSerializer.Deserialize<RegisterWebMessage>(e.WebMessageAsJson, JsonOptions);
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

	private async Task ProcessWebMessageAsync(RegisterWebMessage message)
	{
		switch (message.Type)
		{
		case "dragWindow":
			BeginWindowDrag();
			break;
		case "minimize":
			base.WindowState = FormWindowState.Minimized;
			break;
		case "maximize":
			base.WindowState = ((base.WindowState != FormWindowState.Maximized) ? FormWindowState.Maximized : FormWindowState.Normal);
			break;
		case "close":
			Close();
			break;
		case "login":
			if (!isSubmitting)
			{
				NavigateTo(DesktopAppContext.CreateLoginForm());
			}
			break;
		case "register":
			await RegisterAsync(message);
			break;
		}
	}

	private void RegisterWebView_NavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
	{
		if (e.IsUserInitiated && !e.Uri.StartsWith("about:blank", StringComparison.OrdinalIgnoreCase) && !e.Uri.StartsWith("data:text/html", StringComparison.OrdinalIgnoreCase))
		{
			e.Cancel = true;
		}
	}

	private async Task RegisterAsync(RegisterWebMessage message)
	{
		if (isSubmitting || isNavigating)
		{
			return;
		}
		string text = message.DisplayName?.Trim() ?? string.Empty;
		string phone = NormalizePhone(message.Phone);
		string text2 = message.Email?.Trim() ?? string.Empty;
		string text3 = message.Password ?? string.Empty;
		if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(text2) || string.IsNullOrWhiteSpace(text3))
		{
			await SetRegisterStateAsync(isBusy: false, "Vui lòng nhập tên hiển thị, email và mật khẩu.", isError: true);
			return;
		}
		if (text3.Length >= 6)
		{
			isSubmitting = true;
			try
			{
				try
				{
					AuthActionResult result = await DesktopAppContext.AuthSessionManager.RegisterAsync(text, phone, text2, text3, message.Remember, disposalCancellation.Token);
					if (result.HasSession)
					{
						NavigateTo(DesktopAppContext.CreateStorefrontForm());
						return;
					}
					if (result.RequiresChallenge && result.Challenge != null)
					{
						await SetRegisterStateAsync(isBusy: false);
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
					await SetRegisterStateAsync(isBusy: false, string.IsNullOrWhiteSpace(result.Message) ? "Không thể tạo tài khoản." : result.Message, isError: true);
				}
				catch (OperationCanceledException) when (disposalCancellation.IsCancellationRequested)
				{
				}
				catch (Exception ex2)
				{
					await SetRegisterStateAsync(isBusy: false, "Không thể kết nối: " + ex2.Message, isError: true);
				}
				return;
			}
			finally
			{
				isSubmitting = false;
			}
		}
		await SetRegisterStateAsync(isBusy: false, "Mật khẩu cần có ít nhất 6 ký tự.", isError: true);
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

	private Task SetRegisterStateAsync(bool isBusy, string message = "", bool isError = false)
	{
		string value = JsonSerializer.Serialize(message, JsonOptions);
		string value2 = (isBusy ? "true" : "false");
		string value3 = (isError ? "true" : "false");
		return ExecuteScriptAsync($"window.__nestgRegister?.setState({value2}, {value}, {value3});");
	}

	private async Task ExecuteScriptAsync(string script)
	{
		try
		{
			if (!registerWebView.IsDisposed && registerWebView.CoreWebView2 != null)
			{
				await registerWebView.CoreWebView2.ExecuteScriptAsync(script);
			}
		}
		catch (ObjectDisposedException)
		{
		}
		catch (InvalidOperationException)
		{
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

	private static string? NormalizePhone(string? value)
	{
		if (!string.IsNullOrWhiteSpace(value))
		{
			return value.Trim();
		}
		return null;
	}

	[DllImport("user32.dll")]
	private static extern bool ReleaseCapture();

	[DllImport("user32.dll")]
	private static extern nint SendMessage(nint hWnd, int message, int wParam, int lParam);
}
