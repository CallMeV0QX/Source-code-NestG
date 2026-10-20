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

internal sealed class PasswordRecoveryWebViewForm : Form
{
	private sealed class RecoveryWebMessage
	{
		public string Type { get; set; } = string.Empty;

		public string? Email { get; set; }

		public string? Otp { get; set; }

		public string? Password { get; set; }

		public bool Remember { get; set; }
	}

	private const int WmNcButtonDown = 161;

	private const int HtCaption = 2;

	private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
	{
		PropertyNameCaseInsensitive = true
	};

	private readonly WebView2 recoveryWebView = new WebView2();

	private readonly CancellationTokenSource disposalCancellation = new CancellationTokenSource();

	private readonly DeferredWebViewMessageDispatcher webMessageDispatcher;

	private Task? webViewInitializationTask;

	private OtpChallengeInfo? currentChallenge;

	private bool isSubmitting;

	private bool isNavigating;

	public PasswordRecoveryWebViewForm()
	{
		webMessageDispatcher = new DeferredWebViewMessageDispatcher(this, (Exception exception) => SetRecoveryStateAsync(isBusy: false, DesktopUserMessage.FromException(exception, "Không thể hoàn tất thao tác. Vui lòng thử lại."), isError: true), disposalCancellation.Token);
		InitializeShell();
	}

	protected override async void OnShown(EventArgs e)
	{
		base.OnShown(e);
		try
		{
			await EnsureWebViewAsync();
			if (!recoveryWebView.IsDisposed)
			{
				recoveryWebView.NavigateToString(PasswordRecoveryWebViewHtmlBuilder.BuildDocument(DesktopAppContext.Settings.RememberSessionByDefault));
			}
		}
		catch (Exception ex)
		{
			await LauncherMessageBox.ShowAsync(this, "Không thể khởi tạo giao diện khôi phục mật khẩu.\n\n" + ex.Message, "Lỗi WebView2", MessageBoxButtons.OK, MessageBoxIcon.Hand);
		}
	}

	protected override void OnFormClosed(FormClosedEventArgs e)
	{
		disposalCancellation.Cancel();
		disposalCancellation.Dispose();
		recoveryWebView.Dispose();
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
		recoveryWebView.Dock = DockStyle.Fill;
		recoveryWebView.Margin = Padding.Empty;
		recoveryWebView.DefaultBackgroundColor = Color.FromArgb(7, 13, 20);
		base.Controls.Add(recoveryWebView);
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
		await recoveryWebView.EnsureCoreWebView2Async();
		if (!recoveryWebView.IsDisposed && recoveryWebView.CoreWebView2 != null)
		{
			CoreWebView2Settings settings = recoveryWebView.CoreWebView2.Settings;
			settings.AreDefaultContextMenusEnabled = false;
			settings.AreDevToolsEnabled = false;
			settings.AreBrowserAcceleratorKeysEnabled = false;
			settings.IsStatusBarEnabled = false;
			settings.IsZoomControlEnabled = false;
			string text = Path.Combine(AppContext.BaseDirectory, "Assets");
			if (Directory.Exists(text))
			{
				recoveryWebView.CoreWebView2.SetVirtualHostNameToFolderMapping("nestg-assets.local", text, CoreWebView2HostResourceAccessKind.Allow);
			}
			recoveryWebView.CoreWebView2.WebMessageReceived -= RecoveryWebView_WebMessageReceived;
			recoveryWebView.CoreWebView2.WebMessageReceived += RecoveryWebView_WebMessageReceived;
			recoveryWebView.CoreWebView2.NavigationStarting -= RecoveryWebView_NavigationStarting;
			recoveryWebView.CoreWebView2.NavigationStarting += RecoveryWebView_NavigationStarting;
		}
	}

	private void RecoveryWebView_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
	{
		RecoveryWebMessage message;
		try
		{
			message = JsonSerializer.Deserialize<RecoveryWebMessage>(e.WebMessageAsJson, JsonOptions);
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

	private async Task ProcessWebMessageAsync(RecoveryWebMessage message)
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
				if (type == "login" && !isSubmitting)
				{
					NavigateTo(DesktopAppContext.CreateLoginForm());
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
			if (type == "forgotPassword")
			{
				await SendOtpAsync(message.Email);
			}
			break;
		case 9:
			if (type == "resendOtp")
			{
				await SendOtpAsync(message.Email ?? currentChallenge?.Email, isResend: true);
			}
			break;
		case 13:
			if (type == "resetPassword")
			{
				await ResetPasswordAsync(message);
			}
			break;
		case 6:
		case 7:
		case 11:
		case 12:
			break;
		}
	}

	private void RecoveryWebView_NavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
	{
		if (e.IsUserInitiated && !e.Uri.StartsWith("about:blank", StringComparison.OrdinalIgnoreCase) && !e.Uri.StartsWith("data:text/html", StringComparison.OrdinalIgnoreCase))
		{
			e.Cancel = true;
		}
	}

	private async Task SendOtpAsync(string? rawEmail, bool isResend = false)
	{
		if (isSubmitting || isNavigating)
		{
			return;
		}
		string text = rawEmail?.Trim() ?? string.Empty;
		if (!string.IsNullOrWhiteSpace(text))
		{
			isSubmitting = true;
			try
			{
				try
				{
					AuthActionResult authActionResult = await DesktopAppContext.AuthSessionManager.ForgotPasswordAsync(text, disposalCancellation.Token);
					if (!authActionResult.RequiresChallenge || authActionResult.Challenge == null)
					{
						await SetRecoveryStateAsync(isBusy: false, string.IsNullOrWhiteSpace(authActionResult.Message) ? "Không thể gửi OTP." : authActionResult.Message, isError: true);
						return;
					}
					currentChallenge = authActionResult.Challenge;
					await SetChallengeAsync(currentChallenge);
					if (isResend)
					{
						await SetRecoveryStateAsync(isBusy: false, currentChallenge.Message, isError: false, isSuccess: true);
					}
				}
				catch (OperationCanceledException) when (disposalCancellation.IsCancellationRequested)
				{
				}
				catch (Exception ex2)
				{
					await SetRecoveryStateAsync(isBusy: false, "Không thể kết nối: " + ex2.Message, isError: true);
				}
				return;
			}
			finally
			{
				isSubmitting = false;
			}
		}
		await SetRecoveryStateAsync(isBusy: false, "Vui lòng nhập email để nhận OTP.", isError: true);
	}

	private async Task ResetPasswordAsync(RecoveryWebMessage message)
	{
		if (isSubmitting || isNavigating)
		{
			return;
		}
		string text = currentChallenge?.Email ?? message.Email?.Trim() ?? string.Empty;
		string text2 = message.Otp?.Trim() ?? string.Empty;
		string text3 = message.Password ?? string.Empty;
		if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(text2) || string.IsNullOrWhiteSpace(text3))
		{
			await SetRecoveryStateAsync(isBusy: false, "Vui lòng nhập đầy đủ OTP và mật khẩu mới.", isError: true);
			return;
		}
		if (text3.Length >= 6)
		{
			isSubmitting = true;
			try
			{
				try
				{
					AuthActionResult authActionResult = await DesktopAppContext.AuthSessionManager.ResetPasswordAsync(text, text2, text3, message.Remember, disposalCancellation.Token);
					if (authActionResult.HasSession)
					{
						await SetRecoveryStateAsync(isBusy: false, "Mật khẩu đã được cập nhật. Đang mở cửa hàng...", isError: false, isSuccess: true);
						NavigateTo(DesktopAppContext.CreateStorefrontForm());
						return;
					}
					await SetRecoveryStateAsync(isBusy: false, string.IsNullOrWhiteSpace(authActionResult.Message) ? "Không thể đặt lại mật khẩu." : authActionResult.Message, isError: true);
				}
				catch (OperationCanceledException) when (disposalCancellation.IsCancellationRequested)
				{
				}
				catch (Exception ex2)
				{
					await SetRecoveryStateAsync(isBusy: false, "Không thể kết nối: " + ex2.Message, isError: true);
				}
				return;
			}
			finally
			{
				isSubmitting = false;
			}
		}
		await SetRecoveryStateAsync(isBusy: false, "Mật khẩu mới phải có ít nhất 6 ký tự.", isError: true);
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

	private Task SetRecoveryStateAsync(bool isBusy, string message = "", bool isError = false, bool isSuccess = false)
	{
		string value = JsonSerializer.Serialize(message, JsonOptions);
		string value2 = (isBusy ? "true" : "false");
		string value3 = (isError ? "true" : "false");
		string value4 = (isSuccess ? "true" : "false");
		return ExecuteScriptAsync($"window.__nestgRecovery?.setState({value2}, {value}, {value3}, {value4});");
	}

	private Task SetChallengeAsync(OtpChallengeInfo challenge)
	{
		var value = new
		{
			email = challenge.Email,
			message = (challenge.EmailDispatched ? challenge.Message : (challenge.Message + " Bạn có thể tạo mã mới từ nút gửi lại OTP.")),
			expiresAtText = $"OTP hết hạn lúc {challenge.ExpiresAtUtc.ToLocalTime():HH:mm dd/MM/yyyy}"
		};
		return ExecuteScriptAsync("window.__nestgRecovery?.setChallenge(" + JsonSerializer.Serialize(value, JsonOptions) + ");");
	}

	private async Task ExecuteScriptAsync(string script)
	{
		try
		{
			if (!recoveryWebView.IsDisposed && recoveryWebView.CoreWebView2 != null)
			{
				await recoveryWebView.CoreWebView2.ExecuteScriptAsync(script);
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

	[DllImport("user32.dll")]
	private static extern bool ReleaseCapture();

	[DllImport("user32.dll")]
	private static extern nint SendMessage(nint hWnd, int message, int wParam, int lParam);
}
