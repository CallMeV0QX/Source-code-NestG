using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using app_desktop.Services;

namespace UI_Desktop.Store;

internal sealed class StoreSupportChatPopup : Form
{
	private sealed class PopupMessage
	{
		public string? Type { get; set; }

		public string? Body { get; set; }

		public string? ClientMessageId { get; set; }

		public string? ReadyId { get; set; }
	}

	private static readonly Size PreferredClientSize = new Size(540, 720);

	private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);

	private readonly Func<string, Guid, Task> sendMessage;

	private readonly Func<Guid?, Task> markRead;

	private readonly WebView2 webView = new WebView2
	{
		Dock = DockStyle.Fill,
		DefaultBackgroundColor = Color.FromArgb(7, 16, 24)
	};

	private readonly Label rendererError = new Label
	{
		Dock = DockStyle.Fill,
		BackColor = Color.FromArgb(12, 25, 38),
		ForeColor = Color.White,
		TextAlign = ContentAlignment.MiddleCenter,
		Padding = new Padding(24),
		Visible = false
	};

	private readonly Dictionary<Guid, DesktopSupportMessageResponse> messages = new Dictionary<Guid, DesktopSupportMessageResponse>();

	private DesktopSupportConversationResponse? conversation;

	private Task? initializationTask;

	private TaskCompletionSource? readySignal;

	private Guid readyNavigationId;

	private CoreWebView2? configuredCore;

	private bool documentReady;

	private Guid? pendingReadMessageId;

	private string statusText = "Sẵn sàng kết nối";

	private bool statusIsError;

	public bool IsChatOpen
	{
		get
		{
			if (base.Visible)
			{
				return !base.IsDisposed;
			}
			return false;
		}
	}

	public StoreSupportChatPopup(Func<string, Guid, Task> sendMessage, Func<Guid?, Task> markRead)
	{
		this.sendMessage = sendMessage;
		this.markRead = markRead;
		base.AutoScaleMode = AutoScaleMode.Dpi;
		BackColor = Color.FromArgb(12, 25, 38);
		base.ClientSize = PreferredClientSize;
		base.FormBorderStyle = FormBorderStyle.None;
		base.StartPosition = FormStartPosition.Manual;
		base.ShowInTaskbar = false;
		Text = "Chat hỗ trợ";
		StoreUiHelper.ApplyWindowIcon(this);
		base.Controls.Add(webView);
		base.Controls.Add(rendererError);
		rendererError.BringToFront();
	}

	public async Task ShowChatAsync(Form owner)
	{
		if (base.IsDisposed || owner.IsDisposed)
		{
			return;
		}
		StoreUiHelper.RestoreWindowToForeground(owner);
		if (!base.Visible)
		{
			PositionBeside(owner);
			if (base.Owner == null)
			{
				Show(owner);
			}
			else
			{
				Show();
			}
		}
		BringToFront();
		Activate();
		SetStatus("Đang kết nối...");
		try
		{
			if (initializationTask == null)
			{
				initializationTask = InitializeWebViewAsync();
			}
			await initializationTask;
			if (IsChatOpen)
			{
				BringToFront();
				Activate();
				await ExecuteScriptAsync("window.__nestgSupport?.focus();");
			}
		}
		catch (Exception exception)
		{
			DesktopDiagnosticLog.Write("SupportChat", "OpenFailed", null, exception);
			initializationTask = null;
			readySignal = null;
			documentReady = false;
			rendererError.Text = "Không thể mở giao diện chat hỗ trợ.\nHãy đóng cửa sổ và thử lại.";
			rendererError.Visible = true;
			rendererError.BringToFront();
			HideChat();
			throw;
		}
	}

	public void HideChat()
	{
		if (!base.IsDisposed)
		{
			Hide();
		}
	}

	public void ClearChat()
	{
		if (!base.IsDisposed)
		{
			Hide();
			conversation = null;
			pendingReadMessageId = null;
			messages.Clear();
			statusText = "Sẵn sàng kết nối";
			statusIsError = false;
			ExecuteScriptAsync("window.__nestgSupport?.clear();");
		}
	}

	public void LoadChat(DesktopSupportConversationDetailResponse detail)
	{
		if (base.IsDisposed)
		{
			return;
		}
		conversation = detail.Conversation;
		messages.Clear();
		foreach (DesktopSupportMessageResponse message in detail.Messages)
		{
			Remember(message);
		}
		pendingReadMessageId = detail.Messages.LastOrDefault((DesktopSupportMessageResponse message) => !message.IsInternal && !message.IsDeleted)?.MessageId;
		SetStatus(detail.Conversation.StatusText);
		RenderLoadedChatAndMarkReadAsync(detail.Conversation.ConversationId);
	}

	public void Receive(DesktopSupportMessageReceivedPayload payload)
	{
		if (!IsChatOpen)
		{
			return;
		}
		Guid? guid = conversation?.ConversationId;
		Guid conversationId = payload.Message.ConversationId;
		if (guid.HasValue && !(guid.GetValueOrDefault() != conversationId) && Remember(payload.Message))
		{
			if (!documentReady)
			{
				pendingReadMessageId = payload.Message.MessageId;
			}
			else
			{
				RenderReceivedAndMarkReadAsync(payload);
			}
		}
	}

	public void UpdateConversation(DesktopSupportConversationResponse updated)
	{
		if (!base.IsDisposed && !(conversation?.ConversationId != updated.ConversationId))
		{
			conversation = updated;
			SetStatus(updated.StatusText);
			ExecuteScriptAsync("window.__nestgSupport?.updateConversation(" + Serialize(updated) + ");");
		}
	}

	public void Confirm(Guid clientMessageId, DesktopSupportMutationResponse result)
	{
		if (!base.IsDisposed)
		{
			conversation = result.Conversation;
			DesktopSupportMessageResponse supportMessage = result.SupportMessage;
			if (supportMessage != null)
			{
				Remember(supportMessage);
			}
			SetStatus("Đã gửi");
			ExecuteScriptAsync($"window.__nestgSupport?.confirmMessage({Serialize(clientMessageId.ToString("D"))}, {Serialize(result)});");
		}
	}

	public void Fail(Guid clientMessageId, string message)
	{
		if (!base.IsDisposed)
		{
			SetStatus(message, isError: true);
			ExecuteScriptAsync($"window.__nestgSupport?.failMessage({Serialize(clientMessageId.ToString("D"))}, {Serialize(message)});");
		}
	}

	public void SetError(string message)
	{
		SetStatus(message, isError: true);
	}

	protected override void OnFormClosing(FormClosingEventArgs e)
	{
		if (e.CloseReason == CloseReason.UserClosing)
		{
			e.Cancel = true;
			HideChat();
		}
		else
		{
			base.OnFormClosing(e);
		}
	}

	private void PositionBeside(Form owner)
	{
		Rectangle workingArea = Screen.FromControl(owner).WorkingArea;
		float num = Math.Max(1f, (float)owner.DeviceDpi / 96f);
		base.Size = new Size(Math.Min((int)Math.Round((float)PreferredClientSize.Width * num), Math.Max(1, workingArea.Width - 24)), Math.Min((int)Math.Round((float)PreferredClientSize.Height * num), Math.Max(1, workingArea.Height - 24)));
		base.Location = new Point(Math.Clamp(owner.Right - base.Width - 24, workingArea.Left, workingArea.Right - base.Width), Math.Clamp(owner.Bottom - base.Height - 24, workingArea.Top, workingArea.Bottom - base.Height));
	}

	private async Task InitializeWebViewAsync()
	{
		await webView.EnsureCoreWebView2Async();
		if (base.IsDisposed || webView.IsDisposed || webView.CoreWebView2 == null)
		{
			return;
		}
		CoreWebView2 coreWebView = webView.CoreWebView2;
		coreWebView.Settings.AreDefaultContextMenusEnabled = false;
		coreWebView.Settings.AreDevToolsEnabled = false;
		coreWebView.Settings.IsStatusBarEnabled = false;
		coreWebView.Settings.IsZoomControlEnabled = false;
		if (configuredCore != coreWebView)
		{
			coreWebView.WebMessageReceived += WebView_WebMessageReceived;
			coreWebView.NewWindowRequested += delegate(object? _, CoreWebView2NewWindowRequestedEventArgs e)
			{
				e.Handled = true;
			};
			coreWebView.NavigationCompleted += delegate(object? _, CoreWebView2NavigationCompletedEventArgs e)
			{
				if (!e.IsSuccess)
				{
					DesktopDiagnosticLog.Write("SupportChat", "NavigationFailed", $"WebErrorStatus={e.WebErrorStatus}; NavigationId={e.NavigationId}");
				}
			};
			configuredCore = coreWebView;
		}
		readyNavigationId = Guid.NewGuid();
		readySignal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		webView.NavigateToString(StoreSupportChatHtmlBuilder.BuildDocument(readyNavigationId));
		try
		{
			await readySignal.Task.WaitAsync(TimeSpan.FromSeconds(15.0));
		}
		catch (TimeoutException innerException)
		{
			throw new TimeoutException("Không thể hiển thị giao diện chat hỗ trợ. Vui lòng thử lại.", innerException);
		}
		readySignal = null;
		documentReady = true;
		rendererError.Visible = false;
		if (conversation != null)
		{
			await RenderCurrentChatAsync();
			await ExecuteScriptAsync($"window.__nestgSupport?.setStatus({Serialize(statusText)}, {Serialize(statusIsError)});");
			await MarkPendingReadAsync(conversation.ConversationId);
		}
		else
		{
			await ExecuteScriptAsync($"window.__nestgSupport?.setStatus({Serialize(statusText)}, {Serialize(statusIsError)});");
		}
	}

	private async Task RenderLoadedChatAndMarkReadAsync(Guid conversationId)
	{
		if (await RenderCurrentChatAsync())
		{
			await MarkPendingReadAsync(conversationId);
		}
	}

	private async Task RenderReceivedAndMarkReadAsync(DesktopSupportMessageReceivedPayload payload)
	{
		if (await ExecuteScriptAsync("window.__nestgSupport?.receiveMessage(" + Serialize(payload) + ");") && IsChatOpen)
		{
			await markRead(payload.Message.MessageId);
		}
		else
		{
			pendingReadMessageId = payload.Message.MessageId;
		}
	}

	private async Task MarkPendingReadAsync(Guid conversationId)
	{
		if (IsChatOpen)
		{
			DesktopSupportConversationResponse? desktopSupportConversationResponse = conversation;
			if (desktopSupportConversationResponse != null && !(desktopSupportConversationResponse.ConversationId != conversationId) && pendingReadMessageId.HasValue)
			{
				Guid? arg = pendingReadMessageId;
				pendingReadMessageId = null;
				await markRead(arg);
			}
		}
	}

	private Task<bool> RenderCurrentChatAsync()
	{
		if (!documentReady || conversation == null)
		{
			return Task.FromResult(result: false);
		}
		DesktopSupportConversationDetailResponse value = new DesktopSupportConversationDetailResponse
		{
			Conversation = conversation,
			Messages = messages.Values.OrderBy((DesktopSupportMessageResponse message) => message.CreatedAt).ToList()
		};
		return ExecuteScriptAsync("window.__nestgSupport?.load(" + Serialize(value) + ");");
	}

	private async Task<bool> ExecuteScriptAsync(string script)
	{
		if (!documentReady || base.IsDisposed || webView.IsDisposed || webView.CoreWebView2 == null)
		{
			return false;
		}
		try
		{
			await webView.CoreWebView2.ExecuteScriptAsync(script);
			return true;
		}
		catch
		{
			return false;
		}
	}

	private void SetStatus(string message, bool isError = false)
	{
		if (!base.IsDisposed)
		{
			statusText = (string.IsNullOrWhiteSpace(message) ? "Đang trực tuyến" : message);
			statusIsError = isError;
			ExecuteScriptAsync($"window.__nestgSupport?.setStatus({Serialize(statusText)}, {Serialize(isError)});");
		}
	}

	private bool Remember(DesktopSupportMessageResponse message)
	{
		if (message.IsInternal || message.IsDeleted || message.MessageId == Guid.Empty)
		{
			return false;
		}
		return messages.TryAdd(message.MessageId, message);
	}

	private void WebView_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
	{
		if (!string.Equals(e.Source, "about:blank", StringComparison.OrdinalIgnoreCase))
		{
			return;
		}
		PopupMessage popupMessage;
		try
		{
			popupMessage = JsonSerializer.Deserialize<PopupMessage>(e.WebMessageAsJson, JsonOptions);
		}
		catch
		{
			return;
		}
		if (popupMessage == null)
		{
			return;
		}
		if (popupMessage.Type == "supportChatReady" && Guid.TryParse(popupMessage.ReadyId, out var result) && result == readyNavigationId)
		{
			readySignal?.TrySetResult();
			return;
		}
		string type = popupMessage.Type;
		if (!(type == "closeSupportChat"))
		{
			if (type == "sendSupportMessage" && IsChatOpen && !string.IsNullOrWhiteSpace(popupMessage.Body) && Guid.TryParse(popupMessage.ClientMessageId, out var result2))
			{
				sendMessage(popupMessage.Body, result2);
			}
		}
		else
		{
			HideChat();
		}
	}

	private static string Serialize<T>(T value)
	{
		return JsonSerializer.Serialize(value, JsonOptions);
	}
}
