using System;
using System.Drawing;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using UI_Desktop;

namespace app_desktop.Services;

internal sealed class DenuvoActiveNotificationForm : Form
{
	private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);

	private readonly string notificationTitle;

	private readonly string notificationMessage;

	private readonly WebView2 notificationWebView = new WebView2();

	private Task? webViewInitializationTask;

	internal DenuvoActiveNotificationForm(string title, string message)
	{
		notificationTitle = (string.IsNullOrWhiteSpace(title) ? "Denuvo Active" : title.Trim());
		notificationMessage = (string.IsNullOrWhiteSpace(message) ? "Không có nội dung thông báo." : message.Trim());
		InitializeShell();
	}

	protected override async void OnShown(EventArgs e)
	{
		base.OnShown(e);
		try
		{
			await EnsureWebViewAsync().ConfigureAwait(continueOnCapturedContext: true);
			if (!notificationWebView.IsDisposed)
			{
				notificationWebView.NavigateToString(BuildDocument(notificationTitle, notificationMessage));
			}
		}
		catch (Exception ex)
		{
			MessageBox.Show(this, "Không thể khởi tạo thông báo Denuvo Active.\n\n" + ex.Message, "Lỗi WebView2", MessageBoxButtons.OK, MessageBoxIcon.Hand);
			Close();
		}
	}

	protected override void OnFormClosed(FormClosedEventArgs e)
	{
		notificationWebView.Dispose();
		base.OnFormClosed(e);
	}

	private void InitializeShell()
	{
		AppIconProvider.ApplyIcon(this);
		base.AutoScaleMode = AutoScaleMode.None;
		BackColor = Color.FromArgb(7, 13, 20);
		base.ClientSize = new Size(640, 700);
		MinimumSize = new Size(560, 520);
		base.FormBorderStyle = FormBorderStyle.FixedDialog;
		base.MaximizeBox = false;
		base.MinimizeBox = false;
		base.ShowInTaskbar = false;
		base.StartPosition = FormStartPosition.CenterParent;
		Text = notificationTitle;
		base.KeyPreview = true;
		notificationWebView.Dock = DockStyle.Fill;
		notificationWebView.Margin = Padding.Empty;
		notificationWebView.DefaultBackgroundColor = Color.FromArgb(7, 13, 20);
		base.Controls.Add(notificationWebView);
		base.KeyDown += delegate(object? _, KeyEventArgs e)
		{
			if (e.KeyCode == Keys.Escape)
			{
				base.DialogResult = DialogResult.OK;
				Close();
			}
		};
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
		await notificationWebView.EnsureCoreWebView2Async().ConfigureAwait(continueOnCapturedContext: true);
		if (!notificationWebView.IsDisposed && notificationWebView.CoreWebView2 != null)
		{
			CoreWebView2Settings settings = notificationWebView.CoreWebView2.Settings;
			settings.AreDefaultContextMenusEnabled = false;
			settings.AreDevToolsEnabled = false;
			settings.AreBrowserAcceleratorKeysEnabled = false;
			settings.IsStatusBarEnabled = false;
			settings.IsZoomControlEnabled = false;
			notificationWebView.CoreWebView2.WebMessageReceived -= NotificationWebView_WebMessageReceived;
			notificationWebView.CoreWebView2.WebMessageReceived += NotificationWebView_WebMessageReceived;
			notificationWebView.CoreWebView2.NavigationStarting -= NotificationWebView_NavigationStarting;
			notificationWebView.CoreWebView2.NavigationStarting += NotificationWebView_NavigationStarting;
		}
	}

	private void NotificationWebView_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
	{
		if (!(e.TryGetWebMessageAsString() != "close"))
		{
			base.DialogResult = DialogResult.OK;
			Close();
		}
	}

	private static void NotificationWebView_NavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
	{
		if (!e.Uri.StartsWith("about:blank", StringComparison.OrdinalIgnoreCase) && !e.Uri.StartsWith("data:text/html", StringComparison.OrdinalIgnoreCase))
		{
			e.Cancel = true;
		}
	}

	private static string BuildDocument(string title, string message)
	{
		string text = JsonSerializer.Serialize(new { title, message }, JsonOptions);
		return "<!doctype html>\r\n<html lang=\"vi\">\r\n<head>\r\n  <meta charset=\"utf-8\">\r\n  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">\r\n  <title>Denuvo Active</title>\r\n  <style>\r\n    :root {\r\n      color-scheme: dark;\r\n      font-family: \"Segoe UI\", Arial, sans-serif;\r\n      background: #070d14;\r\n      color: #f4f7fb;\r\n    }\r\n    * { box-sizing: border-box; }\r\n    body {\r\n      margin: 0;\r\n      min-height: 100vh;\r\n      background: #070d14;\r\n      color: #f4f7fb;\r\n      overflow: hidden;\r\n    }\r\n    .shell {\r\n      min-height: 100vh;\r\n      display: grid;\r\n      grid-template-rows: auto 1fr auto;\r\n      gap: 18px;\r\n      padding: 28px 30px 26px;\r\n    }\r\n    .hero {\r\n      min-height: 160px;\r\n      display: grid;\r\n      grid-template-columns: minmax(0, 1fr) 86px;\r\n      gap: 22px;\r\n      align-items: center;\r\n      padding: 22px;\r\n      border-radius: 8px;\r\n      border: 1px solid rgba(87, 166, 132, 0.48);\r\n      background: #0e2c25;\r\n    }\r\n    .kicker {\r\n      display: block;\r\n      color: #9ef2d2;\r\n      font-weight: 800;\r\n      font-size: 0.78rem;\r\n      text-transform: uppercase;\r\n      letter-spacing: 0;\r\n    }\r\n    h1 {\r\n      margin: 10px 0 0;\r\n      color: #ffffff;\r\n      font-size: 1.85rem;\r\n      line-height: 1.15;\r\n      letter-spacing: 0;\r\n    }\r\n    .summary {\r\n      margin: 12px 0 0;\r\n      max-width: 430px;\r\n      color: #cbd8ea;\r\n      font-size: 0.96rem;\r\n      line-height: 1.45;\r\n    }\r\n    .badge {\r\n      width: 76px;\r\n      height: 76px;\r\n      display: grid;\r\n      place-items: center;\r\n      border-radius: 8px;\r\n      border: 1px solid rgba(127, 239, 198, 0.36);\r\n      background: rgba(6, 42, 34, 0.85);\r\n      color: #9ef2d2;\r\n      font-size: 2.2rem;\r\n      font-weight: 800;\r\n    }\r\n    .content {\r\n      min-height: 0;\r\n      display: grid;\r\n      grid-template-rows: auto 1fr auto;\r\n      gap: 14px;\r\n      padding: 20px 22px;\r\n      border-radius: 8px;\r\n      border: 1px solid rgba(160, 178, 207, 0.22);\r\n      background: #171f2c;\r\n    }\r\n    h2 {\r\n      margin: 0;\r\n      color: #ffffff;\r\n      font-size: 1.23rem;\r\n      line-height: 1.25;\r\n      letter-spacing: 0;\r\n    }\r\n    .message {\r\n      min-height: 0;\r\n      overflow: auto;\r\n      padding-right: 4px;\r\n      color: #f5f7fb;\r\n      font-size: 1rem;\r\n      line-height: 1.55;\r\n      white-space: pre-wrap;\r\n      overflow-wrap: anywhere;\r\n    }\r\n    .hint {\r\n      margin: 0;\r\n      color: #9fb0c9;\r\n      font-size: 0.88rem;\r\n      line-height: 1.4;\r\n    }\r\n    .actions {\r\n      display: flex;\r\n      justify-content: flex-end;\r\n      min-height: 48px;\r\n    }\r\n    button {\r\n      min-width: 148px;\r\n      min-height: 46px;\r\n      border: 1px solid rgba(93, 164, 127, 0.56);\r\n      border-radius: 8px;\r\n      background: #245f48;\r\n      color: #ffffff;\r\n      font: 800 0.98rem \"Segoe UI\", Arial, sans-serif;\r\n      cursor: pointer;\r\n    }\r\n    button:hover,\r\n    button:focus-visible {\r\n      background: #2f7459;\r\n      outline: none;\r\n    }\r\n    @media (max-width: 620px) {\r\n      .shell { padding: 20px; }\r\n      .hero { grid-template-columns: 1fr; }\r\n      .badge { display: none; }\r\n      h1 { font-size: 1.55rem; }\r\n      button { width: 100%; }\r\n    }\r\n  </style>\r\n</head>\r\n<body>\r\n  <main class=\"shell\">\r\n    <section class=\"hero\">\r\n      <div>\r\n        <span class=\"kicker\">Thông báo từ launcher</span>\r\n        <h1 id=\"title\"></h1>\r\n        <p class=\"summary\">Thông báo này được gửi từ launcher để cập nhật trạng thái hoặc kết quả xử lý.</p>\r\n      </div>\r\n      <div class=\"badge\" aria-hidden=\"true\">i</div>\r\n    </section>\r\n    <section class=\"content\">\r\n      <h2>Nội dung thông báo</h2>\r\n      <div class=\"message\" id=\"message\"></div>\r\n      <p class=\"hint\">Bấm Đóng để tiếp tục sử dụng launcher.</p>\r\n    </section>\r\n    <footer class=\"actions\">\r\n      <button type=\"button\" id=\"closeButton\">Đóng</button>\r\n    </footer>\r\n  </main>\r\n  <script>\r\n    const payload = " + text + ";\r\n    document.getElementById('title').textContent = payload.title || 'Denuvo Active';\r\n    document.getElementById('message').textContent = payload.message || 'Không có nội dung thông báo.';\r\n    document.getElementById('closeButton').addEventListener('click', () => {\r\n      window.chrome?.webview?.postMessage('close');\r\n    });\r\n    document.addEventListener('keydown', (event) => {\r\n      if (event.key === 'Escape') {\r\n        window.chrome?.webview?.postMessage('close');\r\n      }\r\n    });\r\n  </script>\r\n</body>\r\n</html>";
	}
}
