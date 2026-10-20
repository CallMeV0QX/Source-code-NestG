using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using app_desktop;
using app_desktop.Services;

namespace UI_Desktop.Store;

internal sealed class StoreCartView : UserControl
{
	private const int PreferredSummaryWidth = 356;

	private readonly StorefrontApiClient storefrontApiClient;

	private readonly Func<Task>? continueShoppingAsync;

	private readonly Func<Task>? openWalletAsync;

	private readonly TableLayoutPanel rootLayout = new TableLayoutPanel();

	private readonly Panel itemsColumnPanel = new Panel();

	private readonly StoreScrollPanel summaryColumnHostPanel = new StoreScrollPanel();

	private readonly RoundedPanel summaryColumnPanel = new RoundedPanel();

	private readonly FlowLayoutPanel itemCardsFlowPanel = new FlowLayoutPanel();

	private readonly StoreScrollPanel itemScrollPanel = new StoreScrollPanel();

	private readonly Panel summaryScrollPanel = new Panel();

	private readonly Panel summaryHost = new Panel();

	private readonly FlowLayoutPanel summaryStack = new FlowLayoutPanel();

	private readonly RoundedPanel promoShell = new RoundedPanel();

	private readonly RoundedPanel rowsCard = new RoundedPanel();

	private readonly RoundedPanel totalCard = new RoundedPanel();

	private readonly TableLayoutPanel summaryRowsTable = new TableLayoutPanel();

	private readonly Label itemHeadingLabel = new Label();

	private readonly Label itemSubHeadingLabel = new Label();

	private readonly Label statusLabel = new Label();

	private readonly TextBox promoCodeTextBox = new TextBox();

	private readonly Button applyPromoButton = new Button();

	private readonly FlowLayoutPanel promoSuggestionsFlowPanel = new FlowLayoutPanel();

	private readonly Label promoMessageLabel = new Label();

	private readonly Label subtotalValueLabel = new Label();

	private readonly Label quantityValueLabel = new Label();

	private readonly Label discountValueLabel = new Label();

	private readonly Label totalHeadingLabel = new Label();

	private readonly Label totalValueLabel = new Label();

	private readonly RoundedPanel walletCard = new RoundedPanel();

	private readonly Label walletTitleLabel = new Label();

	private readonly Label walletBalanceValueLabel = new Label();

	private readonly Label walletStatusBadgeLabel = new Label();

	private readonly Label walletHintLabel = new Label();

	private readonly GradientButton walletCheckoutButton = new GradientButton();

	private readonly Button topUpWalletButton = new Button();

	private readonly GradientButton checkoutButton = new GradientButton();

	private readonly Button continueButton = new Button();

	private DesktopCartResponse? currentCart;

	private string currentPromoCode = string.Empty;

	private bool isRefreshing;

	private bool isCreatingCheckout;

	private string currentCheckoutMethod = "sepay";

	public event Action<int>? CartCountChanged;

	public event Action<decimal, string>? WalletBalanceChanged;

	public StoreCartView(StorefrontApiClient storefrontApiClient, Func<Task>? continueShoppingAsync = null, Func<Task>? openWalletAsync = null)
	{
		this.storefrontApiClient = storefrontApiClient;
		this.continueShoppingAsync = continueShoppingAsync;
		this.openWalletAsync = openWalletAsync;
		Dock = DockStyle.Fill;
		BackColor = Color.Transparent;
		base.Margin = Padding.Empty;
		ConfigureShell();
		WireEvents();
		ShowStatus("Đang tải giỏ hàng storefront...");
	}

	public async Task RefreshCartAsync(string? promoCode = null, CancellationToken cancellationToken = default(CancellationToken))
	{
		if (isRefreshing)
		{
			return;
		}
		isRefreshing = true;
		try
		{
			string text = await DesktopAppContext.AuthSessionManager.GetAccessTokenAsync(cancellationToken);
			if (string.IsNullOrWhiteSpace(text))
			{
				currentCart = null;
				ShowStatus("Không thể tải giỏ hàng vì phiên đăng nhập đã hết hạn.");
				return;
			}
			string promoCode2 = promoCode ?? currentPromoCode;
			DesktopCartResponse desktopCartResponse = (currentCart = await storefrontApiClient.GetCartAsync(text, promoCode2, cancellationToken));
			currentPromoCode = desktopCartResponse.PromoCode;
			promoCodeTextBox.Text = desktopCartResponse.PromoCode;
			RenderCart(desktopCartResponse);
			this.CartCountChanged?.Invoke(desktopCartResponse.TotalQuantity);
			this.WalletBalanceChanged?.Invoke(desktopCartResponse.WalletBalance, desktopCartResponse.WalletBalanceText);
		}
		catch (Exception ex)
		{
			currentCart = null;
			ShowStatus(string.IsNullOrWhiteSpace(ex.Message) ? "Không thể đồng bộ giỏ hàng lúc này." : ex.Message);
		}
		finally
		{
			isRefreshing = false;
		}
	}

	private void ConfigureShell()
	{
		StoreUiHelper.EnableDoubleBuffer(this);
		StoreUiHelper.EnableDoubleBuffer(rootLayout);
		StoreUiHelper.EnableDoubleBuffer(itemCardsFlowPanel);
		StoreUiHelper.EnableDoubleBuffer(itemScrollPanel);
		StoreUiHelper.EnableDoubleBuffer(summaryStack);
		StoreUiHelper.EnableDoubleBuffer(summaryScrollPanel);
		base.Controls.Add(rootLayout);
		rootLayout.Dock = DockStyle.Fill;
		rootLayout.Margin = Padding.Empty;
		rootLayout.Padding = Padding.Empty;
		rootLayout.ColumnCount = 2;
		rootLayout.RowCount = 1;
		rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 69f));
		rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 31f));
		rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
		itemsColumnPanel.Dock = DockStyle.Fill;
		itemsColumnPanel.Margin = new Padding(0, 0, 18, 0);
		itemsColumnPanel.BackColor = Color.Transparent;
		summaryColumnHostPanel.Dock = DockStyle.Fill;
		summaryColumnHostPanel.Margin = Padding.Empty;
		summaryColumnHostPanel.Padding = Padding.Empty;
		summaryColumnHostPanel.BackColor = Color.Transparent;
		summaryColumnHostPanel.AutoScroll = true;
		summaryColumnPanel.Dock = DockStyle.None;
		summaryColumnPanel.Location = new Point(0, 0);
		summaryColumnPanel.Margin = Padding.Empty;
		summaryColumnPanel.Padding = new Padding(18, 18, 18, 22);
		summaryColumnPanel.SurfaceColor = Color.FromArgb(15, 21, 29);
		summaryColumnPanel.BorderColor = Color.FromArgb(51, 66, 86);
		summaryColumnPanel.BorderThickness = 1;
		summaryColumnPanel.CornerRadius = 12;
		rootLayout.Controls.Add(itemsColumnPanel, 0, 0);
		rootLayout.Controls.Add(summaryColumnHostPanel, 1, 0);
		summaryColumnHostPanel.Controls.Add(summaryColumnPanel);
		ConfigureItemsColumn();
		ConfigureSummaryColumn();
	}

	private void ConfigureItemsColumn()
	{
		RoundedPanel roundedPanel = new RoundedPanel
		{
			Dock = DockStyle.Top,
			Height = 100,
			Margin = Padding.Empty,
			Padding = new Padding(20, 14, 20, 12),
			SurfaceColor = Color.FromArgb(17, 24, 34),
			BorderColor = Color.FromArgb(55, 69, 89),
			BorderThickness = 1,
			CornerRadius = 12
		};
		TableLayoutPanel tableLayoutPanel = new TableLayoutPanel
		{
			Dock = DockStyle.Fill,
			ColumnCount = 1,
			RowCount = 2,
			Margin = Padding.Empty,
			Padding = Padding.Empty,
			BackColor = Color.Transparent
		};
		tableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
		tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 40f));
		tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 26f));
		itemHeadingLabel.AutoSize = false;
		itemHeadingLabel.Dock = DockStyle.Fill;
		itemHeadingLabel.Margin = Padding.Empty;
		itemHeadingLabel.ForeColor = AppTheme.PrimaryText;
		itemHeadingLabel.Font = new Font("Segoe UI Semibold", 18f, FontStyle.Bold, GraphicsUnit.Point);
		itemHeadingLabel.TextAlign = ContentAlignment.BottomLeft;
		itemHeadingLabel.Text = "Giỏ hàng";
		itemSubHeadingLabel.AutoSize = false;
		itemSubHeadingLabel.Dock = DockStyle.Fill;
		itemSubHeadingLabel.Margin = new Padding(0, 1, 0, 0);
		itemSubHeadingLabel.ForeColor = AppTheme.SecondaryText;
		itemSubHeadingLabel.Font = new Font("Segoe UI", 9.4f, FontStyle.Regular, GraphicsUnit.Point);
		itemSubHeadingLabel.TextAlign = ContentAlignment.MiddleLeft;
		itemSubHeadingLabel.Text = "Danh sách game và version đã chọn";
		tableLayoutPanel.Controls.Add(itemHeadingLabel, 0, 0);
		tableLayoutPanel.Controls.Add(itemSubHeadingLabel, 0, 1);
		roundedPanel.Controls.Add(tableLayoutPanel);
		statusLabel.AutoSize = true;
		statusLabel.ForeColor = Color.FromArgb(166, 182, 203);
		statusLabel.Font = new Font("Segoe UI", 9f, FontStyle.Regular, GraphicsUnit.Point);
		statusLabel.Location = new Point(0, 104);
		statusLabel.Margin = new Padding(0, 10, 0, 10);
		itemScrollPanel.Dock = DockStyle.Fill;
		itemScrollPanel.BackColor = Color.Transparent;
		itemScrollPanel.AutoScroll = true;
		itemScrollPanel.Location = new Point(0, 130);
		itemCardsFlowPanel.Dock = DockStyle.Top;
		itemCardsFlowPanel.FlowDirection = FlowDirection.TopDown;
		itemCardsFlowPanel.WrapContents = false;
		itemCardsFlowPanel.AutoSize = true;
		itemCardsFlowPanel.AutoSizeMode = AutoSizeMode.GrowAndShrink;
		itemCardsFlowPanel.Margin = Padding.Empty;
		itemCardsFlowPanel.Padding = new Padding(0, 6, 0, 0);
		itemScrollPanel.Controls.Add(itemCardsFlowPanel);
		itemsColumnPanel.Controls.Add(itemScrollPanel);
		itemsColumnPanel.Controls.Add(statusLabel);
		itemsColumnPanel.Controls.Add(roundedPanel);
	}

	private void ConfigureSummaryColumn()
	{
		summaryScrollPanel.Dock = DockStyle.Fill;
		summaryScrollPanel.AutoScroll = false;
		summaryScrollPanel.BackColor = Color.Transparent;
		summaryHost.AutoSize = true;
		summaryHost.AutoSizeMode = AutoSizeMode.GrowAndShrink;
		summaryHost.BackColor = Color.Transparent;
		summaryStack.FlowDirection = FlowDirection.TopDown;
		summaryStack.WrapContents = false;
		summaryStack.AutoSize = true;
		summaryStack.AutoSizeMode = AutoSizeMode.GrowAndShrink;
		summaryStack.Margin = Padding.Empty;
		summaryStack.Padding = Padding.Empty;
		summaryStack.BackColor = Color.Transparent;
		summaryHost.Controls.Add(summaryStack);
		summaryScrollPanel.Controls.Add(summaryHost);
		summaryColumnPanel.Controls.Add(summaryScrollPanel);
		Label value = new Label
		{
			AutoSize = true,
			ForeColor = AppTheme.PrimaryText,
			Font = new Font("Segoe UI Semibold", 16.4f, FontStyle.Bold, GraphicsUnit.Point),
			Text = "Tạm tính",
			Margin = new Padding(0, 0, 0, 14)
		};
		Label value2 = new Label
		{
			AutoSize = true,
			ForeColor = AppTheme.SecondaryText,
			Font = new Font("Segoe UI", 8.9f, FontStyle.Regular, GraphicsUnit.Point),
			Text = "Mã giảm giá",
			Margin = new Padding(0, 0, 0, 6)
		};
		promoShell.Height = 46;
		promoShell.SurfaceColor = Color.FromArgb(22, 31, 43);
		promoShell.BorderColor = Color.FromArgb(61, 80, 106);
		promoShell.BorderThickness = 1;
		promoShell.CornerRadius = 8;
		promoShell.Margin = new Padding(0, 0, 0, 12);
		TableLayoutPanel tableLayoutPanel = new TableLayoutPanel
		{
			Dock = DockStyle.Fill,
			ColumnCount = 2,
			RowCount = 1,
			Padding = new Padding(10, 6, 6, 6),
			Margin = Padding.Empty,
			BackColor = Color.Transparent
		};
		tableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
		tableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 116f));
		tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
		promoCodeTextBox.BorderStyle = BorderStyle.None;
		promoCodeTextBox.BackColor = promoShell.SurfaceColor;
		promoCodeTextBox.ForeColor = AppTheme.PrimaryText;
		promoCodeTextBox.Font = new Font("Segoe UI Semibold", 9.4f, FontStyle.Bold, GraphicsUnit.Point);
		promoCodeTextBox.Dock = DockStyle.Fill;
		promoCodeTextBox.Margin = new Padding(0, 8, 10, 6);
		applyPromoButton.Text = "Áp dụng";
		applyPromoButton.Dock = DockStyle.Fill;
		applyPromoButton.FlatStyle = FlatStyle.Flat;
		applyPromoButton.BackColor = Color.FromArgb(36, 51, 71);
		applyPromoButton.ForeColor = AppTheme.PrimaryText;
		applyPromoButton.Font = new Font("Segoe UI Semibold", 8.9f, FontStyle.Bold, GraphicsUnit.Point);
		applyPromoButton.Cursor = Cursors.Hand;
		applyPromoButton.Margin = Padding.Empty;
		applyPromoButton.FlatAppearance.BorderColor = Color.FromArgb(83, 105, 135);
		applyPromoButton.FlatAppearance.BorderSize = 1;
		tableLayoutPanel.Controls.Add(promoCodeTextBox, 0, 0);
		tableLayoutPanel.Controls.Add(applyPromoButton, 1, 0);
		promoShell.Controls.Add(tableLayoutPanel);
		promoSuggestionsFlowPanel.FlowDirection = FlowDirection.TopDown;
		promoSuggestionsFlowPanel.WrapContents = false;
		promoSuggestionsFlowPanel.AutoSize = true;
		promoSuggestionsFlowPanel.AutoSizeMode = AutoSizeMode.GrowAndShrink;
		promoSuggestionsFlowPanel.Margin = new Padding(0, 0, 0, 10);
		promoSuggestionsFlowPanel.Padding = Padding.Empty;
		promoMessageLabel.AutoSize = true;
		promoMessageLabel.ForeColor = AppTheme.SecondaryText;
		promoMessageLabel.Font = new Font("Segoe UI", 8.8f, FontStyle.Regular, GraphicsUnit.Point);
		promoMessageLabel.Margin = new Padding(0, 2, 0, 12);
		rowsCard.Height = 154;
		rowsCard.Padding = new Padding(14, 10, 14, 10);
		rowsCard.SurfaceColor = Color.FromArgb(20, 27, 37);
		rowsCard.BorderColor = Color.FromArgb(52, 68, 88);
		rowsCard.BorderThickness = 1;
		rowsCard.CornerRadius = 10;
		rowsCard.Margin = new Padding(0, 0, 0, 12);
		summaryRowsTable.Dock = DockStyle.Fill;
		summaryRowsTable.ColumnCount = 2;
		summaryRowsTable.RowCount = 3;
		summaryRowsTable.Margin = Padding.Empty;
		summaryRowsTable.Padding = Padding.Empty;
		summaryRowsTable.BackColor = Color.Transparent;
		summaryRowsTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
		summaryRowsTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 126f));
		summaryRowsTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 42f));
		summaryRowsTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 42f));
		summaryRowsTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 42f));
		ConfigureSummaryValueLabel(subtotalValueLabel);
		ConfigureSummaryValueLabel(quantityValueLabel);
		ConfigureSummaryValueLabel(discountValueLabel);
		AddSummaryRow(0, "Tạm tính", subtotalValueLabel);
		AddSummaryRow(1, "Số lượng", quantityValueLabel);
		AddSummaryRow(2, "Giảm giá", discountValueLabel);
		rowsCard.Controls.Add(summaryRowsTable);
		totalCard.Height = 102;
		totalCard.Padding = new Padding(14, 12, 14, 12);
		totalCard.SurfaceColor = Color.FromArgb(28, 20, 25);
		totalCard.BorderColor = Color.FromArgb(138, 56, 65);
		totalCard.BorderThickness = 1;
		totalCard.CornerRadius = 10;
		totalCard.Margin = new Padding(0, 0, 0, 12);
		TableLayoutPanel tableLayoutPanel2 = new TableLayoutPanel
		{
			Dock = DockStyle.Fill,
			ColumnCount = 1,
			RowCount = 2,
			Margin = Padding.Empty,
			Padding = Padding.Empty,
			BackColor = Color.Transparent
		};
		tableLayoutPanel2.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
		tableLayoutPanel2.RowStyles.Add(new RowStyle(SizeType.Absolute, 28f));
		tableLayoutPanel2.RowStyles.Add(new RowStyle(SizeType.Absolute, 44f));
		totalHeadingLabel.AutoSize = false;
		totalHeadingLabel.Dock = DockStyle.Fill;
		totalHeadingLabel.Margin = Padding.Empty;
		totalHeadingLabel.ForeColor = AppTheme.PrimaryText;
		totalHeadingLabel.Font = new Font("Segoe UI Semibold", 10.8f, FontStyle.Bold, GraphicsUnit.Point);
		totalHeadingLabel.TextAlign = ContentAlignment.MiddleLeft;
		totalHeadingLabel.Text = "Tổng thanh toán";
		totalValueLabel.AutoSize = false;
		totalValueLabel.Dock = DockStyle.Fill;
		totalValueLabel.Margin = Padding.Empty;
		totalValueLabel.ForeColor = Color.FromArgb(255, 214, 136);
		totalValueLabel.Font = new Font("Segoe UI Semibold", 15.6f, FontStyle.Bold, GraphicsUnit.Point);
		totalValueLabel.Text = "0 d";
		totalValueLabel.TextAlign = ContentAlignment.MiddleLeft;
		tableLayoutPanel2.Controls.Add(totalHeadingLabel, 0, 0);
		tableLayoutPanel2.Controls.Add(totalValueLabel, 0, 1);
		totalCard.Controls.Add(tableLayoutPanel2);
		walletCard.Height = 168;
		walletCard.Padding = new Padding(16, 14, 16, 14);
		walletCard.SurfaceColor = Color.FromArgb(18, 31, 36);
		walletCard.BorderColor = Color.FromArgb(54, 92, 87);
		walletCard.BorderThickness = 1;
		walletCard.CornerRadius = 10;
		walletCard.Margin = new Padding(0, 0, 0, 12);
		walletCard.Resize += delegate
		{
			LayoutWalletCard();
		};
		walletTitleLabel.AutoSize = false;
		walletTitleLabel.ForeColor = AppTheme.SecondaryText;
		walletTitleLabel.Font = new Font("Segoe UI", 8.8f, FontStyle.Regular, GraphicsUnit.Point);
		walletTitleLabel.Text = "Ví NestG";
		walletTitleLabel.TextAlign = ContentAlignment.MiddleLeft;
		walletBalanceValueLabel.AutoSize = false;
		walletBalanceValueLabel.ForeColor = Color.FromArgb(181, 244, 121);
		walletBalanceValueLabel.Font = new Font("Segoe UI Semibold", 16.2f, FontStyle.Bold, GraphicsUnit.Point);
		walletBalanceValueLabel.Text = "0 d";
		walletBalanceValueLabel.TextAlign = ContentAlignment.MiddleLeft;
		walletStatusBadgeLabel.AutoSize = false;
		walletStatusBadgeLabel.Padding = new Padding(10, 5, 10, 5);
		walletStatusBadgeLabel.ForeColor = Color.FromArgb(201, 244, 215);
		walletStatusBadgeLabel.BackColor = Color.FromArgb(35, 74, 58);
		walletStatusBadgeLabel.Font = new Font("Segoe UI Semibold", 8.2f, FontStyle.Bold, GraphicsUnit.Point);
		walletStatusBadgeLabel.Text = "VÍ SẴN SÀNG";
		walletStatusBadgeLabel.TextAlign = ContentAlignment.MiddleCenter;
		walletHintLabel.AutoSize = false;
		walletHintLabel.ForeColor = AppTheme.SecondaryText;
		walletHintLabel.Font = new Font("Segoe UI", 8.8f, FontStyle.Regular, GraphicsUnit.Point);
		walletHintLabel.Text = "Đồng bộ số dư ví để thanh toán nhanh hơn.";
		walletHintLabel.TextAlign = ContentAlignment.TopLeft;
		walletCard.Controls.Add(walletHintLabel);
		walletCard.Controls.Add(walletStatusBadgeLabel);
		walletCard.Controls.Add(walletBalanceValueLabel);
		walletCard.Controls.Add(walletTitleLabel);
		LayoutWalletCard();
		walletCheckoutButton.Text = "Thanh toán bằng ví";
		walletCheckoutButton.Height = 50;
		walletCheckoutButton.CornerRadius = 10;
		walletCheckoutButton.StartColor = Color.FromArgb(69, 169, 124);
		walletCheckoutButton.EndColor = Color.FromArgb(37, 116, 79);
		walletCheckoutButton.Font = new Font("Segoe UI Semibold", 10.2f, FontStyle.Bold, GraphicsUnit.Point);
		walletCheckoutButton.Margin = new Padding(0, 0, 0, 10);
		topUpWalletButton.Text = "Nạp thêm vào ví";
		topUpWalletButton.Height = 42;
		topUpWalletButton.FlatStyle = FlatStyle.Flat;
		topUpWalletButton.BackColor = Color.FromArgb(22, 34, 47);
		topUpWalletButton.ForeColor = AppTheme.PrimaryText;
		topUpWalletButton.Font = new Font("Segoe UI Semibold", 9.2f, FontStyle.Bold, GraphicsUnit.Point);
		topUpWalletButton.Cursor = Cursors.Hand;
		topUpWalletButton.FlatAppearance.BorderColor = Color.FromArgb(76, 101, 117);
		topUpWalletButton.FlatAppearance.BorderSize = 1;
		topUpWalletButton.Margin = new Padding(0, 0, 0, 10);
		checkoutButton.Text = "Thanh toán với SePay";
		checkoutButton.Height = 50;
		checkoutButton.CornerRadius = 10;
		checkoutButton.StartColor = Color.FromArgb(224, 56, 62);
		checkoutButton.EndColor = Color.FromArgb(171, 22, 32);
		checkoutButton.Font = new Font("Segoe UI Semibold", 10.2f, FontStyle.Bold, GraphicsUnit.Point);
		checkoutButton.Margin = new Padding(0, 0, 0, 10);
		continueButton.Text = "Tiếp tục mua sắm";
		continueButton.Height = 42;
		continueButton.FlatStyle = FlatStyle.Flat;
		continueButton.BackColor = Color.FromArgb(28, 38, 53);
		continueButton.ForeColor = AppTheme.PrimaryText;
		continueButton.Font = new Font("Segoe UI Semibold", 9.3f, FontStyle.Bold, GraphicsUnit.Point);
		continueButton.Cursor = Cursors.Hand;
		continueButton.FlatAppearance.BorderColor = Color.FromArgb(77, 96, 122);
		continueButton.FlatAppearance.BorderSize = 1;
		continueButton.Margin = Padding.Empty;
		summaryStack.Controls.Add(value);
		summaryStack.Controls.Add(value2);
		summaryStack.Controls.Add(promoShell);
		summaryStack.Controls.Add(promoSuggestionsFlowPanel);
		summaryStack.Controls.Add(promoMessageLabel);
		summaryStack.Controls.Add(rowsCard);
		summaryStack.Controls.Add(totalCard);
		summaryStack.Controls.Add(walletCard);
		summaryStack.Controls.Add(walletCheckoutButton);
		summaryStack.Controls.Add(topUpWalletButton);
		summaryStack.Controls.Add(checkoutButton);
		summaryStack.Controls.Add(continueButton);
		UpdateSummaryLayout();
	}

	private static void ConfigureSummaryValueLabel(Label label)
	{
		label.AutoSize = false;
		label.Dock = DockStyle.Fill;
		label.TextAlign = ContentAlignment.MiddleRight;
		label.ForeColor = AppTheme.PrimaryText;
		label.Font = new Font("Segoe UI Semibold", 9.8f, FontStyle.Bold, GraphicsUnit.Point);
		label.Text = "--";
		label.Margin = Padding.Empty;
	}

	private void LayoutWalletCard()
	{
		if (walletCard.ClientSize.Width > 0)
		{
			int num = Math.Max(180, walletCard.ClientSize.Width - 32);
			Size size = MeasureBadgeSize(walletStatusBadgeLabel.Text, walletStatusBadgeLabel.Font, walletStatusBadgeLabel.Padding);
			walletStatusBadgeLabel.Size = size;
			walletStatusBadgeLabel.Location = new Point(Math.Max(16, walletCard.ClientSize.Width - 16 - size.Width), 16);
			int width = Math.Max(120, num - size.Width - 10);
			walletTitleLabel.Location = new Point(16, 18);
			walletTitleLabel.Size = new Size(width, MeasureTextHeight(walletTitleLabel.Text, walletTitleLabel.Font, width, allowWrap: false));
			int y = Math.Max(walletTitleLabel.Bottom + 12, walletStatusBadgeLabel.Bottom + 12);
			walletBalanceValueLabel.Location = new Point(16, y);
			walletBalanceValueLabel.Size = new Size(num, MeasureTextHeight(walletBalanceValueLabel.Text, walletBalanceValueLabel.Font, num));
			walletHintLabel.Location = new Point(16, walletBalanceValueLabel.Bottom + 10);
			walletHintLabel.Size = new Size(num, Math.Max(36, MeasureTextHeight(walletHintLabel.Text, walletHintLabel.Font, num)));
			walletCard.Height = walletHintLabel.Bottom + 16;
		}
	}

	private static Size MeasureBadgeSize(string text, Font font, Padding padding)
	{
		Size size = TextRenderer.MeasureText(text, font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
		return new Size(size.Width + padding.Horizontal, size.Height + padding.Vertical);
	}

	private static int MeasureTextHeight(string text, Font font, int width, bool allowWrap = true)
	{
		TextFormatFlags textFormatFlags = TextFormatFlags.NoPadding;
		textFormatFlags = (TextFormatFlags)((int)textFormatFlags | (allowWrap ? 16 : 32));
		Size size = TextRenderer.MeasureText(string.IsNullOrWhiteSpace(text) ? " " : text, font, new Size(Math.Max(1, width), int.MaxValue), textFormatFlags);
		return Math.Max(font.Height + 2, size.Height);
	}

	private void AddSummaryRow(int rowIndex, string labelText, Label valueLabel)
	{
		Label control = new Label
		{
			AutoSize = false,
			Dock = DockStyle.Fill,
			TextAlign = ContentAlignment.MiddleLeft,
			ForeColor = AppTheme.SecondaryText,
			Font = new Font("Segoe UI", 9f, FontStyle.Regular, GraphicsUnit.Point),
			Text = labelText,
			Margin = Padding.Empty
		};
		summaryRowsTable.Controls.Add(control, 0, rowIndex);
		summaryRowsTable.Controls.Add(valueLabel, 1, rowIndex);
	}

	private void WireEvents()
	{
		itemScrollPanel.SizeChanged += delegate
		{
			UpdateItemCardWidths();
		};
		summaryColumnHostPanel.SizeChanged += delegate
		{
			UpdateSummaryLayout();
		};
		summaryScrollPanel.SizeChanged += delegate
		{
			UpdateSummaryLayout();
		};
		promoCodeTextBox.KeyDown += async delegate(object? _, KeyEventArgs eventArgs)
		{
			if (eventArgs.KeyCode == Keys.Return)
			{
				eventArgs.SuppressKeyPress = true;
				await RefreshCartAsync(promoCodeTextBox.Text.Trim());
			}
		};
		applyPromoButton.Click += async delegate
		{
			await RefreshCartAsync(promoCodeTextBox.Text.Trim());
		};
		walletCheckoutButton.Click += async delegate
		{
			await HandleCheckoutAsync("wallet");
		};
		topUpWalletButton.Click += async delegate
		{
			await OpenWalletAsync();
		};
		checkoutButton.Click += async delegate
		{
			await HandleCheckoutAsync("sepay");
		};
		continueButton.Click += async delegate
		{
			if (continueShoppingAsync != null)
			{
				await continueShoppingAsync();
			}
		};
	}

	private void RenderCart(DesktopCartResponse cart)
	{
		itemHeadingLabel.Text = (cart.IsEmpty ? "Giỏ hàng trống" : "Giỏ hàng");
		itemSubHeadingLabel.Text = (cart.IsEmpty ? "Chưa có sản phẩm nào trong giỏ" : $"Có {cart.TotalQuantity} sản phẩm ({cart.DistinctItemCount} dòng)");
		RenderItemCards(cart);
		RenderPromoSuggestions(cart);
		RenderSummary(cart);
		if (cart.PromoApplied)
		{
			promoMessageLabel.ForeColor = Color.FromArgb(179, 236, 198);
		}
		else if (cart.PromoInvalid)
		{
			promoMessageLabel.ForeColor = Color.FromArgb(255, 186, 190);
		}
		else
		{
			promoMessageLabel.ForeColor = AppTheme.SecondaryText;
		}
		promoMessageLabel.Text = (string.IsNullOrWhiteSpace(cart.PromoMessage) ? "Nhập mã giảm giá để tính lại tổng thanh toán." : (string.IsNullOrWhiteSpace(cart.PromoDescription) ? cart.PromoMessage : (cart.PromoMessage + "\n" + cart.PromoDescription)));
		statusLabel.Text = (cart.IsEmpty ? "Giỏ hàng hiện trống. Bạn có thể quay lại cửa hàng để thêm sản phẩm." : "Dữ liệu giỏ hàng đang đồng bộ theo logic web storefront.");
		UpdateSummaryLayout();
	}

	private void RenderItemCards(DesktopCartResponse cart)
	{
		itemCardsFlowPanel.SuspendLayout();
		foreach (Control control in itemCardsFlowPanel.Controls)
		{
			control.Dispose();
		}
		itemCardsFlowPanel.Controls.Clear();
		if (cart.IsEmpty)
		{
			RoundedPanel roundedPanel = new RoundedPanel
			{
				Width = Math.Max(700, itemScrollPanel.ClientSize.Width - 6),
				Height = 162,
				SurfaceColor = Color.FromArgb(17, 24, 34),
				BorderColor = Color.FromArgb(55, 69, 89),
				BorderThickness = 1,
				CornerRadius = 12
			};
			roundedPanel.Controls.Add(new Label
			{
				AutoSize = true,
				MaximumSize = new Size(roundedPanel.Width - 40, 0),
				ForeColor = AppTheme.SecondaryText,
				Font = new Font("Segoe UI", 10.2f, FontStyle.Regular, GraphicsUnit.Point),
				Text = "Chưa có sản phẩm nào trong giỏ hàng. Hãy quay lại cửa hàng và thêm game bạn muốn mua.",
				Location = new Point(18, 22)
			});
			itemCardsFlowPanel.Controls.Add(roundedPanel);
		}
		else
		{
			foreach (DesktopCartLineItemResponse item in cart.Items)
			{
				StoreCartItemCard storeCartItemCard = new StoreCartItemCard(item);
				storeCartItemCard.QuantityUpdateRequested += HandleQuantityUpdateAsync;
				storeCartItemCard.RemoveRequested += HandleRemoveItemAsync;
				itemCardsFlowPanel.Controls.Add(storeCartItemCard);
			}
		}
		itemCardsFlowPanel.ResumeLayout();
		UpdateItemCardWidths();
	}

	private void RenderPromoSuggestions(DesktopCartResponse cart)
	{
		promoSuggestionsFlowPanel.SuspendLayout();
		foreach (Control control in promoSuggestionsFlowPanel.Controls)
		{
			control.Dispose();
		}
		promoSuggestionsFlowPanel.Controls.Clear();
		if (cart.PromoSuggestions.Count == 0)
		{
			promoSuggestionsFlowPanel.Controls.Add(new Label
			{
				AutoSize = true,
				MaximumSize = new Size(356, 0),
				ForeColor = AppTheme.SecondaryText,
				Font = new Font("Segoe UI", 8.6f, FontStyle.Regular, GraphicsUnit.Point),
				Text = "Chưa có voucher phù hợp cho giỏ hàng hiện tại.",
				Margin = new Padding(0, 0, 0, 8)
			});
		}
		else
		{
			foreach (DesktopCartPromoSuggestionResponse promoSuggestion in cart.PromoSuggestions)
			{
				Button button = new Button
				{
					Height = 54,
					Text = promoSuggestion.Code + "  |  " + promoSuggestion.Title,
					FlatStyle = FlatStyle.Flat,
					BackColor = (promoSuggestion.IsApplied ? Color.FromArgb(50, 34, 39) : Color.FromArgb(22, 31, 43)),
					ForeColor = (promoSuggestion.IsApplied ? Color.FromArgb(255, 224, 226) : AppTheme.PrimaryText),
					Font = new Font("Segoe UI Semibold", 8.9f, FontStyle.Bold, GraphicsUnit.Point),
					TextAlign = ContentAlignment.MiddleLeft,
					Padding = new Padding(14, 0, 14, 0),
					Cursor = Cursors.Hand,
					Margin = new Padding(0, 0, 0, 8),
					Tag = promoSuggestion.Code
				};
				button.FlatAppearance.BorderColor = (promoSuggestion.IsApplied ? Color.FromArgb(198, 64, 74) : Color.FromArgb(71, 89, 115));
				button.FlatAppearance.BorderSize = 1;
				button.Click += async delegate
				{
					await RefreshCartAsync((string)button.Tag);
				};
				promoSuggestionsFlowPanel.Controls.Add(button);
			}
		}
		promoSuggestionsFlowPanel.ResumeLayout();
		UpdateSummaryLayout();
	}

	private void RenderSummary(DesktopCartResponse cart)
	{
		subtotalValueLabel.Text = cart.SubtotalText;
		quantityValueLabel.Text = cart.TotalQuantity.ToString();
		discountValueLabel.Text = ((cart.DiscountAmount > 0m) ? ("-" + cart.DiscountAmountText) : "0 d");
		totalValueLabel.Text = cart.TotalAmountText;
		walletBalanceValueLabel.Text = (string.IsNullOrWhiteSpace(cart.WalletBalanceText) ? "0 d" : cart.WalletBalanceText);
		walletStatusBadgeLabel.Text = (string.Equals(cart.WalletStatus, "active", StringComparison.OrdinalIgnoreCase) ? "VÍ SẴN SÀNG" : ("VI " + cart.WalletStatus.ToUpperInvariant()));
		walletStatusBadgeLabel.BackColor = (string.Equals(cart.WalletStatus, "active", StringComparison.OrdinalIgnoreCase) ? Color.FromArgb(35, 74, 58) : Color.FromArgb(78, 56, 28));
		walletStatusBadgeLabel.ForeColor = (string.Equals(cart.WalletStatus, "active", StringComparison.OrdinalIgnoreCase) ? Color.FromArgb(191, 238, 205) : Color.FromArgb(255, 223, 162));
		walletHintLabel.Text = (cart.IsEmpty ? "Thêm sản phẩm vào giỏ để sử dụng ví NestG cho đơn hàng." : (cart.CanPayWithWallet ? "Số dư ví đã đủ để thanh toán ngay mà không cần quét QR." : ("Cần nạp thêm " + cart.WalletShortfallText + " để thanh toán bằng ví.")));
		LayoutWalletCard();
		ApplyCheckoutButtonState();
		UpdateSummaryLayout();
	}

	private void ShowStatus(string message)
	{
		statusLabel.Text = message;
		subtotalValueLabel.Text = "--";
		quantityValueLabel.Text = "--";
		discountValueLabel.Text = "--";
		totalValueLabel.Text = "--";
		walletBalanceValueLabel.Text = "--";
		walletStatusBadgeLabel.Text = "CHỜ ĐỒNG BỘ";
		walletStatusBadgeLabel.BackColor = Color.FromArgb(62, 68, 86);
		walletStatusBadgeLabel.ForeColor = AppTheme.PrimaryText;
		walletHintLabel.Text = "Đang chờ dữ liệu ví và tổng thanh toán.";
		LayoutWalletCard();
		ApplyCheckoutButtonState();
		UpdateSummaryLayout();
	}

	private async Task HandleQuantityUpdateAsync(DesktopCartLineItemResponse item, int quantity)
	{
		string text = await DesktopAppContext.AuthSessionManager.GetAccessTokenAsync();
		if (string.IsNullOrWhiteSpace(text))
		{
			LauncherMessageBox.Show("Phiên đăng nhập đã hết hạn.", "NestG Launcher", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
			return;
		}
		try
		{
			StorefrontApiClient.StorefrontCartMutationResponse storefrontCartMutationResponse = await storefrontApiClient.UpdateCartItemAsync(item.CartItemId, quantity, currentPromoCode, text);
			this.CartCountChanged?.Invoke(storefrontCartMutationResponse.CartItemCount);
			await RefreshCartAsync(currentPromoCode);
		}
		catch (Exception ex)
		{
			LauncherMessageBox.Show(ex.Message, "Cập nhật giỏ hàng thất bại", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
		}
	}

	private async Task HandleRemoveItemAsync(DesktopCartLineItemResponse item)
	{
		if (LauncherMessageBox.Show("Xóa " + item.Name + " khỏi giỏ hàng?", "NestG Launcher", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
		{
			return;
		}
		string text = await DesktopAppContext.AuthSessionManager.GetAccessTokenAsync();
		if (string.IsNullOrWhiteSpace(text))
		{
			LauncherMessageBox.Show("Phiên đăng nhập đã hết hạn.", "NestG Launcher", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
			return;
		}
		try
		{
			StorefrontApiClient.StorefrontCartMutationResponse storefrontCartMutationResponse = await storefrontApiClient.RemoveCartItemAsync(item.CartItemId, currentPromoCode, text);
			this.CartCountChanged?.Invoke(storefrontCartMutationResponse.CartItemCount);
			await RefreshCartAsync(currentPromoCode);
		}
		catch (Exception ex)
		{
			LauncherMessageBox.Show(ex.Message, "Xóa sản phẩm thất bại", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
		}
	}

	private async Task HandleCheckoutAsync(string paymentMethod)
	{
		if (isCreatingCheckout)
		{
			return;
		}
		if (currentCart == null || currentCart.IsEmpty)
		{
			LauncherMessageBox.Show("Giỏ hàng đang trống, không thể tạo mã thanh toán.", "NestG Launcher", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
			return;
		}
		try
		{
			isCreatingCheckout = true;
			currentCheckoutMethod = paymentMethod;
			ApplyCheckoutButtonState();
			string text = await DesktopAppContext.AuthSessionManager.GetAccessTokenAsync();
			if (string.IsNullOrWhiteSpace(text))
			{
				throw new InvalidOperationException("Phiên đăng nhập đã hết hạn.");
			}
			decimal expectedTotalAmount = currentCart.TotalAmount;
			DesktopCheckoutPaymentResponse desktopCheckoutPaymentResponse = await storefrontApiClient.CreateCheckoutAsync(currentPromoCode, expectedTotalAmount, text, paymentMethod);
			if (decimal.Round(desktopCheckoutPaymentResponse.TotalAmount, 2, MidpointRounding.AwayFromZero) != decimal.Round(expectedTotalAmount, 2, MidpointRounding.AwayFromZero))
			{
				throw new InvalidOperationException("Tổng thanh toán đã thay đổi. Vui lòng tải lại giỏ hàng trước khi thanh toán.");
			}
			if (desktopCheckoutPaymentResponse.IsWalletPayment || !desktopCheckoutPaymentResponse.HasQrCode)
			{
				LauncherMessageBox.Show(desktopCheckoutPaymentResponse.StatusDescription, desktopCheckoutPaymentResponse.StatusTitle, MessageBoxButtons.OK, desktopCheckoutPaymentResponse.IsPaid ? MessageBoxIcon.Asterisk : MessageBoxIcon.Exclamation);
			}
			else
			{
				using StorePaymentQrDialog storePaymentQrDialog = new StorePaymentQrDialog(storefrontApiClient, desktopCheckoutPaymentResponse);
				storePaymentQrDialog.ShowDialog(FindForm());
			}
			await RefreshCartAsync(currentPromoCode);
		}
		catch (Exception ex)
		{
			LauncherMessageBox.Show(ex.Message, "Không thể tạo mã thanh toán", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
		}
		finally
		{
			isCreatingCheckout = false;
			currentCheckoutMethod = "sepay";
			ApplyCheckoutButtonState();
		}
	}

	private async Task OpenWalletAsync()
	{
		if (openWalletAsync != null)
		{
			await openWalletAsync();
		}
		else
		{
			LauncherMessageBox.Show("Hãy mở khu vực tài khoản để nạp tiền vào ví NestG.", "NestG Launcher", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
		}
	}

	private void UpdateItemCardWidths()
	{
		int num = itemScrollPanel.ClientSize.Width;
		if (itemScrollPanel.VerticalScroll.Visible)
		{
			num -= SystemInformation.VerticalScrollBarWidth;
		}
		num = Math.Max(700, num - 6);
		foreach (Control control in itemCardsFlowPanel.Controls)
		{
			control.Width = num;
		}
	}

	private void UpdateSummaryLayout()
	{
		if (summaryScrollPanel.IsDisposed || summaryStack.IsDisposed || summaryColumnPanel.IsDisposed)
		{
			return;
		}
		int num = Math.Max(300, summaryColumnHostPanel.ClientSize.Width - 4);
		int val = Math.Max(300, num - summaryColumnPanel.Padding.Horizontal);
		int num2 = Math.Min(356, val);
		int width = num2 + summaryColumnPanel.Padding.Horizontal;
		summaryStack.Width = num2;
		promoShell.Width = num2;
		promoSuggestionsFlowPanel.Width = num2;
		promoMessageLabel.MaximumSize = new Size(num2, 0);
		rowsCard.Width = num2;
		totalCard.Width = num2;
		walletCard.Width = num2;
		walletCheckoutButton.Width = num2;
		topUpWalletButton.Width = num2;
		checkoutButton.Width = num2;
		continueButton.Width = num2;
		LayoutWalletCard();
		foreach (Control control in promoSuggestionsFlowPanel.Controls)
		{
			control.Width = num2;
		}
		summaryStack.PerformLayout();
		summaryHost.Width = num2;
		summaryHost.Top = 0;
		summaryHost.Left = 0;
		int num3 = Math.Max(summaryStack.Height, summaryStack.PreferredSize.Height);
		summaryColumnPanel.Size = new Size(width, num3 + summaryColumnPanel.Padding.Vertical);
	}

	private void ApplyCheckoutButtonState()
	{
		DesktopCartResponse desktopCartResponse = currentCart;
		bool flag = desktopCartResponse != null && !desktopCartResponse.IsEmpty;
		checkoutButton.Text = ((isCreatingCheckout && string.Equals(currentCheckoutMethod, "sepay", StringComparison.OrdinalIgnoreCase)) ? "Đang tạo QR..." : "Thanh toán với SePay");
		walletCheckoutButton.Text = ((isCreatingCheckout && string.Equals(currentCheckoutMethod, "wallet", StringComparison.OrdinalIgnoreCase)) ? "Đang trừ tiền..." : "Thanh toán bằng ví");
		checkoutButton.Enabled = flag && !isCreatingCheckout;
		walletCheckoutButton.Enabled = flag && currentCart != null && currentCart.CanPayWithWallet && !isCreatingCheckout;
		topUpWalletButton.Enabled = !isCreatingCheckout;
	}
}
