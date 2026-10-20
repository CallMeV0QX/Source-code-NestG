using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace UI_Desktop.Store;

internal sealed class StorePaymentSuccessDialog : Form
{
	private readonly DesktopCheckoutPaymentResponse checkout;

	private readonly Button closeButton = new Button();

	private bool isConfirmed;

	public StorePaymentSuccessDialog(DesktopCheckoutPaymentResponse checkout)
	{
		this.checkout = checkout;
		InitializeComponent();
	}

	private void InitializeComponent()
	{
		base.SuspendLayout();
		this.Text = (this.checkout.IsWalletTopUp ? "Nạp tiền thành công" : "Thanh toán thành công");
		base.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
		base.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
		base.ControlBox = false;
		base.MaximizeBox = false;
		base.MinimizeBox = false;
		base.ShowInTaskbar = false;
		base.ClientSize = new System.Drawing.Size(760, 560);
		this.MinimumSize = new System.Drawing.Size(760, 560);
		this.BackColor = UI_Desktop.AppTheme.WindowBackground;
		this.ForeColor = UI_Desktop.AppTheme.PrimaryText;
		this.Font = new System.Drawing.Font("Segoe UI", 9.75f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
		UI_Desktop.Store.StoreUiHelper.ApplyWindowIcon(this);
		UI_Desktop.Store.StoreUiHelper.EnableDoubleBuffer(this);
		System.Windows.Forms.TableLayoutPanel tableLayoutPanel = new System.Windows.Forms.TableLayoutPanel
		{
			Dock = System.Windows.Forms.DockStyle.Fill,
			ColumnCount = 1,
			RowCount = 3,
			Padding = new System.Windows.Forms.Padding(24),
			BackColor = System.Drawing.Color.Transparent
		};
		tableLayoutPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100f));
		tableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
		tableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100f));
		tableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 72f));
		UI_Desktop.Store.StoreUiHelper.EnableDoubleBuffer(tableLayoutPanel);
		tableLayoutPanel.Controls.Add(this.BuildHeroCard(), 0, 0);
		tableLayoutPanel.Controls.Add(this.BuildContentCard(), 0, 1);
		tableLayoutPanel.Controls.Add(this.BuildActionBar(), 0, 2);
		base.Controls.Add(tableLayoutPanel);
		this.closeButton.Text = "Xác Thực";
		this.closeButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
		this.closeButton.FlatAppearance.BorderSize = 1;
		this.closeButton.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(67, 136, 109);
		this.closeButton.BackColor = System.Drawing.Color.FromArgb(33, 70, 57);
		this.closeButton.ForeColor = UI_Desktop.AppTheme.PrimaryText;
		this.closeButton.Font = new System.Drawing.Font("Segoe UI Semibold", 10.5f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
		this.closeButton.Cursor = System.Windows.Forms.Cursors.Hand;
		this.closeButton.Size = new System.Drawing.Size(156, 48);
		this.closeButton.Click += delegate
		{
			this.isConfirmed = true;
			base.DialogResult = System.Windows.Forms.DialogResult.OK;
			base.Close();
		};
		base.AcceptButton = this.closeButton;
		base.CancelButton = null;
		base.Shown += delegate
		{
			this.closeButton.Focus();
		};
		base.FormClosing += delegate(object? _, System.Windows.Forms.FormClosingEventArgs eventArgs)
		{
			if (!this.isConfirmed && eventArgs.CloseReason == System.Windows.Forms.CloseReason.UserClosing)
			{
				eventArgs.Cancel = true;
			}
		};
		base.ResumeLayout(false);
	}

	private Control BuildHeroCard()
	{
		RoundedPanel card = CreateCard(Color.FromArgb(16, 24, 31), Color.FromArgb(50, 93, 77), new Padding(22));
		card.AutoSize = true;
		card.AutoSizeMode = AutoSizeMode.GrowAndShrink;
		card.MinimumSize = new Size(0, 190);
		TableLayoutPanel tableLayoutPanel = new TableLayoutPanel
		{
			Dock = DockStyle.Fill,
			AutoSize = true,
			AutoSizeMode = AutoSizeMode.GrowAndShrink,
			ColumnCount = 2,
			RowCount = 1,
			Margin = Padding.Empty,
			BackColor = Color.Transparent
		};
		tableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
		tableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112f));
		tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
		TableLayoutPanel tableLayoutPanel2 = new TableLayoutPanel
		{
			Dock = DockStyle.Fill,
			AutoSize = true,
			AutoSizeMode = AutoSizeMode.GrowAndShrink,
			ColumnCount = 1,
			RowCount = 4,
			Margin = Padding.Empty,
			BackColor = Color.Transparent
		};
		tableLayoutPanel2.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
		tableLayoutPanel2.RowStyles.Add(new RowStyle(SizeType.AutoSize));
		tableLayoutPanel2.RowStyles.Add(new RowStyle(SizeType.AutoSize));
		tableLayoutPanel2.RowStyles.Add(new RowStyle(SizeType.AutoSize));
		tableLayoutPanel2.RowStyles.Add(new RowStyle(SizeType.AutoSize));
		Label control = new Label
		{
			AutoSize = true,
			Margin = Padding.Empty,
			ForeColor = Color.FromArgb(147, 216, 188),
			Font = new Font("Segoe UI Semibold", 9.6f, FontStyle.Bold, GraphicsUnit.Point),
			Text = "SEPAY ĐÃ XÁC NHẬN"
		};
		Label titleLabel = new Label
		{
			AutoSize = true,
			Margin = new Padding(0, 10, 0, 0),
			ForeColor = AppTheme.PrimaryText,
			Font = new Font("Segoe UI Semibold", 23f, FontStyle.Bold, GraphicsUnit.Point),
			Text = (checkout.IsWalletTopUp ? "Nạp tiền thành công" : "Thanh toán thành công")
		};
		Label summaryLabel = new Label
		{
			AutoSize = true,
			Margin = new Padding(0, 12, 0, 0),
			ForeColor = AppTheme.SecondaryText,
			Font = new Font("Segoe UI", 10.2f, FontStyle.Regular, GraphicsUnit.Point),
			Text = ResolveSummaryMessage()
		};
		Label metaLabel = new Label
		{
			AutoSize = true,
			Margin = new Padding(0, 10, 0, 0),
			ForeColor = AppTheme.MutedText,
			Font = new Font("Segoe UI", 9.15f, FontStyle.Regular, GraphicsUnit.Point),
			Text = ResolveMetaText()
		};
		RoundedPanel badgePanel = new RoundedPanel
		{
			Dock = DockStyle.Top,
			Size = new Size(88, 88),
			Margin = new Padding(16, 0, 0, 0),
			SurfaceColor = Color.FromArgb(29, 62, 51),
			BorderColor = Color.FromArgb(62, 127, 102),
			BorderThickness = 1,
			CornerRadius = 28
		};
		Label value = new Label
		{
			Dock = DockStyle.Fill,
			ForeColor = Color.FromArgb(197, 250, 226),
			Font = new Font("Segoe UI Semibold", 32f, FontStyle.Bold, GraphicsUnit.Point),
			Text = "✓",
			TextAlign = ContentAlignment.MiddleCenter
		};
		badgePanel.Controls.Add(value);
		tableLayoutPanel2.Controls.Add(control, 0, 0);
		tableLayoutPanel2.Controls.Add(titleLabel, 0, 1);
		tableLayoutPanel2.Controls.Add(summaryLabel, 0, 2);
		tableLayoutPanel2.Controls.Add(metaLabel, 0, 3);
		tableLayoutPanel.Controls.Add(tableLayoutPanel2, 0, 0);
		tableLayoutPanel.Controls.Add(badgePanel, 1, 0);
		card.Controls.Add(tableLayoutPanel);
		card.Resize += delegate
		{
			ApplyHeroWidths();
		};
		ApplyHeroWidths();
		return card;
		void ApplyHeroWidths()
		{
			int num = Math.Max(420, card.ClientSize.Width - card.Padding.Horizontal);
			int width = Math.Max(320, num - badgePanel.Width - 18);
			titleLabel.MaximumSize = new Size(width, 0);
			summaryLabel.MaximumSize = new Size(width, 0);
			metaLabel.MaximumSize = new Size(width, 0);
		}
	}

	private Control BuildContentCard()
	{
		RoundedPanel roundedPanel = CreateCard(Color.FromArgb(17, 24, 34), Color.FromArgb(48, 63, 82), new Padding(18));
		Label value = new Label
		{
			Dock = DockStyle.Top,
			Height = 30,
			ForeColor = AppTheme.PrimaryText,
			Font = new Font("Segoe UI Semibold", 13.5f, FontStyle.Bold, GraphicsUnit.Point),
			Text = "Chi tiết giao dịch",
			TextAlign = ContentAlignment.MiddleLeft
		};
		StoreScrollPanel storeScrollPanel = new StoreScrollPanel
		{
			Dock = DockStyle.Fill,
			AutoScroll = true,
			BackColor = Color.Transparent,
			Margin = new Padding(0, 12, 0, 0)
		};
		StoreUiHelper.EnableDoubleBuffer(storeScrollPanel);
		TableLayoutPanel tableLayoutPanel = new TableLayoutPanel
		{
			Dock = DockStyle.Top,
			AutoSize = true,
			AutoSizeMode = AutoSizeMode.GrowAndShrink,
			ColumnCount = 1,
			Margin = Padding.Empty,
			Padding = Padding.Empty,
			BackColor = Color.Transparent
		};
		tableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
		StoreUiHelper.EnableDoubleBuffer(tableLayoutPanel);
		tableLayoutPanel.Controls.Add(CreateSectionLabel("Tổng quan"));
		tableLayoutPanel.Controls.Add(CreateInfoLine("Mã giao dịch", ResolvePaymentReference(), AppTheme.PrimaryText));
		tableLayoutPanel.Controls.Add(CreateInfoLine("Tổng thanh toán", checkout.TotalAmountText, Color.FromArgb(255, 214, 136)));
		tableLayoutPanel.Controls.Add(CreateInfoLine("Xác nhận lúc", ResolvePaidAtText(), Color.FromArgb(147, 216, 188)));
		tableLayoutPanel.Controls.Add(CreateInfoLine("Nội dung chuyển khoản", ResolveTransferContent(), AppTheme.PrimaryText));
		tableLayoutPanel.Controls.Add(CreateInfoLine("Sản phẩm", ResolveItemSummary(), AppTheme.PrimaryText));
		tableLayoutPanel.Controls.Add(CreateSectionSpacer());
		tableLayoutPanel.Controls.Add(CreateSectionLabel("Tiếp theo"));
		foreach (string item in BuildNextStepMessages())
		{
			tableLayoutPanel.Controls.Add(CreateMessageRow(item));
		}
		storeScrollPanel.Controls.Add(tableLayoutPanel);
		roundedPanel.Controls.Add(storeScrollPanel);
		roundedPanel.Controls.Add(value);
		return roundedPanel;
	}

	private Control BuildActionBar()
	{
		Panel actionPanel = new Panel
		{
			Dock = DockStyle.Fill,
			BackColor = Color.Transparent,
			Margin = Padding.Empty,
			Padding = new Padding(0, 8, 0, 0)
		};
		closeButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
		closeButton.Location = new Point(Math.Max(0, actionPanel.Width - closeButton.Width), 8);
		actionPanel.Resize += delegate
		{
			closeButton.Location = new Point(Math.Max(0, actionPanel.ClientSize.Width - closeButton.Width), Math.Max(8, (actionPanel.ClientSize.Height - closeButton.Height) / 2));
		};
		actionPanel.Controls.Add(closeButton);
		return actionPanel;
	}

	private Label CreateSectionLabel(string text)
	{
		return new Label
		{
			AutoSize = true,
			Margin = new Padding(0, 0, 0, 10),
			ForeColor = AppTheme.PrimaryText,
			Font = new Font("Segoe UI Semibold", 10.8f, FontStyle.Bold, GraphicsUnit.Point),
			Text = text
		};
	}

	private Control CreateSectionSpacer()
	{
		return new Panel
		{
			Height = 10,
			Margin = Padding.Empty,
			BackColor = Color.Transparent
		};
	}

	private Control CreateInfoLine(string caption, string value, Color valueColor)
	{
		TableLayoutPanel line = new TableLayoutPanel
		{
			AutoSize = true,
			AutoSizeMode = AutoSizeMode.GrowAndShrink,
			ColumnCount = 1,
			Margin = new Padding(0, 0, 0, 14),
			Padding = Padding.Empty,
			BackColor = Color.Transparent
		};
		line.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
		Label control = new Label
		{
			AutoSize = true,
			Margin = Padding.Empty,
			ForeColor = AppTheme.SecondaryText,
			Font = new Font("Segoe UI", 8.6f, FontStyle.Regular, GraphicsUnit.Point),
			Text = caption.ToUpperInvariant()
		};
		Label valueLabel = new Label
		{
			AutoSize = true,
			Margin = new Padding(0, 6, 0, 0),
			ForeColor = valueColor,
			Font = new Font("Segoe UI Semibold", 10.35f, FontStyle.Bold, GraphicsUnit.Point),
			Text = value
		};
		Panel separator = new Panel
		{
			Height = 1,
			Margin = new Padding(0, 12, 0, 0),
			BackColor = Color.FromArgb(43, 56, 74)
		};
		line.Controls.Add(control, 0, 0);
		line.Controls.Add(valueLabel, 0, 1);
		line.Controls.Add(separator, 0, 2);
		line.Resize += delegate
		{
			ApplyWidths();
		};
		ApplyWidths();
		return line;
		void ApplyWidths()
		{
			int width = Math.Max(420, line.ClientSize.Width);
			valueLabel.MaximumSize = new Size(width, 0);
			separator.Width = width;
		}
	}

	private Control CreateMessageRow(string message)
	{
		TableLayoutPanel row = new TableLayoutPanel
		{
			AutoSize = true,
			AutoSizeMode = AutoSizeMode.GrowAndShrink,
			ColumnCount = 2,
			Margin = new Padding(0, 0, 0, 10),
			Padding = Padding.Empty,
			BackColor = Color.Transparent
		};
		row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 18f));
		row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
		Label control = new Label
		{
			AutoSize = true,
			Margin = Padding.Empty,
			ForeColor = Color.FromArgb(82, 138, 255),
			Font = new Font("Segoe UI Semibold", 10.5f, FontStyle.Bold, GraphicsUnit.Point),
			Text = "•"
		};
		Label messageLabel = new Label
		{
			AutoSize = true,
			Margin = Padding.Empty,
			ForeColor = AppTheme.SecondaryText,
			Font = new Font("Segoe UI", 9.6f, FontStyle.Regular, GraphicsUnit.Point),
			Text = message
		};
		row.Controls.Add(control, 0, 0);
		row.Controls.Add(messageLabel, 1, 0);
		row.Resize += delegate
		{
			ApplyWidths();
		};
		ApplyWidths();
		return row;
		void ApplyWidths()
		{
			int width = Math.Max(360, row.ClientSize.Width - 18);
			messageLabel.MaximumSize = new Size(width, 0);
		}
	}

	private RoundedPanel CreateCard(Color surfaceColor, Color borderColor, Padding padding)
	{
		return new RoundedPanel
		{
			Dock = DockStyle.Fill,
			Margin = Padding.Empty,
			Padding = padding,
			SurfaceColor = surfaceColor,
			BorderColor = borderColor,
			BorderThickness = 1,
			CornerRadius = 18
		};
	}

	private string ResolveSummaryMessage()
	{
		if (!checkout.IsWalletTopUp)
		{
			return "Giao dịch " + ResolvePaymentReference() + " đã được xác nhận. Thư viện và quyền sở hữu sẽ được cập nhật ngay trong launcher.";
		}
		return "Hệ thống đã ghi nhận giao dịch SePay và cộng " + checkout.TotalAmountText + " vào ví NestG của bạn.";
	}

	private string ResolveMetaText()
	{
		List<string> list = new List<string>
		{
			"Mã giao dịch " + ResolvePaymentReference(),
			"Tổng " + checkout.TotalAmountText
		};
		string text = ResolvePaidAtText();
		if (!string.IsNullOrWhiteSpace(text))
		{
			list.Add(text);
		}
		return string.Join("  |  ", list);
	}

	private string ResolvePaidAtText()
	{
		if (!string.IsNullOrWhiteSpace(checkout.PaidAtText))
		{
			return checkout.PaidAtText.Trim();
		}
		return "Vừa xác nhận xong";
	}

	private string ResolveTransferContent()
	{
		if (!string.IsNullOrWhiteSpace(checkout.TransferContent))
		{
			return checkout.TransferContent.Trim();
		}
		return "Không có";
	}

	private string ResolvePaymentReference()
	{
		if (!string.IsNullOrWhiteSpace(checkout.TransferContent))
		{
			return checkout.TransferContent.Trim();
		}
		if (checkout.IsWalletPayment)
		{
			return "Ví NestG";
		}
		if (!string.IsNullOrWhiteSpace(checkout.PaymentTransactionCode))
		{
			return checkout.PaymentTransactionCode.Trim();
		}
		return "Không có";
	}

	private string ResolveItemSummary()
	{
		if (checkout.Items.Count == 0)
		{
			if (!checkout.IsWalletTopUp)
			{
				return "Không có sản phẩm chi tiết";
			}
			return "Nạp tiền vào ví NestG";
		}
		return string.Join(", ", checkout.Items.Select(delegate(DesktopCheckoutPaymentLineItemResponse item)
		{
			string text = (string.IsNullOrWhiteSpace(item.Name) ? "Sản phẩm" : item.Name.Trim());
			return (item.Quantity > 1) ? $"{text} x{item.Quantity}" : text;
		}));
	}

	private IReadOnlyList<string> BuildNextStepMessages()
	{
		if (checkout.IsWalletTopUp)
		{
			return new _003C_003Ez__ReadOnlyArray<string>(new string[3] { "Số dư ví NestG đã được cập nhật ngay sau khi SePay xác nhận giao dịch.", "Bạn có thể đóng cửa sổ này và quay lại giỏ hàng để thanh toán bằng ví nếu muốn.", "Nếu số dư chưa hiển thị ngay, hãy mở lại trang Tài khoản để làm mới dữ liệu." });
		}
		return new _003C_003Ez__ReadOnlyArray<string>(new string[3] { "Sản phẩm vừa mua đã được đưa vào thư viện của bạn trên launcher.", "Nếu màn hình thư viện chưa cập nhật ngay, hãy đóng popup này rồi mở lại trang Thư viện.", "Bạn có thể lưu lại mã đơn hàng để đối chiếu khi cần hỗ trợ." });
	}
}
