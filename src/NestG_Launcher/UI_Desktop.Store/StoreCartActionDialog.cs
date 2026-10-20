using System;
using System.Drawing;
using System.Windows.Forms;

namespace UI_Desktop.Store;

internal sealed class StoreCartActionDialog : Form
{
	private readonly StoreGameDetailData detail;

	private readonly StoreProductEdition? edition;

	private readonly bool includeCheckoutHint;

	private readonly string serverMessage;

	private readonly int cartItemCount;

	private readonly Button closeButton = new Button();

	private readonly Button proceedButton = new Button();

	internal StoreCartActionDialog(StoreGameDetailData detail, StoreProductEdition? edition, bool includeCheckoutHint, string? serverMessage, int cartItemCount)
	{
		this.detail = detail;
		this.edition = edition;
		this.includeCheckoutHint = includeCheckoutHint;
		this.serverMessage = (string.IsNullOrWhiteSpace(serverMessage) ? "Giỏ hàng đã được cập nhật thành công." : serverMessage.Trim());
		this.cartItemCount = Math.Max(0, cartItemCount);
		InitializeComponent();
	}

	private void InitializeComponent()
	{
		base.SuspendLayout();
		this.Text = "Thông báo giỏ hàng";
		base.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
		base.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
		base.MaximizeBox = false;
		base.MinimizeBox = false;
		base.ShowInTaskbar = false;
		base.ClientSize = new System.Drawing.Size(640, 420);
		this.MinimumSize = new System.Drawing.Size(640, 420);
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
			Padding = new System.Windows.Forms.Padding(22),
			BackColor = System.Drawing.Color.Transparent
		};
		tableLayoutPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100f));
		tableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
		tableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100f));
		tableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 68f));
		UI_Desktop.Store.StoreUiHelper.EnableDoubleBuffer(tableLayoutPanel);
		tableLayoutPanel.Controls.Add(this.BuildHeroCard(), 0, 0);
		tableLayoutPanel.Controls.Add(this.BuildDetailsCard(), 0, 1);
		tableLayoutPanel.Controls.Add(this.BuildActionBar(), 0, 2);
		base.Controls.Add(tableLayoutPanel);
		this.closeButton.Text = "Đóng";
		this.closeButton.DialogResult = System.Windows.Forms.DialogResult.Cancel;
		this.closeButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
		this.closeButton.FlatAppearance.BorderSize = 1;
		this.closeButton.FlatAppearance.BorderColor = UI_Desktop.AppTheme.CardBorder;
		this.closeButton.BackColor = System.Drawing.Color.FromArgb(22, 30, 41);
		this.closeButton.ForeColor = UI_Desktop.AppTheme.PrimaryText;
		this.closeButton.Font = new System.Drawing.Font("Segoe UI Semibold", 10.25f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
		this.closeButton.Cursor = System.Windows.Forms.Cursors.Hand;
		this.closeButton.Size = new System.Drawing.Size(136, 46);
		this.proceedButton.Text = (this.includeCheckoutHint ? "Tiếp tục thanh toán" : "Chuyển đến giỏ hàng");
		this.proceedButton.DialogResult = System.Windows.Forms.DialogResult.OK;
		this.proceedButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
		this.proceedButton.FlatAppearance.BorderSize = 1;
		this.proceedButton.FlatAppearance.BorderColor = this.ResolveAccentColor();
		this.proceedButton.BackColor = this.ResolveAccentColor();
		this.proceedButton.ForeColor = System.Drawing.Color.White;
		this.proceedButton.Font = new System.Drawing.Font("Segoe UI Semibold", 10.25f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
		this.proceedButton.Cursor = System.Windows.Forms.Cursors.Hand;
		this.proceedButton.Size = new System.Drawing.Size(this.includeCheckoutHint ? 210 : 190, 46);
		base.CancelButton = this.closeButton;
		base.Shown += delegate
		{
			this.closeButton.Focus();
		};
		base.ResumeLayout(false);
	}

	private Control BuildHeroCard()
	{
		RoundedPanel card = CreateCard(Color.FromArgb(16, 22, 31), ResolveAccentBorderColor(), new Padding(22));
		card.AutoSize = true;
		card.AutoSizeMode = AutoSizeMode.GrowAndShrink;
		TableLayoutPanel tableLayoutPanel = new TableLayoutPanel
		{
			Dock = DockStyle.Fill,
			AutoSize = true,
			AutoSizeMode = AutoSizeMode.GrowAndShrink,
			ColumnCount = 1,
			RowCount = 4,
			BackColor = Color.Transparent,
			Margin = Padding.Empty
		};
		tableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
		tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
		tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
		tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
		tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
		Label control = new Label
		{
			AutoSize = true,
			Margin = Padding.Empty,
			ForeColor = ResolveAccentColor(),
			Font = new Font("Segoe UI Semibold", 9.75f, FontStyle.Bold, GraphicsUnit.Point),
			Text = (includeCheckoutHint ? "THANH TOÁN SẴN SÀNG" : "GIỎ HÀNG ĐÃ CẬP NHẬT")
		};
		Label titleLabel = new Label
		{
			AutoSize = true,
			Margin = new Padding(0, 8, 0, 0),
			ForeColor = AppTheme.PrimaryText,
			Font = new Font("Segoe UI Semibold", 22f, FontStyle.Bold, GraphicsUnit.Point),
			Text = (includeCheckoutHint ? "Sẵn sàng thanh toán" : "Bạn đã thêm vào giỏ hàng")
		};
		Label gameLabel = new Label
		{
			AutoSize = true,
			Margin = new Padding(0, 10, 0, 0),
			ForeColor = AppTheme.PrimaryText,
			Font = new Font("Segoe UI Semibold", 11f, FontStyle.Bold, GraphicsUnit.Point),
			Text = detail.Name
		};
		Label summaryLabel = new Label
		{
			Dock = DockStyle.Top,
			AutoSize = true,
			Margin = new Padding(0, 8, 0, 0),
			ForeColor = AppTheme.SecondaryText,
			Font = new Font("Segoe UI", 10f, FontStyle.Regular, GraphicsUnit.Point),
			Text = (includeCheckoutHint ? "Sản phẩm đã được đưa vào giỏ hàng. Bấm \"Tiếp tục thanh toán\" để mở page giỏ hàng và hoàn tất đơn mua." : "Sản phẩm đã được thêm vào giỏ hàng. Bạn có thể mở giỏ hàng ngay bây giờ hoặc tiếp tục xem thêm game.")
		};
		tableLayoutPanel.Controls.Add(control, 0, 0);
		tableLayoutPanel.Controls.Add(titleLabel, 0, 1);
		tableLayoutPanel.Controls.Add(gameLabel, 0, 2);
		tableLayoutPanel.Controls.Add(summaryLabel, 0, 3);
		card.Controls.Add(tableLayoutPanel);
		card.Resize += delegate
		{
			ApplyWidths();
		};
		ApplyWidths();
		return card;
		void ApplyWidths()
		{
			int width = Math.Max(320, card.ClientSize.Width - card.Padding.Horizontal);
			titleLabel.MaximumSize = new Size(width, 0);
			gameLabel.MaximumSize = new Size(width, 0);
			summaryLabel.MaximumSize = new Size(width, 0);
		}
	}

	private Control BuildDetailsCard()
	{
		RoundedPanel roundedPanel = CreateCard(Color.FromArgb(18, 25, 34), Color.FromArgb(46, 59, 78), new Padding(20));
		TableLayoutPanel tableLayoutPanel = new TableLayoutPanel
		{
			Dock = DockStyle.Fill,
			AutoSize = true,
			AutoSizeMode = AutoSizeMode.GrowAndShrink,
			ColumnCount = 1,
			RowCount = 6,
			BackColor = Color.Transparent,
			Margin = Padding.Empty
		};
		tableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
		tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
		tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
		tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
		tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
		tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
		tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
		StoreUiHelper.EnableDoubleBuffer(tableLayoutPanel);
		Label control = new Label
		{
			AutoSize = true,
			Margin = Padding.Empty,
			ForeColor = AppTheme.PrimaryText,
			Font = new Font("Segoe UI Semibold", 13f, FontStyle.Bold, GraphicsUnit.Point),
			Text = "Thông tin giỏ hàng"
		};
		Label control2 = new Label
		{
			AutoSize = true,
			Margin = new Padding(0, 6, 0, 16),
			ForeColor = AppTheme.SecondaryText,
			Font = new Font("Segoe UI", 9.5f, FontStyle.Regular, GraphicsUnit.Point),
			Text = (includeCheckoutHint ? "Launcher sẽ đưa bạn đến giỏ hàng để tiếp tục thanh toán." : "Bạn có thể vào giỏ hàng để xem lại sản phẩm vừa thêm.")
		};
		tableLayoutPanel.Controls.Add(control, 0, 0);
		tableLayoutPanel.Controls.Add(control2, 0, 1);
		tableLayoutPanel.Controls.Add(CreateDetailRow("Phiên bản", ResolveEditionName()), 0, 2);
		tableLayoutPanel.Controls.Add(CreateDetailRow("Giá hiện tại", ResolvePriceText()), 0, 3);
		tableLayoutPanel.Controls.Add(CreateDetailRow("Số mục trong giỏ hàng", $"{cartItemCount} mục"), 0, 4);
		tableLayoutPanel.Controls.Add(CreateDetailRow("Trạng thái", serverMessage), 0, 5);
		roundedPanel.Controls.Add(tableLayoutPanel);
		return roundedPanel;
	}

	private Control BuildActionBar()
	{
		TableLayoutPanel obj = new TableLayoutPanel
		{
			Dock = DockStyle.Fill,
			ColumnCount = 3,
			RowCount = 1,
			BackColor = Color.Transparent,
			Margin = Padding.Empty,
			Padding = new Padding(0, 10, 0, 0),
			ColumnStyles = 
			{
				new ColumnStyle(SizeType.Percent, 100f),
				new ColumnStyle(SizeType.AutoSize),
				new ColumnStyle(SizeType.AutoSize)
			},
			RowStyles = 
			{
				new RowStyle(SizeType.Percent, 100f)
			}
		};
		closeButton.Margin = new Padding(0, 0, 12, 0);
		proceedButton.Margin = Padding.Empty;
		obj.Controls.Add(closeButton, 1, 0);
		obj.Controls.Add(proceedButton, 2, 0);
		return obj;
	}

	private Control CreateDetailRow(string label, string value)
	{
		TableLayoutPanel rowLayout = new TableLayoutPanel
		{
			Dock = DockStyle.Top,
			AutoSize = true,
			AutoSizeMode = AutoSizeMode.GrowAndShrink,
			ColumnCount = 1,
			RowCount = 3,
			BackColor = Color.Transparent,
			Margin = new Padding(0, 0, 0, 14)
		};
		rowLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
		rowLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
		rowLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
		rowLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
		Label control = new Label
		{
			AutoSize = true,
			Margin = Padding.Empty,
			ForeColor = AppTheme.MutedText,
			Font = new Font("Segoe UI", 8.75f, FontStyle.Regular, GraphicsUnit.Point),
			Text = label.ToUpperInvariant()
		};
		Label valueLabel = new Label
		{
			Dock = DockStyle.Top,
			AutoSize = true,
			Margin = new Padding(0, 6, 0, 0),
			ForeColor = AppTheme.PrimaryText,
			Font = new Font("Segoe UI Semibold", 10.5f, FontStyle.Bold, GraphicsUnit.Point),
			Text = value
		};
		Panel control2 = new Panel
		{
			Dock = DockStyle.Top,
			Height = 1,
			Margin = new Padding(0, 12, 0, 0),
			BackColor = Color.FromArgb(42, 54, 72)
		};
		rowLayout.Controls.Add(control, 0, 0);
		rowLayout.Controls.Add(valueLabel, 0, 1);
		rowLayout.Controls.Add(control2, 0, 2);
		rowLayout.Resize += delegate
		{
			ApplyWidths();
		};
		ApplyWidths();
		return rowLayout;
		void ApplyWidths()
		{
			int width = Math.Max(280, rowLayout.ClientSize.Width);
			valueLabel.MaximumSize = new Size(width, 0);
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

	private string ResolveEditionName()
	{
		if (!string.IsNullOrWhiteSpace(edition?.Name))
		{
			return edition.Name.Trim();
		}
		StoreProductEdition activeEdition = detail.GetActiveEdition();
		if (!string.IsNullOrWhiteSpace(activeEdition?.Name))
		{
			return activeEdition.Name.Trim();
		}
		return "Phiên bản mặc định";
	}

	private string ResolvePriceText()
	{
		if (!string.IsNullOrWhiteSpace(edition?.CurrentPriceText))
		{
			return edition.CurrentPriceText.Trim();
		}
		if (!string.IsNullOrWhiteSpace(detail.CurrentPriceText))
		{
			return detail.CurrentPriceText.Trim();
		}
		return "Liên hệ";
	}

	private Color ResolveAccentColor()
	{
		if (!includeCheckoutHint)
		{
			return Color.FromArgb(73, 209, 164);
		}
		return Color.FromArgb(74, 131, 255);
	}

	private Color ResolveAccentBorderColor()
	{
		if (!includeCheckoutHint)
		{
			return Color.FromArgb(47, 93, 80);
		}
		return Color.FromArgb(58, 92, 154);
	}
}
