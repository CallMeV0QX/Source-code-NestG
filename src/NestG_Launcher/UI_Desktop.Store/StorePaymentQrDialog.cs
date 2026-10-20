using System;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.WinForms;
using app_desktop;
using app_desktop.Services;

namespace UI_Desktop.Store;

internal sealed class StorePaymentQrDialog : Form
{
	private readonly StorefrontApiClient storefrontApiClient;

	private DesktopCheckoutPaymentResponse checkout;

	private readonly bool closeAfterPaymentSuccess;

	private readonly Label statusTitleLabel = new Label();

	private readonly Label statusDescriptionLabel = new Label();

	private readonly Label orderCodeValueLabel = new Label();

	private readonly Label totalAmountValueLabel = new Label();

	private readonly Label createdAtValueLabel = new Label();

	private readonly Label expiresAtValueLabel = new Label();

	private readonly Label bankNameValueLabel = new Label();

	private readonly Label accountNumberValueLabel = new Label();

	private readonly Label accountNameValueLabel = new Label();

	private readonly Label transferContentValueLabel = new Label();

	private readonly Label subtotalValueLabel = new Label();

	private readonly Label discountValueLabel = new Label();

	private readonly Label summaryTotalValueLabel = new Label();

	private readonly Label promoCodeValueLabel = new Label();

	private readonly PictureBox qrPictureBox = new PictureBox();

	private readonly WebView2 qrFallbackWebView = new WebView2();

	private readonly Label qrLoadingLabel = new Label();

	private readonly FlowLayoutPanel itemsFlowPanel = new FlowLayoutPanel();

	private readonly Button refreshStatusButton = new Button();

	private readonly Timer statusPollTimer = new Timer
	{
		Interval = 5000
	};

	private readonly StoreLoadingOverlay loadingOverlay;

	private bool isRefreshingStatus;

	private bool paymentSuccessPopupShown;

	private int qrRequestId;

	public bool IsPaid => checkout.IsPaid;

	public StorePaymentQrDialog(StorefrontApiClient storefrontApiClient, DesktopCheckoutPaymentResponse checkout, bool closeAfterPaymentSuccess = false)
	{
		StorePaymentQrDialog storePaymentQrDialog = this;
		this.storefrontApiClient = storefrontApiClient;
		this.checkout = checkout;
		this.closeAfterPaymentSuccess = closeAfterPaymentSuccess;
		Text = (checkout.IsWalletTopUp ? "Nạp tiền SePay" : "Thanh toán SePay");
		base.StartPosition = FormStartPosition.CenterParent;
		base.FormBorderStyle = FormBorderStyle.Sizable;
		MinimumSize = new Size(1040, 780);
		base.Size = new Size(1160, 840);
		BackColor = AppTheme.WindowBackground;
		ForeColor = AppTheme.PrimaryText;
		Font = new Font("Segoe UI", 9.5f, FontStyle.Regular, GraphicsUnit.Point);
		StoreUiHelper.ApplyWindowIcon(this);
		BuildShell();
		loadingOverlay = new StoreLoadingOverlay(this);
		RenderCheckout();
		DesktopAppContext.PaymentRealtimeClient.PaymentStatusUpdated += HandlePaymentStatusUpdated;
		statusPollTimer.Tick += async delegate
		{
			await storePaymentQrDialog.RefreshStatusAsync(showErrors: false, showOverlay: false);
		};
		base.Shown += async delegate
		{
			try
			{
				await DesktopAppContext.PaymentRealtimeClient.StartAsync();
			}
			catch
			{
			}
			await storePaymentQrDialog.loadingOverlay.RunAsync("Đang chuẩn bị thanh toán", "Launcher đang tải mã QR và thông tin đơn hàng để bạn thanh toán nhanh hơn.", "Đang tải mã QR", storePaymentQrDialog.LoadQrAsync, 280);
			if (!checkout.IsPaid)
			{
				storePaymentQrDialog.statusPollTimer.Start();
			}
			storePaymentQrDialog.ShowPaymentSuccessPopupIfNeeded();
		};
		base.FormClosed += delegate
		{
			storePaymentQrDialog.statusPollTimer.Stop();
			DesktopAppContext.PaymentRealtimeClient.PaymentStatusUpdated -= storePaymentQrDialog.HandlePaymentStatusUpdated;
			storePaymentQrDialog.loadingOverlay.Dispose();
		};
	}

	private void BuildShell()
	{
		TableLayoutPanel tableLayoutPanel = new TableLayoutPanel
		{
			Dock = DockStyle.Fill,
			ColumnCount = 2,
			RowCount = 1,
			Padding = new Padding(24),
			BackColor = Color.Transparent
		};
		tableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 390f));
		tableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
		tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
		StoreUiHelper.EnableDoubleBuffer(tableLayoutPanel);
		Control control = BuildQrCard();
		Control control2 = BuildDetailsPanel();
		tableLayoutPanel.Controls.Add(control, 0, 0);
		tableLayoutPanel.Controls.Add(control2, 1, 0);
		base.Controls.Add(tableLayoutPanel);
	}

	private Control BuildQrCard()
	{
		RoundedPanel obj = new RoundedPanel
		{
			Dock = DockStyle.Fill,
			Margin = Padding.Empty,
			Padding = new Padding(20),
			SurfaceColor = Color.FromArgb(17, 24, 34),
			BorderColor = Color.FromArgb(55, 69, 89),
			BorderThickness = 1,
			CornerRadius = 16
		};
		Label value = new Label
		{
			Dock = DockStyle.Top,
			Height = 42,
			ForeColor = AppTheme.PrimaryText,
			Font = new Font("Segoe UI Semibold", 18f, FontStyle.Bold, GraphicsUnit.Point),
			TextAlign = ContentAlignment.BottomLeft,
			Text = "QR thanh toán"
		};
		statusTitleLabel.Dock = DockStyle.Top;
		statusTitleLabel.Height = 34;
		statusTitleLabel.Font = new Font("Segoe UI Semibold", 11.4f, FontStyle.Bold, GraphicsUnit.Point);
		statusDescriptionLabel.Dock = DockStyle.Top;
		statusDescriptionLabel.AutoSize = false;
		statusDescriptionLabel.Height = 64;
		statusDescriptionLabel.ForeColor = AppTheme.SecondaryText;
		statusDescriptionLabel.Font = new Font("Segoe UI", 9.2f, FontStyle.Regular, GraphicsUnit.Point);
		RoundedPanel roundedPanel = new RoundedPanel
		{
			Dock = DockStyle.Top,
			Height = 344,
			Margin = new Padding(0, 14, 0, 14),
			Padding = new Padding(18),
			SurfaceColor = Color.FromArgb(11, 16, 23),
			BorderColor = Color.FromArgb(61, 80, 106),
			BorderThickness = 1,
			CornerRadius = 14
		};
		qrPictureBox.Dock = DockStyle.Fill;
		qrPictureBox.SizeMode = PictureBoxSizeMode.Zoom;
		qrPictureBox.BackColor = Color.Transparent;
		qrFallbackWebView.Dock = DockStyle.Fill;
		qrFallbackWebView.Visible = false;
		qrFallbackWebView.AllowExternalDrop = false;
		qrFallbackWebView.CreationProperties = null;
		qrFallbackWebView.DefaultBackgroundColor = Color.FromArgb(11, 16, 23);
		qrLoadingLabel.Dock = DockStyle.Fill;
		qrLoadingLabel.TextAlign = ContentAlignment.MiddleCenter;
		qrLoadingLabel.ForeColor = AppTheme.SecondaryText;
		qrLoadingLabel.Font = new Font("Segoe UI", 10f, FontStyle.Regular, GraphicsUnit.Point);
		qrLoadingLabel.Text = "Đang tải mã QR...";
		roundedPanel.Controls.Add(qrFallbackWebView);
		roundedPanel.Controls.Add(qrPictureBox);
		roundedPanel.Controls.Add(qrLoadingLabel);
		Label label = CreateCaptionLabel("Tổng cần thanh toán");
		label.Dock = DockStyle.Top;
		label.Height = 22;
		label.Margin = new Padding(0, 0, 0, 2);
		totalAmountValueLabel.Dock = DockStyle.Top;
		totalAmountValueLabel.Height = 48;
		totalAmountValueLabel.ForeColor = Color.FromArgb(255, 214, 136);
		totalAmountValueLabel.Font = new Font("Segoe UI Semibold", 19f, FontStyle.Bold, GraphicsUnit.Point);
		RoundedPanel roundedPanel2 = new RoundedPanel
		{
			Dock = DockStyle.Top,
			Height = 134,
			Margin = new Padding(0, 14, 0, 0),
			Padding = new Padding(14, 10, 14, 10),
			SurfaceColor = Color.FromArgb(20, 27, 37),
			BorderColor = Color.FromArgb(52, 68, 88),
			BorderThickness = 1,
			CornerRadius = 12
		};
		TableLayoutPanel tableLayoutPanel = new TableLayoutPanel
		{
			Dock = DockStyle.Fill,
			ColumnCount = 2,
			RowCount = 3,
			Margin = Padding.Empty,
			Padding = Padding.Empty,
			BackColor = Color.Transparent
		};
		tableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 108f));
		tableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
		tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 36f));
		tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 36f));
		tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 36f));
		AddMetaRow(tableLayoutPanel, 0, "Mã giao dịch", orderCodeValueLabel);
		AddMetaRow(tableLayoutPanel, 1, "Tạo lúc", createdAtValueLabel);
		AddMetaRow(tableLayoutPanel, 2, "Hết hạn", expiresAtValueLabel);
		roundedPanel2.Controls.Add(tableLayoutPanel);
		obj.Controls.Add(roundedPanel2);
		obj.Controls.Add(totalAmountValueLabel);
		obj.Controls.Add(label);
		obj.Controls.Add(roundedPanel);
		obj.Controls.Add(statusDescriptionLabel);
		obj.Controls.Add(statusTitleLabel);
		obj.Controls.Add(value);
		return obj;
	}

	private Control BuildDetailsPanel()
	{
		StoreScrollPanel panel = new StoreScrollPanel
		{
			Dock = DockStyle.Fill,
			BackColor = AppTheme.WindowBackground,
			Margin = new Padding(20, 0, 0, 0),
			AutoScroll = true
		};
		StoreUiHelper.EnableDoubleBuffer(panel);
		FlowLayoutPanel stack = new FlowLayoutPanel
		{
			Dock = DockStyle.Top,
			FlowDirection = FlowDirection.TopDown,
			WrapContents = false,
			AutoSize = true,
			AutoSizeMode = AutoSizeMode.GrowAndShrink,
			Margin = Padding.Empty,
			Padding = Padding.Empty,
			BackColor = Color.Transparent
		};
		StoreUiHelper.EnableDoubleBuffer(stack);
		stack.Controls.Add(BuildReceiverCard());
		stack.Controls.Add(BuildSummaryCard());
		stack.Controls.Add(BuildItemsCard());
		stack.Controls.Add(BuildActionBar());
		panel.Controls.Add(stack);
		panel.SizeChanged += delegate
		{
			int width = Math.Max(420, panel.ClientSize.Width - (panel.VerticalScroll.Visible ? SystemInformation.VerticalScrollBarWidth : 0) - 2);
			foreach (Control control in stack.Controls)
			{
				control.Width = width;
			}
		};
		return panel;
	}

	private Control BuildReceiverCard()
	{
		RoundedPanel obj = new RoundedPanel
		{
			Height = 266,
			Margin = new Padding(0, 0, 0, 16),
			Padding = new Padding(18),
			SurfaceColor = Color.FromArgb(17, 24, 34),
			BorderColor = Color.FromArgb(55, 69, 89),
			BorderThickness = 1,
			CornerRadius = 16
		};
		Label value = new Label
		{
			Dock = DockStyle.Top,
			Height = 40,
			ForeColor = AppTheme.PrimaryText,
			Font = new Font("Segoe UI Semibold", 17f, FontStyle.Bold, GraphicsUnit.Point),
			TextAlign = ContentAlignment.BottomLeft,
			Text = "Thông tin chuyển khoản"
		};
		TableLayoutPanel tableLayoutPanel = new TableLayoutPanel
		{
			Dock = DockStyle.Fill,
			ColumnCount = 2,
			RowCount = 4,
			Margin = new Padding(0, 14, 0, 0),
			Padding = Padding.Empty,
			BackColor = Color.Transparent
		};
		tableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 156f));
		tableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
		tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42f));
		tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42f));
		tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42f));
		tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 66f));
		AddMetaRow(tableLayoutPanel, 0, "Ngân hàng", bankNameValueLabel);
		AddMetaRow(tableLayoutPanel, 1, "Số tài khoản", accountNumberValueLabel);
		AddMetaRow(tableLayoutPanel, 2, "Chủ tài khoản", accountNameValueLabel);
		AddMetaRow(tableLayoutPanel, 3, "Nội dung CK", transferContentValueLabel, allowWrap: true);
		obj.Controls.Add(tableLayoutPanel);
		obj.Controls.Add(value);
		return obj;
	}

	private Control BuildSummaryCard()
	{
		RoundedPanel obj = new RoundedPanel
		{
			Height = 224,
			Margin = new Padding(0, 0, 0, 16),
			Padding = new Padding(18),
			SurfaceColor = Color.FromArgb(17, 24, 34),
			BorderColor = Color.FromArgb(55, 69, 89),
			BorderThickness = 1,
			CornerRadius = 16
		};
		Label value = new Label
		{
			Dock = DockStyle.Top,
			Height = 40,
			ForeColor = AppTheme.PrimaryText,
			Font = new Font("Segoe UI Semibold", 17f, FontStyle.Bold, GraphicsUnit.Point),
			TextAlign = ContentAlignment.BottomLeft,
			Text = "Tổng hợp đơn hàng"
		};
		TableLayoutPanel tableLayoutPanel = new TableLayoutPanel
		{
			Dock = DockStyle.Fill,
			ColumnCount = 2,
			RowCount = 4,
			Margin = new Padding(0, 14, 0, 0),
			Padding = Padding.Empty,
			BackColor = Color.Transparent
		};
		tableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
		tableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180f));
		tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 36f));
		tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 36f));
		tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 36f));
		tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 48f));
		AddSummaryRow(tableLayoutPanel, 0, "Tạm tính", subtotalValueLabel);
		AddSummaryRow(tableLayoutPanel, 1, "Giảm giá", discountValueLabel);
		AddSummaryRow(tableLayoutPanel, 2, "Voucher", promoCodeValueLabel);
		AddSummaryRow(tableLayoutPanel, 3, "Tổng thanh toán", summaryTotalValueLabel, emphasizeValue: true);
		obj.Controls.Add(tableLayoutPanel);
		obj.Controls.Add(value);
		return obj;
	}

	private Control BuildItemsCard()
	{
		RoundedPanel roundedPanel = new RoundedPanel
		{
			Height = 250,
			Margin = new Padding(0, 0, 0, 16),
			Padding = new Padding(18),
			SurfaceColor = Color.FromArgb(17, 24, 34),
			BorderColor = Color.FromArgb(55, 69, 89),
			BorderThickness = 1,
			CornerRadius = 16
		};
		Label value = new Label
		{
			Dock = DockStyle.Top,
			Height = 40,
			ForeColor = AppTheme.PrimaryText,
			Font = new Font("Segoe UI Semibold", 17f, FontStyle.Bold, GraphicsUnit.Point),
			TextAlign = ContentAlignment.BottomLeft,
			Text = "Sản phẩm trong đơn"
		};
		StoreScrollPanel scrollPanel = new StoreScrollPanel
		{
			Dock = DockStyle.Fill,
			AutoScroll = true,
			Margin = new Padding(0, 14, 0, 0),
			BackColor = roundedPanel.SurfaceColor
		};
		itemsFlowPanel.Dock = DockStyle.Top;
		itemsFlowPanel.FlowDirection = FlowDirection.TopDown;
		itemsFlowPanel.WrapContents = false;
		itemsFlowPanel.AutoSize = true;
		itemsFlowPanel.AutoSizeMode = AutoSizeMode.GrowAndShrink;
		itemsFlowPanel.Margin = Padding.Empty;
		itemsFlowPanel.Padding = Padding.Empty;
		itemsFlowPanel.BackColor = Color.Transparent;
		StoreUiHelper.EnableDoubleBuffer(itemsFlowPanel);
		scrollPanel.Controls.Add(itemsFlowPanel);
		scrollPanel.SizeChanged += delegate
		{
			int width = Math.Max(340, scrollPanel.ClientSize.Width - (scrollPanel.VerticalScroll.Visible ? SystemInformation.VerticalScrollBarWidth : 0) - 2);
			foreach (Control control in itemsFlowPanel.Controls)
			{
				control.Width = width;
			}
		};
		roundedPanel.Controls.Add(scrollPanel);
		roundedPanel.Controls.Add(value);
		return roundedPanel;
	}

	private Control BuildActionBar()
	{
		Panel obj = new Panel
		{
			Height = 46,
			Margin = Padding.Empty,
			BackColor = Color.Transparent
		};
		refreshStatusButton.Text = "Làm mới trạng thái";
		refreshStatusButton.Size = new Size(164, 40);
		refreshStatusButton.FlatStyle = FlatStyle.Flat;
		refreshStatusButton.BackColor = Color.FromArgb(28, 38, 53);
		refreshStatusButton.ForeColor = AppTheme.PrimaryText;
		refreshStatusButton.Font = new Font("Segoe UI Semibold", 9.2f, FontStyle.Bold, GraphicsUnit.Point);
		refreshStatusButton.Cursor = Cursors.Hand;
		refreshStatusButton.FlatAppearance.BorderColor = Color.FromArgb(77, 96, 122);
		refreshStatusButton.FlatAppearance.BorderSize = 1;
		refreshStatusButton.Location = new Point(0, 0);
		refreshStatusButton.Click += async delegate
		{
			await RefreshStatusAsync();
		};
		GradientButton gradientButton = new GradientButton
		{
			Text = "Đóng",
			Width = 140,
			Height = 40,
			CornerRadius = 10,
			StartColor = Color.FromArgb(78, 95, 120),
			EndColor = Color.FromArgb(51, 67, 91),
			Font = new Font("Segoe UI Semibold", 9.6f, FontStyle.Bold, GraphicsUnit.Point),
			Location = new Point(180, 0)
		};
		gradientButton.Click += delegate
		{
			Close();
		};
		obj.Controls.Add(gradientButton);
		obj.Controls.Add(refreshStatusButton);
		return obj;
	}

	private void RenderCheckout()
	{
		Text = checkout.PageTitle + " - SePay";
		orderCodeValueLabel.Text = ResolvePaymentReference();
		totalAmountValueLabel.Text = checkout.TotalAmountText;
		createdAtValueLabel.Text = checkout.CreatedAtText;
		expiresAtValueLabel.Text = checkout.ExpiresAtText ?? "Không giới hạn";
		bankNameValueLabel.Text = FormatBankDisplayName(checkout.ReceiverBankName, checkout.ReceiverBankShortName);
		accountNumberValueLabel.Text = checkout.ReceiverAccountNumber;
		accountNameValueLabel.Text = checkout.ReceiverAccountName;
		transferContentValueLabel.Text = checkout.TransferContent;
		subtotalValueLabel.Text = checkout.SubtotalText;
		discountValueLabel.Text = ((checkout.DiscountAmount > 0m) ? ("-" + checkout.DiscountAmountText) : "0 d");
		promoCodeValueLabel.Text = (string.IsNullOrWhiteSpace(checkout.PromoCode) ? "Không áp dụng" : checkout.PromoCode);
		summaryTotalValueLabel.Text = checkout.TotalAmountText;
		statusTitleLabel.Text = checkout.StatusTitle;
		statusDescriptionLabel.Text = checkout.StatusDescription;
		ApplyStatusTheme();
		RenderItems();
	}

	private string ResolvePaymentReference()
	{
		if (!string.IsNullOrWhiteSpace(checkout.TransferContent))
		{
			return checkout.TransferContent.Trim();
		}
		if (!string.IsNullOrWhiteSpace(checkout.PaymentTransactionCode))
		{
			return checkout.PaymentTransactionCode.Trim();
		}
		return "Không có";
	}

	private void RenderItems()
	{
		itemsFlowPanel.SuspendLayout();
		foreach (Control control in itemsFlowPanel.Controls)
		{
			control.Dispose();
		}
		itemsFlowPanel.Controls.Clear();
		foreach (DesktopCheckoutPaymentLineItemResponse item in checkout.Items)
		{
			RoundedPanel itemCard = new RoundedPanel
			{
				Height = 82,
				Margin = new Padding(0, 0, 0, 10),
				Padding = new Padding(14, 10, 14, 10),
				SurfaceColor = Color.FromArgb(20, 27, 37),
				BorderColor = Color.FromArgb(52, 68, 88),
				BorderThickness = 1,
				CornerRadius = 12
			};
			Label title = new Label
			{
				Location = new Point(14, 10),
				Size = new Size(360, 24),
				AutoEllipsis = true,
				ForeColor = AppTheme.PrimaryText,
				Font = new Font("Segoe UI Semibold", 10.2f, FontStyle.Bold, GraphicsUnit.Point),
				Text = item.Name
			};
			Label subtitle = new Label
			{
				Location = new Point(14, 36),
				Size = new Size(360, 20),
				AutoEllipsis = true,
				ForeColor = AppTheme.SecondaryText,
				Font = new Font("Segoe UI", 8.7f, FontStyle.Regular, GraphicsUnit.Point),
				Text = $"{item.VersionName}  |  x{item.Quantity}"
			};
			Label lineTotal = new Label
			{
				Anchor = (AnchorStyles.Top | AnchorStyles.Right),
				AutoSize = false,
				TextAlign = ContentAlignment.MiddleRight,
				ForeColor = Color.FromArgb(255, 214, 136),
				Font = new Font("Segoe UI Semibold", 10.4f, FontStyle.Bold, GraphicsUnit.Point),
				Text = item.LineTotalText
			};
			itemCard.Controls.Add(lineTotal);
			itemCard.Controls.Add(subtitle);
			itemCard.Controls.Add(title);
			itemCard.Resize += delegate
			{
				int num = Math.Max(180, itemCard.ClientSize.Width - 28);
				int num2 = 160;
				title.Size = new Size(Math.Max(150, num - num2 - 12), 24);
				subtitle.Size = new Size(title.Width, 20);
				lineTotal.Location = new Point(itemCard.ClientSize.Width - num2 - 14, 26);
				lineTotal.Size = new Size(num2, 24);
			};
			itemCard.PerformLayout();
			itemsFlowPanel.Controls.Add(itemCard);
		}
		itemsFlowPanel.ResumeLayout();
	}

	private async Task LoadQrAsync()
	{
		qrLoadingLabel.BringToFront();
		qrLoadingLabel.Visible = true;
		qrLoadingLabel.Text = "Đang tải mã QR...";
		qrPictureBox.Image = null;
		qrFallbackWebView.Visible = false;
		int requestId = ++qrRequestId;
		if (!checkout.HasQrCode || string.IsNullOrWhiteSpace(checkout.QrImageUrl))
		{
			qrLoadingLabel.Text = "Không có mã QR cho giao dịch này.";
			qrLoadingLabel.Visible = true;
			return;
		}
		Image image = await StoreRemoteImageCache.GetAsync(checkout.QrImageUrl);
		if (base.IsDisposed || qrPictureBox.IsDisposed || requestId != qrRequestId)
		{
			return;
		}
		if (image == null)
		{
			if (!(await TryShowQrFallbackAsync(checkout.QrImageUrl, requestId)))
			{
				qrLoadingLabel.Text = "Không tải được mã QR.\nBạn vẫn có thể chuyển khoản bằng tay với thông tin bên phải.";
				qrLoadingLabel.Visible = true;
			}
		}
		else
		{
			qrPictureBox.Image = image;
			qrLoadingLabel.Visible = false;
		}
	}

	private async Task<bool> TryShowQrFallbackAsync(string qrImageUrl, int requestId)
	{
		try
		{
			await qrFallbackWebView.EnsureCoreWebView2Async();
			if (base.IsDisposed || qrFallbackWebView.IsDisposed || requestId != qrRequestId)
			{
				return false;
			}
			qrFallbackWebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
			qrFallbackWebView.CoreWebView2.Settings.AreDevToolsEnabled = false;
			qrFallbackWebView.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = false;
			qrFallbackWebView.CoreWebView2.Settings.IsZoomControlEnabled = false;
			string text = WebUtility.HtmlEncode(qrImageUrl);
			qrFallbackWebView.NavigateToString("\r\n                <!DOCTYPE html>\r\n                <html lang=\"vi\">\r\n                <head>\r\n                  <meta charset=\"utf-8\" />\r\n                  <style>\r\n                    html, body {\r\n                      margin: 0;\r\n                      width: 100%;\r\n                      height: 100%;\r\n                      overflow: hidden;\r\n                      background: #0b1017;\r\n                    }\r\n                    body {\r\n                      display: flex;\r\n                      align-items: center;\r\n                      justify-content: center;\r\n                    }\r\n                    img {\r\n                      max-width: 100%;\r\n                      max-height: 100%;\r\n                      object-fit: contain;\r\n                    }\r\n                  </style>\r\n                </head>\r\n                <body>\r\n                  <img src=\"" + text + "\" alt=\"QR SePay\" />\r\n                </body>\r\n                </html>");
			qrFallbackWebView.Visible = true;
			qrFallbackWebView.BringToFront();
			qrLoadingLabel.Visible = false;
			return true;
		}
		catch
		{
			return false;
		}
	}

	private async Task RefreshStatusAsync(bool showErrors = true, bool showOverlay = true)
	{
		if (isRefreshingStatus || checkout.IsPaid)
		{
			return;
		}
		bool wasPaid = checkout.IsPaid;
		try
		{
			_ = 1;
			try
			{
				isRefreshingStatus = true;
				if (showOverlay)
				{
					refreshStatusButton.Enabled = false;
					refreshStatusButton.Text = "Đang kiểm tra...";
					loadingOverlay.Show("Đang kiểm tra thanh toán", "Launcher đang cập nhật trạng thái đơn hàng và thông tin thanh toán mới nhất.", "Đang cập nhật trạng thái đơn hàng");
				}
				string text = await DesktopAppContext.AuthSessionManager.GetAccessTokenAsync();
				if (string.IsNullOrWhiteSpace(text))
				{
					throw new InvalidOperationException("Phiên đăng nhập đã hết hạn.");
				}
				ApplyStatus(await storefrontApiClient.GetCheckoutStatusAsync(checkout.OrderCode, text));
			}
			catch (Exception exception)
			{
				if (showErrors)
				{
					LauncherMessageBox.Show(DesktopUserMessage.FromException(exception, "Không thể kiểm tra trạng thái thanh toán. Vui lòng thử lại."), "Không thể kiểm tra trạng thái thanh toán", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
				}
			}
		}
		finally
		{
			isRefreshingStatus = false;
			refreshStatusButton.Enabled = !checkout.IsPaid;
			refreshStatusButton.Text = (checkout.IsPaid ? "Đã thanh toán" : "Làm mới trạng thái");
			if (showOverlay)
			{
				await loadingOverlay.HideAsync(220);
			}
			if (!wasPaid && checkout.IsPaid)
			{
				ShowPaymentSuccessPopupIfNeeded();
			}
		}
	}

	private void HandlePaymentStatusUpdated(object? sender, DesktopCheckoutStatusResponse status)
	{
		if (!string.Equals(status.OrderCode, checkout.OrderCode, StringComparison.OrdinalIgnoreCase) || base.IsDisposed)
		{
			return;
		}
		if (base.InvokeRequired)
		{
			BeginInvoke(delegate
			{
				HandlePaymentStatusUpdated(sender, status);
			});
			return;
		}
		bool isPaid = checkout.IsPaid;
		ApplyStatus(status);
		if (!isPaid && checkout.IsPaid)
		{
			ShowPaymentSuccessPopupIfNeeded();
		}
	}

	private void ApplyStatus(DesktopCheckoutStatusResponse status)
	{
		checkout.StatusTitle = status.StatusTitle;
		checkout.StatusDescription = status.StatusDescription;
		checkout.OrderStatus = status.OrderStatus;
		checkout.PaymentStatus = status.PaymentStatus;
		checkout.IsPaid = status.IsPaid;
		checkout.IsExpired = status.IsExpired;
		statusTitleLabel.Text = checkout.StatusTitle;
		statusDescriptionLabel.Text = checkout.StatusDescription;
		ApplyStatusTheme();
	}

	private void ShowPaymentSuccessPopupIfNeeded()
	{
		if (paymentSuccessPopupShown || !checkout.IsPaid || base.IsDisposed)
		{
			return;
		}
		paymentSuccessPopupShown = true;
		if (closeAfterPaymentSuccess)
		{
			BeginInvoke(delegate
			{
				if (!base.IsDisposed)
				{
					base.DialogResult = DialogResult.OK;
					Close();
				}
			});
			return;
		}
		using StorePaymentSuccessDialog storePaymentSuccessDialog = new StorePaymentSuccessDialog(checkout);
		storePaymentSuccessDialog.ShowDialog(this);
	}

	private void ApplyStatusTheme()
	{
		if (checkout.IsPaid)
		{
			statusTitleLabel.ForeColor = AppTheme.SuccessText;
			refreshStatusButton.Enabled = false;
			refreshStatusButton.Text = "Đã thanh toán";
			statusPollTimer.Stop();
		}
		else if (checkout.IsExpired)
		{
			statusTitleLabel.ForeColor = Color.FromArgb(255, 171, 176);
			refreshStatusButton.Enabled = true;
			refreshStatusButton.Text = "Làm mới trạng thái";
		}
		else
		{
			statusTitleLabel.ForeColor = Color.FromArgb(255, 214, 136);
			refreshStatusButton.Enabled = true;
			refreshStatusButton.Text = "Làm mới trạng thái";
		}
	}

	private static string FormatBankDisplayName(string bankName, string bankShortName)
	{
		string text = bankName?.Trim() ?? string.Empty;
		string text2 = bankShortName?.Trim() ?? string.Empty;
		if (string.IsNullOrWhiteSpace(text))
		{
			return text2;
		}
		if (string.IsNullOrWhiteSpace(text2))
		{
			return text;
		}
		if (!(NormalizeBankAliasForDisplay(text) == NormalizeBankAliasForDisplay(text2)))
		{
			return text + " (" + text2 + ")";
		}
		return text;
	}

	private static string NormalizeBankAliasForDisplay(string value)
	{
		string text = string.Concat(from character in value.Trim().ToUpperInvariant()
			where char.IsLetterOrDigit(character)
			select character);
		return text switch
		{
			"MBBANK" => "MB", 
			"MBB" => "MB", 
			"MILITARYBANK" => "MB", 
			_ => text, 
		};
	}

	private static Label CreateCaptionLabel(string text)
	{
		return new Label
		{
			ForeColor = AppTheme.SecondaryText,
			Font = new Font("Segoe UI", 8.9f, FontStyle.Regular, GraphicsUnit.Point),
			Text = text
		};
	}

	private static void AddMetaRow(TableLayoutPanel layout, int row, string labelText, Label valueLabel, bool allowWrap = false)
	{
		Label control = new Label
		{
			Dock = DockStyle.Fill,
			Margin = Padding.Empty,
			TextAlign = ContentAlignment.MiddleLeft,
			ForeColor = AppTheme.SecondaryText,
			Font = new Font("Segoe UI", 8.8f, FontStyle.Regular, GraphicsUnit.Point),
			Text = labelText
		};
		valueLabel.Dock = DockStyle.Fill;
		valueLabel.Margin = Padding.Empty;
		valueLabel.AutoEllipsis = !allowWrap;
		valueLabel.ForeColor = AppTheme.PrimaryText;
		valueLabel.Font = new Font("Segoe UI Semibold", allowWrap ? 9.6f : 9.8f, FontStyle.Bold, GraphicsUnit.Point);
		valueLabel.TextAlign = ContentAlignment.MiddleLeft;
		if (allowWrap)
		{
			valueLabel.AutoSize = false;
		}
		layout.Controls.Add(control, 0, row);
		layout.Controls.Add(valueLabel, 1, row);
	}

	private static void AddSummaryRow(TableLayoutPanel layout, int row, string labelText, Label valueLabel, bool emphasizeValue = false)
	{
		Label control = new Label
		{
			Dock = DockStyle.Fill,
			Margin = Padding.Empty,
			TextAlign = ContentAlignment.MiddleLeft,
			ForeColor = (emphasizeValue ? AppTheme.PrimaryText : AppTheme.SecondaryText),
			Font = new Font("Segoe UI", emphasizeValue ? 10f : 9f, emphasizeValue ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Point),
			Text = labelText
		};
		valueLabel.Dock = DockStyle.Fill;
		valueLabel.Margin = Padding.Empty;
		valueLabel.TextAlign = ContentAlignment.MiddleRight;
		valueLabel.ForeColor = (emphasizeValue ? Color.FromArgb(255, 214, 136) : AppTheme.PrimaryText);
		valueLabel.Font = new Font("Segoe UI Semibold", emphasizeValue ? 13.4f : 9.8f, FontStyle.Bold, GraphicsUnit.Point);
		layout.Controls.Add(control, 0, row);
		layout.Controls.Add(valueLabel, 1, row);
	}
}
