using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using app_desktop;
using app_desktop.Services;

namespace UI_Desktop.Store;

internal sealed class StoreAccountView : UserControl
{
	private enum AccountSection
	{
		Overview,
		Orders,
		Downloads,
		Wallet
	}

	private readonly StorefrontApiClient storefrontApiClient;

	private readonly Func<Task> signOutAsync;

	private readonly Func<Task>? openLibraryAsync;

	private readonly TableLayoutPanel rootLayout = new TableLayoutPanel();

	private readonly RoundedPanel sidebarPanel = new RoundedPanel();

	private readonly RoundedPanel contentPanel = new RoundedPanel();

	private readonly StoreScrollPanel contentScrollPanel = new StoreScrollPanel();

	private readonly TableLayoutPanel contentStack = new TableLayoutPanel();

	private readonly StoreScrollPanel sidebarLayout = new StoreScrollPanel();

	private readonly Label accountTitleLabel = new Label();

	private readonly Label accountNameLabel = new Label();

	private readonly Label accountEmailLabel = new Label();

	private readonly Label accountStatusLabel = new Label();

	private readonly Label accountMetaLabel = new Label();

	private readonly Label totalOrdersValueLabel = new Label();

	private readonly Label totalLibraryValueLabel = new Label();

	private readonly Label pendingOrdersValueLabel = new Label();

	private readonly RoundedPanel profileCard = new RoundedPanel();

	private readonly TableLayoutPanel profileStatsLayout = new TableLayoutPanel();

	private readonly Button overviewButton = new Button();

	private readonly Button ordersButton = new Button();

	private readonly Button downloadsButton = new Button();

	private readonly Button walletButton = new Button();

	private readonly GradientButton signOutButton = new GradientButton();

	private DesktopAccountResponse? accountData;

	private AccountSection currentSection;

	private bool isRefreshing;

	private bool isRendering;

	private bool isCreatingWalletTopUp;

	private string walletAmountDraft = string.Empty;

	public event Action<decimal, string>? WalletBalanceChanged;

	public StoreAccountView(StorefrontApiClient storefrontApiClient, Func<Task> signOutAsync, Func<Task>? openLibraryAsync = null)
	{
		this.storefrontApiClient = storefrontApiClient;
		this.signOutAsync = signOutAsync;
		this.openLibraryAsync = openLibraryAsync;
		Dock = DockStyle.Fill;
		BackColor = Color.Transparent;
		base.Margin = Padding.Empty;
		ConfigureShell();
		WireEvents();
		ShowStatusCard("Tài khoản", "Chọn mục bên trái để xem thông tin tài khoản, đơn hàng và thư viện.");
	}

	public async Task RefreshAccountAsync(CancellationToken cancellationToken = default(CancellationToken))
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
				accountData = null;
				ApplySidebarState(null);
				ShowStatusCard("Không thể tải tài khoản", "Phiên đăng nhập đã hết hạn. Hãy đăng nhập lại để tiếp tục.");
			}
			else
			{
				ShowStatusCard("Đang đồng bộ tài khoản", "Đang tải thông tin hồ sơ, đơn hàng và thư viện.");
				DesktopAccountResponse desktopAccountResponse = (accountData = await storefrontApiClient.GetAccountAsync(text, cancellationToken));
				ApplySidebarState(desktopAccountResponse);
				this.WalletBalanceChanged?.Invoke(desktopAccountResponse.WalletBalance, desktopAccountResponse.WalletBalanceText);
				RenderCurrentSection();
			}
		}
		catch (Exception ex)
		{
			accountData = null;
			ApplySidebarState(null);
			ShowStatusCard("Không thể tải tài khoản", string.IsNullOrWhiteSpace(ex.Message) ? "Đã xảy ra lỗi khi tải dữ liệu tài khoản." : ex.Message);
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
		StoreUiHelper.EnableDoubleBuffer(contentScrollPanel);
		StoreUiHelper.EnableDoubleBuffer(contentStack);
		base.Controls.Add(rootLayout);
		rootLayout.Dock = DockStyle.Fill;
		rootLayout.Margin = Padding.Empty;
		rootLayout.Padding = Padding.Empty;
		rootLayout.ColumnCount = 2;
		rootLayout.RowCount = 1;
		rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, ScaleValue(320)));
		rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
		rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
		sidebarPanel.Dock = DockStyle.Fill;
		sidebarPanel.Margin = new Padding(0, 0, ScaleValue(18), 0);
		sidebarPanel.Padding = new Padding(ScaleValue(20));
		sidebarPanel.SurfaceColor = Color.FromArgb(17, 23, 31);
		sidebarPanel.BorderColor = Color.FromArgb(46, 61, 79);
		sidebarPanel.BorderThickness = 1;
		sidebarPanel.CornerRadius = ScaleValue(18);
		contentPanel.Dock = DockStyle.Fill;
		contentPanel.Margin = Padding.Empty;
		contentPanel.Padding = new Padding(ScaleValue(22));
		contentPanel.SurfaceColor = Color.FromArgb(15, 21, 29);
		contentPanel.BorderColor = Color.FromArgb(46, 61, 79);
		contentPanel.BorderThickness = 1;
		contentPanel.CornerRadius = ScaleValue(18);
		rootLayout.Controls.Add(sidebarPanel, 0, 0);
		rootLayout.Controls.Add(contentPanel, 1, 0);
		ConfigureSidebar();
		ConfigureContent();
	}

	private void ConfigureSidebar()
	{
		profileCard.Width = ScaleValue(248);
		profileCard.Height = ScaleValue(272);
		profileCard.Margin = Padding.Empty;
		profileCard.Padding = new Padding(ScaleValue(16));
		profileCard.SurfaceColor = Color.FromArgb(24, 17, 21);
		profileCard.BorderColor = Color.FromArgb(118, 46, 53);
		profileCard.BorderThickness = 1;
		profileCard.CornerRadius = ScaleValue(18);
		accountTitleLabel.AutoSize = true;
		accountTitleLabel.ForeColor = Color.FromArgb(255, 157, 166);
		accountTitleLabel.Font = new Font("Segoe UI Semibold", 9.1f, FontStyle.Bold, GraphicsUnit.Point);
		accountTitleLabel.Text = "TÀI KHOẢN";
		accountTitleLabel.Location = new Point(16, 16);
		accountNameLabel.AutoSize = true;
		accountNameLabel.MaximumSize = new Size(ScaleValue(216), 0);
		accountNameLabel.ForeColor = AppTheme.PrimaryText;
		accountNameLabel.Font = new Font("Segoe UI Semibold", 16.8f, FontStyle.Bold, GraphicsUnit.Point);
		accountNameLabel.Location = new Point(16, 44);
		accountNameLabel.Text = "Đang đồng bộ...";
		accountEmailLabel.AutoSize = true;
		accountEmailLabel.MaximumSize = new Size(ScaleValue(216), 0);
		accountEmailLabel.ForeColor = AppTheme.SecondaryText;
		accountEmailLabel.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular, GraphicsUnit.Point);
		accountEmailLabel.Location = new Point(16, 78);
		accountEmailLabel.Text = "backend profile";
		accountStatusLabel.AutoSize = true;
		accountStatusLabel.Padding = new Padding(ScaleValue(14), ScaleValue(6), ScaleValue(14), ScaleValue(6));
		accountStatusLabel.Location = new Point(16, 112);
		accountStatusLabel.Font = new Font("Segoe UI Semibold", 8.8f, FontStyle.Bold, GraphicsUnit.Point);
		accountStatusLabel.TextAlign = ContentAlignment.MiddleCenter;
		accountMetaLabel.AutoSize = true;
		accountMetaLabel.MaximumSize = new Size(ScaleValue(216), 0);
		accountMetaLabel.ForeColor = AppTheme.SecondaryText;
		accountMetaLabel.Font = new Font("Segoe UI", 8.7f, FontStyle.Regular, GraphicsUnit.Point);
		accountMetaLabel.Location = new Point(16, 148);
		accountMetaLabel.Text = "Thông tin thành viên";
		profileStatsLayout.Location = new Point(ScaleValue(16), ScaleValue(192));
		profileStatsLayout.Size = new Size(ScaleValue(216), ScaleValue(54));
		profileStatsLayout.Margin = Padding.Empty;
		profileStatsLayout.Padding = Padding.Empty;
		profileStatsLayout.ColumnCount = 3;
		profileStatsLayout.RowCount = 1;
		profileStatsLayout.ColumnStyles.Clear();
		profileStatsLayout.RowStyles.Clear();
		profileStatsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
		profileStatsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
		profileStatsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.34f));
		profileStatsLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
		profileStatsLayout.Controls.Add(CreateSidebarStat("Đơn", totalOrdersValueLabel), 0, 0);
		profileStatsLayout.Controls.Add(CreateSidebarStat("Game", totalLibraryValueLabel), 1, 0);
		profileStatsLayout.Controls.Add(CreateSidebarStat("Chờ", pendingOrdersValueLabel), 2, 0);
		profileCard.Controls.Add(profileStatsLayout);
		profileCard.Controls.Add(accountMetaLabel);
		profileCard.Controls.Add(accountStatusLabel);
		profileCard.Controls.Add(accountEmailLabel);
		profileCard.Controls.Add(accountNameLabel);
		profileCard.Controls.Add(accountTitleLabel);
		ConfigureSidebarButton(overviewButton, "Tổng quan");
		ConfigureSidebarButton(ordersButton, "Lịch sử mua hàng");
		ConfigureSidebarButton(downloadsButton, "Tải app và thư viện");
		ConfigureSidebarButton(walletButton, "Ví NestG");
		signOutButton.Text = "Đăng xuất";
		signOutButton.StartColor = Color.FromArgb(224, 56, 62);
		signOutButton.EndColor = Color.FromArgb(171, 22, 32);
		signOutButton.CornerRadius = ScaleValue(18);
		signOutButton.Font = new Font("Segoe UI Semibold", 10.8f, FontStyle.Bold, GraphicsUnit.Point);
		signOutButton.Width = ScaleValue(248);
		sidebarLayout.Dock = DockStyle.Fill;
		sidebarLayout.Margin = Padding.Empty;
		sidebarLayout.Padding = Padding.Empty;
		sidebarLayout.AutoScroll = true;
		sidebarLayout.BackColor = sidebarPanel.SurfaceColor;
		sidebarLayout.HorizontalScroll.Enabled = false;
		sidebarLayout.HorizontalScroll.Visible = false;
		sidebarLayout.Controls.Clear();
		sidebarLayout.Controls.Add(profileCard);
		sidebarLayout.Controls.Add(overviewButton);
		sidebarLayout.Controls.Add(ordersButton);
		sidebarLayout.Controls.Add(downloadsButton);
		sidebarLayout.Controls.Add(walletButton);
		sidebarLayout.Controls.Add(signOutButton);
		sidebarPanel.Controls.Add(sidebarLayout);
		UpdateSidebarButtons();
		ApplySidebarState(null);
		ApplySidebarSizing(GetSidebarAvailableWidth());
	}

	private void ConfigureContent()
	{
		contentScrollPanel.Dock = DockStyle.Fill;
		contentScrollPanel.AutoScroll = true;
		contentScrollPanel.BackColor = contentPanel.SurfaceColor;
		contentStack.Dock = DockStyle.Top;
		contentStack.AutoSize = true;
		contentStack.AutoSizeMode = AutoSizeMode.GrowAndShrink;
		contentStack.Margin = Padding.Empty;
		contentStack.Padding = Padding.Empty;
		contentStack.ColumnCount = 1;
		contentStack.RowCount = 0;
		contentStack.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
		contentScrollPanel.Controls.Add(contentStack);
		contentPanel.Controls.Add(contentScrollPanel);
	}

	private void WireEvents()
	{
		overviewButton.Click += delegate
		{
			ApplySection(AccountSection.Overview);
		};
		ordersButton.Click += delegate
		{
			ApplySection(AccountSection.Orders);
		};
		downloadsButton.Click += delegate
		{
			ApplySection(AccountSection.Downloads);
		};
		walletButton.Click += delegate
		{
			ApplySection(AccountSection.Wallet);
		};
		signOutButton.Click += async delegate
		{
			await BeginSignOutAsync();
		};
		rootLayout.SizeChanged += delegate
		{
			UpdateResponsiveLayout();
		};
		sidebarPanel.SizeChanged += delegate
		{
			ApplySidebarSizing(GetSidebarAvailableWidth());
		};
		sidebarLayout.SizeChanged += delegate
		{
			ApplySidebarSizing(GetSidebarAvailableWidth());
		};
		contentScrollPanel.SizeChanged += delegate
		{
			if (accountData != null && !isRendering)
			{
				RenderCurrentSection();
			}
		};
	}

	private Control CreateSidebarStat(string caption, Label valueLabel)
	{
		RoundedPanel obj = new RoundedPanel
		{
			Dock = DockStyle.Fill,
			Margin = new Padding(0, 0, ScaleValue(8), 0),
			Padding = new Padding(ScaleValue(8), ScaleValue(6), ScaleValue(8), ScaleValue(6)),
			SurfaceColor = Color.FromArgb(28, 34, 46),
			BorderColor = Color.FromArgb(59, 73, 94),
			BorderThickness = 1,
			CornerRadius = ScaleValue(12)
		};
		TableLayoutPanel tableLayoutPanel = new TableLayoutPanel
		{
			Dock = DockStyle.Fill,
			Margin = Padding.Empty,
			Padding = Padding.Empty,
			ColumnCount = 1,
			RowCount = 2
		};
		tableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
		tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, ScaleValue(14)));
		tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
		Label control = new Label
		{
			AutoSize = false,
			Dock = DockStyle.Fill,
			ForeColor = AppTheme.SecondaryText,
			Font = new Font("Segoe UI", 7.8f, FontStyle.Regular, GraphicsUnit.Point),
			Text = caption,
			TextAlign = ContentAlignment.TopCenter,
			Margin = Padding.Empty
		};
		valueLabel.AutoSize = false;
		valueLabel.Dock = DockStyle.Fill;
		valueLabel.ForeColor = AppTheme.PrimaryText;
		valueLabel.Font = new Font("Segoe UI Semibold", 9.6f, FontStyle.Bold, GraphicsUnit.Point);
		valueLabel.TextAlign = ContentAlignment.MiddleCenter;
		valueLabel.Text = "--";
		valueLabel.Margin = Padding.Empty;
		tableLayoutPanel.Controls.Add(control, 0, 0);
		tableLayoutPanel.Controls.Add(valueLabel, 0, 1);
		obj.Controls.Add(tableLayoutPanel);
		return obj;
	}

	private static Control CreateSidebarSpacer(int height)
	{
		return new Panel
		{
			Width = 1,
			Height = height,
			Margin = Padding.Empty,
			Padding = Padding.Empty
		};
	}

	private void ConfigureSidebarButton(Button button, string text)
	{
		button.Text = text;
		button.Width = ScaleValue(248);
		button.Height = ScaleValue(46);
		button.Margin = new Padding(0, 0, 0, ScaleValue(10));
		button.FlatStyle = FlatStyle.Flat;
		button.FlatAppearance.BorderSize = 1;
		button.Font = new Font("Segoe UI Semibold", 9.6f, FontStyle.Bold, GraphicsUnit.Point);
		button.TextAlign = ContentAlignment.MiddleLeft;
		button.Padding = new Padding(ScaleValue(16), 0, ScaleValue(16), 0);
		button.Cursor = Cursors.Hand;
	}

	private void UpdateResponsiveLayout()
	{
		int num = ((base.Width >= ScaleValue(1440)) ? ScaleValue(320) : ((base.Width >= ScaleValue(1180)) ? ScaleValue(288) : ScaleValue(260)));
		rootLayout.ColumnStyles[0].Width = num;
		ApplySidebarSizing(GetSidebarAvailableWidth());
	}

	private void ApplySidebarState(DesktopAccountResponse? response)
	{
		if (response == null)
		{
			accountNameLabel.Text = "Tài khoản desktop";
			accountEmailLabel.Text = "Đang sẵn sàng đồng bộ";
			accountStatusLabel.Text = "CHỜ DỮ LIỆU";
			accountStatusLabel.BackColor = Color.FromArgb(62, 68, 86);
			accountStatusLabel.ForeColor = AppTheme.PrimaryText;
			accountMetaLabel.Text = "Đang tải thông tin tài khoản.";
			totalOrdersValueLabel.Text = "--";
			totalLibraryValueLabel.Text = "--";
			pendingOrdersValueLabel.Text = "--";
			UpdateSidebarProfileLayout();
		}
		else
		{
			accountNameLabel.Text = response.DisplayName;
			accountEmailLabel.Text = response.UserEmail;
			accountStatusLabel.Text = (response.EmailVerified ? "ĐÃ XÁC THỰC" : "CHƯA XÁC THỰC");
			accountStatusLabel.BackColor = (response.EmailVerified ? Color.FromArgb(35, 74, 58) : Color.FromArgb(90, 56, 28));
			accountStatusLabel.ForeColor = (response.EmailVerified ? Color.FromArgb(191, 238, 205) : Color.FromArgb(255, 222, 163));
			accountMetaLabel.Text = ((response.LastLoginAtText != null) ? $"Tham gia {response.CreatedAtText}{Environment.NewLine}Lần cuối {response.LastLoginAtText}" : ("Tham gia " + response.CreatedAtText));
			totalOrdersValueLabel.Text = response.TotalOrders.ToString();
			totalLibraryValueLabel.Text = response.TotalLibraryItems.ToString();
			pendingOrdersValueLabel.Text = response.PendingOrders.ToString();
			UpdateSidebarProfileLayout();
		}
	}

	private void ApplySection(AccountSection section)
	{
		currentSection = section;
		UpdateSidebarButtons();
		RenderCurrentSection();
	}

	private void UpdateSidebarButtons()
	{
		ApplySidebarButtonState(overviewButton, currentSection == AccountSection.Overview);
		ApplySidebarButtonState(ordersButton, currentSection == AccountSection.Orders);
		ApplySidebarButtonState(downloadsButton, currentSection == AccountSection.Downloads);
		ApplySidebarButtonState(walletButton, currentSection == AccountSection.Wallet);
	}

	private static void ApplySidebarButtonState(Button button, bool isActive)
	{
		button.BackColor = (isActive ? Color.FromArgb(39, 52, 70) : Color.FromArgb(22, 29, 39));
		button.ForeColor = (isActive ? AppTheme.PrimaryText : AppTheme.SecondaryText);
		button.FlatAppearance.BorderColor = (isActive ? Color.FromArgb(212, 62, 69) : Color.FromArgb(58, 73, 94));
	}

	private void RenderCurrentSection()
	{
		if (accountData == null)
		{
			return;
		}
		isRendering = true;
		try
		{
			contentStack.SuspendLayout();
			contentStack.Controls.Clear();
			contentStack.RowStyles.Clear();
			contentStack.RowCount = 0;
			switch (currentSection)
			{
			case AccountSection.Overview:
				RenderOverview(accountData);
				break;
			case AccountSection.Orders:
				RenderOrders(accountData);
				break;
			case AccountSection.Downloads:
				RenderDownloads(accountData);
				break;
			case AccountSection.Wallet:
				RenderWallet(accountData);
				break;
			}
		}
		finally
		{
			contentStack.ResumeLayout(performLayout: true);
			isRendering = false;
		}
	}

	private void RenderOverview(DesktopAccountResponse response)
	{
		AddSection(CreateHeaderCard("Tổng quan tài khoản"));
		AddSection(CreateMetricsGrid(response));
		AddSection(CreateWalletOverviewCard(response));
		AddSection(CreateProfileTimelineCard(response));
		AddSection(CreateOrdersSection("Đơn hàng mới nhất", response.RecentOrders, "Bạn chưa có đơn hàng nào gần đây."));
	}

	private void RenderOrders(DesktopAccountResponse response)
	{
		AddSection(CreateHeaderCard("Lịch sử mua hàng"));
		AddSection(CreateOrderSummaryCard(response));
		AddSection(CreateOrdersSection("Tất cả đơn hàng", response.Orders, "Bạn chưa có đơn hàng nào."));
	}

	private void RenderDownloads(DesktopAccountResponse response)
	{
		AddSection(CreateHeaderCard("Tải app và thư viện"));
		AddSection(CreateLauncherSection(response));
		AddSection(CreateDownloadsSection(response));
	}

	private void RenderWallet(DesktopAccountResponse response)
	{
		AddSection(CreateHeaderCard("Ví NestG"));
		AddSection(CreateWalletBalanceCard(response));
		AddSection(CreateWalletTopUpSection(response));
		AddSection(CreateWalletTransactionsSection(response));
	}

	public void ShowWalletSection()
	{
		ApplySection(AccountSection.Wallet);
	}

	private void AddSection(Control control)
	{
		control.Margin = new Padding(0, 0, 0, 18);
		control.Dock = DockStyle.Top;
		contentStack.RowStyles.Add(new RowStyle());
		contentStack.Controls.Add(control, 0, contentStack.RowCount);
		contentStack.RowCount++;
	}

	private Control CreateHeaderCard(string title)
	{
		RoundedPanel roundedPanel = CreateSectionShell();
		roundedPanel.Height = ScaleValue(94);
		Label value = new Label
		{
			AutoSize = true,
			ForeColor = AppTheme.PrimaryText,
			Font = new Font("Segoe UI Semibold", 19f, FontStyle.Bold, GraphicsUnit.Point),
			Text = title,
			Location = new Point(ScaleValue(20), ScaleValue(24))
		};
		roundedPanel.Controls.Add(value);
		return roundedPanel;
	}

	private Control CreateMetricsGrid(DesktopAccountResponse response)
	{
		RoundedPanel roundedPanel = CreateSectionShell();
		int sectionContentWidth = GetSectionContentWidth();
		int num = ScaleValue(14);
		int num2 = ScaleValue(102);
		int metricColumnCount = GetMetricColumnCount(sectionContentWidth);
		int num3 = ((metricColumnCount == 1) ? sectionContentWidth : ((sectionContentWidth - num * (metricColumnCount - 1)) / metricColumnCount));
		(string, string, Color)[] array = new(string, string, Color)[6]
		{
			("Email", response.UserEmail, AppTheme.PrimaryText),
			("Tên hiển thị", response.DisplayName, AppTheme.PrimaryText),
			("Trạng thái", response.EmailVerified ? "Đã xác thực" : "Chưa xác thực", response.EmailVerified ? Color.FromArgb(177, 235, 197) : Color.FromArgb(255, 214, 150)),
			("Thư viện game", $"{response.TotalLibraryItems} sản phẩm", Color.FromArgb(255, 130, 130)),
			("Đơn đã thanh toán", response.PaidOrders.ToString(), Color.FromArgb(255, 214, 137)),
			("Đang chờ thanh toán", response.PendingOrders.ToString(), Color.FromArgb(151, 188, 255))
		};
		int num4 = (int)Math.Ceiling((double)array.Length / (double)metricColumnCount);
		for (int i = 0; i < array.Length; i++)
		{
			(string, string, Color) tuple = array[i];
			Control control = CreateMetricCard(tuple.Item1, tuple.Item2, tuple.Item3);
			int num5 = i / metricColumnCount;
			int num6 = i % metricColumnCount;
			control.Location = new Point(ScaleValue(18) + num6 * (num3 + num), ScaleValue(18) + num5 * (num2 + num));
			control.Size = new Size(num3, num2);
			roundedPanel.Controls.Add(control);
		}
		roundedPanel.Height = ScaleValue(18) + num4 * num2 + (num4 - 1) * num + ScaleValue(18);
		return roundedPanel;
	}

	private Control CreateMetricCard(string label, string value, Color valueColor)
	{
		RoundedPanel card = new RoundedPanel
		{
			Margin = Padding.Empty,
			Padding = Padding.Empty,
			SurfaceColor = Color.FromArgb(20, 27, 37),
			BorderColor = Color.FromArgb(52, 66, 84),
			BorderThickness = 1,
			CornerRadius = ScaleValue(14)
		};
		Label captionLabel = new Label
		{
			AutoSize = false,
			ForeColor = AppTheme.SecondaryText,
			Font = new Font("Segoe UI", 8.8f, FontStyle.Regular, GraphicsUnit.Point),
			Text = label,
			TextAlign = ContentAlignment.MiddleLeft,
			Margin = Padding.Empty,
			UseMnemonic = false
		};
		Label valueLabel = new Label
		{
			AutoSize = false,
			ForeColor = valueColor,
			Font = new Font("Segoe UI Semibold", 12f, FontStyle.Bold, GraphicsUnit.Point),
			Text = value,
			TextAlign = ContentAlignment.MiddleLeft,
			AutoEllipsis = true,
			Margin = Padding.Empty,
			UseMnemonic = false
		};
		card.Controls.Add(valueLabel);
		card.Controls.Add(captionLabel);
		LayoutMetricCard(card, captionLabel, valueLabel);
		card.Resize += delegate
		{
			LayoutMetricCard(card, captionLabel, valueLabel);
		};
		return card;
	}

	private Control CreateProfileTimelineCard(DesktopAccountResponse response)
	{
		RoundedPanel roundedPanel = CreateSectionShell("Hoạt động");
		int sectionContentWidth = GetSectionContentWidth();
		int sectionBodyTop = GetSectionBodyTop();
		FlowLayoutPanel flowLayoutPanel = new FlowLayoutPanel
		{
			Location = new Point(ScaleValue(18), sectionBodyTop),
			MaximumSize = new Size(sectionContentWidth, 0),
			AutoSize = true,
			AutoSizeMode = AutoSizeMode.GrowAndShrink,
			FlowDirection = FlowDirection.LeftToRight,
			WrapContents = true,
			Margin = Padding.Empty,
			Padding = Padding.Empty
		};
		flowLayoutPanel.Controls.Add(CreateTimelinePill("Thành viên từ", response.CreatedAtText, Color.FromArgb(39, 52, 70), AppTheme.PrimaryText));
		flowLayoutPanel.Controls.Add(CreateTimelinePill("Đăng nhập gần nhất", response.LastLoginAtText ?? "Chưa ghi nhận", Color.FromArgb(35, 72, 58), Color.FromArgb(188, 238, 205)));
		flowLayoutPanel.Controls.Add(CreateTimelinePill("Email", response.EmailVerified ? "Đã xác thực" : "Chưa xác thực", Color.FromArgb(78, 56, 28), Color.FromArgb(255, 220, 160)));
		roundedPanel.Controls.Add(flowLayoutPanel);
		roundedPanel.Height = sectionBodyTop + flowLayoutPanel.GetPreferredSize(new Size(sectionContentWidth, 0)).Height + ScaleValue(18);
		return roundedPanel;
	}

	private Label CreateTimelinePill(string title, string value, Color background, Color foreground)
	{
		return new Label
		{
			AutoSize = true,
			BackColor = background,
			ForeColor = foreground,
			Font = new Font("Segoe UI Semibold", 9f, FontStyle.Bold, GraphicsUnit.Point),
			Padding = new Padding(12, 10, 12, 10),
			Margin = new Padding(0, 0, 12, 0),
			Text = title + ": " + value
		};
	}

	private Control CreateOrderSummaryCard(DesktopAccountResponse response)
	{
		RoundedPanel roundedPanel = CreateSectionShell("Tổng quan giao dịch");
		int sectionContentWidth = GetSectionContentWidth();
		int sectionBodyTop = GetSectionBodyTop();
		FlowLayoutPanel flowLayoutPanel = new FlowLayoutPanel
		{
			Location = new Point(ScaleValue(18), sectionBodyTop),
			MaximumSize = new Size(sectionContentWidth, 0),
			AutoSize = true,
			AutoSizeMode = AutoSizeMode.GrowAndShrink,
			WrapContents = true,
			Margin = Padding.Empty,
			Padding = Padding.Empty
		};
		flowLayoutPanel.Controls.Add(CreateSummaryChip("Tổng đơn", response.TotalOrders.ToString(), Color.FromArgb(41, 53, 71), AppTheme.PrimaryText));
		flowLayoutPanel.Controls.Add(CreateSummaryChip("Đã thanh toán", response.PaidOrders.ToString(), Color.FromArgb(35, 72, 58), Color.FromArgb(188, 238, 205)));
		flowLayoutPanel.Controls.Add(CreateSummaryChip("Đang chờ", response.PendingOrders.ToString(), Color.FromArgb(78, 56, 28), Color.FromArgb(255, 223, 162)));
		flowLayoutPanel.Controls.Add(CreateSummaryChip("Thư viện", response.TotalLibraryItems.ToString(), Color.FromArgb(80, 34, 42), Color.FromArgb(255, 205, 209)));
		roundedPanel.Controls.Add(flowLayoutPanel);
		roundedPanel.Height = sectionBodyTop + flowLayoutPanel.GetPreferredSize(new Size(sectionContentWidth, 0)).Height + ScaleValue(18);
		return roundedPanel;
	}

	private Label CreateSummaryChip(string title, string value, Color background, Color foreground)
	{
		return new Label
		{
			AutoSize = true,
			BackColor = background,
			ForeColor = foreground,
			Font = new Font("Segoe UI Semibold", 9.2f, FontStyle.Bold, GraphicsUnit.Point),
			Padding = new Padding(12, 7, 12, 7),
			Margin = new Padding(0, 0, 12, 12),
			Text = title + ": " + value
		};
	}

	private Control CreateWalletOverviewCard(DesktopAccountResponse response)
	{
		return CreateWalletBalanceCard(response, "Số dư và trạng thái ví hiện tại được đồng bộ từ hệ thống web.");
	}

	private Control CreateWalletBalanceCard(DesktopAccountResponse response, string? subtitle = null)
	{
		RoundedPanel roundedPanel = CreateSectionShell("Số dư ví", subtitle);
		int sectionContentWidth = GetSectionContentWidth();
		int sectionBodyTop = GetSectionBodyTop();
		int num = ScaleValue(18);
		int num2 = Math.Max(ScaleValue(240), sectionContentWidth - num * 2);
		bool flag = string.Equals(response.WalletStatus, "active", StringComparison.OrdinalIgnoreCase);
		string text = response.WalletStatusText.ToUpperInvariant();
		string text2 = "Nạp tối thiểu " + FormatAmount(response.MinimumTopUpAmount) + "  |  Mỗi lần tối đa " + FormatAmount(response.MaximumTopUpAmount);
		RoundedPanel roundedPanel2 = new RoundedPanel
		{
			Location = new Point(ScaleValue(18), sectionBodyTop),
			Size = new Size(sectionContentWidth, ScaleValue(244)),
			Padding = new Padding(num),
			SurfaceColor = Color.FromArgb(18, 31, 36),
			BorderColor = Color.FromArgb(54, 92, 87),
			BorderThickness = 1,
			CornerRadius = ScaleValue(16)
		};
		Label label = new Label
		{
			AutoSize = false,
			ForeColor = AppTheme.SecondaryText,
			Font = new Font("Segoe UI", 8.8f, FontStyle.Regular, GraphicsUnit.Point),
			Text = "Số dư khả dụng",
			TextAlign = ContentAlignment.MiddleLeft
		};
		Label label2 = new Label
		{
			AutoSize = false,
			ForeColor = Color.FromArgb(181, 244, 121),
			Font = new Font("Segoe UI Semibold", 22f, FontStyle.Bold, GraphicsUnit.Point),
			Text = response.WalletBalanceText,
			TextAlign = ContentAlignment.MiddleLeft
		};
		Label label3 = new Label
		{
			AutoSize = false,
			BackColor = (flag ? Color.FromArgb(35, 74, 58) : Color.FromArgb(78, 56, 28)),
			ForeColor = (flag ? Color.FromArgb(191, 238, 205) : Color.FromArgb(255, 223, 162)),
			Padding = new Padding(ScaleValue(12), ScaleValue(6), ScaleValue(12), ScaleValue(6)),
			Font = new Font("Segoe UI Semibold", 8.8f, FontStyle.Bold, GraphicsUnit.Point),
			Text = text,
			TextAlign = ContentAlignment.MiddleCenter
		};
		Label label4 = new Label
		{
			AutoSize = false,
			ForeColor = AppTheme.SecondaryText,
			Font = new Font("Segoe UI", 9f, FontStyle.Regular, GraphicsUnit.Point),
			Text = text2,
			TextAlign = ContentAlignment.TopLeft
		};
		FlowLayoutPanel flowLayoutPanel = new FlowLayoutPanel
		{
			Location = new Point(num, num),
			Width = num2,
			AutoSize = true,
			AutoSizeMode = AutoSizeMode.GrowAndShrink,
			FlowDirection = FlowDirection.LeftToRight,
			WrapContents = true,
			Margin = Padding.Empty,
			Padding = Padding.Empty,
			MaximumSize = new Size(num2, 0),
			BackColor = Color.Transparent
		};
		int walletMetricColumnCount = GetWalletMetricColumnCount(num2);
		int num3 = ScaleValue(12);
		int width = ((walletMetricColumnCount == 1) ? num2 : ((num2 - (walletMetricColumnCount - 1) * num3) / walletMetricColumnCount));
		int height = ScaleValue(72);
		flowLayoutPanel.Controls.Add(CreateWalletMetaCard("Trạng thái", response.WalletStatusText, width, height));
		flowLayoutPanel.Controls.Add(CreateWalletMetaCard("Nạp tối thiểu", FormatAmount(response.MinimumTopUpAmount), width, height));
		flowLayoutPanel.Controls.Add(CreateWalletMetaCard("Nạp tối đa", FormatAmount(response.MaximumTopUpAmount), width, height));
		for (int i = 0; i < flowLayoutPanel.Controls.Count; i++)
		{
			bool flag2 = i == flowLayoutPanel.Controls.Count - 1;
			bool flag3 = (i + 1) % walletMetricColumnCount == 0;
			flowLayoutPanel.Controls[i].Margin = new Padding(0, 0, (!(flag3 || flag2)) ? num3 : 0, num3);
		}
		Size size2 = (label3.Size = MeasureBadgeSize(text, label3.Font, label3.Padding));
		label3.Location = new Point(Math.Max(num, roundedPanel2.ClientSize.Width - num - size2.Width), num);
		int width2 = Math.Max(ScaleValue(140), num2 - size2.Width - ScaleValue(12));
		label.Location = new Point(num, num + ScaleValue(2));
		label.Size = new Size(width2, MeasureTextHeight(label.Text, label.Font, width2, allowWrap: false));
		int y = Math.Max(label.Bottom + ScaleValue(12), label3.Bottom + ScaleValue(12));
		label2.Location = new Point(num, y);
		label2.Size = new Size(num2, MeasureTextHeight(label2.Text, label2.Font, num2));
		label4.Location = new Point(num, label2.Bottom + ScaleValue(12));
		label4.Size = new Size(num2, MeasureTextHeight(text2, label4.Font, num2));
		flowLayoutPanel.Location = new Point(num, label4.Bottom + ScaleValue(16));
		roundedPanel2.Controls.Add(label);
		roundedPanel2.Controls.Add(label2);
		roundedPanel2.Controls.Add(label3);
		roundedPanel2.Controls.Add(label4);
		roundedPanel2.Controls.Add(flowLayoutPanel);
		flowLayoutPanel.PerformLayout();
		roundedPanel2.Height = flowLayoutPanel.Bottom + num;
		roundedPanel.Controls.Add(roundedPanel2);
		roundedPanel.Height = roundedPanel2.Bottom + ScaleValue(18);
		return roundedPanel;
	}

	private Control CreateWalletTopUpSection(DesktopAccountResponse response)
	{
		RoundedPanel roundedPanel = CreateSectionShell("Nạp tiền bằng SePay", "Tạo phiếu nạp tiền trực tiếp trong launcher và quét QR như bên web.");
		int sectionContentWidth = GetSectionContentWidth();
		int sectionBodyTop = GetSectionBodyTop();
		int num = ScaleValue(18);
		int num2 = Math.Max(ScaleValue(240), sectionContentWidth - num * 2);
		RoundedPanel roundedPanel2 = new RoundedPanel
		{
			Location = new Point(ScaleValue(18), sectionBodyTop),
			Size = new Size(sectionContentWidth, ScaleValue(252)),
			Padding = new Padding(num),
			SurfaceColor = Color.FromArgb(20, 27, 37),
			BorderColor = Color.FromArgb(52, 66, 84),
			BorderThickness = 1,
			CornerRadius = ScaleValue(16)
		};
		Label label = new Label
		{
			AutoSize = true,
			ForeColor = AppTheme.PrimaryText,
			Font = new Font("Segoe UI Semibold", 11.6f, FontStyle.Bold, GraphicsUnit.Point),
			Text = "Chọn nhanh hoặc nhập số tiền muốn nạp"
		};
		Label label2 = new Label
		{
			AutoSize = true,
			MaximumSize = new Size(num2, 0),
			ForeColor = AppTheme.SecondaryText,
			Font = new Font("Segoe UI", 8.8f, FontStyle.Regular, GraphicsUnit.Point),
			Text = $"Mức hợp lệ: từ {FormatAmount(response.MinimumTopUpAmount)} đến {FormatAmount(response.MaximumTopUpAmount)}."
		};
		RoundedPanel amountShell = new RoundedPanel
		{
			Size = new Size(num2, ScaleValue(48)),
			SurfaceColor = Color.FromArgb(22, 31, 43),
			BorderColor = Color.FromArgb(61, 80, 106),
			BorderThickness = 1,
			CornerRadius = ScaleValue(10)
		};
		TextBox amountTextBox = new TextBox
		{
			BorderStyle = BorderStyle.None,
			BackColor = amountShell.SurfaceColor,
			ForeColor = AppTheme.PrimaryText,
			Font = new Font("Segoe UI Semibold", 11f, FontStyle.Bold, GraphicsUnit.Point),
			PlaceholderText = "Nhập số tiền cần nạp (VND)",
			Text = walletAmountDraft,
			Location = new Point(ScaleValue(14), ScaleValue(14)),
			Width = amountShell.Width - ScaleValue(28),
			Anchor = (AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right)
		};
		StoreUiHelper.RefreshCueBanner(amountTextBox);
		amountTextBox.TextChanged += delegate
		{
			walletAmountDraft = amountTextBox.Text;
		};
		amountShell.Resize += delegate
		{
			amountTextBox.Width = amountShell.Width - ScaleValue(28);
		};
		amountShell.Controls.Add(amountTextBox);
		FlowLayoutPanel flowLayoutPanel = new FlowLayoutPanel
		{
			Width = num2,
			AutoSize = true,
			AutoSizeMode = AutoSizeMode.GrowAndShrink,
			MaximumSize = new Size(num2, 0),
			FlowDirection = FlowDirection.LeftToRight,
			WrapContents = true,
			Margin = Padding.Empty,
			Padding = Padding.Empty,
			BackColor = Color.Transparent
		};
		foreach (DesktopAccountWalletQuickAmountResponse quickAmount in response.QuickAmounts)
		{
			Button button = new Button
			{
				AutoSize = true,
				AutoSizeMode = AutoSizeMode.GrowAndShrink,
				FlatStyle = FlatStyle.Flat,
				BackColor = Color.FromArgb(28, 38, 53),
				ForeColor = AppTheme.PrimaryText,
				Font = new Font("Segoe UI Semibold", 8.8f, FontStyle.Bold, GraphicsUnit.Point),
				Padding = new Padding(ScaleValue(12), ScaleValue(8), ScaleValue(12), ScaleValue(8)),
				Margin = new Padding(0, 0, ScaleValue(10), ScaleValue(10)),
				Text = quickAmount.AmountText,
				Cursor = Cursors.Hand,
				Tag = quickAmount.Amount
			};
			button.FlatAppearance.BorderColor = Color.FromArgb(76, 101, 117);
			button.FlatAppearance.BorderSize = 1;
			button.Click += delegate
			{
				amountTextBox.Text = quickAmount.Amount.ToString("0", CultureInfo.InvariantCulture);
				amountTextBox.SelectionStart = amountTextBox.TextLength;
				amountTextBox.Focus();
			};
			flowLayoutPanel.Controls.Add(button);
		}
		GradientButton gradientButton = new GradientButton
		{
			Text = (isCreatingWalletTopUp ? "Đang tạo QR..." : "Tạo phiếu nạp tiền"),
			Width = Math.Min(ScaleValue(220), num2),
			Height = ScaleValue(48),
			StartColor = Color.FromArgb(69, 169, 124),
			EndColor = Color.FromArgb(37, 116, 79),
			CornerRadius = ScaleValue(14),
			Font = new Font("Segoe UI Semibold", 10.2f, FontStyle.Bold, GraphicsUnit.Point),
			Enabled = !isCreatingWalletTopUp
		};
		gradientButton.Click += async delegate
		{
			await BeginWalletTopUpAsync(amountTextBox.Text);
		};
		FlowLayoutPanel flowLayoutPanel2 = new FlowLayoutPanel
		{
			Location = new Point(num, num),
			Width = num2,
			AutoSize = true,
			AutoSizeMode = AutoSizeMode.GrowAndShrink,
			FlowDirection = FlowDirection.TopDown,
			WrapContents = false,
			Margin = Padding.Empty,
			Padding = Padding.Empty,
			BackColor = Color.Transparent
		};
		label.Margin = Padding.Empty;
		label2.Margin = new Padding(0, ScaleValue(8), 0, 0);
		amountShell.Margin = new Padding(0, ScaleValue(16), 0, 0);
		flowLayoutPanel.Margin = new Padding(0, ScaleValue(16), 0, 0);
		gradientButton.Margin = new Padding(0, ScaleValue(10), 0, 0);
		flowLayoutPanel2.Controls.Add(label);
		flowLayoutPanel2.Controls.Add(label2);
		flowLayoutPanel2.Controls.Add(amountShell);
		flowLayoutPanel2.Controls.Add(flowLayoutPanel);
		flowLayoutPanel2.Controls.Add(gradientButton);
		roundedPanel2.Controls.Add(flowLayoutPanel2);
		flowLayoutPanel2.PerformLayout();
		roundedPanel2.Height = flowLayoutPanel2.Bottom + num;
		roundedPanel.Controls.Add(roundedPanel2);
		roundedPanel.Height = roundedPanel2.Bottom + ScaleValue(18);
		return roundedPanel;
	}

	private RoundedPanel CreateWalletMetaCard(string label, string value, int width, int height)
	{
		RoundedPanel obj = new RoundedPanel
		{
			Size = new Size(width, height),
			Padding = new Padding(ScaleValue(12), ScaleValue(10), ScaleValue(12), ScaleValue(10)),
			SurfaceColor = Color.FromArgb(24, 35, 48),
			BorderColor = Color.FromArgb(63, 86, 105),
			BorderThickness = 1,
			CornerRadius = ScaleValue(12)
		};
		Label value2 = new Label
		{
			AutoSize = false,
			Dock = DockStyle.Top,
			Height = ScaleValue(18),
			ForeColor = AppTheme.SecondaryText,
			Font = new Font("Segoe UI", 8.1f, FontStyle.Regular, GraphicsUnit.Point),
			Text = label,
			TextAlign = ContentAlignment.MiddleLeft
		};
		Label value3 = new Label
		{
			AutoSize = false,
			Dock = DockStyle.Fill,
			ForeColor = AppTheme.PrimaryText,
			Font = new Font("Segoe UI Semibold", 9.8f, FontStyle.Bold, GraphicsUnit.Point),
			Text = value,
			TextAlign = ContentAlignment.MiddleLeft
		};
		obj.Controls.Add(value3);
		obj.Controls.Add(value2);
		return obj;
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

	private int GetWalletMetricColumnCount(int contentWidth)
	{
		if (contentWidth >= ScaleValue(860))
		{
			return 3;
		}
		if (contentWidth >= ScaleValue(560))
		{
			return 2;
		}
		return 1;
	}

	private Control CreateWalletTransactionsSection(DesktopAccountResponse response)
	{
		RoundedPanel roundedPanel = CreateSectionShell("Lịch sử giao dịch ví");
		int sectionContentWidth = GetSectionContentWidth();
		int sectionBodyTop = GetSectionBodyTop();
		if (response.WalletTransactions.Count == 0)
		{
			roundedPanel.Height = sectionBodyTop + ScaleValue(58) + ScaleValue(18);
			roundedPanel.Controls.Add(CreateEmptyState("Chưa có giao dịch nào trong ví NestG.", new Point(ScaleValue(18), sectionBodyTop), sectionContentWidth));
			return roundedPanel;
		}
		FlowLayoutPanel flowLayoutPanel = new FlowLayoutPanel
		{
			Location = new Point(ScaleValue(18), sectionBodyTop),
			Size = new Size(sectionContentWidth, 10),
			AutoSize = true,
			AutoSizeMode = AutoSizeMode.GrowAndShrink,
			FlowDirection = FlowDirection.TopDown,
			WrapContents = false,
			Margin = Padding.Empty,
			Padding = Padding.Empty
		};
		foreach (DesktopAccountWalletTransactionResponse walletTransaction in response.WalletTransactions)
		{
			Control value = CreateWalletTransactionCard(walletTransaction, sectionContentWidth);
			flowLayoutPanel.Controls.Add(value);
		}
		roundedPanel.AutoSize = true;
		roundedPanel.AutoSizeMode = AutoSizeMode.GrowAndShrink;
		roundedPanel.Controls.Add(flowLayoutPanel);
		return roundedPanel;
	}

	private Control CreateWalletTransactionCard(DesktopAccountWalletTransactionResponse transaction, int contentWidth)
	{
		RoundedPanel card = new RoundedPanel
		{
			Width = contentWidth,
			Height = ScaleValue(138),
			Margin = new Padding(0, 0, 0, ScaleValue(14)),
			Padding = new Padding(ScaleValue(18)),
			SurfaceColor = Color.FromArgb(18, 24, 33),
			BorderColor = AppTheme.CardBorder,
			BorderThickness = 1,
			CornerRadius = ScaleValue(16)
		};
		Label titleLabel = new Label
		{
			AutoSize = false,
			ForeColor = AppTheme.PrimaryText,
			Font = new Font("Segoe UI Semibold", 11.3f, FontStyle.Bold, GraphicsUnit.Point),
			Text = transaction.Title,
			AutoEllipsis = true
		};
		Label subtitleLabel = new Label
		{
			AutoSize = false,
			ForeColor = AppTheme.SecondaryText,
			Font = new Font("Segoe UI", 8.9f, FontStyle.Regular, GraphicsUnit.Point),
			Text = transaction.Subtitle,
			AutoEllipsis = true
		};
		Label amountLabel = new Label
		{
			AutoSize = false,
			ForeColor = (string.Equals(transaction.Direction, "credit", StringComparison.OrdinalIgnoreCase) ? Color.FromArgb(133, 231, 176) : Color.FromArgb(255, 214, 137)),
			Font = new Font("Segoe UI Semibold", 12.6f, FontStyle.Bold, GraphicsUnit.Point),
			Text = transaction.AmountText,
			TextAlign = ContentAlignment.MiddleRight
		};
		Label createdAtLabel = new Label
		{
			AutoSize = false,
			ForeColor = Color.FromArgb(138, 156, 181),
			Font = new Font("Segoe UI", 8.6f, FontStyle.Regular, GraphicsUnit.Point),
			Text = transaction.CreatedAtText,
			AutoEllipsis = true
		};
		Label directionLabel = new Label
		{
			AutoSize = true,
			BackColor = (string.Equals(transaction.Direction, "credit", StringComparison.OrdinalIgnoreCase) ? Color.FromArgb(35, 74, 58) : Color.FromArgb(78, 56, 28)),
			ForeColor = (string.Equals(transaction.Direction, "credit", StringComparison.OrdinalIgnoreCase) ? Color.FromArgb(191, 238, 205) : Color.FromArgb(255, 223, 162)),
			Padding = new Padding(ScaleValue(12), ScaleValue(6), ScaleValue(12), ScaleValue(6)),
			Font = new Font("Segoe UI Semibold", 8.4f, FontStyle.Bold, GraphicsUnit.Point),
			Text = transaction.DirectionText.ToUpperInvariant()
		};
		Label statusLabel = new Label
		{
			AutoSize = true,
			Padding = new Padding(ScaleValue(12), ScaleValue(6), ScaleValue(12), ScaleValue(6)),
			Font = new Font("Segoe UI Semibold", 8.4f, FontStyle.Bold, GraphicsUnit.Point),
			Text = transaction.StatusText.ToUpperInvariant()
		};
		ApplyTransactionStatusTheme(statusLabel, transaction.StatusTone);
		Button actionButton = new Button
		{
			Visible = !string.IsNullOrWhiteSpace(transaction.OrderCode),
			Text = (string.IsNullOrWhiteSpace(transaction.ActionLabel) ? "Mở chi tiết" : transaction.ActionLabel),
			Width = ScaleValue(144),
			Height = ScaleValue(40),
			FlatStyle = FlatStyle.Flat,
			BackColor = Color.FromArgb(31, 42, 58),
			ForeColor = AppTheme.PrimaryText,
			Font = new Font("Segoe UI Semibold", 9.2f, FontStyle.Bold, GraphicsUnit.Point),
			Cursor = Cursors.Hand
		};
		actionButton.FlatAppearance.BorderColor = Color.FromArgb(84, 103, 129);
		actionButton.FlatAppearance.BorderSize = 1;
		actionButton.Click += async delegate
		{
			if (!string.IsNullOrWhiteSpace(transaction.OrderCode))
			{
				await OpenCheckoutDialogAsync(transaction.OrderCode);
			}
		};
		card.Controls.Add(actionButton);
		card.Controls.Add(statusLabel);
		card.Controls.Add(directionLabel);
		card.Controls.Add(createdAtLabel);
		card.Controls.Add(amountLabel);
		card.Controls.Add(subtitleLabel);
		card.Controls.Add(titleLabel);
		card.Resize += delegate
		{
			LayoutCard();
		};
		LayoutCard();
		return card;
		void LayoutCard()
		{
			int num = ScaleValue(18);
			int num2 = card.ClientSize.Width - num;
			int num3 = ScaleValue(190);
			titleLabel.Location = new Point(num, num);
			titleLabel.Size = new Size(Math.Max(ScaleValue(220), num2 - num - num3 - ScaleValue(12)), ScaleValue(26));
			subtitleLabel.Location = new Point(num, titleLabel.Bottom + ScaleValue(4));
			subtitleLabel.Size = new Size(titleLabel.Width, ScaleValue(20));
			directionLabel.Location = new Point(num, subtitleLabel.Bottom + ScaleValue(12));
			statusLabel.Location = new Point(directionLabel.Right + ScaleValue(10), subtitleLabel.Bottom + ScaleValue(12));
			createdAtLabel.Location = new Point(num, directionLabel.Bottom + ScaleValue(10));
			createdAtLabel.Size = new Size(titleLabel.Width, ScaleValue(18));
			amountLabel.Location = new Point(num2 - num3, num + ScaleValue(6));
			amountLabel.Size = new Size(num3, ScaleValue(28));
			actionButton.Location = new Point(num2 - actionButton.Width, card.ClientSize.Height - actionButton.Height - num);
		}
	}

	private Control CreateOrdersSection(string title, IReadOnlyList<DesktopAccountOrderResponse> orders, string emptyMessage)
	{
		RoundedPanel roundedPanel = CreateSectionShell(title);
		int sectionContentWidth = GetSectionContentWidth();
		int sectionBodyTop = GetSectionBodyTop();
		if (orders.Count == 0)
		{
			roundedPanel.Height = sectionBodyTop + ScaleValue(58) + ScaleValue(18);
			roundedPanel.Controls.Add(CreateEmptyState(emptyMessage, new Point(ScaleValue(18), sectionBodyTop), sectionContentWidth));
			return roundedPanel;
		}
		FlowLayoutPanel flowLayoutPanel = new FlowLayoutPanel
		{
			Location = new Point(ScaleValue(18), sectionBodyTop),
			Size = new Size(sectionContentWidth, 10),
			AutoSize = true,
			AutoSizeMode = AutoSizeMode.GrowAndShrink,
			FlowDirection = FlowDirection.TopDown,
			WrapContents = false,
			Margin = Padding.Empty,
			Padding = Padding.Empty
		};
		using (IEnumerator<DesktopAccountOrderResponse> enumerator = orders.GetEnumerator())
		{
			while (enumerator.MoveNext())
			{
				StoreAccountOrderCard value = new StoreAccountOrderCard(enumerator.Current, HandleOrderActionAsync)
				{
					Width = sectionContentWidth
				};
				flowLayoutPanel.Controls.Add(value);
			}
		}
		roundedPanel.AutoSize = true;
		roundedPanel.AutoSizeMode = AutoSizeMode.GrowAndShrink;
		roundedPanel.Controls.Add(flowLayoutPanel);
		return roundedPanel;
	}

	private Control CreateLauncherSection(DesktopAccountResponse response)
	{
		RoundedPanel roundedPanel = CreateSectionShell("Launcher");
		int sectionContentWidth = GetSectionContentWidth();
		int sectionBodyTop = GetSectionBodyTop();
		RoundedPanel roundedPanel2 = new RoundedPanel
		{
			Location = new Point(ScaleValue(18), sectionBodyTop),
			Size = new Size(sectionContentWidth, ScaleValue(108)),
			Padding = new Padding(ScaleValue(18)),
			SurfaceColor = Color.FromArgb(20, 27, 37),
			BorderColor = Color.FromArgb(52, 66, 84),
			BorderThickness = 1,
			CornerRadius = ScaleValue(14)
		};
		Label label = new Label
		{
			AutoSize = true,
			ForeColor = AppTheme.PrimaryText,
			Font = new Font("Segoe UI Semibold", 11.6f, FontStyle.Bold, GraphicsUnit.Point),
			Text = "NestG Launcher đã sẵn sàng"
		};
		Label label2 = new Label
		{
			AutoSize = true,
			ForeColor = AppTheme.SecondaryText,
			Font = new Font("Segoe UI", 9.1f, FontStyle.Regular, GraphicsUnit.Point),
			Text = ((response.LibraryItems.Count == 0) ? "Tài khoản hiện chưa có game trong thư viện." : $"Thư viện hiện có {response.LibraryItems.Count} sản phẩm đã được cấp.")
		};
		GradientButton gradientButton = new GradientButton
		{
			Text = "Mở Thư viện",
			Width = ScaleValue(148),
			Height = ScaleValue(48),
			StartColor = Color.FromArgb(224, 56, 62),
			EndColor = Color.FromArgb(171, 22, 32),
			CornerRadius = ScaleValue(16),
			Font = new Font("Segoe UI Semibold", 10f, FontStyle.Bold, GraphicsUnit.Point)
		};
		gradientButton.Click += async delegate
		{
			if (openLibraryAsync != null)
			{
				await openLibraryAsync();
			}
		};
		bool flag = sectionContentWidth < ScaleValue(760);
		label.Location = new Point(ScaleValue(18), ScaleValue(18));
		label2.MaximumSize = new Size(flag ? (roundedPanel2.Width - ScaleValue(36)) : Math.Max(ScaleValue(220), roundedPanel2.Width - gradientButton.Width - ScaleValue(72)), 0);
		label2.Location = new Point(ScaleValue(18), label.Bottom + ScaleValue(8));
		if (flag)
		{
			gradientButton.Location = new Point(ScaleValue(18), label2.Bottom + ScaleValue(16));
			roundedPanel2.Height = gradientButton.Bottom + ScaleValue(18);
		}
		else
		{
			gradientButton.Location = new Point(roundedPanel2.Width - gradientButton.Width - ScaleValue(18), ScaleValue(26));
			roundedPanel2.Height = Math.Max(ScaleValue(108), label2.Bottom + ScaleValue(18));
		}
		roundedPanel2.Controls.Add(gradientButton);
		roundedPanel2.Controls.Add(label2);
		roundedPanel2.Controls.Add(label);
		roundedPanel.Controls.Add(roundedPanel2);
		roundedPanel.Height = roundedPanel2.Bottom + ScaleValue(18);
		return roundedPanel;
	}

	private Control CreateDownloadsSection(DesktopAccountResponse response)
	{
		RoundedPanel roundedPanel = CreateSectionShell("Game đã được cấp");
		int sectionContentWidth = GetSectionContentWidth();
		int sectionBodyTop = GetSectionBodyTop();
		if (response.LibraryItems.Count == 0)
		{
			roundedPanel.Height = sectionBodyTop + ScaleValue(58) + ScaleValue(18);
			roundedPanel.Controls.Add(CreateEmptyState("Chưa có game nào được cấp vào thư viện.", new Point(ScaleValue(18), sectionBodyTop), sectionContentWidth));
			return roundedPanel;
		}
		FlowLayoutPanel flowLayoutPanel = new FlowLayoutPanel
		{
			Location = new Point(ScaleValue(18), sectionBodyTop),
			Size = new Size(sectionContentWidth, 10),
			AutoSize = true,
			AutoSizeMode = AutoSizeMode.GrowAndShrink,
			FlowDirection = FlowDirection.TopDown,
			WrapContents = false,
			Margin = Padding.Empty,
			Padding = Padding.Empty
		};
		using (List<DesktopAccountLibraryItemResponse>.Enumerator enumerator = response.LibraryItems.GetEnumerator())
		{
			while (enumerator.MoveNext())
			{
				StoreAccountLibraryItemCard value = new StoreAccountLibraryItemCard(enumerator.Current)
				{
					Width = sectionContentWidth
				};
				flowLayoutPanel.Controls.Add(value);
			}
		}
		roundedPanel.AutoSize = true;
		roundedPanel.AutoSizeMode = AutoSizeMode.GrowAndShrink;
		roundedPanel.Controls.Add(flowLayoutPanel);
		return roundedPanel;
	}

	private Label CreateEmptyState(string message, Point location, int width)
	{
		return new Label
		{
			AutoSize = true,
			MaximumSize = new Size(width, 0),
			ForeColor = AppTheme.SecondaryText,
			Font = new Font("Segoe UI", 9.4f, FontStyle.Regular, GraphicsUnit.Point),
			Text = message,
			Location = location
		};
	}

	private RoundedPanel CreateSectionShell(string? title = null, string? subtitle = null)
	{
		int num = ScaleValue(18);
		RoundedPanel roundedPanel = new RoundedPanel
		{
			BackColor = Color.Transparent,
			SurfaceColor = Color.FromArgb(17, 24, 34),
			BorderColor = Color.FromArgb(46, 61, 79),
			BorderThickness = 1,
			CornerRadius = ScaleValue(18),
			Padding = new Padding(num)
		};
		if (string.IsNullOrWhiteSpace(title))
		{
			return roundedPanel;
		}
		Label label = new Label
		{
			AutoSize = true,
			ForeColor = AppTheme.PrimaryText,
			Font = new Font("Segoe UI Semibold", 14.6f, FontStyle.Bold, GraphicsUnit.Point),
			Text = title,
			Location = new Point(num, num)
		};
		roundedPanel.Controls.Add(label);
		if (!string.IsNullOrWhiteSpace(subtitle))
		{
			Label value = new Label
			{
				AutoSize = true,
				MaximumSize = new Size(Math.Max(ScaleValue(260), GetContentWidth() - ScaleValue(70)), 0),
				ForeColor = AppTheme.SecondaryText,
				Font = new Font("Segoe UI", 9.1f, FontStyle.Regular, GraphicsUnit.Point),
				Text = subtitle,
				Location = new Point(num, label.Bottom + ScaleValue(6))
			};
			roundedPanel.Controls.Add(value);
		}
		return roundedPanel;
	}

	private void ApplySidebarSizing(int contentWidth)
	{
		bool flag = contentWidth <= ScaleValue(240);
		int num = (flag ? ScaleValue(16) : ScaleValue(20));
		if (sidebarPanel.Padding.Left != num)
		{
			sidebarPanel.Padding = new Padding(num);
		}
		profileCard.Padding = new Padding(flag ? ScaleValue(14) : ScaleValue(16));
		accountStatusLabel.Padding = (flag ? new Padding(ScaleValue(12), ScaleValue(5), ScaleValue(12), ScaleValue(5)) : new Padding(ScaleValue(14), ScaleValue(6), ScaleValue(14), ScaleValue(6)));
		int width = Math.Max(ScaleValue(194), contentWidth - ScaleValue(flag ? 12 : 8));
		profileCard.Width = width;
		overviewButton.Width = width;
		ordersButton.Width = width;
		downloadsButton.Width = width;
		walletButton.Width = width;
		signOutButton.Width = width;
		UpdateSidebarProfileLayout();
		LayoutSidebarContent();
	}

	private void LayoutSidebarContent()
	{
		int num = ScaleValue(14);
		int num2 = ScaleValue(18);
		int y = 0;
		profileCard.Location = new Point(0, y);
		y = profileCard.Bottom + num2;
		overviewButton.Location = new Point(0, y);
		y = overviewButton.Bottom + num;
		ordersButton.Location = new Point(0, y);
		y = ordersButton.Bottom + num;
		downloadsButton.Location = new Point(0, y);
		y = downloadsButton.Bottom + num2;
		walletButton.Location = new Point(0, y);
		y = walletButton.Bottom + num2;
		signOutButton.Location = new Point(0, y);
		sidebarLayout.AutoScrollMinSize = new Size(0, signOutButton.Bottom);
		if (sidebarLayout.HorizontalScroll.Visible)
		{
			sidebarLayout.HorizontalScroll.Value = 0;
		}
	}

	private int GetSidebarAvailableWidth()
	{
		int num = sidebarPanel.ClientSize.Width - sidebarPanel.Padding.Horizontal;
		if (num <= 0)
		{
			num = sidebarLayout.ClientSize.Width;
		}
		if (signOutButton.Bottom > 0 && signOutButton.Bottom > sidebarLayout.ClientSize.Height)
		{
			num -= SystemInformation.VerticalScrollBarWidth;
		}
		return Math.Max(ScaleValue(210), num);
	}

	private void UpdateSidebarProfileLayout()
	{
		int width = profileCard.Width - profileCard.Padding.Horizontal;
		int left = profileCard.Padding.Left;
		int top = profileCard.Padding.Top;
		bool flag = profileCard.Width <= ScaleValue(220);
		int num = ScaleValue(10);
		int num2 = ScaleValue(4);
		int num3 = ScaleValue(12);
		int num4 = ScaleValue(12);
		int num5 = ScaleValue(14);
		int height = ScaleValue(flag ? 50 : 54);
		accountNameLabel.MaximumSize = new Size(width, 0);
		accountEmailLabel.MaximumSize = new Size(width, 0);
		accountMetaLabel.MaximumSize = new Size(width, 0);
		accountTitleLabel.Location = new Point(left, top);
		accountNameLabel.Location = new Point(left, accountTitleLabel.Bottom + num);
		accountEmailLabel.Location = new Point(left, accountNameLabel.Bottom + num2);
		accountStatusLabel.Location = new Point(left, accountEmailLabel.Bottom + num3);
		accountMetaLabel.Location = new Point(left, accountStatusLabel.Bottom + num4);
		profileStatsLayout.Location = new Point(left, accountMetaLabel.Bottom + num5);
		profileStatsLayout.Size = new Size(width, height);
		profileCard.Height = profileStatsLayout.Bottom + profileCard.Padding.Bottom;
		profileCard.Invalidate();
	}

	private void LayoutMetricCard(Control card, Label captionLabel, Label valueLabel)
	{
		int num = ScaleValue(16);
		int num2 = ScaleValue(14);
		int num3 = ScaleValue(8);
		int num4 = ScaleValue(14);
		int num5 = Math.Max(captionLabel.Font.Height, TextRenderer.MeasureText(captionLabel.Text, captionLabel.Font).Height);
		int val = Math.Max(valueLabel.Font.Height, TextRenderer.MeasureText("Ag", valueLabel.Font).Height) + ScaleValue(4);
		int width = Math.Max(120, card.ClientSize.Width - num * 2);
		int num6 = num2 + num5 + num3;
		int height = Math.Max(val, card.ClientSize.Height - num6 - num4);
		captionLabel.Location = new Point(num, num2);
		captionLabel.Size = new Size(width, num5);
		valueLabel.Location = new Point(num, num6);
		valueLabel.Size = new Size(width, height);
	}

	private int GetMetricColumnCount(int contentWidth)
	{
		if (contentWidth >= ScaleValue(1040))
		{
			return 3;
		}
		if (contentWidth >= ScaleValue(700))
		{
			return 2;
		}
		return 1;
	}

	private int GetSectionBodyTop()
	{
		return ScaleValue(66);
	}

	private int GetSectionContentWidth()
	{
		return Math.Max(ScaleValue(240), GetContentWidth() - ScaleValue(36));
	}

	private int ScaleValue(int value)
	{
		return (int)Math.Round((float)value * ((float)base.DeviceDpi / 96f));
	}

	private static void ApplyTransactionStatusTheme(Label label, string statusTone)
	{
		switch (statusTone)
		{
		case "is-completed":
			label.BackColor = Color.FromArgb(35, 74, 58);
			label.ForeColor = Color.FromArgb(191, 238, 205);
			break;
		case "is-pending":
			label.BackColor = Color.FromArgb(78, 56, 28);
			label.ForeColor = Color.FromArgb(255, 223, 162);
			break;
		case "is-failed":
			label.BackColor = Color.FromArgb(88, 37, 42);
			label.ForeColor = Color.FromArgb(255, 199, 203);
			break;
		default:
			label.BackColor = Color.FromArgb(48, 55, 70);
			label.ForeColor = AppTheme.PrimaryText;
			break;
		}
	}

	private async Task BeginWalletTopUpAsync(string rawAmount)
	{
		if (isCreatingWalletTopUp)
		{
			return;
		}
		DesktopAccountResponse desktopAccountResponse = accountData;
		if (desktopAccountResponse == null)
		{
			return;
		}
		decimal amount = ParseCurrencyValue(rawAmount);
		if (amount < desktopAccountResponse.MinimumTopUpAmount)
		{
			LauncherMessageBox.Show("Số tiền nạp tối thiểu là " + FormatAmount(desktopAccountResponse.MinimumTopUpAmount) + ".", "Nạp tiền vào ví", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
			return;
		}
		if (amount > desktopAccountResponse.MaximumTopUpAmount)
		{
			LauncherMessageBox.Show("Mỗi lần nạp tối đa " + FormatAmount(desktopAccountResponse.MaximumTopUpAmount) + ".", "Nạp tiền vào ví", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
			return;
		}
		try
		{
			isCreatingWalletTopUp = true;
			base.UseWaitCursor = true;
			string text = await DesktopAppContext.AuthSessionManager.GetAccessTokenAsync();
			if (string.IsNullOrWhiteSpace(text))
			{
				throw new InvalidOperationException("Phiên đăng nhập đã hết hạn.");
			}
			DesktopCheckoutPaymentResponse desktopCheckoutPaymentResponse = await storefrontApiClient.CreateWalletTopUpAsync(amount, text);
			walletAmountDraft = amount.ToString("0", CultureInfo.InvariantCulture);
			if (desktopCheckoutPaymentResponse.HasQrCode)
			{
				using StorePaymentQrDialog storePaymentQrDialog = new StorePaymentQrDialog(storefrontApiClient, desktopCheckoutPaymentResponse);
				storePaymentQrDialog.ShowDialog(FindForm());
			}
			else
			{
				LauncherMessageBox.Show(desktopCheckoutPaymentResponse.StatusDescription, desktopCheckoutPaymentResponse.StatusTitle, MessageBoxButtons.OK, desktopCheckoutPaymentResponse.IsPaid ? MessageBoxIcon.Asterisk : MessageBoxIcon.Exclamation);
			}
			await RefreshAccountAsync();
		}
		catch (Exception ex)
		{
			LauncherMessageBox.Show(ex.Message, "Không thể tạo phiếu nạp tiền", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
		}
		finally
		{
			isCreatingWalletTopUp = false;
			base.UseWaitCursor = false;
			if (accountData != null && currentSection == AccountSection.Wallet)
			{
				RenderCurrentSection();
			}
		}
	}

	private async Task OpenCheckoutDialogAsync(string orderCode)
	{
		if (string.IsNullOrWhiteSpace(orderCode))
		{
			return;
		}
		try
		{
			base.UseWaitCursor = true;
			string text = await DesktopAppContext.AuthSessionManager.GetAccessTokenAsync();
			if (string.IsNullOrWhiteSpace(text))
			{
				throw new InvalidOperationException("Phiên đăng nhập đã hết hạn.");
			}
			DesktopCheckoutPaymentResponse desktopCheckoutPaymentResponse = await storefrontApiClient.GetCheckoutAsync(orderCode, text);
			if (desktopCheckoutPaymentResponse.HasQrCode)
			{
				using StorePaymentQrDialog storePaymentQrDialog = new StorePaymentQrDialog(storefrontApiClient, desktopCheckoutPaymentResponse);
				storePaymentQrDialog.ShowDialog(FindForm());
			}
			else
			{
				LauncherMessageBox.Show(desktopCheckoutPaymentResponse.StatusDescription, desktopCheckoutPaymentResponse.StatusTitle, MessageBoxButtons.OK, desktopCheckoutPaymentResponse.IsPaid ? MessageBoxIcon.Asterisk : MessageBoxIcon.Exclamation);
			}
			await RefreshAccountAsync();
		}
		catch (Exception ex)
		{
			LauncherMessageBox.Show(ex.Message, "Không thể mở chi tiết giao dịch", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
		}
		finally
		{
			base.UseWaitCursor = false;
		}
	}

	private async Task HandleOrderActionAsync(DesktopAccountOrderResponse order)
	{
		if (IsPendingOrderStatus(order.Status) && !string.IsNullOrWhiteSpace(order.OrderCode))
		{
			await OpenCheckoutDialogAsync(order.OrderCode);
		}
		else if (!string.IsNullOrWhiteSpace(order.ActionUrl))
		{
			try
			{
				Process.Start(new ProcessStartInfo
				{
					FileName = order.ActionUrl,
					UseShellExecute = true
				});
			}
			catch
			{
				LauncherMessageBox.Show("Không mở được liên kết thanh toán lúc này.", "NestG Launcher", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
			}
		}
	}

	private static bool IsPendingOrderStatus(string? status)
	{
		if (!string.Equals(status, "pending_payment", StringComparison.OrdinalIgnoreCase))
		{
			return string.Equals(status, "processing", StringComparison.OrdinalIgnoreCase);
		}
		return true;
	}

	private static decimal ParseCurrencyValue(string rawText)
	{
		if (string.IsNullOrWhiteSpace(rawText))
		{
			return 0m;
		}
		if (!decimal.TryParse(new string(rawText.Where(char.IsDigit).ToArray()), NumberStyles.Number, CultureInfo.InvariantCulture, out var result))
		{
			return 0m;
		}
		return result;
	}

	private static string FormatAmount(decimal amount)
	{
		return string.Format(CultureInfo.GetCultureInfo("vi-VN"), "{0:N0} đ", Math.Max(0m, amount));
	}

	private void ShowStatusCard(string title, string message)
	{
		contentStack.SuspendLayout();
		contentStack.Controls.Clear();
		contentStack.RowStyles.Clear();
		contentStack.RowCount = 0;
		RoundedPanel roundedPanel = CreateSectionShell(title);
		Label label = new Label
		{
			AutoSize = true,
			MaximumSize = new Size(Math.Max(ScaleValue(260), GetContentWidth() - ScaleValue(80)), 0),
			ForeColor = AppTheme.SecondaryText,
			Font = new Font("Segoe UI", 10.1f, FontStyle.Regular, GraphicsUnit.Point),
			Text = message,
			Location = new Point(ScaleValue(18), GetSectionBodyTop())
		};
		roundedPanel.Controls.Add(label);
		roundedPanel.Height = label.Bottom + ScaleValue(18);
		AddSection(roundedPanel);
		contentStack.ResumeLayout(performLayout: true);
	}

	private async Task BeginSignOutAsync()
	{
		if (LauncherMessageBox.Show("Đăng xuất khỏi launcher và quay lại màn hình đăng nhập?", "NestG Launcher", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
		{
			await signOutAsync();
		}
	}

	private int GetContentWidth()
	{
		int num = contentScrollPanel.ClientSize.Width;
		if (contentScrollPanel.VerticalScroll.Visible)
		{
			num -= SystemInformation.VerticalScrollBarWidth;
		}
		return Math.Max(ScaleValue(320), num - ScaleValue(8));
	}
}
