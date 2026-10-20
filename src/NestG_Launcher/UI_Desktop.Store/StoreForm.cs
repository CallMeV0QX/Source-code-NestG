using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using UI_Desktop.Auth;
using app_desktop;
using app_desktop.CloudRedirect;
using app_desktop.Models;
using app_desktop.Services;

namespace UI_Desktop.Store;

internal class StoreForm : Form
{
	private enum NavigationSection
	{
		Storefront,
		Cart,
		Library,
		Account,
		Placeholder
	}

	private sealed record StorefrontReturnPosition(int ScrollY, int CatalogVisibleCount, Guid GameId);

	private const int FeaturedDesktopMaxWidth = 1380;

	private const int FeaturedCompactWidth = 1160;

	private const int FeaturedTightWidth = 980;

	private const int SpotlightAutoplayIntervalMs = 3500;

	private const int SpotlightTransitionIntervalMs = 16;

	private const int SpotlightTransitionFrameCount = 24;

	private const int AddGamePopupAnimationIntervalMs = 15;

	private const int AddGamePopupAnimationFrameCount = 8;

	private const int StorefrontCatalogBatchSize = 20;

	private readonly StorefrontApiClient storefrontApiClient;

	private readonly DesktopNotificationApiClient notificationApiClient;

	private readonly DesktopUpdateInstallerService updateInstallerService;

	private readonly List<StoreNavButton> navButtons = new List<StoreNavButton>();

	private readonly List<NavigationSection> navSections = new List<NavigationSection>();

	private readonly List<StoreGameCard> visibleCards = new List<StoreGameCard>();

	private readonly List<StoreSpotlightSidebarItem> spotlightSidebarItems = new List<StoreSpotlightSidebarItem>();

	private readonly Timer spotlightTimer = new Timer
	{
		Interval = 3500
	};

	private readonly Timer spotlightTransitionTimer = new Timer
	{
		Interval = 16
	};

	private readonly Timer addGamePopupTimer = new Timer
	{
		Interval = 15
	};

	private readonly Timer notificationRefreshTimer = new Timer
	{
		Interval = 120000
	};

	private readonly StoreCartView cartView;

	private readonly StoreLibraryView libraryView;

	private readonly StoreAccountView accountView;

	private readonly WebView2 storefrontWebView = new WebView2();

	private readonly Panel featuredMediaPanel = new Panel();

	private readonly PictureBox spotlightTransitionPictureBox = new PictureBox();

	private readonly RoundedPanel spotlightOverlayPanel = new RoundedPanel();

	private readonly Label spotlightChipLabel = new Label();

	private readonly Label spotlightTitleLabel = new Label();

	private readonly Label spotlightMetaLabel = new Label();

	private readonly Label spotlightCopyLabel = new Label();

	private readonly FlowLayoutPanel spotlightPriceFlowPanel = new FlowLayoutPanel();

	private readonly Label spotlightDiscountLabel = new Label();

	private readonly Label spotlightOldPriceLabel = new Label();

	private readonly Label spotlightPriceLabel = new Label();

	private readonly FlowLayoutPanel spotlightActionFlowPanel = new FlowLayoutPanel();

	private readonly Button spotlightBuyButton = new Button();

	private readonly Button spotlightFavoriteButton = new Button();

	private readonly Button spotlightPrevButton = new Button();

	private readonly Button spotlightNextButton = new Button();

	private readonly TableLayoutPanel spotlightSidebarLayout = new TableLayoutPanel();

	private readonly Label spotlightSidebarHeadingLabel = new Label();

	private readonly FlowLayoutPanel spotlightSidebarFlowPanel = new FlowLayoutPanel();

	private readonly Panel embeddedDetailHostPanel = new Panel();

	private readonly RoundedPanel addGamePopupPanel = new RoundedPanel();

	private readonly RoundedPanel addGameInputShell = new RoundedPanel();

	private readonly Label addGameCodeLabel = new Label();

	private readonly Label addGameInputIconLabel = new Label();

	private readonly TextBox addGameCodeTextBox = new TextBox();

	private readonly GradientButton addGameSubmitButton = new GradientButton();

	private readonly StoreLoadingOverlay loadingOverlay;

	private StorePageData pageData;

	private IReadOnlyList<StoreGame> spotlightGames;

	private IReadOnlyList<DesktopNotificationItem> desktopNotifications = Array.Empty<DesktopNotificationItem>();

	private Image? featuredArtwork;

	private Image? featuredTransitionArtwork;

	private Image? notificationGlyphImage;

	private Image? cartGlyphImage;

	private Image? infoGlyphImage;

	private StoreGame? selectedSpotlightGame;

	private bool isBindingCategory;

	private bool isOpeningGameDetail;

	private bool isRefreshingFromApi;

	private bool isRefreshingNotifications;

	private bool hasLoadedStorefront;

	private int currentCartItemCount;

	private int featuredArtworkRequestId;

	private int spotlightTransitionStep;

	private int spotlightIndex;

	private NavigationSection currentSection;

	private Guid? activeDetailGameId;

	private Guid? activeSpotlightVisualGameId;

	private StoreGameDetailForm? embeddedDetailForm;

	private bool isAccountPanelHovered;

	private bool isSpotlightTransitionActive;

	private bool isAddGamePopupOpen;

	private bool isAddGamePopupAnimatingOut;

	private int addGamePopupAnimationStep;

	private StoreSpotlightPalette currentSpotlightPalette = StoreSpotlightPalette.Default;

	private StoreSpotlightPalette spotlightTransitionStartPalette = StoreSpotlightPalette.Default;

	private StoreSpotlightPalette spotlightTransitionTargetPalette = StoreSpotlightPalette.Default;

	private Point addGamePopupTargetLocation = Point.Empty;

	private Task? storefrontWebViewInitializationTask;

	private StorefrontReturnPosition? storefrontReturnPosition;

	private StorefrontReturnPosition? pendingStorefrontReturnPosition;

	private IReadOnlyList<StoreGame> filteredStorefrontGames = Array.Empty<StoreGame>();

	private IReadOnlyList<StoreCategorySection> filteredStorefrontCategorySections = Array.Empty<StoreCategorySection>();

	private DesktopAppUpdateInfo? availableDesktopUpdate;

	private string currentWalletBalanceText = "0 đ";

	private bool isStartingDesktopUpdate;

	private IContainer components;

	private TableLayoutPanel rootLayout;

	private Panel headerPanel;

	private TableLayoutPanel headerLayout;

	private TableLayoutPanel brandLayout;

	private PictureBox headerLogoPictureBox;

	private Label launcherNameLabel;

	private Label versionLabel;

	private FlowLayoutPanel navFlowPanel;

	private FlowLayoutPanel actionFlowPanel;

	private Panel notificationHostPanel;

	private StoreIconButton notificationButton;

	private RoundedPanel notificationBadgePanel;

	private Label notificationBadgeLabel;

	private Panel cartHostPanel;

	private StoreIconButton cartButton;

	private RoundedPanel cartBadgePanel;

	private Label cartBadgeLabel;

	private StoreIconButton helpButton;

	private RoundedPanel accountPanel;

	private TableLayoutPanel accountLayout;

	private Label accountNameLabel;

	private Label accountSeparatorLabel;

	private Label balanceLabel;

	private Panel toolbarPanel;

	private TableLayoutPanel toolbarLayout;

	private ComboBox categoryComboBox;

	private RoundedPanel searchPanel;

	private Label searchIconLabel;

	private TextBox searchTextBox;

	private Label resultCountLabel;

	private StoreScrollPanel scrollContentPanel;

	private TableLayoutPanel contentLayout;

	private RoundedPanel featuredPanel;

	private TableLayoutPanel featuredLayout;

	private PictureBox featuredPictureBox;

	private Panel featuredInfoPanel;

	private TableLayoutPanel featuredInfoLayout;

	private Label featuredPromoLabel;

	private Label featuredTitleLabel;

	private Label featuredSubtitleLabel;

	private TableLayoutPanel featuredSpecsLayout;

	private Label originalPriceCaptionLabel;

	private Label originalPriceValueLabel;

	private Label discountCaptionLabel;

	private Label discountValueLabel;

	private Label salePriceCaptionLabel;

	private Label salePriceValueLabel;

	private Label saleLimitCaptionLabel;

	private Label saleLimitValueLabel;

	private Label genresCaptionLabel;

	private Label genresValueLabel;

	private FlowLayoutPanel sectionHeaderFlowPanel;

	private Label catalogTitleLabel;

	private Label catalogHintLabel;

	private Label emptyStateLabel;

	private StoreCatalogGridPanel catalogFlowPanel;

	private Button addGameButton;

	private static bool UseHtmlStorefront => true;

	public StoreForm()
	{
		storefrontApiClient = new StorefrontApiClient(DesktopAppContext.Settings.ApiBaseUrl);
		notificationApiClient = new DesktopNotificationApiClient(DesktopAppContext.Settings.ApiBaseUrl);
		updateInstallerService = new DesktopUpdateInstallerService(DesktopAppContext.Settings.ApiBaseUrl, DesktopAppContext.Settings.StorageFolderName);
		cartView = new StoreCartView(storefrontApiClient, () => NavigateToSectionAsync(NavigationSection.Storefront), OpenAccountWalletAsync);
		libraryView = new StoreLibraryView(storefrontApiClient);
		accountView = new StoreAccountView(storefrontApiClient, HandleSignOutFromAccountAsync, () => NavigateToSectionAsync(NavigationSection.Library));
		pageData = StoreMockData.Create();
		spotlightGames = pageData.SpotlightGames.Take(6).ToArray();
		InitializeComponent();
		loadingOverlay = new StoreLoadingOverlay(this);
		ConfigureView();
		BindEvents();
		ShowLoadingState();
		loadingOverlay.Show("Đang mở cửa hàng", "NestG Launcher đang tải nội dung và hình ảnh để bạn vào cửa hàng mượt hơn.", "Đang tải danh sách trò chơi");
	}

	protected override async void OnShown(EventArgs e)
	{
		base.OnShown(e);
		if (hasLoadedStorefront)
		{
			return;
		}
		hasLoadedStorefront = true;
		try
		{
			try
			{
				await DesktopAppContext.PaymentRealtimeClient.StartAsync();
			}
			catch
			{
			}
			await RefreshFromApiAsync();
			loadingOverlay.UpdateStatus("Đang tải ảnh nổi bật");
			await EnsureStartupSpotlightVisualReadyAsync();
		}
		finally
		{
			await loadingOverlay.HideAsync();
		}
		await RefreshDesktopNotificationsAsync(showPopups: true);
		notificationRefreshTimer.Start();
		ShowCloudRedirectPrompt();
	}

	protected override void OnFormClosed(FormClosedEventArgs e)
	{
		spotlightTimer.Stop();
		spotlightTransitionTimer.Stop();
		addGamePopupTimer.Stop();
		notificationRefreshTimer.Stop();
		embeddedDetailForm?.Dispose();
		featuredArtwork?.Dispose();
		featuredTransitionArtwork?.Dispose();
		notificationGlyphImage?.Dispose();
		cartGlyphImage?.Dispose();
		infoGlyphImage?.Dispose();
		loadingOverlay.Dispose();
		base.OnFormClosed(e);
	}

	private void ShowCloudRedirectPrompt()
	{
		if (base.IsDisposed || !base.Visible || GameActivationService.UsesBetterSteamToolsRuntime)
		{
			return;
		}
		using CloudRedirectPromptForm cloudRedirectPromptForm = new CloudRedirectPromptForm(storefrontApiClient);
		cloudRedirectPromptForm.ShowDialog(this);
	}

	private void ConfigureView()
	{
		StoreUiHelper.ApplyWindowIcon(this);
		StoreUiHelper.EnableDoubleBuffer(scrollContentPanel);
		StoreUiHelper.EnableDoubleBuffer(contentLayout);
		StoreUiHelper.EnableDoubleBuffer(featuredPanel);
		StoreUiHelper.EnableDoubleBuffer(featuredLayout);
		StoreUiHelper.EnableDoubleBuffer(featuredMediaPanel);
		StoreUiHelper.EnableDoubleBuffer(featuredInfoPanel);
		StoreUiHelper.EnableDoubleBuffer(catalogFlowPanel);
		StoreUiHelper.EnableDoubleBuffer(cartView);
		StoreUiHelper.EnableDoubleBuffer(libraryView);
		StoreUiHelper.EnableDoubleBuffer(accountView);
		StoreUiHelper.LoadLogo(headerLogoPictureBox);
		StoreUiHelper.ConfigureSearchInput(searchPanel, searchIconLabel, searchTextBox);
		ConfigureAccountShell();
		ConfigureSpotlightShell();
		ConfigureEmbeddedDetailHost();
		ConfigureStorefrontWebView();
		ConfigureFloatingAddGameUi();
		featuredPanel.Dock = DockStyle.None;
		featuredPanel.Anchor = AnchorStyles.Top;
		featuredPanel.SurfaceColor = Color.FromArgb(14, 19, 28);
		featuredPanel.BorderColor = Color.FromArgb(49, 70, 98);
		featuredPanel.BorderThickness = 1;
		featuredPanel.CornerRadius = 28;
		contentLayout.BackColor = AppTheme.WindowBackground;
		catalogFlowPanel.AutoSize = false;
		catalogFlowPanel.BackColor = Color.Transparent;
		salePriceCaptionLabel.Text = "Giá sau sale";
		saleLimitCaptionLabel.Text = "Hạn sale";
		catalogTitleLabel.Text = "Kham pha";
		catalogHintLabel.Text = "Đang tải danh sách game storefront";
		searchTextBox.PlaceholderText = "Tìm trò chơi, thể loại hoặc điểm nổi bật...";
		StoreUiHelper.RefreshCueBanner(searchTextBox);
		notificationGlyphImage = StoreUiHelper.LoadAssetImage("icons8-bell-48.png");
		cartGlyphImage = StoreUiHelper.LoadAssetImage("online-shopping.png");
		infoGlyphImage = StoreUiHelper.LoadAssetImage("info.png");
		notificationButton.GlyphImage = notificationGlyphImage;
		cartButton.GlyphImage = cartGlyphImage;
		helpButton.GlyphImage = infoGlyphImage;
		cartView.Visible = false;
		libraryView.Visible = false;
		accountView.Visible = false;
		storefrontWebView.Visible = false;
		cartView.CartCountChanged += HandleCartCountChanged;
		cartView.WalletBalanceChanged += HandleWalletBalanceChanged;
		accountView.WalletBalanceChanged += HandleWalletBalanceChanged;
		scrollContentPanel.Controls.Add(cartView);
		scrollContentPanel.Controls.Add(libraryView);
		scrollContentPanel.Controls.Add(accountView);
		UpdateFloatingAddGameLayout();
	}

	private void ShowLoadingState()
	{
		spotlightTimer.Stop();
		spotlightTransitionTimer.Stop();
		featuredArtwork?.Dispose();
		featuredArtwork = null;
		featuredTransitionArtwork?.Dispose();
		featuredTransitionArtwork = null;
		activeSpotlightVisualGameId = null;
		featuredPictureBox.Image = null;
		spotlightTransitionPictureBox.Image = null;
		spotlightTransitionPictureBox.Visible = false;
		isSpotlightTransitionActive = false;
		ApplySpotlightPalette(StoreSpotlightPalette.Default);
		featuredPromoLabel.Text = "ĐANG TẢI";
		featuredPromoLabel.BackColor = Color.FromArgb(56, 63, 79);
		featuredTitleLabel.Text = "Đang tải game thịnh hành...";
		featuredSubtitleLabel.Text = "Storefront desktop đang đồng bộ dữ liệu";
		originalPriceValueLabel.Text = "--";
		discountValueLabel.Text = "--";
		salePriceValueLabel.Text = "--";
		saleLimitValueLabel.Text = "--";
		genresValueLabel.Text = "--";
	}

	private void ConfigureEmbeddedDetailHost()
	{
		embeddedDetailHostPanel.Dock = DockStyle.Fill;
		embeddedDetailHostPanel.Margin = Padding.Empty;
		embeddedDetailHostPanel.Padding = Padding.Empty;
		embeddedDetailHostPanel.BackColor = AppTheme.WindowBackground;
		embeddedDetailHostPanel.Visible = false;
		StoreUiHelper.EnableDoubleBuffer(embeddedDetailHostPanel);
		featuredPanel.Controls.Add(embeddedDetailHostPanel);
		embeddedDetailHostPanel.BringToFront();
	}

	private void ConfigureStorefrontWebView()
	{
		storefrontWebView.Dock = DockStyle.Fill;
		storefrontWebView.Margin = Padding.Empty;
		storefrontWebView.BackColor = AppTheme.WindowBackground;
		storefrontWebView.Visible = false;
		scrollContentPanel.Controls.Add(storefrontWebView);
		storefrontWebView.BringToFront();
	}

	private Task EnsureStorefrontWebViewAsync()
	{
		if (storefrontWebViewInitializationTask == null)
		{
			storefrontWebViewInitializationTask = EnsureStorefrontWebViewCoreAsync();
		}
		return storefrontWebViewInitializationTask;
	}

	private async Task EnsureStorefrontWebViewCoreAsync()
	{
		await storefrontWebView.EnsureCoreWebView2Async();
		if (!storefrontWebView.IsDisposed && storefrontWebView.CoreWebView2 != null)
		{
			CoreWebView2Settings settings = storefrontWebView.CoreWebView2.Settings;
			settings.AreDefaultContextMenusEnabled = false;
			settings.AreDevToolsEnabled = false;
			settings.AreBrowserAcceleratorKeysEnabled = false;
			settings.IsStatusBarEnabled = false;
			settings.IsZoomControlEnabled = false;
			storefrontWebView.CoreWebView2.WebMessageReceived -= StorefrontWebView_WebMessageReceived;
			storefrontWebView.CoreWebView2.WebMessageReceived += StorefrontWebView_WebMessageReceived;
			storefrontWebView.CoreWebView2.NavigationStarting -= StorefrontWebView_NavigationStarting;
			storefrontWebView.CoreWebView2.NavigationStarting += StorefrontWebView_NavigationStarting;
			storefrontWebView.CoreWebView2.NavigationCompleted -= StorefrontWebView_NavigationCompleted;
			storefrontWebView.CoreWebView2.NavigationCompleted += StorefrontWebView_NavigationCompleted;
		}
	}

	private async Task RefreshStorefrontDocumentAsync()
	{
		if (UseHtmlStorefront && currentSection == NavigationSection.Storefront && (embeddedDetailForm == null || embeddedDetailForm.IsDisposed))
		{
			await EnsureStorefrontWebViewAsync();
			if (!storefrontWebView.IsDisposed)
			{
				int activeSpotlightIndex = ((spotlightGames.Count != 0) ? Math.Clamp(spotlightIndex, 0, spotlightGames.Count - 1) : 0);
				storefrontWebView.NavigateToString(StorefrontHomeHtmlBuilder.BuildDocument(pageData, spotlightGames, filteredStorefrontCategorySections, filteredStorefrontGames, activeSpotlightIndex));
			}
		}
	}

	private async Task<StorefrontReturnPosition> CaptureStorefrontReturnPositionAsync(Guid gameId)
	{
		StorefrontReturnPosition fallback = new StorefrontReturnPosition(0, 20, gameId);
		if (!UseHtmlStorefront)
		{
			return fallback;
		}
		try
		{
			await EnsureStorefrontWebViewAsync();
			if (storefrontWebView.IsDisposed || storefrontWebView.CoreWebView2 == null)
			{
				return fallback;
			}
			string text = JsonSerializer.Deserialize<string>(await storefrontWebView.CoreWebView2.ExecuteScriptAsync("(() => {\r\n    const scrollElement = document.scrollingElement || document.documentElement;\r\n    const rawVisibleCount = window.__nestgStorefront?.getCatalogVisibleCount?.();\r\n    const domVisibleCount = document.querySelectorAll('.catalog-card[data-open-game]').length;\r\n    const parsedVisibleCount = Number(rawVisibleCount);\r\n    const catalogVisibleCount = Math.max(0, Math.round(Number.isFinite(parsedVisibleCount) ? parsedVisibleCount : domVisibleCount));\r\n    const scrollY = Math.max(0, Math.round(window.scrollY || scrollElement?.scrollTop || 0));\r\n    return JSON.stringify({ scrollY, catalogVisibleCount });\r\n})()"));
			if (string.IsNullOrWhiteSpace(text))
			{
				return fallback;
			}
			using JsonDocument jsonDocument = JsonDocument.Parse(text);
			JsonElement rootElement = jsonDocument.RootElement;
			return new StorefrontReturnPosition(ReadNonNegativeInt32(rootElement, "scrollY"), ReadNonNegativeInt32(rootElement, "catalogVisibleCount", 20), gameId);
		}
		catch (Exception)
		{
			return fallback;
		}
	}

	private async Task RestoreStorefrontReturnPositionAsync()
	{
		StorefrontReturnPosition returnPosition = pendingStorefrontReturnPosition ?? storefrontReturnPosition;
		pendingStorefrontReturnPosition = null;
		storefrontReturnPosition = null;
		if (!UseHtmlStorefront || (object)returnPosition == null || currentSection != 0 || storefrontWebView.IsDisposed)
		{
			return;
		}
		try
		{
			await EnsureStorefrontWebViewAsync();
			if (!storefrontWebView.IsDisposed && storefrontWebView.CoreWebView2 != null)
			{
				string text = JsonSerializer.Serialize(new
				{
					scrollY = returnPosition.ScrollY,
					catalogVisibleCount = Math.Max(20, returnPosition.CatalogVisibleCount),
					gameId = returnPosition.GameId
				});
				await storefrontWebView.CoreWebView2.ExecuteScriptAsync("window.__nestgStorefront?.restorePosition(" + text + ");");
			}
		}
		catch (Exception)
		{
		}
	}

	private async void StorefrontWebView_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
	{
		if (e.IsSuccess && (object)pendingStorefrontReturnPosition != null)
		{
			await RestoreStorefrontReturnPositionAsync();
		}
	}

	private static int ReadNonNegativeInt32(JsonElement root, string propertyName, int defaultValue = 0)
	{
		if (!root.TryGetProperty(propertyName, out var value))
		{
			return Math.Max(0, defaultValue);
		}
		if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var value2))
		{
			return Math.Max(0, value2);
		}
		if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out value2))
		{
			return Math.Max(0, value2);
		}
		return Math.Max(0, defaultValue);
	}

	private async void StorefrontWebView_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
	{
		string text = e.TryGetWebMessageAsString();
		if (string.IsNullOrWhiteSpace(text))
		{
			return;
		}
		int result4;
		if (text.StartsWith("game:", StringComparison.OrdinalIgnoreCase))
		{
			if (Guid.TryParse(text.Substring("game:".Length), out var result))
			{
				await OpenGameDetailAsync(result);
			}
		}
		else if (text.StartsWith("catalog-count:", StringComparison.OrdinalIgnoreCase))
		{
			string[] array = text.Substring("catalog-count:".Length).Split(':', 2);
			if (array.Length == 2 && int.TryParse(array[0], out var result2) && int.TryParse(array[1], out var result3))
			{
				UpdateCatalogCountText(result2, result3);
			}
		}
		else if (text.StartsWith("favorite:", StringComparison.OrdinalIgnoreCase))
		{
			if (Guid.TryParse(text.Substring("favorite:".Length), out var gameId))
			{
				StoreGame storeGame = pageData.Games.FirstOrDefault((StoreGame item) => item.GameId == gameId) ?? spotlightGames.FirstOrDefault((StoreGame item) => item.GameId == gameId);
				if (storeGame != null)
				{
					LauncherMessageBox.Show("Yêu thích: " + storeGame.Title, "NestG Launcher", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
				}
			}
		}
		else if (text.StartsWith("spotlight:", StringComparison.OrdinalIgnoreCase) && int.TryParse(text.Substring("spotlight:".Length), out result4) && spotlightGames.Count > 0)
		{
			spotlightIndex = Math.Clamp(result4, 0, spotlightGames.Count - 1);
			selectedSpotlightGame = spotlightGames[spotlightIndex];
		}
	}

	private void StorefrontWebView_NavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
	{
		if (!e.IsUserInitiated || e.Uri.StartsWith("about:blank", StringComparison.OrdinalIgnoreCase) || e.Uri.StartsWith("data:text/html", StringComparison.OrdinalIgnoreCase))
		{
			return;
		}
		e.Cancel = true;
		try
		{
			Process.Start(new ProcessStartInfo
			{
				FileName = e.Uri,
				UseShellExecute = true
			});
		}
		catch
		{
		}
	}

	private void ConfigureFloatingAddGameUi()
	{
		if (addGameButton.Parent != null)
		{
			addGameButton.Parent.Controls.Remove(addGameButton);
		}
		addGameButton.Visible = false;
		addGameButton.Enabled = false;
		if (addGamePopupPanel.Parent != null)
		{
			addGamePopupPanel.Parent.Controls.Remove(addGamePopupPanel);
		}
		addGamePopupPanel.Visible = false;
		addGameButton.AutoSize = false;
		addGameButton.Size = new Size(224, 42);
		addGameButton.Margin = Padding.Empty;
		addGameButton.Padding = new Padding(12, 0, 0, 0);
		addGameButton.TextAlign = ContentAlignment.MiddleLeft;
		addGameButton.BackColor = Color.Transparent;
		addGameButton.ForeColor = Color.FromArgb(162, 174, 191);
		addGameButton.FlatStyle = FlatStyle.Flat;
		addGameButton.FlatAppearance.BorderSize = 0;
		addGameButton.FlatAppearance.BorderColor = Color.FromArgb(24, 31, 42);
		addGameButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(24, 31, 42);
		addGameButton.FlatAppearance.MouseDownBackColor = Color.FromArgb(28, 35, 46);
		addGamePopupPanel.Visible = false;
		addGamePopupPanel.Size = new Size(332, 146);
		addGamePopupPanel.Padding = new Padding(14);
		addGamePopupPanel.SurfaceColor = Color.FromArgb(18, 24, 33);
		addGamePopupPanel.BorderColor = Color.FromArgb(58, 73, 94);
		addGamePopupPanel.BorderThickness = 1;
		addGamePopupPanel.CornerRadius = 12;
		addGameCodeLabel.AutoSize = true;
		addGameCodeLabel.Text = "Nhập mã game";
		addGameCodeLabel.ForeColor = AppTheme.PrimaryText;
		addGameCodeLabel.Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold, GraphicsUnit.Point);
		addGameCodeLabel.Location = new Point(14, 14);
		addGameInputShell.Location = new Point(14, 40);
		addGameInputShell.Size = new Size(304, 44);
		addGameInputIconLabel.Text = "⌕";
		addGameCodeTextBox.PlaceholderText = "Nhập mã nhận game";
		StoreUiHelper.ConfigureSearchInput(addGameInputShell, addGameInputIconLabel, addGameCodeTextBox);
		addGameInputShell.CornerRadius = 12;
		addGameInputShell.Controls.Add(addGameInputIconLabel);
		addGameInputShell.Controls.Add(addGameCodeTextBox);
		addGameSubmitButton.Text = "Thêm game";
		addGameSubmitButton.MinimumSize = Size.Empty;
		addGameSubmitButton.Size = new Size(126, 34);
		addGameSubmitButton.Location = new Point(addGamePopupPanel.Width - addGameSubmitButton.Width - 14, addGamePopupPanel.Height - addGameSubmitButton.Height - 14);
		addGameSubmitButton.StartColor = Color.FromArgb(67, 120, 255);
		addGameSubmitButton.EndColor = Color.FromArgb(45, 92, 212);
		addGameSubmitButton.CornerRadius = 4;
		addGameSubmitButton.Font = new Font("Segoe UI Semibold", 9.6f, FontStyle.Bold, GraphicsUnit.Point);
		addGamePopupPanel.Controls.Add(addGameCodeLabel);
		addGamePopupPanel.Controls.Add(addGameSubmitButton);
		addGamePopupPanel.Controls.Add(addGameInputShell);
		addGamePopupPanel.Visible = false;
	}

	private void ConfigureAccountShell()
	{
		accountPanel.Padding = new Padding(10, 7, 10, 7);
		accountLayout.ColumnStyles.Clear();
		accountLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
		accountLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 14f));
		accountLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
		accountNameLabel.AutoSize = false;
		accountNameLabel.Dock = DockStyle.Fill;
		accountNameLabel.Margin = Padding.Empty;
		accountNameLabel.TextAlign = ContentAlignment.MiddleLeft;
		accountSeparatorLabel.AutoSize = true;
		accountSeparatorLabel.Margin = Padding.Empty;
		accountSeparatorLabel.TextAlign = ContentAlignment.MiddleCenter;
		balanceLabel.AutoSize = true;
		balanceLabel.Dock = DockStyle.None;
		balanceLabel.Anchor = AnchorStyles.Left;
		balanceLabel.Margin = new Padding(6, 0, 0, 0);
		balanceLabel.TextAlign = ContentAlignment.MiddleCenter;
		AttachAccountPanelInteractions(accountPanel);
		UpdateAccountPanelChrome();
	}

	private void BindEvents()
	{
		notificationButton.Click -= notificationButton_Click;
		notificationButton.Click += notificationButton_Click;
		notificationHostPanel.Click += delegate
		{
			notificationButton.PerformClick();
		};
		notificationBadgePanel.Click += delegate
		{
			notificationButton.PerformClick();
		};
		notificationBadgeLabel.Click += delegate
		{
			notificationButton.PerformClick();
		};
		cartButton.Click += async delegate
		{
			await NavigateToSectionAsync(NavigationSection.Cart);
		};
		helpButton.Click += delegate
		{
			LauncherMessageBox.Show("Spotlight desktop đang lấy API riêng và tự động xoay game thịnh hành.", "NestG Launcher", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
		};
		spotlightBuyButton.Click += async delegate
		{
			await OpenSelectedSpotlightDetailAsync();
		};
		spotlightFavoriteButton.Click += delegate
		{
			ShowSpotlightAction("Yêu thích");
		};
		cartBadgePanel.Click += delegate
		{
			cartButton.PerformClick();
		};
		cartBadgeLabel.Click += delegate
		{
			cartButton.PerformClick();
		};
		base.Resize += delegate
		{
			UpdateResponsiveLayout();
		};
		scrollContentPanel.SizeChanged += delegate
		{
			UpdateResponsiveLayout();
		};
		featuredMediaPanel.SizeChanged += delegate
		{
			HandleSpotlightViewportChanged();
		};
		categoryComboBox.SelectedIndexChanged += delegate
		{
			if (!isBindingCategory)
			{
				RenderCatalog();
			}
		};
		searchTextBox.TextChanged += delegate
		{
			RenderCatalog();
		};
		spotlightTimer.Tick += SpotlightTimer_Tick;
		spotlightTransitionTimer.Tick += SpotlightTransitionTimer_Tick;
		addGamePopupTimer.Tick += AddGamePopupTimer_Tick;
		notificationRefreshTimer.Tick += async delegate
		{
			await RefreshDesktopNotificationsAsync(showPopups: false);
		};
		addGameSubmitButton.Click += addGameSubmitButton_Click;
		addGameCodeTextBox.KeyDown += addGameCodeTextBox_KeyDown;
	}

	private void LoadPageData()
	{
		string text = categoryComboBox.SelectedItem?.ToString();
		string fullAccountName = ResolveAccountDisplayName(DesktopAppContext.AuthSessionManager.CurrentSession, pageData.UserName);
		string text2 = BuildCompactAccountLabel(fullAccountName);
		string navigationAccountName = BuildNavigationAccountLabel(fullAccountName);
		launcherNameLabel.Text = pageData.LauncherName;
		versionLabel.Text = "v" + DesktopAppContext.Settings.CurrentVersion;
		accountNameLabel.Text = text2;
		balanceLabel.Text = (string.IsNullOrWhiteSpace(currentWalletBalanceText) ? pageData.BalanceText : currentWalletBalanceText);
		UpdateCartBadge(currentCartItemCount);
		accountPanel.Width = CalculateAccountPanelWidth(text2, balanceLabel.Text);
		BuildNavigation(navigationAccountName);
		isBindingCategory = true;
		categoryComboBox.Items.Clear();
		foreach (string category in pageData.Categories)
		{
			categoryComboBox.Items.Add(category);
		}
		if (!string.IsNullOrWhiteSpace(text) && categoryComboBox.Items.Contains(text))
		{
			categoryComboBox.SelectedItem = text;
		}
		else if (categoryComboBox.Items.Count > 0)
		{
			categoryComboBox.SelectedIndex = 0;
		}
		isBindingCategory = false;
		RenderCatalog();
		IReadOnlyList<StoreGame> games;
		if (spotlightGames.Count <= 0)
		{
			IReadOnlyList<StoreGame> readOnlyList = pageData.SpotlightGames.Take(6).ToArray();
			games = readOnlyList;
		}
		else
		{
			games = spotlightGames;
		}
		BindSpotlightGames(games);
		UpdateResponsiveLayout();
	}

	private async Task RefreshFromApiAsync()
	{
		if (isRefreshingFromApi)
		{
			return;
		}
		isRefreshingFromApi = true;
		try
		{
			string accessToken = await DesktopAppContext.AuthSessionManager.GetAccessTokenAsync();
			Task<StorefrontHomeResponse?> homeTask = TryGetHomeAsync(accessToken);
			Task<IReadOnlyList<StorefrontProductCardResponse>?> spotlightTask = TryGetSpotlightAsync(accessToken);
			Task<int> cartCountTask = TryGetCartItemCountAsync(accessToken);
			Task<DesktopAccountResponse?> accountTask = TryGetAccountAsync(accessToken);
			await Task.WhenAll(homeTask, spotlightTask, cartCountTask, accountTask);
			pageData = ((homeTask.Result != null) ? StorePageData.FromApi(homeTask.Result) : StoreMockData.Create());
			spotlightGames = ResolveSpotlightGames(spotlightTask.Result, pageData);
			currentCartItemCount = cartCountTask.Result;
			currentWalletBalanceText = accountTask.Result?.WalletBalanceText ?? pageData.BalanceText;
			LoadPageData();
			if (homeTask.Result == null && spotlightTask.Result == null)
			{
				catalogHintLabel.Text = "Đang hiển thị dữ liệu dự phòng vì chưa kết nối được storefront.";
			}
		}
		catch
		{
			pageData = StoreMockData.Create();
			spotlightGames = pageData.SpotlightGames.Take(6).ToArray();
			currentCartItemCount = 0;
			currentWalletBalanceText = pageData.BalanceText;
			LoadPageData();
			catalogHintLabel.Text = "Đang hiển thị dữ liệu dự phòng vì chưa kết nối được storefront.";
		}
		finally
		{
			isRefreshingFromApi = false;
		}
	}

	private async Task<StorefrontHomeResponse?> TryGetHomeAsync(string? accessToken)
	{
		try
		{
			return await storefrontApiClient.GetHomeAsync(accessToken);
		}
		catch
		{
			return null;
		}
	}

	private async Task<IReadOnlyList<StorefrontProductCardResponse>?> TryGetSpotlightAsync(string? accessToken)
	{
		try
		{
			return await storefrontApiClient.GetSpotlightAsync(accessToken);
		}
		catch
		{
			return null;
		}
	}

	private async Task<DesktopAccountResponse?> TryGetAccountAsync(string? accessToken)
	{
		if (string.IsNullOrWhiteSpace(accessToken))
		{
			return null;
		}
		try
		{
			return await storefrontApiClient.GetAccountAsync(accessToken);
		}
		catch
		{
			return null;
		}
	}

	private async Task<int> TryGetCartItemCountAsync(string? accessToken)
	{
		try
		{
			return await storefrontApiClient.GetCartItemCountAsync(accessToken);
		}
		catch
		{
			return 0;
		}
	}

	private static IReadOnlyList<StoreGame> ResolveSpotlightGames(IReadOnlyList<StorefrontProductCardResponse>? spotlightResponse, StorePageData currentPageData)
	{
		StoreGame[] array = ((spotlightResponse != null && spotlightResponse.Count > 0) ? (from game in spotlightResponse.Select(StoreGame.FromApi)
			group game by game.GameId into @group
			select @group.First()).Take(6).ToArray() : (from game in currentPageData.SpotlightGames
			group game by game.GameId into @group
			select @group.First()).Take(6).ToArray());
		if (array.Length == 0)
		{
			StoreGame featuredGame = currentPageData.FeaturedGame;
			if (featuredGame == null)
			{
				return Array.Empty<StoreGame>();
			}
			return new StoreGame[1] { featuredGame };
		}
		return array;
	}

	private void BuildNavigation(string navigationAccountName)
	{
		navFlowPanel.SuspendLayout();
		navFlowPanel.Controls.Clear();
		navButtons.Clear();
		navSections.Clear();
		for (int i = 0; i < pageData.NavigationItems.Count; i++)
		{
			string text = ((i == pageData.NavigationItems.Count - 1) ? navigationAccountName : pageData.NavigationItems[i]);
			NavigationSection section = i switch
			{
				0 => NavigationSection.Storefront, 
				1 => NavigationSection.Library, 
				_ => (i != pageData.NavigationItems.Count - 1) ? NavigationSection.Placeholder : NavigationSection.Account, 
			};
			StoreNavButton storeNavButton = new StoreNavButton
			{
				Text = text,
				IsActive = (section == currentSection)
			};
			storeNavButton.Click += async delegate
			{
				await NavigateToSectionAsync(section);
			};
			navButtons.Add(storeNavButton);
			navSections.Add(section);
			navFlowPanel.Controls.Add(storeNavButton);
		}
		navFlowPanel.ResumeLayout();
	}

	private static string ResolveAccountDisplayName(AuthSession? session, string fallback)
	{
		if (!string.IsNullOrWhiteSpace(session?.DisplayName))
		{
			return NormalizeAccountName(session.DisplayName);
		}
		if (!string.IsNullOrWhiteSpace(session?.Email))
		{
			return NormalizeAccountName(session.Email.Split('@')[0]);
		}
		return NormalizeAccountName(fallback);
	}

	private static string BuildCompactAccountLabel(string fullAccountName)
	{
		return fullAccountName;
	}

	private static string BuildNavigationAccountLabel(string fullAccountName)
	{
		return CultureInfo.GetCultureInfo("vi-VN").TextInfo.ToUpper(fullAccountName);
	}

	private static string NormalizeAccountName(string value)
	{
		return string.Join(" ", value.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
	}

	private int CalculateAccountPanelWidth(string accountName, string balanceText)
	{
		int num = Math.Max(accountNameLabel.GetPreferredSize(Size.Empty).Width, TextRenderer.MeasureText(accountName, accountNameLabel.Font, Size.Empty, TextFormatFlags.NoPadding).Width);
		int num2 = Math.Max(balanceLabel.GetPreferredSize(Size.Empty).Width, TextRenderer.MeasureText(balanceText, balanceLabel.Font, Size.Empty, TextFormatFlags.NoPadding).Width);
		int num3 = Math.Max(14, TextRenderer.MeasureText(accountSeparatorLabel.Text, accountSeparatorLabel.Font, Size.Empty, TextFormatFlags.NoPadding).Width);
		int num4 = 8;
		int num5 = accountPanel.Padding.Horizontal + num3 + num4;
		return Math.Clamp(num + num2 + num5, 190, 520);
	}

	private void AttachAccountPanelInteractions(Control control)
	{
		control.Cursor = Cursors.Hand;
		control.Click += async delegate
		{
			await NavigateToSectionAsync(NavigationSection.Account);
		};
		control.MouseEnter += delegate
		{
			SyncAccountPanelHoverState();
		};
		control.MouseLeave += delegate
		{
			SyncAccountPanelHoverState();
		};
		foreach (Control control2 in control.Controls)
		{
			AttachAccountPanelInteractions(control2);
		}
	}

	private void SyncAccountPanelHoverState()
	{
		isAccountPanelHovered = accountPanel.ClientRectangle.Contains(accountPanel.PointToClient(System.Windows.Forms.Cursor.Position));
		UpdateAccountPanelChrome();
	}

	private void UpdateAccountPanelChrome()
	{
		bool flag = currentSection == NavigationSection.Account;
		accountPanel.SurfaceColor = (flag ? Color.FromArgb(28, 18, 23) : (isAccountPanelHovered ? Color.FromArgb(24, 33, 46) : Color.FromArgb(21, 29, 40)));
		accountPanel.BorderColor = (flag ? Color.FromArgb(212, 62, 69) : (isAccountPanelHovered ? Color.FromArgb(88, 117, 154) : Color.FromArgb(53, 72, 95)));
		accountNameLabel.ForeColor = (flag ? Color.FromArgb(255, 240, 241) : AppTheme.PrimaryText);
		balanceLabel.ForeColor = (flag ? Color.FromArgb(255, 211, 152) : Color.FromArgb(181, 244, 121));
		accountSeparatorLabel.ForeColor = (flag ? Color.FromArgb(160, 119, 124) : Color.FromArgb(91, 106, 127));
		accountPanel.Invalidate();
	}

	private async Task NavigateToSectionAsync(NavigationSection section)
	{
		if (base.InvokeRequired)
		{
			await InvokeOnUiThreadAsync(() => NavigateToSectionAsync(section));
		}
		else if (section == NavigationSection.Placeholder)
		{
			LauncherMessageBox.Show("Mục này đang được thiết kế tiếp. Hiện tại launcher đã mở được page Storefront, Thư viện và Tài khoản.", "NestG Launcher", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
		}
		else
		{
			SetActiveNavigationSection(section);
			await SwitchSectionAsync(section);
		}
	}

	private void SetActiveNavigationSection(NavigationSection section)
	{
		for (int i = 0; i < navButtons.Count; i++)
		{
			navButtons[i].IsActive = navSections[i] == section;
		}
		UpdateAccountPanelChrome();
	}

	private async Task SwitchSectionAsync(NavigationSection section)
	{
		currentSection = section;
		bool flag = section == NavigationSection.Storefront;
		bool isCart = section == NavigationSection.Cart;
		bool isLibrary = section == NavigationSection.Library;
		bool isAccount = section == NavigationSection.Account;
		bool flag2 = embeddedDetailForm != null && !embeddedDetailForm.IsDisposed;
		toolbarPanel.Visible = flag;
		rootLayout.RowStyles[1].Height = (flag ? 60f : 0f);
		contentLayout.Visible = flag && (!UseHtmlStorefront || flag2);
		storefrontWebView.Visible = flag && UseHtmlStorefront && !flag2;
		cartView.Visible = isCart;
		libraryView.Visible = isLibrary;
		accountView.Visible = isAccount;
		scrollContentPanel.AutoScroll = false;
		addGameButton.Visible = false;
		if (!flag)
		{
			HideAddGamePopup(animated: false);
		}
		UpdateAccountPanelChrome();
		if (flag)
		{
			RestartSpotlightAutoplay();
			UpdateResponsiveLayout();
			if (UseHtmlStorefront && !flag2)
			{
				RefreshStorefrontDocumentAsync();
			}
			return;
		}
		spotlightTimer.Stop();
		storefrontWebView.Visible = false;
		(string, string, string) sectionLoadingContent = GetSectionLoadingContent(section);
		await loadingOverlay.RunAsync(sectionLoadingContent.Item1, sectionLoadingContent.Item2, sectionLoadingContent.Item3, async delegate
		{
			if (isCart)
			{
				cartView.BringToFront();
				await cartView.RefreshCartAsync();
			}
			else if (isLibrary)
			{
				libraryView.BringToFront();
				await libraryView.RefreshLibraryAsync();
			}
			else if (isAccount)
			{
				accountView.BringToFront();
				await accountView.RefreshAccountAsync();
			}
		}, 280);
		UpdateResponsiveLayout();
	}

	private Task InvokeOnUiThreadAsync(Func<Task> action)
	{
		if (!base.InvokeRequired)
		{
			return action();
		}
		TaskCompletionSource<object?> taskSource = new TaskCompletionSource<object>();
		BeginInvoke(async delegate
		{
			try
			{
				await action();
				taskSource.TrySetResult(null);
			}
			catch (Exception exception)
			{
				taskSource.TrySetException(exception);
			}
		});
		return taskSource.Task;
	}

	private void RenderCatalog()
	{
		IReadOnlyList<StoreCategorySection> filteredCategorySections = GetFilteredCategorySections();
		List<StoreGame> list = (from game in filteredCategorySections.SelectMany((StoreCategorySection section) => section.Games)
			group game by game.GameId into @group
			select @group.First()).ToList();
		int num = filteredCategorySections.Sum((StoreCategorySection section) => section.Games.Count);
		filteredStorefrontCategorySections = filteredCategorySections;
		filteredStorefrontGames = list;
		UpdateCatalogCountText(UseHtmlStorefront ? Math.Min(20, num) : num, num);
		ClearVisibleCards();
		ApplyStorefrontCatalogVisibility(num == 0);
		if (UseHtmlStorefront)
		{
			RefreshStorefrontDocumentAsync();
			return;
		}
		int scrollableContentWidth = GetScrollableContentWidth();
		if (num == 0)
		{
			UpdateCatalogLayout(scrollableContentWidth);
			return;
		}
		catalogFlowPanel.SuspendLayout();
		foreach (StoreGame item in list)
		{
			StoreGameCard card = new StoreGameCard(item);
			card.SetSelected(activeDetailGameId.HasValue && activeDetailGameId.Value == item.GameId);
			card.Selected += async delegate
			{
				await OpenGameDetailAsync(card.Game.GameId);
			};
			visibleCards.Add(card);
			catalogFlowPanel.Controls.Add(card);
		}
		catalogFlowPanel.ResumeLayout();
		UpdateCatalogLayout(scrollableContentWidth);
	}

	private void UpdateCatalogCountText(int visibleCount, int totalCount)
	{
		int num = Math.Max(0, totalCount);
		int value = Math.Clamp(visibleCount, 0, num);
		if (num == 0)
		{
			resultCountLabel.Text = "Không tìm thấy sản phẩm";
			catalogHintLabel.Text = "Dieu chinh tu khoa hoac bo loc";
			return;
		}
		resultCountLabel.Text = $"Hiển thị {value}/{num} sản phẩm";
		catalogHintLabel.Text = $"{value}/{num} trò chơi đang hiển thị";
	}

	private IReadOnlyList<StoreCategorySection> GetFilteredCategorySections()
	{
		string category = categoryComboBox.SelectedItem?.ToString();
		string query = searchTextBox.Text.Trim();
		IReadOnlyList<StoreCategorySection> source;
		if (pageData.CategorySections.Count <= 0)
		{
			IReadOnlyList<StoreCategorySection> readOnlyList = new _003C_003Ez__ReadOnlySingleElementList<StoreCategorySection>(new StoreCategorySection
			{
				Name = "Tất cả game",
				Slug = "tat-ca-game",
				Games = pageData.Games
			});
			source = readOnlyList;
		}
		else
		{
			source = pageData.CategorySections;
		}
		IEnumerable<StoreCategorySection> source2 = source.AsEnumerable();
		if (!IsAllCategory(category))
		{
			source2 = source2.Where((StoreCategorySection section) => string.Equals(section.Name, category, StringComparison.CurrentCultureIgnoreCase));
		}
		if (string.IsNullOrWhiteSpace(query))
		{
			return source2.ToArray();
		}
		return (from section in source2
			select new StoreCategorySection
			{
				Name = section.Name,
				Slug = section.Slug,
				Games = section.Games.Where((StoreGame game) => MatchesCatalogQuery(game, query)).ToArray()
			} into section
			where section.Games.Count > 0
			select section).ToArray();
	}

	private static bool IsAllCategory(string? category)
	{
		if (string.IsNullOrWhiteSpace(category))
		{
			return true;
		}
		string a = category.Trim();
		if (!string.Equals(a, "Tat ca", StringComparison.CurrentCultureIgnoreCase) && !string.Equals(a, "Tất cả", StringComparison.CurrentCultureIgnoreCase))
		{
			return string.Equals(a, "Táº¥t cáº£", StringComparison.CurrentCultureIgnoreCase);
		}
		return true;
	}

	private static bool MatchesCatalogQuery(StoreGame game, string query)
	{
		if (!game.Title.Contains(query, StringComparison.CurrentCultureIgnoreCase) && !game.Subtitle.Contains(query, StringComparison.CurrentCultureIgnoreCase) && !game.Category.Contains(query, StringComparison.CurrentCultureIgnoreCase) && !game.BadgeText.Contains(query, StringComparison.CurrentCultureIgnoreCase))
		{
			return game.Genres.Any((string genre) => genre.Contains(query, StringComparison.CurrentCultureIgnoreCase));
		}
		return true;
	}

	private IEnumerable<StoreGame> GetFilteredGames()
	{
		string category = categoryComboBox.SelectedItem?.ToString();
		string query = searchTextBox.Text.Trim();
		IEnumerable<StoreGame> enumerable = pageData.Games;
		if (!string.IsNullOrWhiteSpace(category) && !string.Equals(category, "Tat ca", StringComparison.CurrentCultureIgnoreCase) && !string.Equals(category, "Tất cả", StringComparison.CurrentCultureIgnoreCase))
		{
			enumerable = enumerable.Where((StoreGame game) => string.Equals(game.Category, category, StringComparison.CurrentCultureIgnoreCase));
		}
		if (!string.IsNullOrWhiteSpace(query))
		{
			enumerable = enumerable.Where((StoreGame game) => game.Title.Contains(query, StringComparison.CurrentCultureIgnoreCase) || game.Subtitle.Contains(query, StringComparison.CurrentCultureIgnoreCase) || game.Category.Contains(query, StringComparison.CurrentCultureIgnoreCase) || game.Genres.Any((string genre) => genre.Contains(query, StringComparison.CurrentCultureIgnoreCase)));
		}
		return enumerable;
	}

	private void BindSpotlightGames(IReadOnlyList<StoreGame> games)
	{
		spotlightGames = (from game in games
			group game by game.GameId into @group
			select @group.First()).Take(6).ToArray();
		if (spotlightGames.Count == 0)
		{
			StoreGame featuredGame = pageData.FeaturedGame;
			IReadOnlyList<StoreGame> readOnlyList2;
			if (featuredGame == null)
			{
				IReadOnlyList<StoreGame> readOnlyList = Array.Empty<StoreGame>();
				readOnlyList2 = readOnlyList;
			}
			else
			{
				IReadOnlyList<StoreGame> readOnlyList = new _003C_003Ez__ReadOnlySingleElementList<StoreGame>(featuredGame);
				readOnlyList2 = readOnlyList;
			}
			spotlightGames = readOnlyList2;
		}
		if (selectedSpotlightGame != null)
		{
			spotlightIndex = spotlightGames.Select((StoreGame game, int index) => new
			{
				GameId = game.GameId,
				Index = index
			}).FirstOrDefault(item => item.GameId == selectedSpotlightGame.GameId)?.Index ?? 0;
		}
		else
		{
			spotlightIndex = 0;
		}
		selectedSpotlightGame = spotlightGames[Math.Clamp(spotlightIndex, 0, spotlightGames.Count - 1)];
		if (UseHtmlStorefront)
		{
			spotlightTimer.Stop();
			RefreshStorefrontDocumentAsync();
		}
		else
		{
			SelectSpotlightByIndex(spotlightIndex);
			RestartSpotlightAutoplay();
			PreloadSpotlightImagesAsync();
		}
	}

	private void BuildSpotlightSidebar()
	{
		foreach (StoreSpotlightSidebarItem spotlightSidebarItem in spotlightSidebarItems)
		{
			spotlightSidebarItem.Dispose();
		}
		spotlightSidebarItems.Clear();
	}

	private void SelectSpotlightByIndex(int index)
	{
		if (spotlightGames.Count != 0)
		{
			spotlightIndex = (index + spotlightGames.Count) % spotlightGames.Count;
			selectedSpotlightGame = spotlightGames[spotlightIndex];
			ApplySpotlightGame(selectedSpotlightGame);
		}
	}

	private void AdvanceSpotlight()
	{
		if (spotlightGames.Count > 1)
		{
			spotlightIndex = (spotlightIndex + 1) % spotlightGames.Count;
			selectedSpotlightGame = spotlightGames[spotlightIndex];
			ApplySpotlightGame(selectedSpotlightGame);
		}
	}

	private void MoveSpotlight(int delta, bool restartAutoplay)
	{
		if (spotlightGames.Count != 0)
		{
			SelectSpotlightByIndex(spotlightIndex + delta);
			if (restartAutoplay)
			{
				RestartSpotlightAutoplay();
			}
		}
	}

	private void ApplySpotlightGame(StoreGame game)
	{
		featuredPanel.SuspendLayout();
		featuredPromoLabel.Text = game.PromoLabel;
		featuredPromoLabel.BackColor = ((game.DiscountPercent > 0) ? Color.FromArgb(39, 113, 65) : (game.IsTrending ? Color.FromArgb(47, 86, 182) : Color.FromArgb(56, 63, 79)));
		featuredTitleLabel.Text = game.Title;
		featuredSubtitleLabel.Text = BuildSpotlightMeta(game);
		originalPriceValueLabel.Text = game.OriginalPriceText;
		originalPriceValueLabel.ForeColor = ((game.DiscountPercent > 0) ? AppTheme.SecondaryText : AppTheme.PrimaryText);
		discountValueLabel.Text = ((game.DiscountPercent > 0) ? $"-{game.DiscountPercent}%" : "0%");
		discountValueLabel.ForeColor = ((game.DiscountPercent > 0) ? Color.FromArgb(73, 209, 164) : AppTheme.SecondaryText);
		salePriceValueLabel.Text = game.SalePriceText;
		salePriceValueLabel.ForeColor = ((game.SalePrice <= 0m) ? Color.FromArgb(123, 181, 255) : AppTheme.Accent);
		saleLimitValueLabel.Text = ((game.DiscountPercent > 0) ? "Không giới hạn" : "Giá hiện hành");
		genresValueLabel.Text = BuildGenresSummary(game);
		featuredPanel.ResumeLayout(performLayout: true);
		RenderFeaturedArtworkAsync(game);
		featuredInfoPanel.Invalidate();
		featuredMediaPanel.Invalidate();
		featuredPanel.Invalidate();
	}

	private async Task RenderFeaturedArtworkAsync(StoreGame? requestedGame = null)
	{
		StoreGame game = requestedGame ?? selectedSpotlightGame;
		if (game == null || featuredMediaPanel.Width <= 8 || featuredMediaPanel.Height <= 8)
		{
			return;
		}
		string text = ((!string.IsNullOrWhiteSpace(game.SpotlightImageUrl)) ? game.SpotlightImageUrl : game.PosterImageUrl);
		int requestId = ++featuredArtworkRequestId;
		bool animate = activeSpotlightVisualGameId != game.GameId || featuredPictureBox.Image == null;
		if (string.IsNullOrWhiteSpace(text))
		{
			Image image = TryCreateFallbackSpotlightBanner(game);
			if (image != null)
			{
				BeginSpotlightVisualTransition(game, image, StoreSpotlightPalette.FromGame(game), animate, ownsImage: true);
			}
			return;
		}
		Size clientSize = featuredMediaPanel.ClientSize;
		if (StoreRemoteImageCache.TryGetPrepared(text, clientSize, StoreImageResizeMode.Stretch, out Image image2) && image2 != null)
		{
			BeginSpotlightVisualTransition(game, image2, StoreSpotlightPalette.Extract(image2, game), animate, ownsImage: false);
			return;
		}
		Image image3 = TryCreateFallbackSpotlightBanner(game);
		if (image3 != null)
		{
			BeginSpotlightVisualTransition(game, image3, StoreSpotlightPalette.FromGame(game), animate, ownsImage: true);
		}
		Image image4 = await StoreRemoteImageCache.GetPreparedAsync(text, clientSize, StoreImageResizeMode.Stretch);
		if (!base.IsDisposed && !featuredPictureBox.IsDisposed && requestId == featuredArtworkRequestId && !(selectedSpotlightGame?.GameId != game.GameId) && image4 != null)
		{
			BeginSpotlightVisualTransition(game, image4, StoreSpotlightPalette.Extract(image4, game), animate: false, ownsImage: false);
		}
	}

	private void HandleSpotlightViewportChanged()
	{
		UpdateSpotlightMediaBounds(isSpotlightTransitionActive ? EaseSpotlightTransition((float)spotlightTransitionStep / 24f) : 1f);
		if (selectedSpotlightGame != null && featuredPictureBox.Image == null && !isRefreshingFromApi)
		{
			RenderFeaturedArtworkAsync(selectedSpotlightGame);
		}
	}

	private void BeginSpotlightVisualTransition(StoreGame game, Image image, StoreSpotlightPalette palette, bool animate, bool ownsImage)
	{
		if (selectedSpotlightGame?.GameId != game.GameId)
		{
			if (ownsImage)
			{
				image.Dispose();
			}
			return;
		}
		CommitSpotlightTransition();
		if (!animate || featuredPictureBox.Image == null || featuredMediaPanel.Width <= 8 || featuredMediaPanel.Height <= 8)
		{
			activeSpotlightVisualGameId = game.GameId;
			SetPrimarySpotlightImage(image, ownsImage);
			spotlightTransitionStartPalette = palette;
			spotlightTransitionTargetPalette = palette;
			ApplySpotlightPalette(palette);
			UpdateSpotlightMediaBounds(1f);
		}
		else
		{
			SetTransitionSpotlightImage(image, ownsImage);
			spotlightTransitionStartPalette = currentSpotlightPalette;
			spotlightTransitionTargetPalette = palette;
			spotlightTransitionStep = 0;
			isSpotlightTransitionActive = true;
			spotlightTransitionPictureBox.Visible = true;
			spotlightTransitionPictureBox.BringToFront();
			UpdateSpotlightMediaBounds(0f);
			spotlightTransitionTimer.Start();
		}
	}

	private void CommitSpotlightTransition()
	{
		spotlightTransitionTimer.Stop();
		if (isSpotlightTransitionActive)
		{
			isSpotlightTransitionActive = false;
			ApplySpotlightPalette(spotlightTransitionTargetPalette);
			Image image = spotlightTransitionPictureBox.Image;
			bool ownsImage = featuredTransitionArtwork != null && featuredTransitionArtwork == image;
			spotlightTransitionPictureBox.Image = null;
			featuredTransitionArtwork = null;
			spotlightTransitionPictureBox.Visible = false;
			if (image != null)
			{
				activeSpotlightVisualGameId = selectedSpotlightGame?.GameId;
				SetPrimarySpotlightImage(image, ownsImage);
			}
			UpdateSpotlightMediaBounds(1f);
		}
	}

	private void SpotlightTransitionTimer_Tick(object? sender, EventArgs e)
	{
		spotlightTransitionStep = Math.Min(24, spotlightTransitionStep + 1);
		float num = EaseSpotlightTransition((float)spotlightTransitionStep / 24f);
		ApplySpotlightPalette(StoreSpotlightPalette.Lerp(spotlightTransitionStartPalette, spotlightTransitionTargetPalette, num));
		UpdateSpotlightMediaBounds(num);
		if (spotlightTransitionStep >= 24)
		{
			CommitSpotlightTransition();
		}
	}

	private void ApplySpotlightPalette(StoreSpotlightPalette palette)
	{
		currentSpotlightPalette = palette;
		featuredPanel.SurfaceColor = palette.PanelSurface;
		featuredPanel.BorderColor = palette.PanelBorder;
		featuredInfoPanel.BackColor = palette.InfoSurface;
		featuredMediaPanel.BackColor = palette.MediaBackdrop;
		featuredPictureBox.BackColor = palette.MediaBackdrop;
		spotlightTransitionPictureBox.BackColor = palette.MediaBackdrop;
		scrollContentPanel.BackColor = palette.PageAmbient;
		contentLayout.BackColor = palette.PageAmbient;
		featuredPanel.Invalidate();
		featuredInfoPanel.Invalidate();
		featuredMediaPanel.Invalidate();
	}

	private void UpdateSpotlightMediaBounds(float progress)
	{
		int width = featuredMediaPanel.ClientSize.Width;
		int height = featuredMediaPanel.ClientSize.Height;
		if (width > 0 && height > 0)
		{
			float num = Math.Clamp(progress, 0f, 1f);
			int num2 = (int)Math.Round((float)width * num, MidpointRounding.AwayFromZero);
			if (!isSpotlightTransitionActive)
			{
				featuredPictureBox.Bounds = new Rectangle(0, 0, width, height);
				spotlightTransitionPictureBox.Bounds = new Rectangle(width, 0, width, height);
				spotlightTransitionPictureBox.Visible = false;
			}
			else
			{
				featuredPictureBox.Bounds = new Rectangle(-num2, 0, width, height);
				spotlightTransitionPictureBox.Bounds = new Rectangle(width - num2, 0, width, height);
			}
		}
	}

	private void SetPrimarySpotlightImage(Image image, bool ownsImage)
	{
		if (featuredPictureBox.Image != image)
		{
			featuredArtwork?.Dispose();
			featuredArtwork = null;
		}
		featuredPictureBox.Image = image;
		if (ownsImage)
		{
			featuredArtwork = image;
		}
	}

	private void SetTransitionSpotlightImage(Image image, bool ownsImage)
	{
		if (spotlightTransitionPictureBox.Image != image)
		{
			featuredTransitionArtwork?.Dispose();
			featuredTransitionArtwork = null;
		}
		spotlightTransitionPictureBox.Image = image;
		if (ownsImage)
		{
			featuredTransitionArtwork = image;
		}
	}

	private Image? TryCreateFallbackSpotlightBanner(StoreGame game)
	{
		if (featuredMediaPanel.Width <= 8 || featuredMediaPanel.Height <= 8)
		{
			return null;
		}
		return StoreArtworkFactory.CreateBanner(game, featuredMediaPanel.Size);
	}

	private static float EaseSpotlightTransition(float progress)
	{
		float num = Math.Clamp(progress, 0f, 1f);
		if (!(num < 0.5f))
		{
			return 1f - (float)Math.Pow(-2f * num + 2f, 3.0) / 2f;
		}
		return 4f * num * num * num;
	}

	private void UpdateSpotlightSelectionState()
	{
	}

	private static string BuildGenresSummary(StoreGame game)
	{
		if (!string.IsNullOrWhiteSpace(game.Category))
		{
			return game.Category;
		}
		return string.Join(", ", game.Genres.Take(2));
	}

	private void ClearVisibleCards()
	{
		foreach (StoreGameCard visibleCard in visibleCards)
		{
			catalogFlowPanel.Controls.Remove(visibleCard);
			visibleCard.Dispose();
		}
		visibleCards.Clear();
	}

	private void UpdateResponsiveLayout()
	{
		for (int i = 0; i < navButtons.Count; i++)
		{
			navButtons[i].Visible = ShouldShowNavigation(i);
		}
		UpdateFloatingAddGameLayout();
		if (currentSection == NavigationSection.Storefront)
		{
			toolbarLayout.ColumnStyles[0].Width = ((base.ClientSize.Width < 1220) ? 148f : 170f);
			resultCountLabel.Visible = base.ClientSize.Width >= 1120;
			int scrollableContentWidth = GetScrollableContentWidth();
			if (embeddedDetailForm != null && !embeddedDetailForm.IsDisposed)
			{
				ConfigureEmbeddedDetailLayout(scrollableContentWidth);
			}
			else if (!UseHtmlStorefront)
			{
				ConfigureFeaturedLayout(scrollableContentWidth);
			}
			ApplyStorefrontCatalogVisibility(filteredStorefrontGames.Count == 0);
			if (!UseHtmlStorefront)
			{
				UpdateCatalogLayout(scrollableContentWidth);
			}
		}
	}

	private async Task EnsureStartupSpotlightVisualReadyAsync()
	{
		if (UseHtmlStorefront)
		{
			return;
		}
		StoreGame game = selectedSpotlightGame;
		if (game == null || base.IsDisposed || featuredPictureBox.IsDisposed)
		{
			return;
		}
		if (featuredMediaPanel.Width <= 8 || featuredMediaPanel.Height <= 8)
		{
			UpdateResponsiveLayout();
			if (featuredMediaPanel.Width <= 8 || featuredMediaPanel.Height <= 8)
			{
				return;
			}
		}
		string text = ((!string.IsNullOrWhiteSpace(game.SpotlightImageUrl)) ? game.SpotlightImageUrl : game.PosterImageUrl);
		if (string.IsNullOrWhiteSpace(text))
		{
			return;
		}
		Size clientSize = featuredMediaPanel.ClientSize;
		if (StoreRemoteImageCache.TryGetPrepared(text, clientSize, StoreImageResizeMode.Stretch, out Image image) && image != null)
		{
			BeginSpotlightVisualTransition(game, image, StoreSpotlightPalette.Extract(image, game), animate: false, ownsImage: false);
			return;
		}
		Image image2 = await StoreRemoteImageCache.GetPreparedAsync(text, clientSize, StoreImageResizeMode.Stretch);
		if (image2 != null && !base.IsDisposed && !featuredPictureBox.IsDisposed && !(selectedSpotlightGame?.GameId != game.GameId))
		{
			BeginSpotlightVisualTransition(game, image2, StoreSpotlightPalette.Extract(image2, game), animate: false, ownsImage: false);
		}
	}

	private static (string Title, string Subtitle, string Status) GetSectionLoadingContent(NavigationSection section)
	{
		return section switch
		{
			NavigationSection.Cart => (Title: "Đang mở giỏ hàng", Subtitle: "Launcher đang đồng bộ các trò chơi bạn đã chọn và thông tin thanh toán để bạn tiếp tục nhanh hơn.", Status: "Đang tải giỏ hàng"), 
			NavigationSection.Library => (Title: "Đang mở thư viện", Subtitle: "Launcher đang tải danh sách trò chơi bạn đã sở hữu và nội dung thư viện liên quan.", Status: "Đang tải thư viện"), 
			NavigationSection.Account => (Title: "Đang mở tài khoản", Subtitle: "Launcher đang tải hồ sơ, đơn hàng và thông tin thư viện để bạn theo dõi dễ dàng hơn.", Status: "Đang tải tài khoản"), 
			_ => (Title: "Đang tải dữ liệu", Subtitle: "Launcher đang chuẩn bị nội dung để bạn tiếp tục mượt hơn.", Status: "Đang chuẩn bị dữ liệu"), 
		};
	}

	private bool ShouldShowNavigation(int index)
	{
		if (base.ClientSize.Width >= 1420)
		{
			return true;
		}
		if (base.ClientSize.Width >= 1260)
		{
			return index < 4;
		}
		if (base.ClientSize.Width >= 1120)
		{
			return index < 3;
		}
		return index < 2;
	}

	private void ConfigureFeaturedLayout(int availableWidth)
	{
		int featuredCardWidth = GetFeaturedCardWidth(availableWidth);
		bool flag = featuredCardWidth < 1160;
		bool flag2 = featuredCardWidth < 980;
		int num = (flag2 ? 12 : 16);
		int num2 = Math.Max(320, featuredCardWidth - num * 2);
		int num3 = Math.Clamp((int)Math.Round((float)num2 * (flag2 ? 0.39f : (flag ? 0.38f : 0.37f))), 280, 496);
		int num4 = Math.Max(260, num2 - num3);
		featuredPanel.Width = featuredCardWidth;
		featuredPanel.Height = (flag2 ? 428 : (flag ? 468 : 520));
		featuredPanel.Margin = new Padding(0, 0, 0, 28);
		featuredPanel.Padding = new Padding(num);
		featuredLayout.Visible = true;
		embeddedDetailHostPanel.Visible = false;
		featuredLayout.SuspendLayout();
		featuredLayout.Controls.Clear();
		featuredLayout.ColumnStyles.Clear();
		featuredLayout.RowStyles.Clear();
		featuredLayout.ColumnCount = 2;
		featuredLayout.RowCount = 1;
		featuredLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, num4));
		featuredLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, num3));
		featuredLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
		featuredMediaPanel.Margin = Padding.Empty;
		featuredInfoPanel.Margin = Padding.Empty;
		featuredInfoPanel.Padding = (flag2 ? new Padding(18, 16, 18, 16) : (flag ? new Padding(20, 18, 20, 16) : new Padding(24, 20, 24, 18)));
		featuredLayout.Controls.Add(featuredMediaPanel, 0, 0);
		featuredLayout.Controls.Add(featuredInfoPanel, 1, 0);
		featuredPromoLabel.Padding = (flag2 ? new Padding(10, 5, 10, 5) : new Padding(12, 6, 12, 6));
		featuredInfoLayout.RowStyles[1].Height = (flag2 ? 126f : (flag ? 138f : 150f));
		featuredInfoLayout.RowStyles[2].Height = (flag2 ? 44f : (flag ? 40f : 36f));
		featuredSpecsLayout.ColumnStyles[0].Width = (flag2 ? 106f : (flag ? 112f : 124f));
		featuredSpecsLayout.RowStyles[0].Height = (flag2 ? 36f : (flag ? 38f : 42f));
		featuredSpecsLayout.RowStyles[1].Height = (flag2 ? 36f : (flag ? 38f : 42f));
		featuredSpecsLayout.RowStyles[2].Height = (flag2 ? 44f : (flag ? 46f : 52f));
		featuredSpecsLayout.RowStyles[3].Height = (flag2 ? 36f : (flag ? 38f : 42f));
		genresValueLabel.MaximumSize = new Size(Math.Max(180, num3 - featuredInfoPanel.Padding.Horizontal - (int)featuredSpecsLayout.ColumnStyles[0].Width), 0);
		featuredLayout.ResumeLayout();
		RenderFeaturedArtworkAsync();
	}

	private void ConfigureEmbeddedDetailLayout(int availableWidth)
	{
		int height = Math.Max(420, scrollContentPanel.ClientSize.Height - scrollContentPanel.Padding.Vertical - 4);
		featuredPanel.Width = availableWidth;
		featuredPanel.Height = height;
		featuredPanel.Margin = Padding.Empty;
		featuredPanel.Padding = Padding.Empty;
		featuredLayout.Visible = false;
		embeddedDetailHostPanel.Visible = true;
		embeddedDetailHostPanel.BringToFront();
	}

	private void ApplyStorefrontCatalogVisibility(bool isCatalogEmpty)
	{
		bool flag = embeddedDetailForm != null && !embeddedDetailForm.IsDisposed;
		bool flag2 = currentSection == NavigationSection.Storefront;
		bool flag3 = flag2 && !flag && !UseHtmlStorefront;
		bool flag4 = flag3 && isCatalogEmpty;
		if (UseHtmlStorefront)
		{
			storefrontWebView.Visible = flag2 && !flag;
			contentLayout.Visible = flag2 && flag;
			addGameButton.Visible = false;
			sectionHeaderFlowPanel.Visible = false;
			catalogFlowPanel.Visible = false;
			emptyStateLabel.Visible = false;
			SetContentLayoutRowVisibility(1, isVisible: false);
			SetContentLayoutRowVisibility(2, isVisible: false);
			SetContentLayoutRowVisibility(3, isVisible: false);
			SetContentLayoutRowVisibility(4, isVisible: false);
			return;
		}
		sectionHeaderFlowPanel.Visible = flag3;
		catalogFlowPanel.Visible = flag3;
		emptyStateLabel.Visible = flag4;
		addGameButton.Visible = false;
		if (!flag3)
		{
			addGamePopupPanel.Visible = false;
		}
		SetContentLayoutRowVisibility(1, flag3);
		SetContentLayoutRowVisibility(2, flag4);
		SetContentLayoutRowVisibility(3, flag3);
		SetContentLayoutRowVisibility(4, flag3);
	}

	private void SetContentLayoutRowVisibility(int rowIndex, bool isVisible)
	{
		if (rowIndex >= 0 && rowIndex < contentLayout.RowStyles.Count)
		{
			RowStyle rowStyle = contentLayout.RowStyles[rowIndex];
			rowStyle.SizeType = ((!isVisible) ? SizeType.Absolute : SizeType.AutoSize);
			rowStyle.Height = 0f;
		}
	}

	private void UpdateSpotlightMediaLayout()
	{
	}

	private void UpdateSpotlightSidebarLayout()
	{
	}

	private void UpdateCatalogLayout(int availableWidth)
	{
		catalogFlowPanel.Width = availableWidth;
		if (visibleCards.Count == 0)
		{
			catalogFlowPanel.Height = 0;
			return;
		}
		int num = ((availableWidth >= 1360) ? 4 : ((availableWidth >= 1020) ? 3 : ((availableWidth < 700) ? 1 : 2)));
		int cardWidth = (availableWidth - 18 * (num - 1)) / num;
		catalogFlowPanel.ApplyGridLayout(visibleCards, availableWidth, cardWidth, num, 18);
	}

	private int GetScrollableContentWidth()
	{
		int num = scrollContentPanel.ClientSize.Width - scrollContentPanel.Padding.Horizontal - 4;
		if (scrollContentPanel.VerticalScroll.Visible)
		{
			num -= SystemInformation.VerticalScrollBarWidth;
		}
		return Math.Max(320, num);
	}

	private static int GetFeaturedCardWidth(int availableWidth)
	{
		return Math.Min(1380, availableWidth);
	}

	private void UpdateCartBadge(int cartItemCount)
	{
		currentCartItemCount = Math.Max(0, cartItemCount);
		cartBadgeLabel.Text = FormatCartBadgeLabel(currentCartItemCount);
	}

	private void HandleCartCountChanged(int cartItemCount)
	{
		UpdateCartBadge(cartItemCount);
	}

	private void HandleWalletBalanceChanged(decimal _, string balanceText)
	{
		if (base.InvokeRequired)
		{
			BeginInvoke(delegate
			{
				HandleWalletBalanceChanged(0m, balanceText);
			});
		}
		else
		{
			currentWalletBalanceText = (string.IsNullOrWhiteSpace(balanceText) ? "0 đ" : balanceText);
			balanceLabel.Text = currentWalletBalanceText;
			accountPanel.Width = CalculateAccountPanelWidth(accountNameLabel.Text, balanceLabel.Text);
		}
	}

	private async Task OpenAccountWalletAsync()
	{
		accountView.ShowWalletSection();
		await NavigateToSectionAsync(NavigationSection.Account);
	}

	private void UpdateSelectedCardState(Guid? gameId)
	{
		activeDetailGameId = gameId;
		if (UseHtmlStorefront)
		{
			return;
		}
		foreach (StoreGameCard visibleCard in visibleCards)
		{
			visibleCard.SetSelected(gameId.HasValue && visibleCard.Game.GameId == gameId.Value);
		}
	}

	private void HandleEmbeddedDetailClosed(object? sender, FormClosedEventArgs e)
	{
		if (!base.IsDisposed && !base.Disposing)
		{
			if (sender is StoreGameDetailForm { UpdatedCartItemCount: not null } storeGameDetailForm)
			{
				UpdateCartBadge(storeGameDetailForm.UpdatedCartItemCount.Value);
			}
			embeddedDetailHostPanel.Controls.Clear();
			embeddedDetailHostPanel.Visible = false;
			embeddedDetailForm = null;
			UpdateSelectedCardState(null);
			scrollContentPanel.AutoScroll = false;
			ApplyStorefrontCatalogVisibility(filteredStorefrontGames.Count == 0);
			UpdateResponsiveLayout();
			RestartSpotlightAutoplay();
			if (UseHtmlStorefront && currentSection == NavigationSection.Storefront)
			{
				pendingStorefrontReturnPosition = storefrontReturnPosition;
				storefrontReturnPosition = null;
				RefreshStorefrontDocumentAsync();
			}
		}
	}

	private void HandleEmbeddedDetailCartPageRequested()
	{
		if (!base.IsDisposed && !base.Disposing)
		{
			NavigateToSectionAsync(NavigationSection.Cart);
		}
	}

	private void SpotlightTimer_Tick(object? sender, EventArgs e)
	{
		if (spotlightGames.Count <= 1)
		{
			spotlightTimer.Stop();
		}
		else
		{
			AdvanceSpotlight();
		}
	}

	private async Task PreloadSpotlightImagesAsync()
	{
		string[] array = (from game in spotlightGames.Take(6)
			select string.IsNullOrWhiteSpace(game.SpotlightImageUrl) ? game.PosterImageUrl : game.SpotlightImageUrl into url
			where !string.IsNullOrWhiteSpace(url)
			select url).Distinct<string>(StringComparer.OrdinalIgnoreCase).Cast<string>().ToArray();
		string[] array2 = array;
		for (int i = 0; i < array2.Length; i++)
		{
			await StoreRemoteImageCache.GetAsync(array2[i]);
		}
	}

	private void RestartSpotlightAutoplay()
	{
		spotlightTimer.Stop();
		if (!UseHtmlStorefront && currentSection == NavigationSection.Storefront && spotlightGames.Count > 1)
		{
			spotlightTimer.Interval = 3500;
			spotlightTimer.Start();
		}
	}

	private void ShowSpotlightAction(string actionName)
	{
		if (selectedSpotlightGame != null)
		{
			LauncherMessageBox.Show(actionName + ": " + selectedSpotlightGame.Title, "NestG Launcher", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
		}
	}

	private async Task OpenSelectedSpotlightDetailAsync()
	{
		if (selectedSpotlightGame != null)
		{
			await OpenGameDetailAsync(selectedSpotlightGame.GameId);
		}
	}

	private async Task OpenGameDetailAsync(Guid gameId)
	{
		if (isOpeningGameDetail)
		{
			return;
		}
		isOpeningGameDetail = true;
		try
		{
			if (UseHtmlStorefront)
			{
				if (storefrontWebView.Visible)
				{
					storefrontReturnPosition = await CaptureStorefrontReturnPositionAsync(gameId);
				}
				storefrontWebView.Visible = false;
				contentLayout.Visible = true;
				scrollContentPanel.AutoScroll = false;
			}
			if (embeddedDetailForm != null && !embeddedDetailForm.IsDisposed)
			{
				await embeddedDetailForm.ShowGameAsync(gameId);
			}
			else
			{
				embeddedDetailHostPanel.Controls.Clear();
				embeddedDetailForm = new StoreGameDetailForm(storefrontApiClient, gameId)
				{
					TopLevel = false,
					FormBorderStyle = FormBorderStyle.None,
					Dock = DockStyle.Fill
				};
				embeddedDetailForm.CartItemCountChanged += UpdateCartBadge;
				embeddedDetailForm.CartPageRequested += HandleEmbeddedDetailCartPageRequested;
				embeddedDetailForm.FormClosed += HandleEmbeddedDetailClosed;
				embeddedDetailHostPanel.Controls.Add(embeddedDetailForm);
				embeddedDetailForm.Show();
			}
			spotlightTimer.Stop();
			UpdateSelectedCardState(gameId);
			ApplyStorefrontCatalogVisibility(filteredStorefrontGames.Count == 0);
			UpdateResponsiveLayout();
			scrollContentPanel.AutoScrollPosition = Point.Empty;
		}
		finally
		{
			isOpeningGameDetail = false;
		}
	}

	private static string FormatCartBadgeLabel(int cartItemCount)
	{
		int num = Math.Max(0, cartItemCount);
		if (num <= 99)
		{
			return num.ToString();
		}
		return "99+";
	}

	private string BuildCartPreviewMessage()
	{
		if (currentCartItemCount > 0)
		{
			return $"Giỏ hàng hiện có {currentCartItemCount} sản phẩm. Desktop đã đồng bộ badge cart theo dữ liệu thật, nhưng màn hình cart chi tiết chưa được mở trong launcher.";
		}
		return "Giỏ hàng của bạn đang trống. Desktop đã lấy dữ liệu thật cho badge, nhưng màn hình cart chi tiết chưa được mở trong launcher.";
	}

	private async Task HandleSignOutFromAccountAsync()
	{
		try
		{
			await DesktopAppContext.PaymentRealtimeClient.StopAsync();
		}
		catch
		{
		}
		await DesktopAppContext.AuthSessionManager.SignOutAsync();
		AuthUiHelper.Navigate(this, DesktopAppContext.CreateLoginForm());
	}

	private void ConfigureSpotlightShell()
	{
		featuredMediaPanel.Dock = DockStyle.Fill;
		featuredMediaPanel.Margin = Padding.Empty;
		featuredMediaPanel.Padding = Padding.Empty;
		featuredMediaPanel.BackColor = StoreSpotlightPalette.Default.MediaBackdrop;
		featuredPictureBox.BackColor = Color.Transparent;
		featuredPictureBox.Dock = DockStyle.None;
		featuredPictureBox.Margin = Padding.Empty;
		featuredPictureBox.SizeMode = PictureBoxSizeMode.StretchImage;
		spotlightTransitionPictureBox.BackColor = Color.Transparent;
		spotlightTransitionPictureBox.Dock = DockStyle.None;
		spotlightTransitionPictureBox.Margin = Padding.Empty;
		spotlightTransitionPictureBox.SizeMode = PictureBoxSizeMode.StretchImage;
		spotlightTransitionPictureBox.Visible = false;
		featuredMediaPanel.Controls.Clear();
		featuredMediaPanel.Controls.Add(featuredPictureBox);
		featuredMediaPanel.Controls.Add(spotlightTransitionPictureBox);
		spotlightTransitionPictureBox.BringToFront();
		featuredInfoPanel.Controls.Clear();
		featuredInfoPanel.BackColor = StoreSpotlightPalette.Default.InfoSurface;
		featuredInfoPanel.Controls.Add(featuredInfoLayout);
		featuredInfoLayout.Dock = DockStyle.Fill;
		featuredInfoLayout.Margin = Padding.Empty;
		featuredInfoLayout.RowStyles.Clear();
		featuredInfoLayout.RowStyles.Add(new RowStyle());
		featuredInfoLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 150f));
		featuredInfoLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36f));
		featuredInfoLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
		featuredPromoLabel.AutoSize = true;
		featuredPromoLabel.Padding = new Padding(12, 6, 12, 6);
		featuredTitleLabel.AutoSize = false;
		featuredTitleLabel.AutoEllipsis = true;
		featuredTitleLabel.Dock = DockStyle.Fill;
		featuredTitleLabel.Font = new Font("Segoe UI Semibold", 20f, FontStyle.Bold);
		featuredTitleLabel.Margin = new Padding(0, 14, 0, 0);
		featuredSubtitleLabel.AutoSize = false;
		featuredSubtitleLabel.AutoEllipsis = true;
		featuredSubtitleLabel.Dock = DockStyle.Fill;
		featuredSubtitleLabel.Font = new Font("Segoe UI", 10.5f, FontStyle.Regular);
		featuredSubtitleLabel.Margin = new Padding(0, 8, 0, 0);
		featuredSpecsLayout.RowStyles.Clear();
		featuredSpecsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42f));
		featuredSpecsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42f));
		featuredSpecsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52f));
		featuredSpecsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42f));
		featuredSpecsLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
		originalPriceValueLabel.AutoSize = false;
		originalPriceValueLabel.Dock = DockStyle.Fill;
		discountValueLabel.AutoSize = false;
		discountValueLabel.Dock = DockStyle.Fill;
		salePriceValueLabel.AutoSize = false;
		salePriceValueLabel.Dock = DockStyle.Fill;
		saleLimitValueLabel.AutoSize = false;
		saleLimitValueLabel.Dock = DockStyle.Fill;
		genresValueLabel.AutoSize = false;
		genresValueLabel.Dock = DockStyle.Fill;
		genresValueLabel.MaximumSize = Size.Empty;
		genresValueLabel.TextAlign = ContentAlignment.TopRight;
		ApplySpotlightPalette(StoreSpotlightPalette.Default);
		UpdateSpotlightMediaBounds(1f);
	}

	private static string BuildSpotlightMeta(StoreGame game)
	{
		List<string> list = new List<string>();
		if (!string.IsNullOrWhiteSpace(game.BadgeText))
		{
			list.Add(game.BadgeText);
		}
		if (!string.IsNullOrWhiteSpace(game.Category))
		{
			list.Add(game.Category);
		}
		else if (game.Genres.Count > 0)
		{
			list.Add(game.Genres[0]);
		}
		return string.Join(" • ", list);
	}

	private void UpdateFloatingAddGameLayout()
	{
		addGameButton.Visible = false;
		addGameButton.Enabled = false;
		addGamePopupPanel.Visible = false;
		if (addGamePopupTimer.Enabled)
		{
			addGamePopupTimer.Stop();
		}
	}

	private void ShowAddGamePopup()
	{
		isAddGamePopupOpen = true;
		isAddGamePopupAnimatingOut = false;
		addGamePopupAnimationStep = 0;
		UpdateFloatingAddGameLayout();
		addGamePopupPanel.Location = new Point(addGamePopupTargetLocation.X - 30, addGamePopupTargetLocation.Y);
		addGamePopupPanel.Visible = true;
		addGamePopupPanel.BringToFront();
		addGamePopupTimer.Start();
		addGameCodeTextBox.Focus();
	}

	private void HideAddGamePopup(bool animated)
	{
		isAddGamePopupOpen = false;
		if (!animated)
		{
			addGamePopupTimer.Stop();
			isAddGamePopupAnimatingOut = false;
			addGamePopupPanel.Visible = false;
		}
		else if (addGamePopupPanel.Visible)
		{
			isAddGamePopupAnimatingOut = true;
			addGamePopupAnimationStep = 0;
			addGamePopupTimer.Start();
		}
	}

	private void AddGamePopupTimer_Tick(object? sender, EventArgs e)
	{
		addGamePopupAnimationStep++;
		float num = Math.Min(1f, (float)addGamePopupAnimationStep / 8f);
		float num2 = 1f - (1f - num) * (1f - num);
		if (isAddGamePopupAnimatingOut)
		{
			int num3 = (int)Math.Round(30f * num2);
			addGamePopupPanel.Location = new Point(addGamePopupTargetLocation.X - num3, addGamePopupTargetLocation.Y);
			if (num >= 1f)
			{
				addGamePopupTimer.Stop();
				isAddGamePopupAnimatingOut = false;
				addGamePopupPanel.Visible = false;
			}
		}
		else
		{
			int num4 = (int)Math.Round(30f * (1f - num2));
			addGamePopupPanel.Location = new Point(addGamePopupTargetLocation.X - num4, addGamePopupTargetLocation.Y);
			if (num >= 1f)
			{
				addGamePopupTimer.Stop();
				addGamePopupPanel.Location = addGamePopupTargetLocation;
			}
		}
	}

	private void addGameButton_Click(object sender, EventArgs e)
	{
		if (isAddGamePopupOpen)
		{
			HideAddGamePopup(animated: true);
		}
		else
		{
			ShowAddGamePopup();
		}
	}

	private async Task RefreshDesktopNotificationsAsync(bool showPopups)
	{
		if (isRefreshingNotifications)
		{
			return;
		}
		isRefreshingNotifications = true;
		try
		{
			string accessToken = await DesktopAppContext.AuthSessionManager.GetAccessTokenAsync();
			if (string.IsNullOrWhiteSpace(accessToken))
			{
				desktopNotifications = Array.Empty<DesktopNotificationItem>();
				availableDesktopUpdate = null;
				UpdateNotificationBadge(0);
				return;
			}
			DesktopAppSettings settings = DesktopAppContext.Settings;
			DesktopNotificationFeed desktopNotificationFeed = await notificationApiClient.GetFeedAsync(accessToken, settings.Platform, settings.ReleaseChannel, settings.CurrentVersion, settings.CurrentBuildNumber);
			desktopNotifications = desktopNotificationFeed.Notifications;
			StoreForm storeForm = this;
			DesktopAppUpdateInfo update = desktopNotificationFeed.Update;
			storeForm.availableDesktopUpdate = ((update != null && update.IsUpdateAvailable) ? update : null);
			UpdateNotificationBadge(desktopNotificationFeed.UnreadCount);
			DesktopAppUpdateInfo update2 = desktopNotificationFeed.Update;
			if (update2 != null && update2.IsUpdateAvailable && update2.IsForceUpdate)
			{
				await ShowMandatoryUpdateAsync(update2);
			}
			else if (showPopups)
			{
				DesktopAppUpdateInfo optionalUpdate = desktopNotificationFeed.Update;
				if (optionalUpdate != null && optionalUpdate.IsUpdateAvailable && !desktopNotifications.Any((DesktopNotificationItem notification) => string.Equals(notification.Type, "app_update", StringComparison.OrdinalIgnoreCase) && notification.DesktopAppVersionId == optionalUpdate.DesktopAppVersionId))
				{
					await ShowOptionalUpdateAsync(optionalUpdate);
				}
				await ShowPopupNotificationsAsync(accessToken);
			}
		}
		catch
		{
		}
		finally
		{
			isRefreshingNotifications = false;
		}
	}

	private async Task ShowPopupNotificationsAsync(string accessToken)
	{
		foreach (DesktopNotificationItem item in (from notification in desktopNotifications
			where notification.IsPopup && !notification.IsRead
			orderby notification.IsPinned descending, NotificationPriorityRank(notification.Priority)
			select notification).Take(3))
		{
			bool hasAction = !string.IsNullOrWhiteSpace(item.ActionUrl);
			string text = BuildNotificationMessage(item);
			DialogResult result = LauncherMessageBox.Show(this, text, string.IsNullOrWhiteSpace(item.Title) ? "NestG Launcher" : item.Title, hasAction ? MessageBoxButtons.YesNo : MessageBoxButtons.OK, ResolveNotificationIcon(item));
			item.IsRead = true;
			await notificationApiClient.MarkReadAsync(accessToken, item.NotificationId);
			if (hasAction && result == DialogResult.Yes)
			{
				if (TryResolveUpdateFromNotification(item, out DesktopAppUpdateInfo update))
				{
					await StartDesktopUpdateAsync(update, closeLauncherWhenStarted: true);
					return;
				}
				OpenExternalUrl(item.ActionUrl);
			}
		}
		UpdateNotificationBadge(desktopNotifications.Count((DesktopNotificationItem notification) => !notification.IsRead));
	}

	private async Task ShowNotificationCenterAsync()
	{
		string accessToken = await DesktopAppContext.AuthSessionManager.GetAccessTokenAsync();
		if (!string.IsNullOrWhiteSpace(accessToken))
		{
			await RefreshDesktopNotificationsAsync(showPopups: false);
		}
		DesktopAppUpdateInfo desktopAppUpdateInfo = availableDesktopUpdate;
		if (desktopAppUpdateInfo != null && desktopAppUpdateInfo.IsUpdateAvailable && LauncherMessageBox.Show(this, BuildNotificationCenterUpdateMessage(desktopAppUpdateInfo, desktopNotifications.Count, DesktopAppContext.Settings.CurrentVersion, DesktopAppContext.Settings.CurrentBuildNumber), "Cập nhật NestG Launcher", MessageBoxButtons.YesNo, desktopAppUpdateInfo.IsForceUpdate ? MessageBoxIcon.Exclamation : MessageBoxIcon.Asterisk) == DialogResult.Yes)
		{
			await StartDesktopUpdateAsync(desktopAppUpdateInfo, closeLauncherWhenStarted: true);
			return;
		}
		if (desktopNotifications.Count == 0)
		{
			StoreForm owner = this;
			DesktopAppUpdateInfo desktopAppUpdateInfo2 = availableDesktopUpdate;
			LauncherMessageBox.Show(owner, (desktopAppUpdateInfo2 != null && desktopAppUpdateInfo2.IsUpdateAvailable) ? "Bạn chưa có thông báo nào khác từ launcher." : "Bạn chưa có thông báo nào từ launcher.", "NestG Launcher", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
			return;
		}
		LauncherMessageBox.Show(this, BuildNotificationCenterMessage(desktopNotifications), "Thông báo NestG Launcher", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
		if (string.IsNullOrWhiteSpace(accessToken))
		{
			return;
		}
		Guid[] unreadIds = (from notification in desktopNotifications
			where !notification.IsRead
			select notification.NotificationId).ToArray();
		if (unreadIds.Length == 0)
		{
			return;
		}
		await notificationApiClient.MarkAllReadAsync(accessToken, (IReadOnlyCollection<Guid>)(object)unreadIds);
		foreach (DesktopNotificationItem item in desktopNotifications.Where((DesktopNotificationItem notification) => unreadIds.Contains(notification.NotificationId)))
		{
			item.IsRead = true;
			item.ReadAt = DateTime.Now;
		}
		UpdateNotificationBadge(0);
	}

	private async Task ShowMandatoryUpdateAsync(DesktopAppUpdateInfo update)
	{
		notificationRefreshTimer.Stop();
		DesktopAppSettings settings = DesktopAppContext.Settings;
		string text = BuildMandatoryUpdateMessage(update, settings.CurrentVersion, settings.CurrentBuildNumber);
		if (LauncherMessageBox.Show(this, text, "Bắt buộc cập nhật NestG Launcher", MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation) == DialogResult.Yes)
		{
			await StartDesktopUpdateAsync(update, closeLauncherWhenStarted: true);
		}
		Close();
	}

	private async Task ShowOptionalUpdateAsync(DesktopAppUpdateInfo update)
	{
		if (LauncherMessageBox.Show(this, BuildOptionalUpdateMessage(update, DesktopAppContext.Settings.CurrentVersion, DesktopAppContext.Settings.CurrentBuildNumber), "Có bản cập nhật NestG Launcher", MessageBoxButtons.YesNo, MessageBoxIcon.Asterisk) == DialogResult.Yes)
		{
			await StartDesktopUpdateAsync(update, closeLauncherWhenStarted: true);
		}
	}

	private async Task StartDesktopUpdateAsync(DesktopAppUpdateInfo update, bool closeLauncherWhenStarted)
	{
		if (isStartingDesktopUpdate)
		{
			return;
		}
		isStartingDesktopUpdate = true;
		notificationRefreshTimer.Stop();
		try
		{
			string setupPath = null;
			Progress<string> progress = new Progress<string>(delegate(string status)
			{
				loadingOverlay.UpdateStatus(status);
			});
			await loadingOverlay.RunAsync("Đang tải bản cập nhật", "NestG Launcher đang tải file setup mới. Sau khi tải xong, setup sẽ được mở tự động.", "Đang chuẩn bị tải setup", async delegate
			{
				setupPath = await updateInstallerService.DownloadSetupAsync(update, progress).ConfigureAwait(continueOnCapturedContext: true);
			}, 320);
			if (string.IsNullOrWhiteSpace(setupPath))
			{
				throw new InvalidOperationException("Không tìm thấy file setup sau khi tải.");
			}
			DesktopUpdateInstallerService.StartSetup(setupPath);
			LauncherMessageBox.Show(this, "Setup mới đã được mở. Launcher sẽ đóng lại để quá trình cập nhật có thể ghi đè file hiện tại.", "Cập nhật NestG Launcher", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
			if (closeLauncherWhenStarted)
			{
				Close();
			}
		}
		catch (Exception ex)
		{
			notificationRefreshTimer.Start();
			LauncherMessageBox.Show(this, "Không thể tải hoặc mở bản cập nhật lúc này.\n\n" + ex.Message, "Cập nhật NestG Launcher", MessageBoxButtons.OK, MessageBoxIcon.Hand);
		}
		finally
		{
			isStartingDesktopUpdate = false;
		}
	}

	private bool TryResolveUpdateFromNotification(DesktopNotificationItem notification, out DesktopAppUpdateInfo update)
	{
		DesktopAppUpdateInfo desktopAppUpdateInfo = availableDesktopUpdate;
		if (desktopAppUpdateInfo != null && desktopAppUpdateInfo.IsUpdateAvailable && string.Equals(notification.Type, "app_update", StringComparison.OrdinalIgnoreCase) && (!notification.DesktopAppVersionId.HasValue || notification.DesktopAppVersionId.Value == desktopAppUpdateInfo.DesktopAppVersionId))
		{
			update = desktopAppUpdateInfo;
			return true;
		}
		update = null;
		return false;
	}

	private static string BuildNotificationCenterUpdateMessage(DesktopAppUpdateInfo update, int notificationCount, string currentVersion, int currentBuildNumber)
	{
		List<string> list = new List<string>
		{
			update.IsForceUpdate ? $"NestG Launcher cần cập nhật lên {update.Version} (build {update.BuildNumber}) để tiếp tục." : $"NestG Launcher có bản mới {update.Version} (build {update.BuildNumber}).",
			string.Empty,
			"Bản hiện tại: " + FormatDesktopVersion(currentVersion, currentBuildNumber),
			"Bản mới: " + FormatDesktopVersion(update.Version, update.BuildNumber),
			"Kenh phat hanh: " + FormatReleaseChannel(update.ReleaseChannel),
			"Trạng thái: " + (update.IsForceUpdate ? "Bắt buộc cập nhật" : "Có thể cập nhật sau"),
			"File tải về: " + update.FileName,
			"Dung lượng: " + update.SizeText,
			string.Empty,
			"Chọn Đồng ý để tải setup mới và mở cập nhật ngay."
		};
		if (notificationCount > 0)
		{
			list.Add("Chọn Hủy để xem các thông báo khác.");
		}
		if (!string.IsNullOrWhiteSpace(update.ReleaseNotes))
		{
			list.Add(string.Empty);
			list.Add("Ghi chú phiên bản:");
			list.Add(update.ReleaseNotes.Trim());
		}
		else
		{
			list.Add(string.Empty);
			list.Add("Ghi chú phiên bản: Chưa có ghi chú từ server.");
		}
		return string.Join(Environment.NewLine, list);
	}

	private static string BuildNotificationCenterMessage(IEnumerable<DesktopNotificationItem> notifications)
	{
		IEnumerable<string> values = (from notification in notifications
			orderby notification.IsPinned descending, NotificationPriorityRank(notification.Priority), notification.PublishedAt ?? notification.CreatedAt descending
			select notification).Select(delegate(DesktopNotificationItem notification, int index)
		{
			string value = (notification.IsRead ? "Đã đọc" : "Mới");
			string value2 = FormatNotificationDate(notification.PublishedAt ?? notification.CreatedAt);
			string value3 = (string.IsNullOrWhiteSpace(notification.ActionUrl) ? string.Empty : $"{Environment.NewLine}Hành động: {notification.ActionLabel ?? "Mở liên kết"} - {notification.ActionUrl}");
			return $"{index + 1}. [{value}] {notification.Title}{Environment.NewLine}Loại: {notification.Type} - Ưu tiên: {notification.Priority} - {value2}{Environment.NewLine}{notification.Message}{value3}";
		});
		return string.Join(Environment.NewLine + Environment.NewLine, values);
	}

	private static string BuildNotificationMessage(DesktopNotificationItem notification)
	{
		List<string> list = new List<string> { notification.Message };
		if (!string.IsNullOrWhiteSpace(notification.ActionUrl))
		{
			list.Add(string.Empty);
			list.Add((notification.ActionLabel ?? "Mở liên kết") + ": " + notification.ActionUrl);
		}
		return string.Join(Environment.NewLine, list);
	}

	private static string BuildMandatoryUpdateMessage(DesktopAppUpdateInfo update, string currentVersion, int currentBuildNumber)
	{
		List<string> list = new List<string>
		{
			"Bạn đang dùng bản cũ. NestG Launcher cần cập nhật để tiếp tục.",
			string.Empty,
			"Bản hiện tại: " + FormatDesktopVersion(currentVersion, currentBuildNumber),
			"Bản mới: " + FormatDesktopVersion(update.Version, update.BuildNumber),
			"Kenh phat hanh: " + FormatReleaseChannel(update.ReleaseChannel),
			"Trạng thái: Bắt buộc cập nhật",
			"File tải về: " + update.FileName,
			"Dung lượng: " + update.SizeText,
			string.Empty,
			"Chọn Đồng ý để tải setup mới và mở cập nhật. Nếu hủy, launcher sẽ đóng lại."
		};
		if (!string.IsNullOrWhiteSpace(update.ReleaseNotes))
		{
			list.Add(string.Empty);
			list.Add("Ghi chú phiên bản:");
			list.Add(update.ReleaseNotes.Trim());
		}
		else
		{
			list.Add(string.Empty);
			list.Add("Ghi chú phiên bản: Chưa có ghi chú từ server.");
		}
		return string.Join(Environment.NewLine, list);
	}

	private static string BuildOptionalUpdateMessage(DesktopAppUpdateInfo update, string currentVersion, int currentBuildNumber)
	{
		List<string> list = new List<string>
		{
			$"NestG Launcher có bản mới {update.Version} (build {update.BuildNumber}).",
			string.Empty,
			"Bản hiện tại: " + FormatDesktopVersion(currentVersion, currentBuildNumber),
			"Bản mới: " + FormatDesktopVersion(update.Version, update.BuildNumber),
			"Kenh phat hanh: " + FormatReleaseChannel(update.ReleaseChannel),
			"Trạng thái: Có thể cập nhật sau",
			"File tải về: " + update.FileName,
			"Dung lượng: " + update.SizeText,
			string.Empty,
			"Chọn Đồng ý để tải setup mới và mở cập nhật ngay."
		};
		if (!string.IsNullOrWhiteSpace(update.ReleaseNotes))
		{
			list.Add(string.Empty);
			list.Add("Ghi chú phiên bản:");
			list.Add(update.ReleaseNotes.Trim());
		}
		else
		{
			list.Add(string.Empty);
			list.Add("Ghi chú phiên bản: Chưa có ghi chú từ server.");
		}
		return string.Join(Environment.NewLine, list);
	}

	private static string FormatDesktopVersion(string version, int buildNumber)
	{
		return $"{(string.IsNullOrWhiteSpace(version) ? "không rõ" : version.Trim())} (build {Math.Max(0, buildNumber)})";
	}

	private static string FormatReleaseChannel(string releaseChannel)
	{
		if (!string.IsNullOrWhiteSpace(releaseChannel))
		{
			return releaseChannel.Trim();
		}
		return "stable";
	}

	private void UpdateNotificationBadge(int unreadCount)
	{
		bool flag = availableDesktopUpdate?.IsUpdateAvailable ?? false;
		int num = Math.Max(0, unreadCount) + (flag ? 1 : 0);
		if (num <= 0)
		{
			notificationButton.BadgeText = string.Empty;
			notificationBadgePanel.Visible = false;
			notificationBadgeLabel.Text = "0";
		}
		else
		{
			notificationButton.BadgeText = string.Empty;
			notificationBadgeLabel.Text = ((num > 9) ? "9+" : num.ToString());
			notificationBadgePanel.Visible = true;
			notificationBadgePanel.BringToFront();
		}
	}

	private static MessageBoxIcon ResolveNotificationIcon(DesktopNotificationItem notification)
	{
		string text = notification.Priority.Trim().ToLowerInvariant();
		if (text == "urgent" || text == "high")
		{
			return MessageBoxIcon.Exclamation;
		}
		if (string.Equals(notification.Type, "maintenance", StringComparison.OrdinalIgnoreCase))
		{
			return MessageBoxIcon.Exclamation;
		}
		return MessageBoxIcon.Asterisk;
	}

	private static int NotificationPriorityRank(string? priority)
	{
		return priority?.Trim().ToLowerInvariant() switch
		{
			"urgent" => 0, 
			"high" => 1, 
			"normal" => 2, 
			"low" => 3, 
			_ => 4, 
		};
	}

	private static string FormatNotificationDate(DateTime value)
	{
		return ((value.Kind == DateTimeKind.Unspecified) ? DateTime.SpecifyKind(value, DateTimeKind.Utc).ToLocalTime() : value.ToLocalTime()).ToString("dd/MM/yyyy HH:mm");
	}

	private static void OpenExternalUrl(string? url)
	{
		if (string.IsNullOrWhiteSpace(url))
		{
			return;
		}
		try
		{
			Process.Start(new ProcessStartInfo
			{
				FileName = url,
				UseShellExecute = true
			});
		}
		catch
		{
		}
	}

	private void addGameSubmitButton_Click(object? sender, EventArgs e)
	{
		if (string.IsNullOrWhiteSpace(addGameCodeTextBox.Text))
		{
			addGameCodeTextBox.Focus();
			return;
		}
		addGameCodeTextBox.Clear();
		HideAddGamePopup(animated: true);
	}

	private void addGameCodeTextBox_KeyDown(object? sender, KeyEventArgs e)
	{
		if (e.KeyCode == Keys.Return)
		{
			e.SuppressKeyPress = true;
			addGameSubmitButton.PerformClick();
		}
		else if (e.KeyCode == Keys.Escape)
		{
			e.SuppressKeyPress = true;
			HideAddGamePopup(animated: true);
		}
	}

	private async void notificationButton_Click(object? sender, EventArgs e)
	{
		try
		{
			await ShowNotificationCenterAsync();
		}
		catch (Exception ex)
		{
			LauncherMessageBox.Show(this, "Không thể tải thông báo lúc này.\n\n" + ex.Message, "NestG Launcher", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
		}
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing && components != null)
		{
			components.Dispose();
		}
		base.Dispose(disposing);
	}

	private void InitializeComponent()
	{
		this.rootLayout = new System.Windows.Forms.TableLayoutPanel();
		this.headerPanel = new System.Windows.Forms.Panel();
		this.headerLayout = new System.Windows.Forms.TableLayoutPanel();
		this.brandLayout = new System.Windows.Forms.TableLayoutPanel();
		this.headerLogoPictureBox = new System.Windows.Forms.PictureBox();
		this.launcherNameLabel = new System.Windows.Forms.Label();
		this.versionLabel = new System.Windows.Forms.Label();
		this.navFlowPanel = new System.Windows.Forms.FlowLayoutPanel();
		this.actionFlowPanel = new System.Windows.Forms.FlowLayoutPanel();
		this.notificationHostPanel = new System.Windows.Forms.Panel();
		this.notificationButton = new UI_Desktop.Store.StoreIconButton();
		this.notificationBadgePanel = new UI_Desktop.RoundedPanel();
		this.notificationBadgeLabel = new System.Windows.Forms.Label();
		this.cartHostPanel = new System.Windows.Forms.Panel();
		this.cartButton = new UI_Desktop.Store.StoreIconButton();
		this.cartBadgePanel = new UI_Desktop.RoundedPanel();
		this.cartBadgeLabel = new System.Windows.Forms.Label();
		this.helpButton = new UI_Desktop.Store.StoreIconButton();
		this.accountPanel = new UI_Desktop.RoundedPanel();
		this.accountLayout = new System.Windows.Forms.TableLayoutPanel();
		this.accountNameLabel = new System.Windows.Forms.Label();
		this.accountSeparatorLabel = new System.Windows.Forms.Label();
		this.balanceLabel = new System.Windows.Forms.Label();
		this.toolbarPanel = new System.Windows.Forms.Panel();
		this.toolbarLayout = new System.Windows.Forms.TableLayoutPanel();
		this.categoryComboBox = new System.Windows.Forms.ComboBox();
		this.searchPanel = new UI_Desktop.RoundedPanel();
		this.searchIconLabel = new System.Windows.Forms.Label();
		this.searchTextBox = new System.Windows.Forms.TextBox();
		this.resultCountLabel = new System.Windows.Forms.Label();
		this.scrollContentPanel = new UI_Desktop.Store.StoreScrollPanel();
		this.contentLayout = new System.Windows.Forms.TableLayoutPanel();
		this.featuredPanel = new UI_Desktop.RoundedPanel();
		this.featuredLayout = new System.Windows.Forms.TableLayoutPanel();
		this.featuredPictureBox = new System.Windows.Forms.PictureBox();
		this.featuredInfoPanel = new System.Windows.Forms.Panel();
		this.featuredInfoLayout = new System.Windows.Forms.TableLayoutPanel();
		this.featuredPromoLabel = new System.Windows.Forms.Label();
		this.featuredTitleLabel = new System.Windows.Forms.Label();
		this.featuredSubtitleLabel = new System.Windows.Forms.Label();
		this.featuredSpecsLayout = new System.Windows.Forms.TableLayoutPanel();
		this.originalPriceCaptionLabel = new System.Windows.Forms.Label();
		this.originalPriceValueLabel = new System.Windows.Forms.Label();
		this.discountCaptionLabel = new System.Windows.Forms.Label();
		this.discountValueLabel = new System.Windows.Forms.Label();
		this.salePriceCaptionLabel = new System.Windows.Forms.Label();
		this.salePriceValueLabel = new System.Windows.Forms.Label();
		this.saleLimitCaptionLabel = new System.Windows.Forms.Label();
		this.saleLimitValueLabel = new System.Windows.Forms.Label();
		this.genresCaptionLabel = new System.Windows.Forms.Label();
		this.genresValueLabel = new System.Windows.Forms.Label();
		this.sectionHeaderFlowPanel = new System.Windows.Forms.FlowLayoutPanel();
		this.catalogTitleLabel = new System.Windows.Forms.Label();
		this.catalogHintLabel = new System.Windows.Forms.Label();
		this.emptyStateLabel = new System.Windows.Forms.Label();
		this.catalogFlowPanel = new UI_Desktop.Store.StoreCatalogGridPanel();
		this.addGameButton = new System.Windows.Forms.Button();
		this.rootLayout.SuspendLayout();
		this.headerPanel.SuspendLayout();
		this.headerLayout.SuspendLayout();
		this.brandLayout.SuspendLayout();
		((System.ComponentModel.ISupportInitialize)this.headerLogoPictureBox).BeginInit();
		this.actionFlowPanel.SuspendLayout();
		this.notificationHostPanel.SuspendLayout();
		this.notificationBadgePanel.SuspendLayout();
		this.cartHostPanel.SuspendLayout();
		this.cartBadgePanel.SuspendLayout();
		this.accountPanel.SuspendLayout();
		this.accountLayout.SuspendLayout();
		this.toolbarPanel.SuspendLayout();
		this.toolbarLayout.SuspendLayout();
		this.searchPanel.SuspendLayout();
		this.scrollContentPanel.SuspendLayout();
		this.contentLayout.SuspendLayout();
		this.featuredPanel.SuspendLayout();
		this.featuredLayout.SuspendLayout();
		((System.ComponentModel.ISupportInitialize)this.featuredPictureBox).BeginInit();
		this.featuredInfoPanel.SuspendLayout();
		this.featuredInfoLayout.SuspendLayout();
		this.featuredSpecsLayout.SuspendLayout();
		this.sectionHeaderFlowPanel.SuspendLayout();
		base.SuspendLayout();
		this.rootLayout.BackColor = System.Drawing.Color.FromArgb(13, 18, 25);
		this.rootLayout.ColumnCount = 1;
		this.rootLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100f));
		this.rootLayout.Controls.Add(this.headerPanel, 0, 0);
		this.rootLayout.Controls.Add(this.toolbarPanel, 0, 1);
		this.rootLayout.Controls.Add(this.scrollContentPanel, 0, 2);
		this.rootLayout.Dock = System.Windows.Forms.DockStyle.Fill;
		this.rootLayout.Location = new System.Drawing.Point(0, 0);
		this.rootLayout.Margin = new System.Windows.Forms.Padding(0);
		this.rootLayout.Name = "rootLayout";
		this.rootLayout.RowCount = 3;
		this.rootLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 74f));
		this.rootLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 60f));
		this.rootLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100f));
		this.rootLayout.Size = new System.Drawing.Size(1464, 821);
		this.rootLayout.TabIndex = 0;
		this.headerPanel.BackColor = System.Drawing.Color.FromArgb(16, 21, 29);
		this.headerPanel.Controls.Add(this.headerLayout);
		this.headerPanel.Dock = System.Windows.Forms.DockStyle.Fill;
		this.headerPanel.Location = new System.Drawing.Point(0, 0);
		this.headerPanel.Margin = new System.Windows.Forms.Padding(0);
		this.headerPanel.Name = "headerPanel";
		this.headerPanel.Padding = new System.Windows.Forms.Padding(26, 12, 18, 10);
		this.headerPanel.Size = new System.Drawing.Size(1464, 74);
		this.headerPanel.TabIndex = 0;
		this.headerLayout.ColumnCount = 3;
		this.headerLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
		this.headerLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100f));
		this.headerLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
		this.headerLayout.Controls.Add(this.brandLayout, 0, 0);
		this.headerLayout.Controls.Add(this.navFlowPanel, 1, 0);
		this.headerLayout.Controls.Add(this.actionFlowPanel, 2, 0);
		this.headerLayout.Dock = System.Windows.Forms.DockStyle.Fill;
		this.headerLayout.Location = new System.Drawing.Point(26, 12);
		this.headerLayout.Margin = new System.Windows.Forms.Padding(0);
		this.headerLayout.Name = "headerLayout";
		this.headerLayout.RowCount = 1;
		this.headerLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100f));
		this.headerLayout.Size = new System.Drawing.Size(1420, 52);
		this.headerLayout.TabIndex = 0;
		this.brandLayout.AutoSize = true;
		this.brandLayout.ColumnCount = 3;
		this.brandLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
		this.brandLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
		this.brandLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
		this.brandLayout.Controls.Add(this.headerLogoPictureBox, 0, 0);
		this.brandLayout.Controls.Add(this.launcherNameLabel, 1, 0);
		this.brandLayout.Controls.Add(this.versionLabel, 2, 0);
		this.brandLayout.Dock = System.Windows.Forms.DockStyle.Fill;
		this.brandLayout.Location = new System.Drawing.Point(0, 0);
		this.brandLayout.Margin = new System.Windows.Forms.Padding(0);
		this.brandLayout.Name = "brandLayout";
		this.brandLayout.RowCount = 1;
		this.brandLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100f));
		this.brandLayout.Size = new System.Drawing.Size(257, 52);
		this.brandLayout.TabIndex = 0;
		this.headerLogoPictureBox.Location = new System.Drawing.Point(0, 6);
		this.headerLogoPictureBox.Margin = new System.Windows.Forms.Padding(0, 6, 10, 0);
		this.headerLogoPictureBox.Name = "headerLogoPictureBox";
		this.headerLogoPictureBox.Size = new System.Drawing.Size(32, 32);
		this.headerLogoPictureBox.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
		this.headerLogoPictureBox.TabIndex = 0;
		this.headerLogoPictureBox.TabStop = false;
		this.launcherNameLabel.Anchor = System.Windows.Forms.AnchorStyles.Left;
		this.launcherNameLabel.AutoSize = true;
		this.launcherNameLabel.Font = new System.Drawing.Font("Segoe UI Semibold", 16f, System.Drawing.FontStyle.Bold);
		this.launcherNameLabel.ForeColor = System.Drawing.Color.FromArgb(239, 243, 248);
		this.launcherNameLabel.Location = new System.Drawing.Point(42, 7);
		this.launcherNameLabel.Margin = new System.Windows.Forms.Padding(0, 0, 10, 0);
		this.launcherNameLabel.Name = "launcherNameLabel";
		this.launcherNameLabel.Size = new System.Drawing.Size(164, 37);
		this.launcherNameLabel.TabIndex = 1;
		this.launcherNameLabel.Text = "NestG Store";
		this.versionLabel.Anchor = System.Windows.Forms.AnchorStyles.Left;
		this.versionLabel.AutoSize = true;
		this.versionLabel.Font = new System.Drawing.Font("Segoe UI Semibold", 10f, System.Drawing.FontStyle.Bold);
		this.versionLabel.ForeColor = System.Drawing.Color.FromArgb(143, 156, 176);
		this.versionLabel.Location = new System.Drawing.Point(216, 14);
		this.versionLabel.Margin = new System.Windows.Forms.Padding(0);
		this.versionLabel.Name = "versionLabel";
		this.versionLabel.Size = new System.Drawing.Size(41, 23);
		this.versionLabel.TabIndex = 2;
		this.versionLabel.Text = "v0.0";
		this.navFlowPanel.AutoSize = true;
		this.navFlowPanel.BackColor = System.Drawing.Color.FromArgb(16, 21, 29);
		this.navFlowPanel.Dock = System.Windows.Forms.DockStyle.Fill;
		this.navFlowPanel.Location = new System.Drawing.Point(281, 0);
		this.navFlowPanel.Margin = new System.Windows.Forms.Padding(24, 0, 0, 0);
		this.navFlowPanel.Name = "navFlowPanel";
		this.navFlowPanel.Padding = new System.Windows.Forms.Padding(0, 2, 0, 0);
		this.navFlowPanel.Size = new System.Drawing.Size(691, 52);
		this.navFlowPanel.TabIndex = 1;
		this.navFlowPanel.WrapContents = false;
		this.actionFlowPanel.AutoSize = true;
		this.actionFlowPanel.BackColor = System.Drawing.Color.FromArgb(16, 21, 29);
		this.actionFlowPanel.Controls.Add(this.notificationHostPanel);
		this.actionFlowPanel.Controls.Add(this.cartHostPanel);
		this.actionFlowPanel.Controls.Add(this.helpButton);
		this.actionFlowPanel.Controls.Add(this.accountPanel);
		this.actionFlowPanel.Dock = System.Windows.Forms.DockStyle.Fill;
		this.actionFlowPanel.Location = new System.Drawing.Point(982, 0);
		this.actionFlowPanel.Margin = new System.Windows.Forms.Padding(10, 0, 0, 0);
		this.actionFlowPanel.Name = "actionFlowPanel";
		this.actionFlowPanel.Padding = new System.Windows.Forms.Padding(0, 2, 0, 0);
		this.actionFlowPanel.Size = new System.Drawing.Size(438, 52);
		this.actionFlowPanel.TabIndex = 2;
		this.actionFlowPanel.WrapContents = false;
		this.notificationHostPanel.BackColor = System.Drawing.Color.FromArgb(16, 21, 29);
		this.notificationHostPanel.Controls.Add(this.notificationButton);
		this.notificationHostPanel.Controls.Add(this.notificationBadgePanel);
		this.notificationHostPanel.Location = new System.Drawing.Point(0, 2);
		this.notificationHostPanel.Margin = new System.Windows.Forms.Padding(0);
		this.notificationHostPanel.Name = "notificationHostPanel";
		this.notificationHostPanel.Size = new System.Drawing.Size(64, 44);
		this.notificationHostPanel.TabIndex = 0;
		this.notificationButton.BackColor = System.Drawing.Color.Transparent;
		this.notificationButton.FlatAppearance.BorderSize = 0;
		this.notificationButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
		this.notificationButton.Font = new System.Drawing.Font("Segoe UI", 18f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
		this.notificationButton.ForeColor = System.Drawing.SystemColors.ButtonFace;
		this.notificationButton.Location = new System.Drawing.Point(0, 0);
		this.notificationButton.Margin = new System.Windows.Forms.Padding(0);
		this.notificationButton.Name = "notificationButton";
		this.notificationButton.Size = new System.Drawing.Size(44, 44);
		this.notificationButton.TabIndex = 0;
		this.notificationButton.TabStop = false;
		this.notificationButton.UseVisualStyleBackColor = false;
		this.notificationButton.Click += new System.EventHandler(notificationButton_Click);
		this.notificationBadgePanel.BackColor = System.Drawing.Color.Transparent;
		this.notificationBadgePanel.Controls.Add(this.notificationBadgeLabel);
		this.notificationBadgePanel.Location = new System.Drawing.Point(38, 1);
		this.notificationBadgePanel.Margin = new System.Windows.Forms.Padding(0);
		this.notificationBadgePanel.Name = "notificationBadgePanel";
		this.notificationBadgePanel.Size = new System.Drawing.Size(22, 22);
		this.notificationBadgePanel.TabIndex = 1;
		this.notificationBadgePanel.Visible = false;
		this.notificationBadgeLabel.Dock = System.Windows.Forms.DockStyle.Fill;
		this.notificationBadgeLabel.Font = new System.Drawing.Font("Segoe UI Semibold", 8.5f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, 0);
		this.notificationBadgeLabel.ForeColor = System.Drawing.Color.White;
		this.notificationBadgeLabel.Location = new System.Drawing.Point(0, 0);
		this.notificationBadgeLabel.Margin = new System.Windows.Forms.Padding(0);
		this.notificationBadgeLabel.Name = "notificationBadgeLabel";
		this.notificationBadgeLabel.Size = new System.Drawing.Size(22, 22);
		this.notificationBadgeLabel.TabIndex = 0;
		this.notificationBadgeLabel.Text = "0";
		this.notificationBadgeLabel.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
		this.notificationBadgeLabel.UseCompatibleTextRendering = true;
		this.cartHostPanel.BackColor = System.Drawing.Color.FromArgb(16, 21, 29);
		this.cartHostPanel.Controls.Add(this.cartButton);
		this.cartHostPanel.Controls.Add(this.cartBadgePanel);
		this.cartHostPanel.Location = new System.Drawing.Point(56, 2);
		this.cartHostPanel.Margin = new System.Windows.Forms.Padding(0, 0, 12, 0);
		this.cartHostPanel.Name = "cartHostPanel";
		this.cartHostPanel.Size = new System.Drawing.Size(64, 44);
		this.cartHostPanel.TabIndex = 1;
		this.cartButton.BackColor = System.Drawing.Color.Transparent;
		this.cartButton.FlatAppearance.BorderSize = 0;
		this.cartButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
		this.cartButton.Location = new System.Drawing.Point(0, 0);
		this.cartButton.Margin = new System.Windows.Forms.Padding(0);
		this.cartButton.Name = "cartButton";
		this.cartButton.Size = new System.Drawing.Size(44, 44);
		this.cartButton.TabIndex = 0;
		this.cartButton.TabStop = false;
		this.cartButton.UseVisualStyleBackColor = false;
		this.cartBadgePanel.BackColor = System.Drawing.Color.Transparent;
		this.cartBadgePanel.Controls.Add(this.cartBadgeLabel);
		this.cartBadgePanel.Location = new System.Drawing.Point(38, 1);
		this.cartBadgePanel.Margin = new System.Windows.Forms.Padding(0);
		this.cartBadgePanel.Name = "cartBadgePanel";
		this.cartBadgePanel.Size = new System.Drawing.Size(22, 22);
		this.cartBadgePanel.TabIndex = 1;
		this.cartBadgeLabel.Dock = System.Windows.Forms.DockStyle.Fill;
		this.cartBadgeLabel.Font = new System.Drawing.Font("Segoe UI Semibold", 8.5f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, 0);
		this.cartBadgeLabel.ForeColor = System.Drawing.Color.White;
		this.cartBadgeLabel.Location = new System.Drawing.Point(0, 0);
		this.cartBadgeLabel.Margin = new System.Windows.Forms.Padding(0);
		this.cartBadgeLabel.Name = "cartBadgeLabel";
		this.cartBadgeLabel.Size = new System.Drawing.Size(22, 22);
		this.cartBadgeLabel.TabIndex = 0;
		this.cartBadgeLabel.Text = "0";
		this.cartBadgeLabel.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
		this.cartBadgeLabel.UseCompatibleTextRendering = true;
		this.helpButton.BackColor = System.Drawing.Color.Transparent;
		this.helpButton.FlatAppearance.BorderSize = 0;
		this.helpButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
		this.helpButton.Location = new System.Drawing.Point(132, 2);
		this.helpButton.Margin = new System.Windows.Forms.Padding(0, 0, 14, 0);
		this.helpButton.Name = "helpButton";
		this.helpButton.Size = new System.Drawing.Size(44, 44);
		this.helpButton.TabIndex = 2;
		this.helpButton.TabStop = false;
		this.helpButton.UseVisualStyleBackColor = false;
		this.accountPanel.BackColor = System.Drawing.Color.Transparent;
		this.accountPanel.Controls.Add(this.accountLayout);
		this.accountPanel.Location = new System.Drawing.Point(190, 3);
		this.accountPanel.Margin = new System.Windows.Forms.Padding(0, 1, 0, 0);
		this.accountPanel.Name = "accountPanel";
		this.accountPanel.Padding = new System.Windows.Forms.Padding(16, 8, 16, 8);
		this.accountPanel.Size = new System.Drawing.Size(248, 44);
		this.accountPanel.TabIndex = 3;
		this.accountLayout.ColumnCount = 3;
		this.accountLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100f));
		this.accountLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
		this.accountLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
		this.accountLayout.Controls.Add(this.accountNameLabel, 0, 0);
		this.accountLayout.Controls.Add(this.accountSeparatorLabel, 1, 0);
		this.accountLayout.Controls.Add(this.balanceLabel, 2, 0);
		this.accountLayout.Dock = System.Windows.Forms.DockStyle.Fill;
		this.accountLayout.Location = new System.Drawing.Point(16, 8);
		this.accountLayout.Margin = new System.Windows.Forms.Padding(0);
		this.accountLayout.Name = "accountLayout";
		this.accountLayout.RowCount = 1;
		this.accountLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100f));
		this.accountLayout.Size = new System.Drawing.Size(216, 28);
		this.accountLayout.TabIndex = 0;
		this.accountNameLabel.Anchor = System.Windows.Forms.AnchorStyles.Left;
		this.accountNameLabel.AutoSize = true;
		this.accountNameLabel.Font = new System.Drawing.Font("Segoe UI Semibold", 9.5f, System.Drawing.FontStyle.Bold);
		this.accountNameLabel.ForeColor = System.Drawing.Color.FromArgb(239, 243, 248);
		this.accountNameLabel.Location = new System.Drawing.Point(0, 3);
		this.accountNameLabel.Margin = new System.Windows.Forms.Padding(0, 0, 12, 0);
		this.accountNameLabel.Name = "accountNameLabel";
		this.accountNameLabel.Size = new System.Drawing.Size(91, 21);
		this.accountNameLabel.TabIndex = 1;
		this.accountNameLabel.Text = "nguyenduc";
		this.accountSeparatorLabel.Anchor = System.Windows.Forms.AnchorStyles.None;
		this.accountSeparatorLabel.AutoSize = true;
		this.accountSeparatorLabel.Font = new System.Drawing.Font("Segoe UI Semibold", 10f, System.Drawing.FontStyle.Bold);
		this.accountSeparatorLabel.ForeColor = System.Drawing.Color.FromArgb(91, 106, 127);
		this.accountSeparatorLabel.Location = new System.Drawing.Point(156, 2);
		this.accountSeparatorLabel.Margin = new System.Windows.Forms.Padding(0, 0, 12, 0);
		this.accountSeparatorLabel.Name = "accountSeparatorLabel";
		this.accountSeparatorLabel.Size = new System.Drawing.Size(15, 23);
		this.accountSeparatorLabel.TabIndex = 2;
		this.accountSeparatorLabel.Text = "|";
		this.balanceLabel.Anchor = System.Windows.Forms.AnchorStyles.Right;
		this.balanceLabel.AutoSize = true;
		this.balanceLabel.Font = new System.Drawing.Font("Segoe UI Semibold", 9.5f, System.Drawing.FontStyle.Bold);
		this.balanceLabel.ForeColor = System.Drawing.Color.FromArgb(181, 244, 121);
		this.balanceLabel.Location = new System.Drawing.Point(183, 3);
		this.balanceLabel.Margin = new System.Windows.Forms.Padding(0);
		this.balanceLabel.Name = "balanceLabel";
		this.balanceLabel.Size = new System.Drawing.Size(33, 21);
		this.balanceLabel.TabIndex = 3;
		this.balanceLabel.Text = "0 đ";
		this.toolbarPanel.BackColor = System.Drawing.Color.FromArgb(16, 21, 29);
		this.toolbarPanel.Controls.Add(this.toolbarLayout);
		this.toolbarPanel.Dock = System.Windows.Forms.DockStyle.Fill;
		this.toolbarPanel.Location = new System.Drawing.Point(0, 74);
		this.toolbarPanel.Margin = new System.Windows.Forms.Padding(0);
		this.toolbarPanel.Name = "toolbarPanel";
		this.toolbarPanel.Padding = new System.Windows.Forms.Padding(26, 8, 26, 10);
		this.toolbarPanel.Size = new System.Drawing.Size(1464, 60);
		this.toolbarPanel.TabIndex = 1;
		this.toolbarLayout.ColumnCount = 3;
		this.toolbarLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 170f));
		this.toolbarLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100f));
		this.toolbarLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
		this.toolbarLayout.Controls.Add(this.categoryComboBox, 0, 0);
		this.toolbarLayout.Controls.Add(this.searchPanel, 1, 0);
		this.toolbarLayout.Controls.Add(this.resultCountLabel, 2, 0);
		this.toolbarLayout.Dock = System.Windows.Forms.DockStyle.Fill;
		this.toolbarLayout.Location = new System.Drawing.Point(26, 8);
		this.toolbarLayout.Margin = new System.Windows.Forms.Padding(0);
		this.toolbarLayout.Name = "toolbarLayout";
		this.toolbarLayout.RowCount = 1;
		this.toolbarLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100f));
		this.toolbarLayout.Size = new System.Drawing.Size(1412, 42);
		this.toolbarLayout.TabIndex = 0;
		this.categoryComboBox.BackColor = System.Drawing.Color.FromArgb(31, 40, 53);
		this.categoryComboBox.Dock = System.Windows.Forms.DockStyle.Left;
		this.categoryComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
		this.categoryComboBox.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
		this.categoryComboBox.Font = new System.Drawing.Font("Segoe UI Semibold", 10f, System.Drawing.FontStyle.Bold);
		this.categoryComboBox.ForeColor = System.Drawing.Color.FromArgb(239, 243, 248);
		this.categoryComboBox.FormattingEnabled = true;
		this.categoryComboBox.IntegralHeight = false;
		this.categoryComboBox.ItemHeight = 23;
		this.categoryComboBox.Location = new System.Drawing.Point(0, 4);
		this.categoryComboBox.Margin = new System.Windows.Forms.Padding(0, 4, 16, 0);
		this.categoryComboBox.MaxDropDownItems = 12;
		this.categoryComboBox.Name = "categoryComboBox";
		this.categoryComboBox.Size = new System.Drawing.Size(154, 31);
		this.categoryComboBox.TabIndex = 0;
		this.searchPanel.BackColor = System.Drawing.Color.Transparent;
		this.searchPanel.Controls.Add(this.searchIconLabel);
		this.searchPanel.Controls.Add(this.searchTextBox);
		this.searchPanel.Dock = System.Windows.Forms.DockStyle.Fill;
		this.searchPanel.Location = new System.Drawing.Point(186, 0);
		this.searchPanel.Margin = new System.Windows.Forms.Padding(16, 0, 16, 0);
		this.searchPanel.Name = "searchPanel";
		this.searchPanel.Size = new System.Drawing.Size(1061, 42);
		this.searchPanel.TabIndex = 1;
		this.searchIconLabel.AutoSize = true;
		this.searchIconLabel.Location = new System.Drawing.Point(16, 11);
		this.searchIconLabel.Name = "searchIconLabel";
		this.searchIconLabel.Size = new System.Drawing.Size(20, 20);
		this.searchIconLabel.TabIndex = 0;
		this.searchIconLabel.Text = "⌕";
		this.searchTextBox.BackColor = System.Drawing.Color.FromArgb(24, 31, 42);
		this.searchTextBox.BorderStyle = System.Windows.Forms.BorderStyle.None;
		this.searchTextBox.Font = new System.Drawing.Font("Segoe UI", 10.5f);
		this.searchTextBox.ForeColor = System.Drawing.Color.FromArgb(239, 243, 248);
		this.searchTextBox.Location = new System.Drawing.Point(44, 10);
		this.searchTextBox.Margin = new System.Windows.Forms.Padding(0);
		this.searchTextBox.Name = "searchTextBox";
		this.searchTextBox.PlaceholderText = "Tìm trò chơi, thể loại hoặc điểm nổi bật...";
		this.searchTextBox.Size = new System.Drawing.Size(280, 24);
		this.searchTextBox.TabIndex = 1;
		this.resultCountLabel.Anchor = System.Windows.Forms.AnchorStyles.Right;
		this.resultCountLabel.AutoSize = true;
		this.resultCountLabel.Font = new System.Drawing.Font("Segoe UI", 9.5f);
		this.resultCountLabel.ForeColor = System.Drawing.Color.FromArgb(143, 156, 176);
		this.resultCountLabel.Location = new System.Drawing.Point(1263, 10);
		this.resultCountLabel.Margin = new System.Windows.Forms.Padding(0);
		this.resultCountLabel.Name = "resultCountLabel";
		this.resultCountLabel.Size = new System.Drawing.Size(149, 21);
		this.resultCountLabel.TabIndex = 2;
		this.resultCountLabel.Text = "Hiển thị 0 sản phẩm";
		this.scrollContentPanel.AutoScroll = true;
		this.scrollContentPanel.BackColor = System.Drawing.Color.FromArgb(13, 18, 25);
		this.scrollContentPanel.Controls.Add(this.contentLayout);
		this.scrollContentPanel.Dock = System.Windows.Forms.DockStyle.Fill;
		this.scrollContentPanel.ForeColor = System.Drawing.SystemColors.ControlLight;
		this.scrollContentPanel.Location = new System.Drawing.Point(0, 134);
		this.scrollContentPanel.Margin = new System.Windows.Forms.Padding(0);
		this.scrollContentPanel.Name = "scrollContentPanel";
		this.scrollContentPanel.Padding = new System.Windows.Forms.Padding(30, 22, 30, 26);
		this.scrollContentPanel.Size = new System.Drawing.Size(1464, 687);
		this.scrollContentPanel.TabIndex = 2;
		this.contentLayout.AutoSize = true;
		this.contentLayout.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
		this.contentLayout.ColumnCount = 1;
		this.contentLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100f));
		this.contentLayout.Controls.Add(this.featuredPanel, 0, 0);
		this.contentLayout.Controls.Add(this.sectionHeaderFlowPanel, 0, 1);
		this.contentLayout.Controls.Add(this.emptyStateLabel, 0, 2);
		this.contentLayout.Controls.Add(this.catalogFlowPanel, 0, 3);
		this.contentLayout.Controls.Add(this.addGameButton, 0, 4);
		this.contentLayout.Dock = System.Windows.Forms.DockStyle.Top;
		this.contentLayout.Location = new System.Drawing.Point(30, 22);
		this.contentLayout.Margin = new System.Windows.Forms.Padding(0);
		this.contentLayout.Name = "contentLayout";
		this.contentLayout.RowCount = 5;
		this.contentLayout.RowStyles.Add(new System.Windows.Forms.RowStyle());
		this.contentLayout.RowStyles.Add(new System.Windows.Forms.RowStyle());
		this.contentLayout.RowStyles.Add(new System.Windows.Forms.RowStyle());
		this.contentLayout.RowStyles.Add(new System.Windows.Forms.RowStyle());
		this.contentLayout.RowStyles.Add(new System.Windows.Forms.RowStyle());
		this.contentLayout.Size = new System.Drawing.Size(1404, 569);
		this.contentLayout.TabIndex = 0;
		this.featuredPanel.BackColor = System.Drawing.Color.Transparent;
		this.featuredPanel.Controls.Add(this.featuredLayout);
		this.featuredPanel.Dock = System.Windows.Forms.DockStyle.Top;
		this.featuredPanel.Location = new System.Drawing.Point(0, 0);
		this.featuredPanel.Margin = new System.Windows.Forms.Padding(0, 0, 0, 28);
		this.featuredPanel.Name = "featuredPanel";
		this.featuredPanel.Padding = new System.Windows.Forms.Padding(14);
		this.featuredPanel.Size = new System.Drawing.Size(1404, 390);
		this.featuredPanel.TabIndex = 0;
		this.featuredLayout.ColumnCount = 2;
		this.featuredLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 64f));
		this.featuredLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 36f));
		this.featuredLayout.Controls.Add(this.featuredPictureBox, 0, 0);
		this.featuredLayout.Controls.Add(this.featuredInfoPanel, 1, 0);
		this.featuredLayout.Dock = System.Windows.Forms.DockStyle.Fill;
		this.featuredLayout.Location = new System.Drawing.Point(14, 14);
		this.featuredLayout.Margin = new System.Windows.Forms.Padding(0);
		this.featuredLayout.Name = "featuredLayout";
		this.featuredLayout.RowCount = 1;
		this.featuredLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100f));
		this.featuredLayout.Size = new System.Drawing.Size(1376, 362);
		this.featuredLayout.TabIndex = 0;
		this.featuredPictureBox.BackColor = System.Drawing.Color.FromArgb(10, 15, 22);
		this.featuredPictureBox.Dock = System.Windows.Forms.DockStyle.Fill;
		this.featuredPictureBox.Location = new System.Drawing.Point(0, 0);
		this.featuredPictureBox.Margin = new System.Windows.Forms.Padding(0);
		this.featuredPictureBox.Name = "featuredPictureBox";
		this.featuredPictureBox.Size = new System.Drawing.Size(880, 362);
		this.featuredPictureBox.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
		this.featuredPictureBox.TabIndex = 0;
		this.featuredPictureBox.TabStop = false;
		this.featuredInfoPanel.BackColor = System.Drawing.Color.FromArgb(14, 20, 29);
		this.featuredInfoPanel.Controls.Add(this.featuredInfoLayout);
		this.featuredInfoPanel.Dock = System.Windows.Forms.DockStyle.Fill;
		this.featuredInfoPanel.Location = new System.Drawing.Point(880, 0);
		this.featuredInfoPanel.Margin = new System.Windows.Forms.Padding(0);
		this.featuredInfoPanel.Name = "featuredInfoPanel";
		this.featuredInfoPanel.Padding = new System.Windows.Forms.Padding(22, 20, 22, 16);
		this.featuredInfoPanel.Size = new System.Drawing.Size(496, 362);
		this.featuredInfoPanel.TabIndex = 1;
		this.featuredInfoLayout.ColumnCount = 1;
		this.featuredInfoLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100f));
		this.featuredInfoLayout.Controls.Add(this.featuredPromoLabel, 0, 0);
		this.featuredInfoLayout.Controls.Add(this.featuredTitleLabel, 0, 1);
		this.featuredInfoLayout.Controls.Add(this.featuredSubtitleLabel, 0, 2);
		this.featuredInfoLayout.Controls.Add(this.featuredSpecsLayout, 0, 3);
		this.featuredInfoLayout.Dock = System.Windows.Forms.DockStyle.Fill;
		this.featuredInfoLayout.Location = new System.Drawing.Point(22, 20);
		this.featuredInfoLayout.Margin = new System.Windows.Forms.Padding(0);
		this.featuredInfoLayout.Name = "featuredInfoLayout";
		this.featuredInfoLayout.RowCount = 4;
		this.featuredInfoLayout.RowStyles.Add(new System.Windows.Forms.RowStyle());
		this.featuredInfoLayout.RowStyles.Add(new System.Windows.Forms.RowStyle());
		this.featuredInfoLayout.RowStyles.Add(new System.Windows.Forms.RowStyle());
		this.featuredInfoLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100f));
		this.featuredInfoLayout.Size = new System.Drawing.Size(452, 326);
		this.featuredInfoLayout.TabIndex = 0;
		this.featuredPromoLabel.AutoSize = true;
		this.featuredPromoLabel.BackColor = System.Drawing.Color.FromArgb(30, 82, 67);
		this.featuredPromoLabel.Font = new System.Drawing.Font("Segoe UI Semibold", 9.2f, System.Drawing.FontStyle.Bold);
		this.featuredPromoLabel.ForeColor = System.Drawing.Color.FromArgb(220, 250, 240);
		this.featuredPromoLabel.Location = new System.Drawing.Point(0, 0);
		this.featuredPromoLabel.Margin = new System.Windows.Forms.Padding(0);
		this.featuredPromoLabel.Name = "featuredPromoLabel";
		this.featuredPromoLabel.Padding = new System.Windows.Forms.Padding(12, 6, 12, 6);
		this.featuredPromoLabel.Size = new System.Drawing.Size(106, 33);
		this.featuredPromoLabel.TabIndex = 0;
		this.featuredPromoLabel.Text = "SALE HOT";
		this.featuredTitleLabel.AutoSize = true;
		this.featuredTitleLabel.Font = new System.Drawing.Font("Segoe UI Semibold", 21f, System.Drawing.FontStyle.Bold);
		this.featuredTitleLabel.ForeColor = System.Drawing.Color.FromArgb(239, 243, 248);
		this.featuredTitleLabel.Location = new System.Drawing.Point(0, 47);
		this.featuredTitleLabel.Margin = new System.Windows.Forms.Padding(0, 14, 0, 0);
		this.featuredTitleLabel.MaximumSize = new System.Drawing.Size(430, 0);
		this.featuredTitleLabel.Name = "featuredTitleLabel";
		this.featuredTitleLabel.Size = new System.Drawing.Size(234, 47);
		this.featuredTitleLabel.TabIndex = 1;
		this.featuredTitleLabel.Text = "Featured title";
		this.featuredSubtitleLabel.AutoSize = true;
		this.featuredSubtitleLabel.Font = new System.Drawing.Font("Segoe UI", 10f);
		this.featuredSubtitleLabel.ForeColor = System.Drawing.Color.FromArgb(162, 174, 191);
		this.featuredSubtitleLabel.Location = new System.Drawing.Point(0, 102);
		this.featuredSubtitleLabel.Margin = new System.Windows.Forms.Padding(0, 8, 0, 0);
		this.featuredSubtitleLabel.MaximumSize = new System.Drawing.Size(430, 0);
		this.featuredSubtitleLabel.Name = "featuredSubtitleLabel";
		this.featuredSubtitleLabel.Size = new System.Drawing.Size(268, 23);
		this.featuredSubtitleLabel.TabIndex = 2;
		this.featuredSubtitleLabel.Text = "Bản giới thiệu nổi bật trong ngày.";
		this.featuredSpecsLayout.ColumnCount = 2;
		this.featuredSpecsLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 124f));
		this.featuredSpecsLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100f));
		this.featuredSpecsLayout.Controls.Add(this.originalPriceCaptionLabel, 0, 0);
		this.featuredSpecsLayout.Controls.Add(this.originalPriceValueLabel, 1, 0);
		this.featuredSpecsLayout.Controls.Add(this.discountCaptionLabel, 0, 1);
		this.featuredSpecsLayout.Controls.Add(this.discountValueLabel, 1, 1);
		this.featuredSpecsLayout.Controls.Add(this.salePriceCaptionLabel, 0, 2);
		this.featuredSpecsLayout.Controls.Add(this.salePriceValueLabel, 1, 2);
		this.featuredSpecsLayout.Controls.Add(this.saleLimitCaptionLabel, 0, 3);
		this.featuredSpecsLayout.Controls.Add(this.saleLimitValueLabel, 1, 3);
		this.featuredSpecsLayout.Controls.Add(this.genresCaptionLabel, 0, 4);
		this.featuredSpecsLayout.Controls.Add(this.genresValueLabel, 1, 4);
		this.featuredSpecsLayout.Dock = System.Windows.Forms.DockStyle.Fill;
		this.featuredSpecsLayout.Location = new System.Drawing.Point(0, 147);
		this.featuredSpecsLayout.Margin = new System.Windows.Forms.Padding(0, 22, 0, 0);
		this.featuredSpecsLayout.Name = "featuredSpecsLayout";
		this.featuredSpecsLayout.RowCount = 5;
		this.featuredSpecsLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 42f));
		this.featuredSpecsLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 42f));
		this.featuredSpecsLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 50f));
		this.featuredSpecsLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 40f));
		this.featuredSpecsLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100f));
		this.featuredSpecsLayout.Size = new System.Drawing.Size(452, 179);
		this.featuredSpecsLayout.TabIndex = 3;
		this.originalPriceCaptionLabel.Anchor = System.Windows.Forms.AnchorStyles.Left;
		this.originalPriceCaptionLabel.AutoSize = true;
		this.originalPriceCaptionLabel.Font = new System.Drawing.Font("Segoe UI", 10f);
		this.originalPriceCaptionLabel.ForeColor = System.Drawing.Color.FromArgb(143, 156, 176);
		this.originalPriceCaptionLabel.Location = new System.Drawing.Point(0, 9);
		this.originalPriceCaptionLabel.Margin = new System.Windows.Forms.Padding(0);
		this.originalPriceCaptionLabel.Name = "originalPriceCaptionLabel";
		this.originalPriceCaptionLabel.Size = new System.Drawing.Size(68, 23);
		this.originalPriceCaptionLabel.TabIndex = 0;
		this.originalPriceCaptionLabel.Text = "Giá gốc";
		this.originalPriceValueLabel.Anchor = System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
		this.originalPriceValueLabel.AutoSize = true;
		this.originalPriceValueLabel.Font = new System.Drawing.Font("Segoe UI Semibold", 10f, System.Drawing.FontStyle.Bold);
		this.originalPriceValueLabel.ForeColor = System.Drawing.Color.FromArgb(239, 243, 248);
		this.originalPriceValueLabel.Location = new System.Drawing.Point(124, 9);
		this.originalPriceValueLabel.Margin = new System.Windows.Forms.Padding(0);
		this.originalPriceValueLabel.Name = "originalPriceValueLabel";
		this.originalPriceValueLabel.Size = new System.Drawing.Size(328, 23);
		this.originalPriceValueLabel.TabIndex = 1;
		this.originalPriceValueLabel.Text = "117.000 đ";
		this.originalPriceValueLabel.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
		this.discountCaptionLabel.Anchor = System.Windows.Forms.AnchorStyles.Left;
		this.discountCaptionLabel.AutoSize = true;
		this.discountCaptionLabel.Font = new System.Drawing.Font("Segoe UI", 10f);
		this.discountCaptionLabel.ForeColor = System.Drawing.Color.FromArgb(143, 156, 176);
		this.discountCaptionLabel.Location = new System.Drawing.Point(0, 51);
		this.discountCaptionLabel.Margin = new System.Windows.Forms.Padding(0);
		this.discountCaptionLabel.Name = "discountCaptionLabel";
		this.discountCaptionLabel.Size = new System.Drawing.Size(50, 23);
		this.discountCaptionLabel.TabIndex = 2;
		this.discountCaptionLabel.Text = "Giảm";
		this.discountValueLabel.Anchor = System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
		this.discountValueLabel.AutoSize = true;
		this.discountValueLabel.Font = new System.Drawing.Font("Segoe UI Semibold", 15f, System.Drawing.FontStyle.Bold);
		this.discountValueLabel.ForeColor = System.Drawing.Color.FromArgb(73, 209, 164);
		this.discountValueLabel.Location = new System.Drawing.Point(124, 45);
		this.discountValueLabel.Margin = new System.Windows.Forms.Padding(0);
		this.discountValueLabel.Name = "discountValueLabel";
		this.discountValueLabel.Size = new System.Drawing.Size(328, 35);
		this.discountValueLabel.TabIndex = 3;
		this.discountValueLabel.Text = "-90%";
		this.discountValueLabel.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
		this.salePriceCaptionLabel.Anchor = System.Windows.Forms.AnchorStyles.Left;
		this.salePriceCaptionLabel.AutoSize = true;
		this.salePriceCaptionLabel.Font = new System.Drawing.Font("Segoe UI", 10f);
		this.salePriceCaptionLabel.ForeColor = System.Drawing.Color.FromArgb(143, 156, 176);
		this.salePriceCaptionLabel.Location = new System.Drawing.Point(0, 97);
		this.salePriceCaptionLabel.Margin = new System.Windows.Forms.Padding(0);
		this.salePriceCaptionLabel.Name = "salePriceCaptionLabel";
		this.salePriceCaptionLabel.Size = new System.Drawing.Size(88, 23);
		this.salePriceCaptionLabel.TabIndex = 4;
		this.salePriceCaptionLabel.Text = "Giá ưu đãi";
		this.salePriceValueLabel.Anchor = System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
		this.salePriceValueLabel.AutoSize = true;
		this.salePriceValueLabel.Font = new System.Drawing.Font("Segoe UI Semibold", 17f, System.Drawing.FontStyle.Bold);
		this.salePriceValueLabel.ForeColor = System.Drawing.Color.FromArgb(82, 138, 255);
		this.salePriceValueLabel.Location = new System.Drawing.Point(124, 89);
		this.salePriceValueLabel.Margin = new System.Windows.Forms.Padding(0);
		this.salePriceValueLabel.Name = "salePriceValueLabel";
		this.salePriceValueLabel.Size = new System.Drawing.Size(328, 40);
		this.salePriceValueLabel.TabIndex = 5;
		this.salePriceValueLabel.Text = "11.700 đ";
		this.salePriceValueLabel.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
		this.saleLimitCaptionLabel.Anchor = System.Windows.Forms.AnchorStyles.Left;
		this.saleLimitCaptionLabel.AutoSize = true;
		this.saleLimitCaptionLabel.Font = new System.Drawing.Font("Segoe UI", 10f);
		this.saleLimitCaptionLabel.ForeColor = System.Drawing.Color.FromArgb(143, 156, 176);
		this.saleLimitCaptionLabel.Location = new System.Drawing.Point(0, 142);
		this.saleLimitCaptionLabel.Margin = new System.Windows.Forms.Padding(0);
		this.saleLimitCaptionLabel.Name = "saleLimitCaptionLabel";
		this.saleLimitCaptionLabel.Size = new System.Drawing.Size(72, 23);
		this.saleLimitCaptionLabel.TabIndex = 6;
		this.saleLimitCaptionLabel.Text = "Hiệu lực";
		this.saleLimitValueLabel.Anchor = System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
		this.saleLimitValueLabel.AutoSize = true;
		this.saleLimitValueLabel.Font = new System.Drawing.Font("Segoe UI Semibold", 10f, System.Drawing.FontStyle.Bold);
		this.saleLimitValueLabel.ForeColor = System.Drawing.Color.FromArgb(239, 243, 248);
		this.saleLimitValueLabel.Location = new System.Drawing.Point(124, 142);
		this.saleLimitValueLabel.Margin = new System.Windows.Forms.Padding(0);
		this.saleLimitValueLabel.Name = "saleLimitValueLabel";
		this.saleLimitValueLabel.Size = new System.Drawing.Size(328, 23);
		this.saleLimitValueLabel.TabIndex = 7;
		this.saleLimitValueLabel.Text = "Không giới hạn";
		this.saleLimitValueLabel.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
		this.genresCaptionLabel.Anchor = System.Windows.Forms.AnchorStyles.Left;
		this.genresCaptionLabel.AutoSize = true;
		this.genresCaptionLabel.Font = new System.Drawing.Font("Segoe UI", 10f);
		this.genresCaptionLabel.ForeColor = System.Drawing.Color.FromArgb(143, 156, 176);
		this.genresCaptionLabel.Location = new System.Drawing.Point(0, 175);
		this.genresCaptionLabel.Margin = new System.Windows.Forms.Padding(0, 1, 0, 0);
		this.genresCaptionLabel.Name = "genresCaptionLabel";
		this.genresCaptionLabel.Size = new System.Drawing.Size(70, 4);
		this.genresCaptionLabel.TabIndex = 8;
		this.genresCaptionLabel.Text = "Thể loại";
		this.genresValueLabel.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
		this.genresValueLabel.AutoSize = true;
		this.genresValueLabel.Font = new System.Drawing.Font("Segoe UI Semibold", 9.5f, System.Drawing.FontStyle.Bold);
		this.genresValueLabel.ForeColor = System.Drawing.Color.FromArgb(239, 243, 248);
		this.genresValueLabel.Location = new System.Drawing.Point(124, 175);
		this.genresValueLabel.Margin = new System.Windows.Forms.Padding(0, 1, 0, 0);
		this.genresValueLabel.MaximumSize = new System.Drawing.Size(328, 0);
		this.genresValueLabel.Name = "genresValueLabel";
		this.genresValueLabel.Size = new System.Drawing.Size(328, 4);
		this.genresValueLabel.TabIndex = 9;
		this.genresValueLabel.Text = "Farming Sim, Indie, Casual";
		this.genresValueLabel.TextAlign = System.Drawing.ContentAlignment.TopRight;
		this.sectionHeaderFlowPanel.AutoSize = true;
		this.sectionHeaderFlowPanel.Controls.Add(this.catalogTitleLabel);
		this.sectionHeaderFlowPanel.Controls.Add(this.catalogHintLabel);
		this.sectionHeaderFlowPanel.Location = new System.Drawing.Point(0, 418);
		this.sectionHeaderFlowPanel.Margin = new System.Windows.Forms.Padding(0, 0, 0, 16);
		this.sectionHeaderFlowPanel.Name = "sectionHeaderFlowPanel";
		this.sectionHeaderFlowPanel.Size = new System.Drawing.Size(324, 41);
		this.sectionHeaderFlowPanel.TabIndex = 1;
		this.sectionHeaderFlowPanel.WrapContents = false;
		this.catalogTitleLabel.AutoSize = true;
		this.catalogTitleLabel.Font = new System.Drawing.Font("Segoe UI Semibold", 18f, System.Drawing.FontStyle.Bold);
		this.catalogTitleLabel.ForeColor = System.Drawing.Color.FromArgb(239, 243, 248);
		this.catalogTitleLabel.Location = new System.Drawing.Point(0, 0);
		this.catalogTitleLabel.Margin = new System.Windows.Forms.Padding(0, 0, 12, 0);
		this.catalogTitleLabel.Name = "catalogTitleLabel";
		this.catalogTitleLabel.Size = new System.Drawing.Size(155, 41);
		this.catalogTitleLabel.TabIndex = 0;
		this.catalogTitleLabel.Text = "Khám phá";
		this.catalogHintLabel.AutoSize = true;
		this.catalogHintLabel.Font = new System.Drawing.Font("Segoe UI", 10f);
		this.catalogHintLabel.ForeColor = System.Drawing.Color.FromArgb(143, 156, 176);
		this.catalogHintLabel.Location = new System.Drawing.Point(167, 13);
		this.catalogHintLabel.Margin = new System.Windows.Forms.Padding(0, 13, 0, 0);
		this.catalogHintLabel.Name = "catalogHintLabel";
		this.catalogHintLabel.Size = new System.Drawing.Size(157, 23);
		this.catalogHintLabel.TabIndex = 1;
		this.catalogHintLabel.Text = "Danh sách đang có";
		this.emptyStateLabel.AutoSize = true;
		this.emptyStateLabel.Font = new System.Drawing.Font("Segoe UI", 11f);
		this.emptyStateLabel.ForeColor = System.Drawing.Color.FromArgb(162, 174, 191);
		this.emptyStateLabel.Location = new System.Drawing.Point(0, 475);
		this.emptyStateLabel.Margin = new System.Windows.Forms.Padding(0, 0, 0, 12);
		this.emptyStateLabel.Name = "emptyStateLabel";
		this.emptyStateLabel.Size = new System.Drawing.Size(299, 25);
		this.emptyStateLabel.TabIndex = 2;
		this.emptyStateLabel.Text = "Không có trò chơi phù hợp bộ lọc.";
		this.emptyStateLabel.Visible = false;
		this.catalogFlowPanel.AutoSize = false;
		this.catalogFlowPanel.Dock = System.Windows.Forms.DockStyle.Top;
		this.catalogFlowPanel.Location = new System.Drawing.Point(0, 512);
		this.catalogFlowPanel.Margin = new System.Windows.Forms.Padding(0);
		this.catalogFlowPanel.Name = "catalogFlowPanel";
		this.catalogFlowPanel.Size = new System.Drawing.Size(1404, 0);
		this.catalogFlowPanel.TabIndex = 3;
		this.addGameButton.AutoSize = true;
		this.addGameButton.BackColor = System.Drawing.Color.Transparent;
		this.addGameButton.FlatAppearance.BorderSize = 0;
		this.addGameButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
		this.addGameButton.Font = new System.Drawing.Font("Segoe UI Semibold", 10.5f, System.Drawing.FontStyle.Bold);
		this.addGameButton.ForeColor = System.Drawing.Color.FromArgb(162, 174, 191);
		this.addGameButton.Location = new System.Drawing.Point(0, 534);
		this.addGameButton.Margin = new System.Windows.Forms.Padding(0, 22, 0, 0);
		this.addGameButton.Name = "addGameButton";
		this.addGameButton.Size = new System.Drawing.Size(160, 35);
		this.addGameButton.TabIndex = 4;
		this.addGameButton.Text = "+  Thêm trò chơi";
		this.addGameButton.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
		this.addGameButton.UseVisualStyleBackColor = false;
		this.addGameButton.Click += new System.EventHandler(addGameButton_Click);
		base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
		this.BackColor = System.Drawing.Color.FromArgb(13, 18, 25);
		base.ClientSize = new System.Drawing.Size(1560, 860);
		base.Controls.Add(this.rootLayout);
		this.MinimumSize = new System.Drawing.Size(1020, 760);
		base.Name = "StoreForm";
		base.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
		this.Text = "NestG Launcher";
		this.rootLayout.ResumeLayout(false);
		this.headerPanel.ResumeLayout(false);
		this.headerLayout.ResumeLayout(false);
		this.headerLayout.PerformLayout();
		this.brandLayout.ResumeLayout(false);
		this.brandLayout.PerformLayout();
		((System.ComponentModel.ISupportInitialize)this.headerLogoPictureBox).EndInit();
		this.actionFlowPanel.ResumeLayout(false);
		this.notificationHostPanel.ResumeLayout(false);
		this.notificationBadgePanel.ResumeLayout(false);
		this.cartHostPanel.ResumeLayout(false);
		this.cartBadgePanel.ResumeLayout(false);
		this.accountPanel.ResumeLayout(false);
		this.accountLayout.ResumeLayout(false);
		this.accountLayout.PerformLayout();
		this.toolbarPanel.ResumeLayout(false);
		this.toolbarLayout.ResumeLayout(false);
		this.toolbarLayout.PerformLayout();
		this.searchPanel.ResumeLayout(false);
		this.searchPanel.PerformLayout();
		this.scrollContentPanel.ResumeLayout(false);
		this.scrollContentPanel.PerformLayout();
		this.contentLayout.ResumeLayout(false);
		this.contentLayout.PerformLayout();
		this.featuredPanel.ResumeLayout(false);
		this.featuredLayout.ResumeLayout(false);
		((System.ComponentModel.ISupportInitialize)this.featuredPictureBox).EndInit();
		this.featuredInfoPanel.ResumeLayout(false);
		this.featuredInfoLayout.ResumeLayout(false);
		this.featuredInfoLayout.PerformLayout();
		this.featuredSpecsLayout.ResumeLayout(false);
		this.featuredSpecsLayout.PerformLayout();
		this.sectionHeaderFlowPanel.ResumeLayout(false);
		this.sectionHeaderFlowPanel.PerformLayout();
		base.ResumeLayout(false);
	}

	private void InitializeVisualState()
	{
		base.WindowState = FormWindowState.Normal;
		Color glyphColor = Color.FromArgb(255, 255, 255);
		Color hoverGlyphColor = Color.FromArgb(255, 255, 255);
		Color surfaceColor = Color.FromArgb(19, 25, 34);
		Color hoverSurfaceColor = Color.FromArgb(24, 31, 42);
		Color outlineColor = Color.FromArgb(39, 53, 70);
		Color hoverOutlineColor = Color.FromArgb(59, 77, 101);
		notificationButton.IconKind = StoreIconKind.Notification;
		notificationButton.BadgeText = string.Empty;
		notificationButton.UseOriginalGlyphColors = false;
		notificationButton.ShowChrome = true;
		notificationButton.GlyphSize = 27f;
		notificationButton.GlyphYOffset = -1;
		notificationButton.GlyphStrokeWidth = 1.34f;
		notificationButton.ChromeWidth = 44;
		notificationButton.SurfaceColor = surfaceColor;
		notificationButton.HoverSurfaceColor = hoverSurfaceColor;
		notificationButton.OutlineColor = outlineColor;
		notificationButton.HoverOutlineColor = hoverOutlineColor;
		notificationButton.GlyphColor = glyphColor;
		notificationButton.HoverGlyphColor = hoverGlyphColor;
		cartButton.IconKind = StoreIconKind.Cart;
		cartButton.BadgeText = string.Empty;
		cartButton.UseOriginalGlyphColors = false;
		cartButton.ShowChrome = true;
		cartButton.GlyphSize = 27f;
		cartButton.GlyphYOffset = -1;
		cartButton.GlyphStrokeWidth = 1.32f;
		cartButton.SurfaceColor = surfaceColor;
		cartButton.HoverSurfaceColor = hoverSurfaceColor;
		cartButton.OutlineColor = outlineColor;
		cartButton.HoverOutlineColor = hoverOutlineColor;
		cartButton.GlyphColor = glyphColor;
		cartButton.HoverGlyphColor = hoverGlyphColor;
		helpButton.IconKind = StoreIconKind.Help;
		helpButton.BadgeText = string.Empty;
		helpButton.UseOriginalGlyphColors = false;
		helpButton.ShowChrome = true;
		helpButton.GlyphSize = 27f;
		helpButton.GlyphStrokeWidth = 1.26f;
		helpButton.SurfaceColor = Color.FromArgb(44, 40, 37);
		helpButton.HoverSurfaceColor = Color.FromArgb(61, 54, 49);
		helpButton.OutlineColor = Color.FromArgb(82, 72, 64);
		helpButton.HoverOutlineColor = Color.FromArgb(109, 95, 84);
		helpButton.GlyphColor = glyphColor;
		helpButton.HoverGlyphColor = hoverGlyphColor;
		accountPanel.SurfaceColor = Color.FromArgb(21, 29, 40);
		accountPanel.BorderColor = Color.FromArgb(53, 72, 95);
		accountPanel.BorderThickness = 1;
		accountPanel.CornerRadius = 24;
		notificationBadgePanel.SurfaceColor = AppTheme.Accent;
		notificationBadgePanel.BorderColor = AppTheme.Accent;
		notificationBadgePanel.BorderThickness = 0;
		notificationBadgePanel.CornerRadius = 11;
		cartBadgePanel.SurfaceColor = AppTheme.Accent;
		cartBadgePanel.BorderColor = AppTheme.Accent;
		cartBadgePanel.BorderThickness = 0;
		cartBadgePanel.CornerRadius = 11;
		featuredPanel.SurfaceColor = Color.FromArgb(15, 22, 31);
		featuredPanel.BorderColor = Color.FromArgb(47, 63, 82);
		featuredPanel.BorderThickness = 1;
		featuredPanel.CornerRadius = 24;
		categoryComboBox.Cursor = Cursors.Hand;
		categoryComboBox.DropDownHeight = 240;
		addGameButton.FlatAppearance.MouseDownBackColor = Color.FromArgb(28, 35, 46);
		addGameButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(24, 31, 42);
	}
}
