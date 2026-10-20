using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using app_desktop.Services;

namespace UI_Desktop;

internal sealed class LauncherMessageDialog : Form
{
	private readonly record struct DialogAction(string Label, string ResultToken, DialogResult DialogResult, bool IsPrimary);

	private readonly record struct DialogTone(string Key, string ChipText, string Accent, string Border, string Hero, string BadgeSurface, string Primary, string PrimaryHover, string PrimaryText);

	private const int CsDropShadow = 131072;

	private const int HtCaption = 2;

	private const int WmNcButtonDown = 161;

	private const int DialogWidth = 600;

	private const int DialogHeight = 500;

	private const int DialogScreenMargin = 32;

	private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);

	private readonly string message;

	private readonly string caption;

	private readonly MessageBoxButtons buttons;

	private readonly MessageBoxIcon icon;

	private readonly IReadOnlyDictionary<string, DialogResult> resultsByToken;

	private readonly WebView2 messageWebView = new WebView2();

	private Task? webViewInitializationTask;

	private bool isResponding;

	protected override CreateParams CreateParams
	{
		get
		{
			CreateParams createParams = base.CreateParams;
			createParams.ClassStyle |= 131072;
			return createParams;
		}
	}

	internal LauncherMessageDialog(string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon)
	{
		message = (string.IsNullOrWhiteSpace(text) ? "Không có nội dung thông báo." : text.Trim());
		this.caption = (string.IsNullOrWhiteSpace(caption) ? "NestG Launcher" : caption.Trim());
		this.buttons = buttons;
		this.icon = icon;
		resultsByToken = ResolveActions(buttons).ToDictionary<DialogAction, string, DialogResult>((DialogAction action) => action.ResultToken, (DialogAction action) => action.DialogResult, StringComparer.Ordinal);
		InitializeShell();
	}

	protected override async void OnShown(EventArgs e)
	{
		base.OnShown(e);
		try
		{
			await EnsureWebViewAsync().ConfigureAwait(continueOnCapturedContext: true);
			if (!messageWebView.IsDisposed && messageWebView.CoreWebView2 != null)
			{
				messageWebView.NavigateToString(BuildDocument(message, caption, buttons, icon));
			}
		}
		catch (Exception exception)
		{
			DesktopDiagnosticLog.Write("WebView2.MessageDialog", "InitializationFailed", null, exception);
			await DeferredModalDialog.ShowAsync(this, delegate
			{
				ShowNativeFallback();
				return base.DialogResult;
			});
		}
	}

	protected override void OnFormClosing(FormClosingEventArgs e)
	{
		if (base.DialogResult == DialogResult.None)
		{
			base.DialogResult = ResolveCloseResult(buttons);
		}
		base.OnFormClosing(e);
	}

	protected override void OnLoad(EventArgs e)
	{
		base.OnLoad(e);
		ApplyDpiAwareClientSize();
		CenterToParent();
	}

	protected override void OnFormClosed(FormClosedEventArgs e)
	{
		messageWebView.Dispose();
		base.OnFormClosed(e);
	}

	private void InitializeShell()
	{
		Rectangle workingArea = Screen.FromPoint(System.Windows.Forms.Cursor.Position).WorkingArea;
		int num = Math.Min(600, Math.Max(320, workingArea.Width - 32));
		int num2 = Math.Min(500, Math.Max(360, workingArea.Height - 32));
		AppIconProvider.ApplyIcon(this);
		Text = caption;
		base.AutoScaleMode = AutoScaleMode.None;
		BackColor = Color.FromArgb(7, 13, 20);
		base.ClientSize = new Size(num, num2);
		MinimumSize = new Size(Math.Min(480, num), Math.Min(400, num2));
		base.FormBorderStyle = FormBorderStyle.None;
		base.MaximizeBox = false;
		base.MinimizeBox = false;
		base.ShowInTaskbar = false;
		base.StartPosition = FormStartPosition.CenterParent;
		base.DpiChanged += delegate
		{
			ApplyDpiAwareClientSize();
		};
		messageWebView.Dock = DockStyle.Fill;
		messageWebView.Margin = Padding.Empty;
		messageWebView.DefaultBackgroundColor = Color.FromArgb(7, 13, 20);
		messageWebView.AllowExternalDrop = false;
		base.Controls.Add(messageWebView);
	}

	private void ApplyDpiAwareClientSize()
	{
		Rectangle workingArea = Screen.FromHandle(base.Handle).WorkingArea;
		uint dpiForWindow = GetDpiForWindow(base.Handle);
		Size size2 = (base.ClientSize = CalculateDpiAwareClientSize(workingArea.Size, dpiForWindow));
		MinimumSize = new Size(Math.Min(ScaleLogicalPixels(480, dpiForWindow), size2.Width), Math.Min(ScaleLogicalPixels(400, dpiForWindow), size2.Height));
	}

	internal static Size CalculateDpiAwareClientSize(Size workingArea, uint dpi)
	{
		uint dpi2 = ((dpi == 0) ? 96u : dpi);
		int num = ScaleLogicalPixels(32, dpi2);
		int val = Math.Max(1, workingArea.Width - num);
		int val2 = Math.Max(1, workingArea.Height - num);
		return new Size(Math.Min(ScaleLogicalPixels(600, dpi2), val), Math.Min(ScaleLogicalPixels(500, dpi2), val2));
	}

	private static int ScaleLogicalPixels(int logicalPixels, uint dpi)
	{
		return (int)Math.Round((double)logicalPixels * ((dpi == 0) ? 1.0 : ((double)dpi / 96.0)));
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
		await messageWebView.EnsureCoreWebView2Async().ConfigureAwait(continueOnCapturedContext: true);
		if (!messageWebView.IsDisposed && messageWebView.CoreWebView2 != null)
		{
			CoreWebView2 coreWebView = messageWebView.CoreWebView2;
			CoreWebView2Settings settings = coreWebView.Settings;
			settings.AreBrowserAcceleratorKeysEnabled = false;
			settings.AreDefaultContextMenusEnabled = false;
			settings.AreDefaultScriptDialogsEnabled = false;
			settings.AreDevToolsEnabled = false;
			settings.AreHostObjectsAllowed = false;
			settings.IsStatusBarEnabled = false;
			settings.IsWebMessageEnabled = true;
			settings.IsZoomControlEnabled = false;
			messageWebView.ZoomFactor = 1.0;
			string text = Path.Combine(AppContext.BaseDirectory, "Assets");
			if (Directory.Exists(text))
			{
				coreWebView.SetVirtualHostNameToFolderMapping("nestg-assets.local", text, CoreWebView2HostResourceAccessKind.Allow);
			}
			coreWebView.WebMessageReceived -= MessageWebView_WebMessageReceived;
			coreWebView.WebMessageReceived += MessageWebView_WebMessageReceived;
			coreWebView.NavigationStarting -= MessageWebView_NavigationStarting;
			coreWebView.NavigationStarting += MessageWebView_NavigationStarting;
			coreWebView.NewWindowRequested -= MessageWebView_NewWindowRequested;
			coreWebView.NewWindowRequested += MessageWebView_NewWindowRequested;
		}
	}

	private void MessageWebView_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
	{
		if (!isResponding && !base.IsDisposed && !base.Disposing && string.Equals(e.Source, "about:blank", StringComparison.OrdinalIgnoreCase))
		{
			string text;
			try
			{
				text = e.TryGetWebMessageAsString();
			}
			catch
			{
				return;
			}
			DialogResult value;
			if (string.Equals(text, "window:drag", StringComparison.Ordinal))
			{
				PostWindowAction(BeginWindowDrag);
			}
			else if (string.Equals(text, "window:close", StringComparison.Ordinal))
			{
				CompleteResponse(ResolveCloseResult(buttons));
			}
			else if (resultsByToken.TryGetValue(text, out value))
			{
				CompleteResponse(value);
			}
		}
	}

	private void CompleteResponse(DialogResult result)
	{
		isResponding = true;
		PostWindowAction(delegate
		{
			base.DialogResult = result;
			Close();
		});
	}

	private void PostWindowAction(Action action)
	{
		if (base.IsDisposed || base.Disposing || !base.IsHandleCreated)
		{
			return;
		}
		try
		{
			BeginInvoke(delegate
			{
				if (!base.IsDisposed && !base.Disposing)
				{
					action();
				}
			});
		}
		catch (InvalidOperationException) when (base.IsDisposed || base.Disposing || !base.IsHandleCreated)
		{
		}
	}

	private static void MessageWebView_NavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
	{
		if (!e.Uri.StartsWith("about:blank", StringComparison.OrdinalIgnoreCase) && !e.Uri.StartsWith("data:text/html", StringComparison.OrdinalIgnoreCase))
		{
			e.Cancel = true;
		}
	}

	private static void MessageWebView_NewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
	{
		e.Handled = true;
	}

	private void ShowNativeFallback()
	{
		if (!base.IsDisposed && !base.Disposing)
		{
			base.DialogResult = MessageBox.Show(this, message, caption, buttons, icon);
			Close();
		}
	}

	internal static string BuildDocument(string message, string caption, MessageBoxButtons buttons, MessageBoxIcon icon)
	{
		IReadOnlyList<DialogAction> readOnlyList = ResolveActions(buttons);
		DialogTone dialogTone = ResolveTone(icon);
		string text = JsonSerializer.Serialize(new
		{
			caption = caption,
			message = message,
			chip = dialogTone.ChipText,
			sectionTitle = ((readOnlyList.Count > 1) ? "Nội dung cần xác nhận" : "Nội dung thông báo"),
			shortcutHint = ((readOnlyList.Count > 1) ? "Esc để hủy" : "Enter hoặc Esc để đóng"),
			closeResult = readOnlyList.First((DialogAction action) => action.DialogResult == ResolveCloseResult(buttons)).ResultToken,
			tone = new
			{
				key = dialogTone.Key,
				accent = dialogTone.Accent,
				border = dialogTone.Border,
				hero = dialogTone.Hero,
				badgeSurface = dialogTone.BadgeSurface,
				primary = dialogTone.Primary,
				primaryHover = dialogTone.PrimaryHover,
				primaryText = dialogTone.PrimaryText
			},
			actions = readOnlyList.Select((DialogAction action) => new
			{
				label = action.Label,
				result = action.ResultToken,
				primary = action.IsPrimary
			})
		}, JsonOptions);
		return "<!doctype html>\n<html lang=\"vi\">\n<head>\n  <meta charset=\"utf-8\">\n  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">\n  <title>NestG Launcher</title>\n  <style>\n    :root {\n      color-scheme: dark;\n      font-family: \"Segoe UI Variable Text\", \"Segoe UI\", Arial, sans-serif;\n      background: #070d14;\n      color: #f2f7ff;\n      --window: #0b121c;\n      --surface-raised: #151f2c;\n      --border: #34475d;\n      --border-strong: #52657a;\n      --text: #f2f7ff;\n      --muted: #9badc0;\n      --focus: #66c0f4;\n      --accent: #7ec8ff;\n      --tone-border: #315b7a;\n      --tone-hero: #122538;\n      --tone-badge: #10283b;\n      --tone-primary: #66b8f2;\n      --tone-primary-hover: #82c8f7;\n      --tone-primary-text: #101820;\n      --motion-fast: 120ms;\n      --motion-enter: 160ms;\n    }\n    * { box-sizing: border-box; }\n    html, body { width: 100%; height: 100%; }\n    body {\n      margin: 0;\n      overflow: hidden;\n      background: #070d14;\n      color: var(--text);\n    }\n    button { font: inherit; }\n    .sr-only {\n      position: absolute;\n      width: 1px;\n      height: 1px;\n      padding: 0;\n      margin: -1px;\n      overflow: hidden;\n      clip: rect(0, 0, 0, 0);\n      white-space: nowrap;\n      border: 0;\n    }\n    .window {\n      width: 100%;\n      height: 100%;\n      display: grid;\n      grid-template-rows: 48px minmax(0, 1fr);\n      overflow: hidden;\n      border: 1px solid var(--border-strong);\n      background: var(--window);\n      box-shadow:\n        inset 0 1px 0 rgba(255, 255, 255, 0.04),\n        0 18px 50px rgba(0, 0, 0, 0.42);\n    }\n    .titlebar {\n      display: flex;\n      align-items: center;\n      justify-content: space-between;\n      padding: 0 3px 0 16px;\n      border-bottom: 1px solid #26374a;\n      background: linear-gradient(180deg, #111b28 0%, #0d1621 100%);\n      user-select: none;\n    }\n    .brand {\n      min-width: 0;\n      display: flex;\n      align-items: center;\n      gap: 11px;\n    }\n    .brand-mark {\n      position: relative;\n      width: 25px;\n      height: 28px;\n      display: grid;\n      flex: 0 0 auto;\n      place-items: center;\n      color: #f0b5bc;\n      font-size: 12px;\n      font-weight: 700;\n    }\n    .brand-mark img {\n      position: absolute;\n      inset: 0;\n      width: 100%;\n      height: 100%;\n      object-fit: contain;\n    }\n    .brand-name {\n      overflow: hidden;\n      color: var(--text);\n      font-size: 15px;\n      font-weight: 700;\n      text-overflow: ellipsis;\n      white-space: nowrap;\n    }\n    .close-button {\n      position: relative;\n      width: 44px;\n      height: 44px;\n      flex: 0 0 auto;\n      border: 0;\n      border-radius: 4px;\n      background: transparent;\n      cursor: pointer;\n      touch-action: manipulation;\n      transition: background-color var(--motion-fast) ease;\n    }\n    .close-button::before,\n    .close-button::after {\n      position: absolute;\n      top: 21px;\n      left: 13px;\n      width: 18px;\n      height: 2px;\n      content: \"\";\n      border-radius: 2px;\n      background: #cbd5e1;\n    }\n    .close-button::before { transform: rotate(45deg); }\n    .close-button::after { transform: rotate(-45deg); }\n    .close-button:hover { background: #8e2f3b; }\n    .close-button:hover::before,\n    .close-button:hover::after { background: #ffffff; }\n    .close-button:focus-visible {\n      outline: 2px solid var(--focus);\n      outline-offset: -2px;\n    }\n    .shell {\n      width: 100%;\n      height: 100%;\n      display: grid;\n      grid-template-rows: auto minmax(0, 1fr) auto;\n      gap: 12px;\n      padding: 16px;\n      background: var(--window);\n      animation: dialog-enter var(--motion-enter) cubic-bezier(0.2, 0.75, 0.25, 1) both;\n    }\n    .hero {\n      min-height: 80px;\n      display: grid;\n      grid-template-columns: 48px minmax(0, 1fr);\n      gap: 16px;\n      align-items: center;\n      padding: 12px 16px;\n      border: 1px solid var(--tone-border);\n      border-radius: 6px;\n      background: var(--tone-hero);\n      box-shadow: inset 3px 0 0 var(--accent);\n    }\n    .hero-copy { min-width: 0; }\n    .kicker {\n      display: block;\n      color: var(--accent);\n      font-size: 12px;\n      font-weight: 700;\n      line-height: 1.2;\n      text-transform: uppercase;\n      letter-spacing: 0.08em;\n    }\n    h1 {\n      margin: 6px 0 0;\n      color: var(--text);\n      font-size: 24px;\n      font-weight: 700;\n      line-height: 1.2;\n      overflow-wrap: anywhere;\n    }\n    .status-icon {\n      width: 48px;\n      height: 48px;\n      display: grid;\n      place-items: center;\n      border: 1px solid var(--tone-border);\n      border-radius: 6px;\n      background: var(--tone-badge);\n      color: var(--accent);\n      user-select: none;\n    }\n    .status-icon svg {\n      width: 26px;\n      height: 26px;\n      fill: none;\n      stroke: currentColor;\n      stroke-width: 1.8;\n      stroke-linecap: round;\n      stroke-linejoin: round;\n    }\n    .content {\n      min-height: 0;\n      overflow: hidden;\n      padding: 18px 18px 16px;\n      border: 1px solid var(--border);\n      border-radius: 6px;\n      background: var(--surface-raised);\n      box-shadow: inset 0 1px 0 rgba(255, 255, 255, 0.025);\n    }\n    .message {\n      width: 100%;\n      height: 100%;\n      min-width: 0;\n      min-height: 0;\n      overflow-x: hidden;\n      overflow-y: auto;\n      padding: 0 10px 0 0;\n      color: #e6edf7;\n      font-size: 16px;\n      line-height: 1.55;\n      overflow-wrap: anywhere;\n      scrollbar-color: #64758a transparent;\n      scrollbar-width: thin;\n    }\n    .message:focus-visible {\n      outline: 2px solid var(--focus);\n      outline-offset: 4px;\n    }\n    .message-paragraph {\n      margin: 0 0 16px;\n      white-space: pre-wrap;\n    }\n    .message-paragraph:last-child { margin-bottom: 0; }\n    .message-paragraph.decision {\n      color: var(--text);\n      font-weight: 600;\n    }\n    .notice {\n      margin: 2px 0 16px;\n      padding: 12px 14px 13px;\n      border: 1px solid var(--tone-border);\n      border-left: 3px solid var(--accent);\n      border-radius: 4px;\n      background: var(--tone-hero);\n    }\n    .notice-label {\n      display: block;\n      margin-bottom: 6px;\n      color: var(--accent);\n      font-size: 12px;\n      font-weight: 700;\n      line-height: 1.2;\n      letter-spacing: 0.06em;\n      text-transform: uppercase;\n    }\n    .notice-text {\n      margin: 0;\n      color: #edf3fb;\n      white-space: pre-wrap;\n    }\n    .message::-webkit-scrollbar { width: 8px; }\n    .message::-webkit-scrollbar-track {\n      border-radius: 4px;\n      background: transparent;\n    }\n    .message::-webkit-scrollbar-thumb {\n      border: 2px solid transparent;\n      border-radius: 6px;\n      background: #64758a;\n      background-clip: padding-box;\n    }\n    .footer {\n      min-height: 46px;\n      display: flex;\n      align-items: center;\n      justify-content: space-between;\n      gap: 16px;\n      padding-top: 12px;\n      border-top: 1px solid #26374a;\n    }\n    .shortcut-hint {\n      flex: 0 0 auto;\n      color: var(--muted);\n      font-size: 13px;\n      line-height: 1.3;\n    }\n    .actions {\n      display: flex;\n      justify-content: flex-end;\n      align-items: center;\n      gap: 10px;\n    }\n    .action {\n      min-width: 124px;\n      height: 46px;\n      padding: 0 20px;\n      border: 1px solid #465a72;\n      border-radius: 5px;\n      background: #172332;\n      color: #e8eef7;\n      font-size: 15px;\n      font-weight: 700;\n      white-space: nowrap;\n      cursor: pointer;\n      touch-action: manipulation;\n      transition:\n        border-color var(--motion-fast) ease,\n        background-color var(--motion-fast) ease,\n        color var(--motion-fast) ease,\n        box-shadow var(--motion-fast) ease,\n        filter var(--motion-fast) ease;\n    }\n    .action:hover {\n      border-color: #6a809a;\n      background: #202f41;\n    }\n    .action:active { filter: brightness(0.9); }\n    .action.primary {\n      border-color: var(--tone-primary);\n      background: var(--tone-primary);\n      color: var(--tone-primary-text);\n      box-shadow: 0 8px 22px rgba(0, 0, 0, 0.24);\n    }\n    .action.primary:hover {\n      border-color: var(--tone-primary-hover);\n      background: var(--tone-primary-hover);\n      box-shadow: 0 10px 26px rgba(0, 0, 0, 0.3);\n    }\n    .action:focus-visible {\n      outline: 2px solid var(--focus);\n      outline-offset: 2px;\n    }\n    @keyframes dialog-enter {\n      from {\n        opacity: 0.55;\n        transform: translateY(4px);\n      }\n      to {\n        opacity: 1;\n        transform: translateY(0);\n      }\n    }\n    @media (max-width: 480px) {\n      .shell { gap: 12px; padding: 14px; }\n      .hero {\n        min-height: 76px;\n        grid-template-columns: 42px minmax(0, 1fr);\n        gap: 12px;\n        padding: 12px;\n      }\n      .status-icon { width: 42px; height: 42px; }\n      .status-icon svg { width: 23px; height: 23px; }\n      h1 { font-size: 21px; }\n      .content { padding: 14px; }\n      .footer {\n        align-items: stretch;\n        flex-direction: column-reverse;\n        gap: 9px;\n        padding-top: 12px;\n      }\n      .shortcut-hint { text-align: right; }\n      .actions { width: 100%; gap: 8px; }\n      .action {\n        min-width: 0;\n        flex: 1 1 0;\n        padding-inline: 8px;\n      }\n    }\n    @media (max-height: 430px) {\n      .window { grid-template-rows: 42px minmax(0, 1fr); }\n      .titlebar { padding-left: 12px; }\n      .close-button { width: 40px; height: 40px; }\n      .close-button::before,\n      .close-button::after {\n        top: 19px;\n        left: 12px;\n        width: 16px;\n      }\n      .shell { gap: 6px; padding: 8px 10px; }\n      .hero {\n        min-height: 52px;\n        grid-template-columns: 36px minmax(0, 1fr);\n        gap: 10px;\n        padding: 8px 10px;\n      }\n      .status-icon { width: 36px; height: 36px; }\n      .status-icon svg { width: 21px; height: 21px; }\n      .kicker { display: none; }\n      .content { padding: 10px 12px; }\n      .message { font-size: 15px; line-height: 1.48; }\n      .footer {\n        min-height: 42px;\n        flex-direction: row;\n        padding-top: 6px;\n      }\n      .shortcut-hint { display: none; }\n      .actions { width: 100%; }\n      .action { height: 42px; }\n      h1 { margin-top: 0; font-size: 18px; }\n    }\n    @media (prefers-reduced-motion: reduce) {\n      *, *::before, *::after {\n        scroll-behavior: auto !important;\n        animation-duration: 0.01ms !important;\n        animation-iteration-count: 1 !important;\n        transition-duration: 0.01ms !important;\n      }\n    }\n  </style>\n</head>\n<body>\n  <div\n    class=\"window\"\n    role=\"dialog\"\n    aria-modal=\"true\"\n    aria-labelledby=\"dialogTitle\"\n    aria-describedby=\"message\">\n    <header class=\"titlebar\" id=\"titlebar\">\n      <div class=\"brand\">\n        <span class=\"brand-mark\" aria-hidden=\"true\">\n          <img src=\"https://nestg-assets.local/logo.png\" alt=\"\">\n        </span>\n        <span class=\"brand-name\">NestG Launcher</span>\n      </div>\n      <button class=\"close-button\" id=\"closeButton\" type=\"button\" aria-label=\"Đóng cửa sổ\"></button>\n    </header>\n    <main class=\"shell\">\n      <section class=\"hero\" aria-labelledby=\"dialogTitle\">\n        <div class=\"status-icon\" id=\"statusIcon\" aria-hidden=\"true\"></div>\n        <div class=\"hero-copy\">\n          <span class=\"kicker\" id=\"chip\"></span>\n          <h1 id=\"dialogTitle\"></h1>\n        </div>\n      </section>\n      <section class=\"content\" aria-labelledby=\"contentTitle\">\n        <h2 class=\"sr-only\" id=\"contentTitle\"></h2>\n        <div class=\"message\" id=\"message\" role=\"document\" tabindex=\"-1\"></div>\n      </section>\n      <footer class=\"footer\">\n        <span class=\"shortcut-hint\" id=\"shortcutHint\"></span>\n        <div class=\"actions\" id=\"actions\"></div>\n      </footer>\n    </main>\n  </div>\n  <script>\n    const payload = " + text + ";\n    const root = document.documentElement;\n    root.style.setProperty('--accent', payload.tone.accent);\n    root.style.setProperty('--tone-border', payload.tone.border);\n    root.style.setProperty('--tone-hero', payload.tone.hero);\n    root.style.setProperty('--tone-badge', payload.tone.badgeSurface);\n    root.style.setProperty('--tone-primary', payload.tone.primary);\n    root.style.setProperty('--tone-primary-hover', payload.tone.primaryHover);\n    root.style.setProperty('--tone-primary-text', payload.tone.primaryText);\n\n    document.title = payload.caption;\n    document.getElementById('chip').textContent = payload.chip;\n    document.getElementById('dialogTitle').textContent = payload.caption;\n    document.getElementById('contentTitle').textContent = payload.sectionTitle;\n    document.getElementById('shortcutHint').textContent = payload.shortcutHint;\n\n    const iconMarkupByTone = {\n      warning: '<svg viewBox=\"0 0 24 24\" focusable=\"false\"><path d=\"m21.73 18-8-14a2 2 0 0 0-3.46 0l-8 14A2 2 0 0 0 4 21h16a2 2 0 0 0 1.73-3Z\"></path><path d=\"M12 9v4\"></path><path d=\"M12 17h.01\"></path></svg>',\n      error: '<svg viewBox=\"0 0 24 24\" focusable=\"false\"><circle cx=\"12\" cy=\"12\" r=\"9\"></circle><path d=\"m9 9 6 6\"></path><path d=\"m15 9-6 6\"></path></svg>',\n      question: '<svg viewBox=\"0 0 24 24\" focusable=\"false\"><circle cx=\"12\" cy=\"12\" r=\"9\"></circle><path d=\"M9.8 9a2.4 2.4 0 0 1 4.66.8c0 1.6-2.46 1.8-2.46 3.4\"></path><path d=\"M12 17h.01\"></path></svg>',\n      info: '<svg viewBox=\"0 0 24 24\" focusable=\"false\"><circle cx=\"12\" cy=\"12\" r=\"9\"></circle><path d=\"M12 11v5\"></path><path d=\"M12 8h.01\"></path></svg>'\n    };\n    document.getElementById('statusIcon').innerHTML =\n      iconMarkupByTone[payload.tone.key] || iconMarkupByTone.info;\n\n    const messageHost = document.getElementById('message');\n    const messageBlocks = payload.message\n      .split(/\\r?\\n(?:[ \\t]*\\r?\\n)+/)\n      .map((block) => block.trim())\n      .filter(Boolean);\n    const blocks = messageBlocks.length > 0 ? messageBlocks : [payload.message];\n\n    blocks.forEach((block, index) => {\n      const isDecision = blocks.length > 1 && index === blocks.length - 1;\n      const isImportantWarning =\n        payload.tone.key === 'warning' && blocks.length >= 3 && index > 0 && !isDecision;\n\n      if (isImportantWarning) {\n        const notice = document.createElement('aside');\n        notice.className = 'notice';\n        notice.setAttribute('role', 'note');\n\n        const label = document.createElement('span');\n        label.className = 'notice-label';\n        label.textContent = 'Lưu ý quan trọng';\n\n        const text = document.createElement('p');\n        text.className = 'notice-text';\n        text.textContent = block;\n\n        notice.append(label, text);\n        messageHost.appendChild(notice);\n        return;\n      }\n\n      const paragraph = document.createElement('p');\n      paragraph.className = 'message-paragraph' + (isDecision ? ' decision' : '');\n      paragraph.textContent = block;\n      messageHost.appendChild(paragraph);\n    });\n\n    let isClosing = false;\n    const postResult = (result) => {\n      if (!isClosing && typeof result === 'string' && result.length > 0) {\n        isClosing = true;\n        window.chrome?.webview?.postMessage(result);\n      }\n    };\n\n    document.getElementById('titlebar').addEventListener('mousedown', (event) => {\n      if (event.button === 0 && !event.target.closest('button')) {\n        window.chrome?.webview?.postMessage('window:drag');\n      }\n    });\n    document.getElementById('closeButton').addEventListener('click', () => {\n      if (!isClosing) {\n        isClosing = true;\n        window.chrome?.webview?.postMessage('window:close');\n      }\n    });\n\n    const actionsHost = document.getElementById('actions');\n    for (const action of payload.actions) {\n      const button = document.createElement('button');\n      button.type = 'button';\n      button.className = 'action' + (action.primary ? ' primary' : '');\n      button.textContent = action.label;\n      button.dataset.result = action.result;\n      button.addEventListener('click', () => postResult(action.result));\n      actionsHost.appendChild(button);\n    }\n\n    const primaryButton = actionsHost.querySelector('.primary') || actionsHost.lastElementChild;\n    const updateMessageTabStop = () => {\n      messageHost.tabIndex = messageHost.scrollHeight > messageHost.clientHeight ? 0 : -1;\n    };\n    const getFocusableElements = () => Array.from(\n      document.querySelectorAll('button:not([disabled]), [tabindex=\"0\"]'))\n      .filter((element) => element.getClientRects().length > 0);\n\n    requestAnimationFrame(() => {\n      updateMessageTabStop();\n      primaryButton?.focus();\n    });\n    window.addEventListener('resize', updateMessageTabStop);\n\n    document.addEventListener('keydown', (event) => {\n      if (event.key === 'Tab') {\n        const focusableElements = getFocusableElements();\n        if (focusableElements.length === 0) {\n          event.preventDefault();\n          return;\n        }\n\n        const firstElement = focusableElements[0];\n        const lastElement = focusableElements[focusableElements.length - 1];\n        if (!focusableElements.includes(document.activeElement)) {\n          event.preventDefault();\n          (event.shiftKey ? lastElement : firstElement).focus();\n        } else if (event.shiftKey && document.activeElement === firstElement) {\n          event.preventDefault();\n          lastElement.focus();\n        } else if (!event.shiftKey && document.activeElement === lastElement) {\n          event.preventDefault();\n          firstElement.focus();\n        }\n        return;\n      }\n\n      if (event.key === 'Escape') {\n        event.preventDefault();\n        postResult(payload.closeResult);\n        return;\n      }\n\n      if (event.key === 'Enter' && document.activeElement?.tagName !== 'BUTTON') {\n        event.preventDefault();\n        postResult(primaryButton?.dataset.result);\n      }\n    });\n  </script>\n</body>\n</html>";
	}

	private static IReadOnlyList<DialogAction> ResolveActions(MessageBoxButtons buttons)
	{
		return buttons switch
		{
			MessageBoxButtons.OKCancel => new _003C_003Ez__ReadOnlyArray<DialogAction>(new DialogAction[2]
			{
				new DialogAction("Hủy", "cancel", DialogResult.Cancel, IsPrimary: false),
				new DialogAction("Đồng ý", "ok", DialogResult.OK, IsPrimary: true)
			}), 
			MessageBoxButtons.AbortRetryIgnore => new _003C_003Ez__ReadOnlyArray<DialogAction>(new DialogAction[3]
			{
				new DialogAction("Hủy tác vụ", "abort", DialogResult.Abort, IsPrimary: false),
				new DialogAction("Bỏ qua", "ignore", DialogResult.Ignore, IsPrimary: false),
				new DialogAction("Thử lại", "retry", DialogResult.Retry, IsPrimary: true)
			}), 
			MessageBoxButtons.YesNoCancel => new _003C_003Ez__ReadOnlyArray<DialogAction>(new DialogAction[3]
			{
				new DialogAction("Hủy", "cancel", DialogResult.Cancel, IsPrimary: false),
				new DialogAction("Không", "no", DialogResult.No, IsPrimary: false),
				new DialogAction("Đồng ý", "yes", DialogResult.Yes, IsPrimary: true)
			}), 
			MessageBoxButtons.YesNo => new _003C_003Ez__ReadOnlyArray<DialogAction>(new DialogAction[2]
			{
				new DialogAction("Hủy", "no", DialogResult.No, IsPrimary: false),
				new DialogAction("Đồng ý", "yes", DialogResult.Yes, IsPrimary: true)
			}), 
			MessageBoxButtons.RetryCancel => new _003C_003Ez__ReadOnlyArray<DialogAction>(new DialogAction[2]
			{
				new DialogAction("Hủy", "cancel", DialogResult.Cancel, IsPrimary: false),
				new DialogAction("Thử lại", "retry", DialogResult.Retry, IsPrimary: true)
			}), 
			MessageBoxButtons.CancelTryContinue => new _003C_003Ez__ReadOnlyArray<DialogAction>(new DialogAction[3]
			{
				new DialogAction("Hủy", "cancel", DialogResult.Cancel, IsPrimary: false),
				new DialogAction("Thử lại", "try-again", DialogResult.TryAgain, IsPrimary: false),
				new DialogAction("Tiếp tục", "continue", DialogResult.Continue, IsPrimary: true)
			}), 
			_ => new _003C_003Ez__ReadOnlySingleElementList<DialogAction>(new DialogAction("Đóng", "ok", DialogResult.OK, IsPrimary: true)), 
		};
	}

	private static DialogResult ResolveCloseResult(MessageBoxButtons buttons)
	{
		return buttons switch
		{
			MessageBoxButtons.OK => DialogResult.OK, 
			MessageBoxButtons.YesNo => DialogResult.No, 
			MessageBoxButtons.AbortRetryIgnore => DialogResult.Abort, 
			_ => DialogResult.Cancel, 
		};
	}

	private void BeginWindowDrag()
	{
		ReleaseCapture();
		SendMessage(base.Handle, 161, 2, 0);
	}

	[DllImport("user32.dll")]
	private static extern bool ReleaseCapture();

	[DllImport("user32.dll")]
	private static extern nint SendMessage(nint hWnd, int message, int wParam, int lParam);

	[DllImport("user32.dll")]
	private static extern uint GetDpiForWindow(nint hWnd);

	private static DialogTone ResolveTone(MessageBoxIcon icon)
	{
		return icon switch
		{
			MessageBoxIcon.Exclamation => new DialogTone("warning", "CẢNH BÁO", "#f0d686", "#7f632f", "#251d12", "#34270f", "#e2a644", "#f0bd61", "#111820"), 
			MessageBoxIcon.Hand => new DialogTone("error", "CÓ LỖI XẢY RA", "#ffb2bf", "#7a3f4e", "#2a171d", "#3a1a23", "#e56f84", "#f1899b", "#171117"), 
			MessageBoxIcon.Question => new DialogTone("question", "XÁC NHẬN HÀNH ĐỘNG", "#8ecaff", "#416a94", "#142438", "#102941", "#66b8f2", "#82c8f7", "#101820"), 
			_ => new DialogTone("info", "THÔNG BÁO TỪ LAUNCHER", "#9bd8c0", "#416f5e", "#142722", "#123126", "#70cca8", "#8adbb9", "#101a17"), 
		};
	}
}
