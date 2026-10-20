using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using app_desktop;
using app_desktop.Services;

namespace UI_Desktop.Store;

internal sealed class StoreGameDetailForm : Form
{
	private readonly record struct LayoutMetrics(bool IsCompact, int LeftColumnWidth, int RightColumnWidth);

	private sealed record SummaryCardRefs(RoundedPanel Card, Label CurrentPriceLabel, Label ReferencePriceLabel, Label DiscountLabel);

	private const int ContentGap = 18;

	private const int CompactBreakpoint = 1180;

	private const int HtmlCardMinHeight = 220;

	private const int HtmlCardCollapsedHeight = 620;

	private const int HtmlCardToggleGap = 12;

	private const int HtmlCardToggleThreshold = 56;

	private const int HtmlCardMaxMeasuredHeight = 12000;

	private const int HtmlHeightUpdateThreshold = 12;

	private const int HtmlHeightApplyDelay = 90;

	private readonly StorefrontApiClient storefrontApiClient;

	private readonly TableLayoutPanel rootLayout = new TableLayoutPanel();

	private readonly Panel headerPanel = new Panel();

	private readonly TableLayoutPanel headerLayout = new TableLayoutPanel();

	private readonly Button backButton = new Button();

	private readonly Label titleLabel = new Label();

	private readonly Label metaLabel = new Label();

	private readonly Label loadingLabel = new Label();

	private readonly StoreScrollPanel contentScrollPanel = new StoreScrollPanel();

	private readonly TableLayoutPanel contentLayout = new TableLayoutPanel();

	private readonly WebView2 detailWebView = new WebView2();

	private readonly TableLayoutPanel leftStack = CreateColumnStack();

	private readonly TableLayoutPanel rightStack = CreateColumnStack();

	private readonly Timer resizeRefreshTimer = new Timer
	{
		Interval = 120
	};

	private readonly StoreLoadingOverlay loadingOverlay;

	private Guid currentGameId;

	private bool hasLoadedInitialDetail;

	private bool isLoadingDetail;

	private bool isSubmittingReview;

	private int lastLayoutWidth = -1;

	private int? pendingDetailScrollY;

	private StoreGameDetailData? currentDetailData;

	private Task? detailWebViewInitializationTask;

	[Browsable(false)]
	[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
	internal int? UpdatedCartItemCount { get; private set; }

	internal event Action<int>? CartItemCountChanged;

	internal event Action? CartPageRequested;

	public StoreGameDetailForm(StorefrontApiClient storefrontApiClient, Guid gameId)
	{
		this.storefrontApiClient = storefrontApiClient;
		currentGameId = gameId;
		InitializeShell();
		loadingOverlay = new StoreLoadingOverlay(this);
	}

	protected override async void OnShown(EventArgs e)
	{
		base.OnShown(e);
		if (!hasLoadedInitialDetail)
		{
			hasLoadedInitialDetail = true;
			await LoadDetailAsync(currentGameId);
		}
	}

	protected override void OnFormClosed(FormClosedEventArgs e)
	{
		resizeRefreshTimer.Stop();
		loadingOverlay.Dispose();
		base.OnFormClosed(e);
	}

	internal Task ShowGameAsync(Guid gameId)
	{
		if (currentGameId == gameId && currentDetailData != null)
		{
			return Task.CompletedTask;
		}
		return LoadDetailAsync(gameId);
	}

	private void InitializeShell()
	{
		StoreUiHelper.ApplyWindowIcon(this);
		base.AutoScaleMode = AutoScaleMode.None;
		BackColor = AppTheme.WindowBackground;
		DoubleBuffered = true;
		base.ClientSize = new Size(1400, 860);
		MinimumSize = new Size(880, 620);
		base.FormBorderStyle = FormBorderStyle.None;
		Text = "Chi tiết game";
		rootLayout.Dock = DockStyle.Fill;
		rootLayout.BackColor = AppTheme.WindowBackground;
		rootLayout.ColumnCount = 1;
		rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
		rootLayout.RowCount = 2;
		rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 104f));
		rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
		headerPanel.BackColor = Color.FromArgb(16, 21, 29);
		headerPanel.Padding = new Padding(22, 14, 22, 14);
		headerPanel.Dock = DockStyle.Fill;
		headerLayout.Dock = DockStyle.Fill;
		headerLayout.ColumnCount = 3;
		headerLayout.RowCount = 1;
		headerLayout.ColumnStyles.Add(new ColumnStyle());
		headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
		headerLayout.ColumnStyles.Add(new ColumnStyle());
		headerLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
		ConfigureBackButton();
		TableLayoutPanel tableLayoutPanel = new TableLayoutPanel
		{
			Dock = DockStyle.Fill,
			ColumnCount = 1,
			RowCount = 2,
			Margin = new Padding(18, 0, 0, 0)
		};
		tableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
		tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42f));
		tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 26f));
		titleLabel.AutoSize = false;
		titleLabel.AutoEllipsis = true;
		titleLabel.Dock = DockStyle.Fill;
		titleLabel.Font = new Font("Segoe UI Semibold", 17f, FontStyle.Bold);
		titleLabel.ForeColor = AppTheme.PrimaryText;
		titleLabel.TextAlign = ContentAlignment.MiddleLeft;
		titleLabel.Text = "Đang tải chi tiết game...";
		metaLabel.AutoSize = false;
		metaLabel.Dock = DockStyle.Fill;
		metaLabel.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
		metaLabel.ForeColor = AppTheme.SecondaryText;
		metaLabel.TextAlign = ContentAlignment.MiddleLeft;
		metaLabel.Text = string.Empty;
		tableLayoutPanel.Controls.Add(titleLabel, 0, 0);
		tableLayoutPanel.Controls.Add(metaLabel, 0, 1);
		loadingLabel.AutoSize = true;
		loadingLabel.Anchor = AnchorStyles.Right;
		loadingLabel.Font = new Font("Segoe UI Semibold", 10f, FontStyle.Bold);
		loadingLabel.ForeColor = Color.FromArgb(123, 181, 255);
		loadingLabel.Text = "Đang tải";
		loadingLabel.Visible = false;
		headerLayout.Controls.Add(backButton, 0, 0);
		headerLayout.Controls.Add(tableLayoutPanel, 1, 0);
		headerLayout.Controls.Add(loadingLabel, 2, 0);
		headerPanel.Controls.Add(headerLayout);
		contentScrollPanel.Dock = DockStyle.Fill;
		contentScrollPanel.BackColor = AppTheme.WindowBackground;
		contentScrollPanel.AutoScroll = false;
		contentLayout.AutoSize = true;
		contentLayout.AutoSizeMode = AutoSizeMode.GrowAndShrink;
		contentLayout.Dock = DockStyle.Top;
		contentLayout.GrowStyle = TableLayoutPanelGrowStyle.FixedSize;
		contentLayout.BackColor = AppTheme.WindowBackground;
		contentLayout.Padding = new Padding(22, 20, 22, 22);
		contentLayout.Margin = Padding.Empty;
		contentLayout.Visible = false;
		detailWebView.Dock = DockStyle.Fill;
		detailWebView.Margin = Padding.Empty;
		detailWebView.DefaultBackgroundColor = AppTheme.WindowBackground;
		contentScrollPanel.Controls.Add(contentLayout);
		contentScrollPanel.Controls.Add(detailWebView);
		detailWebView.BringToFront();
		rootLayout.Controls.Add(headerPanel, 0, 0);
		rootLayout.Controls.Add(contentScrollPanel, 0, 1);
		base.Controls.Add(rootLayout);
		StoreUiHelper.EnableDoubleBuffer(contentScrollPanel);
		StoreUiHelper.EnableDoubleBuffer(contentLayout);
		StoreUiHelper.EnableDoubleBuffer(leftStack);
		StoreUiHelper.EnableDoubleBuffer(rightStack);
		resizeRefreshTimer.Tick += ResizeRefreshTimer_Tick;
		base.SizeChanged += delegate
		{
			QueueResponsiveRefresh();
		};
		contentScrollPanel.SizeChanged += delegate
		{
			QueueResponsiveRefresh();
		};
	}

	private void ConfigureBackButton()
	{
		backButton.AutoSize = false;
		backButton.FlatStyle = FlatStyle.Flat;
		backButton.FlatAppearance.BorderSize = 0;
		backButton.BackColor = AppTheme.CardSurface;
		backButton.ForeColor = AppTheme.PrimaryText;
		backButton.Font = new Font("Segoe UI Semibold", 10f, FontStyle.Bold);
		backButton.Padding = new Padding(10, 0, 10, 0);
		backButton.Size = new Size(118, 42);
		backButton.Text = "< Quay lại";
		backButton.Cursor = Cursors.Hand;
		backButton.Click += delegate
		{
			Close();
		};
	}

	private async Task LoadDetailAsync(Guid gameId)
	{
		if (isLoadingDetail)
		{
			return;
		}
		isLoadingDetail = true;
		currentGameId = gameId;
		base.UseWaitCursor = true;
		loadingLabel.Visible = false;
		string title = ((currentDetailData == null) ? "Đang mở chi tiết trò chơi" : "Đang cập nhật chi tiết trò chơi");
		string subtitle = ((currentDetailData == null) ? "Launcher đang tải thông tin, hình ảnh và các phiên bản của trò chơi này." : "Launcher đang cập nhật nội dung mới cho trò chơi bạn vừa chọn.");
		loadingOverlay.Show(title, subtitle, "Đang tải chi tiết trò chơi");
		try
		{
			_ = 1;
			try
			{
				string accessToken = await DesktopAppContext.AuthSessionManager.GetAccessTokenAsync();
				currentDetailData = StoreGameDetailData.FromApi(await storefrontApiClient.GetProductDetailAsync(gameId, accessToken));
				lastLayoutWidth = -1;
				RenderDetail(currentDetailData);
				int? num = pendingDetailScrollY;
				if (!num.HasValue)
				{
					contentScrollPanel.AutoScrollPosition = Point.Empty;
				}
			}
			catch (Exception ex)
			{
				LauncherMessageBox.Show("Không thể tải chi tiết game lúc này.\n\n" + ex.Message, "NestG Launcher", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
				if (!base.IsDisposed)
				{
					Close();
				}
			}
		}
		finally
		{
			base.UseWaitCursor = false;
			loadingLabel.Visible = false;
			isLoadingDetail = false;
			await loadingOverlay.HideAsync(280);
		}
	}

	private void RenderDetail(StoreGameDetailData data)
	{
		Text = data.Name + " - NestG Launcher";
		titleLabel.Text = data.Name;
		metaLabel.Text = BuildMetaText(data);
		lastLayoutWidth = contentScrollPanel.ClientSize.Width;
		RefreshDetailDocumentAsync(data);
	}

	private async Task RefreshDetailDocumentAsync(StoreGameDetailData data)
	{
		await EnsureDetailWebViewAsync();
		if (!detailWebView.IsDisposed)
		{
			bool isAuthenticated = !string.IsNullOrWhiteSpace(await DesktopAppContext.AuthSessionManager.GetAccessTokenAsync());
			detailWebView.NavigateToString(StoreGameDetailHtmlBuilder.BuildDocument(data, isAuthenticated, pendingDetailScrollY));
		}
	}

	private Task EnsureDetailWebViewAsync()
	{
		if (detailWebViewInitializationTask == null)
		{
			detailWebViewInitializationTask = EnsureDetailWebViewCoreAsync();
		}
		return detailWebViewInitializationTask;
	}

	private async Task EnsureDetailWebViewCoreAsync()
	{
		await detailWebView.EnsureCoreWebView2Async();
		if (!detailWebView.IsDisposed && detailWebView.CoreWebView2 != null)
		{
			CoreWebView2Settings settings = detailWebView.CoreWebView2.Settings;
			settings.AreDefaultContextMenusEnabled = false;
			settings.AreDevToolsEnabled = false;
			settings.AreBrowserAcceleratorKeysEnabled = false;
			settings.IsStatusBarEnabled = false;
			settings.IsZoomControlEnabled = false;
			detailWebView.CoreWebView2.WebMessageReceived -= DetailWebView_WebMessageReceived;
			detailWebView.CoreWebView2.WebMessageReceived += DetailWebView_WebMessageReceived;
			detailWebView.CoreWebView2.NavigationStarting -= DetailWebView_NavigationStarting;
			detailWebView.CoreWebView2.NavigationStarting += DetailWebView_NavigationStarting;
			detailWebView.CoreWebView2.NavigationCompleted -= DetailWebView_NavigationCompleted;
			detailWebView.CoreWebView2.NavigationCompleted += DetailWebView_NavigationCompleted;
		}
	}

	private async void DetailWebView_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
	{
		int? num = pendingDetailScrollY;
		if (num.HasValue)
		{
			int valueOrDefault = num.GetValueOrDefault();
			if (!detailWebView.IsDisposed && detailWebView.CoreWebView2 != null)
			{
				pendingDetailScrollY = null;
				string script = string.Format(CultureInfo.InvariantCulture, "(() => {{ const targetY = Math.max(0, {0}); requestAnimationFrame(() => window.scrollTo({{ top: targetY, left: 0 }})); }})();", valueOrDefault);
				await detailWebView.ExecuteScriptAsync(script);
			}
		}
	}

	private async void DetailWebView_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
	{
		string text = e.TryGetWebMessageAsString();
		if (string.IsNullOrWhiteSpace(text) || currentDetailData == null)
		{
			return;
		}
		if (text.StartsWith("gallery:", StringComparison.OrdinalIgnoreCase))
		{
			if (int.TryParse(text.Substring("gallery:".Length), out var result))
			{
				ShowGalleryLightbox(result);
			}
		}
		else if (text.StartsWith("game:", StringComparison.OrdinalIgnoreCase))
		{
			if (Guid.TryParse(text.Substring("game:".Length), out var result2))
			{
				await ShowGameAsync(result2);
			}
		}
		else if (text.StartsWith("open:", StringComparison.OrdinalIgnoreCase))
		{
			string stringToUnescape = text.Substring("open:".Length);
			OpenExternalUrl(Uri.UnescapeDataString(stringToUnescape));
		}
		else if (text.StartsWith("review:", StringComparison.OrdinalIgnoreCase))
		{
			await HandleReviewSubmitAsync(text);
		}
		else if (text.StartsWith("cart:", StringComparison.OrdinalIgnoreCase))
		{
			string[] array = text.Substring("cart:".Length).Split(':');
			int result3;
			int value = ((array.Length != 0 && int.TryParse(array[0], out result3)) ? result3 : GetActiveEditionIndex(currentDetailData));
			bool includeCheckoutHint = array.Length > 1 && (array[1] == "1" || array[1].Equals("true", StringComparison.OrdinalIgnoreCase));
			StoreProductEdition selectedEdition = ((currentDetailData.Editions.Count == 0) ? null : currentDetailData.Editions[Math.Clamp(value, 0, currentDetailData.Editions.Count - 1)]);
			await HandleAddToCartAsync(currentDetailData, selectedEdition, includeCheckoutHint);
		}
	}

	private void DetailWebView_NavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
	{
		if (e.IsUserInitiated && !e.Uri.StartsWith("about:blank", StringComparison.OrdinalIgnoreCase) && !e.Uri.StartsWith("data:text/html", StringComparison.OrdinalIgnoreCase))
		{
			e.Cancel = true;
			OpenExternalUrl(e.Uri);
		}
	}

	private void ShowGalleryLightbox(int startIndex)
	{
		if (currentDetailData == null)
		{
			return;
		}
		IReadOnlyList<StoreProductGalleryItem> gallery;
		if (currentDetailData.Gallery.Count <= 0)
		{
			IReadOnlyList<StoreProductGalleryItem> readOnlyList = new _003C_003Ez__ReadOnlySingleElementList<StoreProductGalleryItem>(new StoreProductGalleryItem
			{
				ThumbnailUrl = currentDetailData.CoverImageUrl,
				FullUrl = currentDetailData.CoverImageUrl,
				Alt = currentDetailData.Name,
				MainPosition = "center center"
			});
			gallery = readOnlyList;
		}
		else
		{
			gallery = currentDetailData.Gallery;
		}
		using StoreGalleryLightboxForm storeGalleryLightboxForm = new StoreGalleryLightboxForm(gallery, startIndex, currentDetailData.Name);
		storeGalleryLightboxForm.ShowDialog(this);
	}

	private static int GetActiveEditionIndex(StoreGameDetailData data)
	{
		for (int i = 0; i < data.Editions.Count; i++)
		{
			if (data.Editions[i].IsActive)
			{
				return i;
			}
		}
		return 0;
	}

	private LayoutMetrics ConfigureResponsiveShell()
	{
		int num = Math.Max(840, contentScrollPanel.ClientSize.Width - 4);
		bool num2 = num < 1180;
		int num3 = num - contentLayout.Padding.Horizontal;
		int num4 = (num2 ? num3 : ((int)Math.Floor((float)(num3 - 18) * 0.64f)));
		int num5 = (num2 ? num3 : (num3 - num4 - 18));
		contentLayout.SuspendLayout();
		leftStack.SuspendLayout();
		rightStack.SuspendLayout();
		contentLayout.Controls.Clear();
		contentLayout.ColumnStyles.Clear();
		contentLayout.RowStyles.Clear();
		contentLayout.Width = num;
		ConfigureColumnStack(leftStack, num4);
		ConfigureColumnStack(rightStack, num5);
		if (num2)
		{
			contentLayout.ColumnCount = 1;
			contentLayout.RowCount = 2;
			contentLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, num3));
			contentLayout.RowStyles.Add(new RowStyle());
			contentLayout.RowStyles.Add(new RowStyle());
			leftStack.Margin = Padding.Empty;
			rightStack.Margin = new Padding(0, 18, 0, 0);
			contentLayout.Controls.Add(leftStack, 0, 0);
			contentLayout.Controls.Add(rightStack, 0, 1);
		}
		else
		{
			contentLayout.ColumnCount = 2;
			contentLayout.RowCount = 1;
			contentLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, num4));
			contentLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, num5 + 18));
			contentLayout.RowStyles.Add(new RowStyle());
			leftStack.Margin = Padding.Empty;
			rightStack.Margin = new Padding(18, 0, 0, 0);
			contentLayout.Controls.Add(leftStack, 0, 0);
			contentLayout.Controls.Add(rightStack, 1, 0);
		}
		lastLayoutWidth = num;
		return new LayoutMetrics(num2, num4, num5);
	}

	private SummaryCardRefs BuildSummaryCard(StoreGameDetailData data, int cardWidth)
	{
		RoundedPanel roundedPanel = CreateCard(cardWidth);
		TableLayoutPanel stack = CreateCardStack(roundedPanel);
		int cardContentWidth = GetCardContentWidth(roundedPanel);
		int height = Math.Max(220, Math.Min(296, (int)((float)cardContentWidth * 0.58f)));
		PictureBox pictureBox = new PictureBox
		{
			Width = cardContentWidth,
			Height = height,
			Margin = Padding.Empty,
			BackColor = Color.FromArgb(10, 15, 22),
			SizeMode = PictureBoxSizeMode.Zoom
		};
		BindPictureAsync(pictureBox, data.CoverImageUrl, PictureBoxSizeMode.Zoom);
		AddCardControl(stack, pictureBox, 16);
		RoundedPanel roundedPanel2 = CreateSubCard(cardContentWidth, 98, new Padding(16, 14, 16, 14));
		roundedPanel2.CornerRadius = 20;
		roundedPanel2.BorderColor = Color.FromArgb(60, 79, 104);
		roundedPanel2.SurfaceColor = Color.FromArgb(18, 25, 35);
		RoundedPanel roundedPanel3 = new RoundedPanel
		{
			Size = new Size(84, 64),
			Location = new Point(16, 16),
			CornerRadius = 18,
			BorderThickness = 0,
			SurfaceColor = Color.FromArgb(29, 55, 103)
		};
		TableLayoutPanel tableLayoutPanel = new TableLayoutPanel
		{
			Dock = DockStyle.Fill,
			ColumnCount = 1,
			RowCount = 2,
			Margin = Padding.Empty,
			Padding = new Padding(0, 4, 0, 4)
		};
		tableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
		tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34f));
		tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
		Label control = new Label
		{
			Dock = DockStyle.Fill,
			AutoEllipsis = true,
			Text = data.AgeLetter,
			TextAlign = ContentAlignment.MiddleCenter,
			Font = new Font("Segoe UI Semibold", 15.5f, FontStyle.Bold),
			ForeColor = Color.White,
			Margin = Padding.Empty
		};
		Label control2 = new Label
		{
			Dock = DockStyle.Fill,
			AutoEllipsis = true,
			Text = data.AgeCaption,
			TextAlign = ContentAlignment.TopCenter,
			Font = new Font("Segoe UI", 7.9f, FontStyle.Bold),
			ForeColor = Color.FromArgb(210, 225, 255)
		};
		tableLayoutPanel.Controls.Add(control, 0, 0);
		tableLayoutPanel.Controls.Add(control2, 0, 1);
		roundedPanel3.Controls.Add(tableLayoutPanel);
		int num = roundedPanel3.Right + 18;
		int width = cardContentWidth - num - 16;
		Label label = CreateBlockLabel(data.StatusText, new Font("Segoe UI Semibold", 10.5f, FontStyle.Bold), AppTheme.PrimaryText, width);
		label.Location = new Point(num, 16);
		Label label2 = CreateBlockLabel(data.AgeDescription, new Font("Segoe UI", 8.9f, FontStyle.Regular), AppTheme.SecondaryText, width);
		label2.Location = new Point(num, label.Bottom + 4);
		roundedPanel2.Controls.Add(roundedPanel3);
		roundedPanel2.Controls.Add(label);
		roundedPanel2.Controls.Add(label2);
		AddCardControl(stack, roundedPanel2, 16);
		Label control3 = CreateParagraphLabel(data.ShortDescription ?? "Dữ liệu mô tả ngắn đang được đồng bộ từ storefront.", cardContentWidth, new Font("Segoe UI", 9.75f, FontStyle.Regular), AppTheme.SecondaryText);
		AddCardControl(stack, control3, 16);
		if (data.SummaryTags.Count > 0)
		{
			FlowLayoutPanel flowLayoutPanel = CreateWrappingFlow(cardContentWidth);
			foreach (string item in data.SummaryTags.Take(10))
			{
				flowLayoutPanel.Controls.Add(CreateTagChip(item));
			}
			AddCardControl(stack, flowLayoutPanel, 16);
		}
		FlowLayoutPanel flowLayoutPanel2 = new FlowLayoutPanel
		{
			AutoSize = true,
			AutoSizeMode = AutoSizeMode.GrowAndShrink,
			WrapContents = true,
			FlowDirection = FlowDirection.LeftToRight,
			Margin = Padding.Empty,
			Width = cardContentWidth,
			MaximumSize = new Size(cardContentWidth, 0)
		};
		Label label3 = new Label
		{
			AutoSize = true,
			Font = new Font("Segoe UI Semibold", 17f, FontStyle.Bold),
			ForeColor = Color.FromArgb(181, 244, 121),
			Text = data.CurrentPriceText,
			Margin = new Padding(0, 6, 12, 0)
		};
		Label label4 = new Label
		{
			AutoSize = true,
			Font = new Font("Segoe UI", 10.5f, FontStyle.Strikeout),
			ForeColor = AppTheme.MutedText,
			Text = (data.ReferencePriceText ?? string.Empty),
			Visible = !string.IsNullOrWhiteSpace(data.ReferencePriceText),
			Margin = new Padding(0, 13, 10, 0)
		};
		Label label5 = new Label
		{
			AutoSize = true,
			Font = new Font("Segoe UI Semibold", 10f, FontStyle.Bold),
			ForeColor = Color.FromArgb(73, 209, 164),
			Text = (data.DiscountText ?? string.Empty),
			Visible = !string.IsNullOrWhiteSpace(data.DiscountText),
			Margin = new Padding(0, 13, 0, 0)
		};
		flowLayoutPanel2.Controls.Add(label3);
		flowLayoutPanel2.Controls.Add(label4);
		flowLayoutPanel2.Controls.Add(label5);
		AddCardControl(stack, flowLayoutPanel2, 0);
		FinalizeCard(roundedPanel, stack);
		return new SummaryCardRefs(roundedPanel, label3, label4, label5);
	}

	private Control BuildEditionCard(StoreGameDetailData data, SummaryCardRefs summaryCard, int cardWidth)
	{
		RoundedPanel card = CreateCard(cardWidth);
		TableLayoutPanel stack = CreateCardStack(card);
		int contentWidth = GetCardContentWidth(card);
		AddCardControl(stack, CreateCardTitle("Chọn phiên bản", contentWidth), 14);
		FlowLayoutPanel flowLayoutPanel = CreateWrappingFlow(contentWidth);
		Label noteLabel = CreateParagraphLabel(data.EditionNote, contentWidth, new Font("Segoe UI", 9.5f), AppTheme.SecondaryText);
		TableLayoutPanel tableLayoutPanel = new TableLayoutPanel
		{
			Width = contentWidth,
			Height = 96,
			Margin = Padding.Empty,
			ColumnCount = 1,
			RowCount = 2
		};
		tableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
		tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 44f));
		tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 44f));
		Button button = CreateActionButton("Mua ngay", primary: true);
		Button button2 = CreateActionButton("Thêm vào giỏ hàng", primary: false);
		tableLayoutPanel.Controls.Add(button, 0, 0);
		tableLayoutPanel.Controls.Add(button2, 0, 1);
		StoreProductEdition selectedEdition = null;
		List<Button> editionButtons = new List<Button>();
		foreach (StoreProductEdition edition2 in data.Editions)
		{
			Button button3 = new Button
			{
				AutoSize = true,
				FlatStyle = FlatStyle.Flat,
				BackColor = Color.FromArgb(19, 25, 34),
				ForeColor = Color.White,
				Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold),
				Cursor = Cursors.Hand,
				Padding = new Padding(12, 7, 12, 7),
				Text = (string.IsNullOrWhiteSpace(edition2.PriceText) ? edition2.Name : (edition2.Name + " - " + edition2.PriceText)),
				Tag = edition2,
				Margin = new Padding(0, 0, 10, 10)
			};
			button3.FlatAppearance.BorderSize = 1;
			button3.FlatAppearance.BorderColor = AppTheme.CardBorder;
			button3.Click += delegate
			{
				ApplyEdition(edition2);
			};
			editionButtons.Add(button3);
			flowLayoutPanel.Controls.Add(button3);
		}
		button.Click += async delegate
		{
			await HandleAddToCartAsync(data, selectedEdition, includeCheckoutHint: true);
		};
		button2.Click += async delegate
		{
			await HandleAddToCartAsync(data, selectedEdition, includeCheckoutHint: false);
		};
		AddCardControl(stack, flowLayoutPanel, 14);
		AddCardControl(stack, noteLabel, 18);
		AddCardControl(stack, tableLayoutPanel, 0);
		ApplyEdition(data.GetActiveEdition() ?? data.Editions.First());
		FinalizeCard(card, stack);
		return card;
		void ApplyEdition(StoreProductEdition edition)
		{
			selectedEdition = edition;
			SetParagraphLabelText(noteLabel, edition.Note, contentWidth);
			summaryCard.CurrentPriceLabel.Text = edition.CurrentPriceText;
			summaryCard.ReferencePriceLabel.Text = edition.ReferencePriceText ?? string.Empty;
			summaryCard.ReferencePriceLabel.Visible = !string.IsNullOrWhiteSpace(edition.ReferencePriceText);
			summaryCard.DiscountLabel.Text = edition.DiscountText ?? string.Empty;
			summaryCard.DiscountLabel.Visible = !string.IsNullOrWhiteSpace(edition.DiscountText);
			foreach (Button item in editionButtons)
			{
				bool flag = item.Tag == edition;
				item.BackColor = (flag ? AppTheme.Accent : Color.FromArgb(19, 25, 34));
				item.ForeColor = Color.White;
				item.FlatAppearance.BorderColor = (flag ? AppTheme.Accent : AppTheme.CardBorder);
			}
			RelayoutCard(card, stack);
		}
	}

	private Control BuildFactsCard(StoreGameDetailData data, int cardWidth)
	{
		RoundedPanel roundedPanel = CreateCard(cardWidth);
		TableLayoutPanel stack = CreateCardStack(roundedPanel);
		int cardContentWidth = GetCardContentWidth(roundedPanel);
		AddCardControl(stack, CreateCardTitle("Thông tin nhanh", cardContentWidth), 14);
		int num = Math.Min(132, Math.Max(112, cardContentWidth / 3));
		int num2 = cardContentWidth - num;
		TableLayoutPanel tableLayoutPanel = new TableLayoutPanel
		{
			AutoSize = true,
			AutoSizeMode = AutoSizeMode.GrowAndShrink,
			Width = cardContentWidth,
			Margin = Padding.Empty,
			ColumnCount = 2
		};
		tableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, num));
		tableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, num2));
		foreach (StoreProductFact fact in data.Facts)
		{
			int row = tableLayoutPanel.RowCount++;
			tableLayoutPanel.RowStyles.Add(new RowStyle());
			Label label = CreateBlockLabel(fact.Label, new Font("Segoe UI", 9.25f, FontStyle.Regular), AppTheme.MutedText, num - 12);
			label.Margin = new Padding(0, 0, 12, 12);
			Label label2 = CreateBlockLabel(fact.Value, new Font("Segoe UI Semibold", 9.25f, FontStyle.Bold), AppTheme.PrimaryText, num2);
			label2.Margin = new Padding(0, 0, 0, 12);
			tableLayoutPanel.Controls.Add(label, 0, row);
			tableLayoutPanel.Controls.Add(label2, 1, row);
		}
		AddCardControl(stack, tableLayoutPanel, 0);
		FinalizeCard(roundedPanel, stack);
		return roundedPanel;
	}

	private Control BuildWebsiteCard(string website, int cardWidth)
	{
		RoundedPanel roundedPanel = CreateCard(cardWidth);
		TableLayoutPanel stack = CreateCardStack(roundedPanel);
		int cardContentWidth = GetCardContentWidth(roundedPanel);
		AddCardControl(stack, CreateCardTitle("Liên kết chính thức", cardContentWidth), 12);
		LinkLabel linkLabel = new LinkLabel
		{
			AutoSize = false,
			Width = cardContentWidth,
			Height = 24,
			Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold),
			LinkColor = Color.FromArgb(123, 181, 255),
			ActiveLinkColor = Color.FromArgb(181, 244, 121),
			VisitedLinkColor = Color.FromArgb(123, 181, 255),
			Text = website,
			AutoEllipsis = true,
			Margin = Padding.Empty
		};
		linkLabel.Click += delegate
		{
			OpenExternalUrl(website);
		};
		AddCardControl(stack, linkLabel, 0);
		FinalizeCard(roundedPanel, stack);
		return roundedPanel;
	}

	private Control BuildGalleryCard(StoreGameDetailData data, int cardWidth)
	{
		RoundedPanel roundedPanel = CreateCard(cardWidth);
		TableLayoutPanel stack = CreateCardStack(roundedPanel);
		int cardContentWidth = GetCardContentWidth(roundedPanel);
		AddCardControl(stack, CreateCardTitle("Gallery", cardContentWidth), 14);
		IReadOnlyList<StoreProductGalleryItem> readOnlyList2;
		if (data.Gallery.Count <= 0)
		{
			IReadOnlyList<StoreProductGalleryItem> readOnlyList = new _003C_003Ez__ReadOnlySingleElementList<StoreProductGalleryItem>(new StoreProductGalleryItem
			{
				ThumbnailUrl = data.CoverImageUrl,
				FullUrl = data.CoverImageUrl,
				Alt = data.Name,
				MainPosition = "center center"
			});
			readOnlyList2 = readOnlyList;
		}
		else
		{
			readOnlyList2 = data.Gallery;
		}
		IReadOnlyList<StoreProductGalleryItem> gallery = readOnlyList2;
		int height = Math.Max(280, Math.Min(520, (int)Math.Round((double)cardContentWidth * 9.0 / 16.0)));
		RoundedPanel mainImageHost = new RoundedPanel
		{
			Width = cardContentWidth,
			Height = height,
			Margin = Padding.Empty,
			Padding = Padding.Empty,
			CornerRadius = 18,
			BorderThickness = 1,
			BorderColor = Color.FromArgb(58, 68, 82),
			SurfaceColor = Color.FromArgb(16, 16, 16)
		};
		StoreGalleryImageView mainImage = new StoreGalleryImageView
		{
			Dock = DockStyle.Fill,
			Cursor = Cursors.Hand,
			FitMode = StoreGalleryImageFit.Cover,
			ObjectPosition = "center center",
			SurfaceColor = Color.FromArgb(16, 16, 16)
		};
		StoreGalleryGlyphButton prevButton = CreateGalleryButton("‹", new Size(44, 80), 12, 24f);
		StoreGalleryGlyphButton nextButton = CreateGalleryButton("›", new Size(44, 80), 12, 24f);
		StoreGalleryGlyphButton expandButton = CreateGalleryButton("⛶", new Size(52, 52), 26, 16f);
		mainImageHost.SizeChanged += delegate
		{
			LayoutMainImageChrome();
		};
		prevButton.Visible = gallery.Count > 1;
		nextButton.Visible = gallery.Count > 1;
		mainImageHost.Controls.Add(mainImage);
		mainImageHost.Controls.Add(prevButton);
		mainImageHost.Controls.Add(nextButton);
		mainImageHost.Controls.Add(expandButton);
		LayoutMainImageChrome();
		AddCardControl(stack, mainImageHost, (gallery.Count > 1) ? 14 : 0);
		StoreScrollPanel thumbViewport = null;
		Panel thumbTrack = null;
		List<RoundedPanel> thumbnailPanels = new List<RoundedPanel>();
		List<StoreGalleryImageView> thumbnailImages = new List<StoreGalleryImageView>();
		int activeIndex = 0;
		int imageRequestId = 0;
		expandButton.Click += delegate
		{
			OpenGalleryLightbox();
		};
		mainImage.Click += delegate
		{
			OpenGalleryLightbox();
		};
		prevButton.Click += async delegate
		{
			await ApplyGalleryAsync(activeIndex - 1);
		};
		nextButton.Click += async delegate
		{
			await ApplyGalleryAsync(activeIndex + 1);
		};
		Panel thumbShell;
		StoreGalleryGlyphButton thumbPrevButton;
		StoreGalleryGlyphButton thumbNextButton;
		if (gallery.Count > 1)
		{
			int num = ((cardContentWidth >= 920) ? 146 : ((cardContentWidth >= 720) ? 132 : 118));
			int height2 = (int)Math.Round((double)num * 9.0 / 16.0);
			thumbShell = new Panel
			{
				Width = cardContentWidth,
				Height = height2,
				Margin = Padding.Empty,
				Padding = Padding.Empty,
				BackColor = Color.Transparent
			};
			thumbViewport = new StoreScrollPanel
			{
				Location = Point.Empty,
				Width = cardContentWidth,
				Height = height2,
				Margin = Padding.Empty,
				Padding = Padding.Empty,
				BackColor = Color.Transparent
			};
			thumbTrack = new Panel
			{
				Location = Point.Empty,
				Width = gallery.Count * num + (gallery.Count - 1) * 12,
				Height = height2,
				Margin = Padding.Empty,
				Padding = Padding.Empty,
				BackColor = Color.Transparent
			};
			thumbPrevButton = CreateGalleryButton("‹", new Size(34, 54), 12, 20f);
			thumbNextButton = CreateGalleryButton("›", new Size(34, 54), 12, 20f);
			thumbViewport.Controls.Add(thumbTrack);
			thumbViewport.AutoScrollMinSize = new Size(thumbTrack.Width, height2);
			thumbShell.Controls.Add(thumbViewport);
			thumbShell.Controls.Add(thumbPrevButton);
			thumbShell.Controls.Add(thumbNextButton);
			thumbPrevButton.BringToFront();
			thumbNextButton.BringToFront();
			thumbShell.SizeChanged += delegate
			{
				LayoutThumbChrome();
			};
			LayoutThumbChrome();
			foreach (var item3 in gallery.Select((StoreProductGalleryItem item, int itemIndex) => (item: item, itemIndex: itemIndex)))
			{
				StoreProductGalleryItem item2 = item3.item;
				int index2 = item3.itemIndex;
				RoundedPanel roundedPanel2 = new RoundedPanel
				{
					Width = num,
					Height = height2,
					Location = new Point(index2 * (num + 12), 0),
					CornerRadius = 10,
					BorderThickness = 1,
					BorderColor = Color.FromArgb(72, 82, 95),
					SurfaceColor = Color.FromArgb(19, 23, 30),
					Cursor = Cursors.Hand,
					Margin = Padding.Empty,
					Padding = Padding.Empty
				};
				StoreGalleryImageView storeGalleryImageView = new StoreGalleryImageView
				{
					Dock = DockStyle.Fill,
					Cursor = Cursors.Hand,
					FitMode = StoreGalleryImageFit.Cover,
					ObjectPosition = item2.MainPosition,
					SurfaceColor = Color.FromArgb(16, 16, 16)
				};
				roundedPanel2.Controls.Add(storeGalleryImageView);
				BindGalleryImageAsync(storeGalleryImageView, item2.ThumbnailUrl, item2.MainPosition, StoreGalleryImageFit.Cover);
				roundedPanel2.Click += async delegate
				{
					await ApplyGalleryAsync(index2);
				};
				storeGalleryImageView.Click += async delegate
				{
					await ApplyGalleryAsync(index2);
				};
				thumbnailPanels.Add(roundedPanel2);
				thumbnailImages.Add(storeGalleryImageView);
				thumbTrack.Controls.Add(roundedPanel2);
			}
			thumbPrevButton.Click += async delegate
			{
				await ApplyGalleryAsync(activeIndex - 1);
			};
			thumbNextButton.Click += async delegate
			{
				await ApplyGalleryAsync(activeIndex + 1);
			};
			AddCardControl(stack, thumbShell, 0);
		}
		FinalizeCard(roundedPanel, stack);
		ApplyGalleryAsync(0);
		return roundedPanel;
		async Task ApplyGalleryAsync(int index)
		{
			activeIndex = (index + gallery.Count) % gallery.Count;
			ApplyThumbChrome();
			ScrollActiveThumbIntoView();
			StoreProductGalleryItem currentImage = gallery[activeIndex];
			int requestId = ++imageRequestId;
			Image image = await StoreRemoteImageCache.GetAsync(currentImage.FullUrl);
			if (!base.IsDisposed && !mainImage.IsDisposed && requestId == imageRequestId && image != null)
			{
				mainImage.DisplayedImage = image;
				mainImage.ObjectPosition = currentImage.MainPosition;
			}
		}
		void ApplyThumbChrome()
		{
			for (int i = 0; i < thumbnailPanels.Count; i++)
			{
				bool flag = i == activeIndex;
				thumbnailPanels[i].BorderColor = (flag ? Color.FromArgb(173, 185, 203) : Color.FromArgb(72, 82, 95));
				thumbnailPanels[i].SurfaceColor = (flag ? Color.FromArgb(28, 32, 38) : Color.FromArgb(19, 23, 30));
				thumbnailImages[i].OverlayColor = (flag ? Color.Transparent : Color.FromArgb(138, 255, 255, 255));
			}
		}
		void LayoutMainImageChrome()
		{
			int y = Math.Max(12, (mainImageHost.Height - prevButton.Height) / 2);
			prevButton.Location = new Point(12, y);
			nextButton.Location = new Point(Math.Max(12, mainImageHost.Width - nextButton.Width - 12), y);
			expandButton.Location = new Point(14, Math.Max(14, mainImageHost.Height - expandButton.Height - 14));
		}
		void LayoutThumbChrome()
		{
			if (thumbViewport != null)
			{
				thumbViewport.Width = thumbShell.ClientSize.Width;
				thumbNextButton.Location = new Point(Math.Max(8, thumbShell.ClientSize.Width - thumbNextButton.Width - 8), Math.Max(0, (thumbShell.ClientSize.Height - thumbNextButton.Height) / 2));
				thumbPrevButton.Location = new Point(8, thumbNextButton.Top);
			}
		}
		void OpenGalleryLightbox()
		{
			using StoreGalleryLightboxForm storeGalleryLightboxForm = new StoreGalleryLightboxForm(gallery, activeIndex, data.Name);
			storeGalleryLightboxForm.ShowDialog(this);
			ApplyGalleryAsync(storeGalleryLightboxForm.SelectedIndex);
		}
		void ScrollActiveThumbIntoView()
		{
			if (thumbViewport != null && thumbTrack != null && thumbnailPanels.Count != 0)
			{
				RoundedPanel roundedPanel3 = thumbnailPanels[activeIndex];
				int num2 = Math.Max(1, thumbViewport.ClientSize.Width);
				int val = Math.Max(0, thumbTrack.Width - num2);
				int val2 = roundedPanel3.Left - Math.Max(0, (num2 - roundedPanel3.Width) / 2);
				int x = Math.Max(0, Math.Min(val, val2));
				thumbViewport.AutoScrollPosition = new Point(x, 0);
			}
		}
	}

	private Control BuildBulletListCard(string titleText, IReadOnlyList<string> items, int cardWidth)
	{
		RoundedPanel roundedPanel = CreateCard(cardWidth);
		TableLayoutPanel stack = CreateCardStack(roundedPanel);
		int cardContentWidth = GetCardContentWidth(roundedPanel);
		AddCardControl(stack, CreateCardTitle(titleText, cardContentWidth), 14);
		TableLayoutPanel tableLayoutPanel = new TableLayoutPanel
		{
			AutoSize = true,
			AutoSizeMode = AutoSizeMode.GrowAndShrink,
			Width = cardContentWidth,
			Margin = Padding.Empty,
			ColumnCount = 1
		};
		tableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, cardContentWidth));
		foreach (string item in items)
		{
			int row = tableLayoutPanel.RowCount++;
			tableLayoutPanel.RowStyles.Add(new RowStyle());
			Control control = CreateBulletRow(item, cardContentWidth);
			control.Margin = new Padding(0, 0, 0, 10);
			tableLayoutPanel.Controls.Add(control, 0, row);
		}
		AddCardControl(stack, tableLayoutPanel, 0);
		FinalizeCard(roundedPanel, stack);
		return roundedPanel;
	}

	private Control BuildTextCard(string titleText, string text, int cardWidth)
	{
		RoundedPanel roundedPanel = CreateCard(cardWidth);
		TableLayoutPanel stack = CreateCardStack(roundedPanel);
		int cardContentWidth = GetCardContentWidth(roundedPanel);
		AddCardControl(stack, CreateCardTitle(titleText, cardContentWidth), 14);
		AddCardControl(stack, CreateParagraphLabel(text, cardContentWidth, new Font("Segoe UI", 9.75f, FontStyle.Regular), AppTheme.SecondaryText), 0);
		FinalizeCard(roundedPanel, stack);
		return roundedPanel;
	}

	private Control BuildHtmlCard(string titleText, string? html, string? fallbackText, int cardWidth, bool preferEmbeddedHtml = false)
	{
		if (string.IsNullOrWhiteSpace(html))
		{
			return BuildTextCard(titleText, fallbackText ?? "Nội dung đang được cập nhật.", cardWidth);
		}
		string text = fallbackText ?? StoreHtmlText.ToPlainText(html) ?? "Nội dung đang được cập nhật.";
		if (!ShouldUseEmbeddedHtml(html, preferEmbeddedHtml))
		{
			return BuildTextCard(titleText, text, cardWidth);
		}
		RoundedPanel roundedPanel = CreateCard(cardWidth);
		TableLayoutPanel stack = CreateCardStack(roundedPanel);
		int cardContentWidth = GetCardContentWidth(roundedPanel);
		AddCardControl(stack, CreateCardTitle(titleText, cardContentWidth), 14);
		AddCardControl(stack, CreateHtmlContentHost(html, text, cardContentWidth, roundedPanel, stack), 0);
		FinalizeCard(roundedPanel, stack);
		return roundedPanel;
	}

	private Control CreateHtmlContentHost(string html, string? fallbackText, int contentWidth, RoundedPanel card, TableLayoutPanel stack)
	{
		Panel container = new Panel
		{
			Width = contentWidth,
			Height = 220,
			Margin = Padding.Empty,
			Padding = Padding.Empty,
			BackColor = Color.Transparent
		};
		Panel host = new Panel
		{
			Width = contentWidth,
			Height = 220,
			Margin = Padding.Empty,
			Padding = Padding.Empty,
			BackColor = Color.FromArgb(18, 25, 35)
		};
		Button toggleButton = CreateHtmlToggleButton();
		toggleButton.Location = new Point(0, host.Bottom + 12);
		WebView2 webView = new WebView2
		{
			Dock = DockStyle.Fill,
			Margin = Padding.Empty,
			DefaultBackgroundColor = Color.FromArgb(18, 25, 35)
		};
		int measuredContentHeight = 220;
		int pendingMeasuredHeight = 220;
		bool isExpanded = true;
		Timer heightApplyTimer = new Timer
		{
			Interval = 90
		};
		heightApplyTimer.Tick += delegate
		{
			heightApplyTimer.Stop();
			if (!container.IsDisposed && !host.IsDisposed && Math.Abs(pendingMeasuredHeight - measuredContentHeight) >= 12)
			{
				measuredContentHeight = pendingMeasuredHeight;
				ApplyHtmlContentState();
			}
		};
		container.Disposed += delegate
		{
			heightApplyTimer.Stop();
			heightApplyTimer.Dispose();
		};
		toggleButton.Click += delegate
		{
			isExpanded = !isExpanded;
			ApplyHtmlContentState(force: true);
		};
		host.Controls.Add(webView);
		container.Controls.Add(host);
		container.Controls.Add(toggleButton);
		ApplyHtmlContentState(force: true);
		InitializeHtmlCardAsync(webView, host, html, fallbackText, QueueMeasuredHeight);
		return container;
		void ApplyHtmlContentState(bool force = false)
		{
			if (!container.IsDisposed && !host.IsDisposed && !toggleButton.IsDisposed)
			{
				bool flag = measuredContentHeight > 676;
				int val = ((flag && !isExpanded) ? 620 : measuredContentHeight);
				val = Math.Max(220, Math.Min(12000, val));
				string text = (isExpanded ? "Ẩn bớt" : "Xem thêm");
				int num = val + (flag ? (toggleButton.Height + 12) : 0);
				if (force || host.Height != val || toggleButton.Visible != flag || !string.Equals(toggleButton.Text, text, StringComparison.Ordinal) || container.Height != num)
				{
					host.Height = val;
					toggleButton.Visible = flag;
					toggleButton.Text = text;
					RelayoutHtmlContentSection(container, host, toggleButton, card, stack);
				}
			}
		}
		void QueueMeasuredHeight(int measuredHeight)
		{
			int num2 = Math.Max(220, Math.Min(12000, measuredHeight + 8));
			if (Math.Abs(num2 - pendingMeasuredHeight) >= 12 || Math.Abs(num2 - measuredContentHeight) >= 12)
			{
				pendingMeasuredHeight = num2;
				heightApplyTimer.Stop();
				heightApplyTimer.Start();
			}
		}
	}

	private async Task InitializeHtmlCardAsync(WebView2 webView, Panel host, string html, string? fallbackText, Action<int> applyMeasuredHeight)
	{
		try
		{
			await webView.EnsureCoreWebView2Async();
			if (webView.IsDisposed || host.IsDisposed)
			{
				return;
			}
			webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
			webView.CoreWebView2.Settings.AreDevToolsEnabled = false;
			webView.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = false;
			webView.CoreWebView2.Settings.IsStatusBarEnabled = false;
			webView.CoreWebView2.Settings.IsZoomControlEnabled = false;
			webView.CoreWebView2.WebMessageReceived += delegate(object? _, CoreWebView2WebMessageReceivedEventArgs args)
			{
				if (!host.IsDisposed && !webView.IsDisposed)
				{
					HandleHtmlWebMessage(args.TryGetWebMessageAsString(), applyMeasuredHeight);
				}
			};
			webView.CoreWebView2.NavigationStarting += delegate(object? _, CoreWebView2NavigationStartingEventArgs args)
			{
				if (args.IsUserInitiated && !args.Uri.StartsWith("about:blank", StringComparison.OrdinalIgnoreCase) && !args.Uri.StartsWith("data:text/html", StringComparison.OrdinalIgnoreCase))
				{
					args.Cancel = true;
					OpenExternalUrl(args.Uri);
				}
			};
			webView.NavigateToString(BuildWebViewHtmlDocument(html));
		}
		catch
		{
			ShowHtmlFallback(host, fallbackText ?? StoreHtmlText.ToPlainText(html) ?? "Nội dung đang được cập nhật.", applyMeasuredHeight);
		}
	}

	private void HandleHtmlWebMessage(string? message, Action<int> applyMeasuredHeight)
	{
		if (string.IsNullOrWhiteSpace(message))
		{
			return;
		}
		int result3;
		if (message.StartsWith("scroll:", StringComparison.OrdinalIgnoreCase))
		{
			if (int.TryParse(message.Substring("scroll:".Length), out var result))
			{
				ScrollDetailContent(result);
			}
		}
		else if (message.StartsWith("height:", StringComparison.OrdinalIgnoreCase))
		{
			if (int.TryParse(message.Substring("height:".Length), out var result2))
			{
				applyMeasuredHeight(result2);
			}
		}
		else if (int.TryParse(message, out result3))
		{
			applyMeasuredHeight(result3);
		}
	}

	private void ScrollDetailContent(int scrollDelta)
	{
		if (scrollDelta != 0 && !contentScrollPanel.IsDisposed && contentScrollPanel.IsHandleCreated)
		{
			contentScrollPanel.ScrollByPixels(Math.Clamp(scrollDelta, -220, 220));
		}
	}

	private void ShowHtmlFallback(Panel host, string text, Action<int> applyMeasuredHeight)
	{
		if (!host.IsDisposed)
		{
			host.Controls.Clear();
			host.BackColor = Color.Transparent;
			Label label = CreateParagraphLabel(text, host.Width, new Font("Segoe UI", 9.75f, FontStyle.Regular), AppTheme.SecondaryText);
			label.Location = Point.Empty;
			host.Controls.Add(label);
			applyMeasuredHeight(label.Bottom);
		}
	}

	private static string BuildWebViewHtmlDocument(string html)
	{
		string value = ColorTranslator.ToHtml(Color.FromArgb(18, 25, 35));
		string value2 = ColorTranslator.ToHtml(Color.FromArgb(10, 15, 22));
		string value3 = ColorTranslator.ToHtml(AppTheme.PrimaryText);
		string value4 = ColorTranslator.ToHtml(AppTheme.SecondaryText);
		string value5 = ColorTranslator.ToHtml(AppTheme.MutedText);
		string value6 = ColorTranslator.ToHtml(AppTheme.Accent);
		string value7 = ColorTranslator.ToHtml(Color.FromArgb(123, 181, 255));
		string value8 = ColorTranslator.ToHtml(Color.FromArgb(15, 22, 32));
		string value9 = ColorTranslator.ToHtml(Color.FromArgb(44, 57, 74));
		return $"<!DOCTYPE html>\r\n<html lang=\"vi\">\r\n<head>\r\n<meta charset=\"utf-8\" />\r\n<meta http-equiv=\"X-UA-Compatible\" content=\"IE=edge\" />\r\n<meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\" />\r\n<style>\r\n:root {{\r\n    color-scheme: dark;\r\n}}\r\nhtml, body {{\r\n    margin: 0;\r\n    padding: 0;\r\n    background: {value};\r\n    color: {value4};\r\n    overflow: hidden;\r\n    font-family: \"Segoe UI\", sans-serif;\r\n}}\r\nbody {{\r\n    padding: 4px 0 0;\r\n}}\r\n.storefront-html-content,\r\n.storefront-html-content * {{\r\n    box-sizing: border-box;\r\n    max-width: 100%;\r\n}}\r\n.storefront-html-content {{\r\n    font-size: 15px;\r\n    line-height: 1.75;\r\n    color: {value4};\r\n}}\r\n.storefront-html-content h1,\r\n.storefront-html-content h2,\r\n.storefront-html-content h3,\r\n.storefront-html-content h4,\r\n.storefront-html-content h5,\r\n.storefront-html-content h6 {{\r\n    margin: 0 0 14px;\r\n    color: {value3};\r\n    line-height: 1.28;\r\n    font-weight: 700;\r\n}}\r\n.storefront-html-content h1 {{ font-size: 28px; }}\r\n.storefront-html-content h2 {{ font-size: 24px; }}\r\n.storefront-html-content h3 {{ font-size: 20px; }}\r\n.storefront-html-content h4,\r\n.storefront-html-content h5,\r\n.storefront-html-content h6 {{ font-size: 17px; }}\r\n.storefront-html-content p,\r\n.storefront-html-content ul,\r\n.storefront-html-content ol,\r\n.storefront-html-content table,\r\n.storefront-html-content blockquote {{\r\n    margin: 0 0 16px;\r\n}}\r\n.storefront-html-content ul,\r\n.storefront-html-content ol {{\r\n    padding-left: 22px;\r\n}}\r\n.storefront-html-content li + li {{\r\n    margin-top: 8px;\r\n}}\r\n.storefront-html-content strong,\r\n.storefront-html-content b {{\r\n    color: {value3};\r\n}}\r\n.storefront-html-content em {{\r\n    color: {value5};\r\n}}\r\n.storefront-html-content img {{\r\n    display: block;\r\n    width: 100%;\r\n    height: auto;\r\n    margin: 16px 0;\r\n    border-radius: 18px;\r\n    background: {value2};\r\n    box-shadow: 0 18px 40px rgba(0, 0, 0, 0.22);\r\n}}\r\n.storefront-html-content video,\r\n.storefront-html-content iframe {{\r\n    display: block;\r\n    width: 100%;\r\n    min-height: 300px;\r\n    margin: 18px 0;\r\n    border: 0;\r\n    border-radius: 18px;\r\n    background: {value2};\r\n    box-shadow: 0 18px 40px rgba(0, 0, 0, 0.22);\r\n    aspect-ratio: 16 / 9;\r\n}}\r\n.storefront-html-content table {{\r\n    width: 100%;\r\n    border-collapse: collapse;\r\n    overflow: hidden;\r\n    border-radius: 16px;\r\n    background: {value8};\r\n}}\r\n.storefront-html-content table th,\r\n.storefront-html-content table td {{\r\n    padding: 12px 14px;\r\n    border: 1px solid {value9};\r\n    text-align: left;\r\n    vertical-align: top;\r\n}}\r\n.storefront-html-content blockquote {{\r\n    padding: 14px 18px;\r\n    border-left: 4px solid {value6};\r\n    border-radius: 16px;\r\n    background: {value8};\r\n}}\r\n.storefront-html-content hr {{\r\n    margin: 18px 0;\r\n    border: 0;\r\n    border-top: 1px solid {value9};\r\n}}\r\n.storefront-html-content a {{\r\n    color: {value7};\r\n    text-decoration: none;\r\n}}\r\n.storefront-html-content a:hover {{\r\n    text-decoration: underline;\r\n}}\r\n::-webkit-scrollbar {{\r\n    width: 0;\r\n    height: 0;\r\n}}\r\n</style>\r\n</head>\r\n<body>\r\n<div class=\"storefront-html-content\">\r\n{html}\r\n</div>\r\n<script>\r\n(() => {{\r\n    const root = document.querySelector('.storefront-html-content');\r\n    let frameHandle = 0;\r\n    let lastPostedHeight = 0;\r\n    let wheelAccumulator = 0;\r\n    let wheelFrameHandle = 0;\r\n\r\n    const readHeight = () => {{\r\n        const contentHeight = root ? Math.ceil(root.getBoundingClientRect().height) : 0;\r\n        const bodyRectHeight = Math.ceil(document.body.getBoundingClientRect().height);\r\n        const documentRectHeight = Math.ceil(document.documentElement.getBoundingClientRect().height);\r\n\r\n        return Math.max(\r\n            contentHeight,\r\n            bodyRectHeight,\r\n            documentRectHeight,\r\n            document.body.scrollHeight,\r\n            document.documentElement.scrollHeight,\r\n            document.body.offsetHeight,\r\n            document.documentElement.offsetHeight\r\n        );\r\n    }};\r\n\r\n    const postHeight = (force = false) => {{\r\n        const value = readHeight();\r\n        if (!force && Math.abs(value - lastPostedHeight) < 12) {{\r\n            return;\r\n        }}\r\n\r\n        lastPostedHeight = value;\r\n\r\n        if (window.chrome && window.chrome.webview) {{\r\n            window.chrome.webview.postMessage(`height:${{Math.ceil(value)}}`);\r\n        }}\r\n    }};\r\n\r\n    const schedulePostHeight = (force = false) => {{\r\n        if (frameHandle) {{\r\n            cancelAnimationFrame(frameHandle);\r\n        }}\r\n\r\n        frameHandle = requestAnimationFrame(() => postHeight(force));\r\n    }};\r\n\r\n    const observer = new ResizeObserver(schedulePostHeight);\r\n    observer.observe(document.body);\r\n    observer.observe(document.documentElement);\r\n\r\n    if (root) {{\r\n        observer.observe(root);\r\n    }}\r\n\r\n    window.addEventListener('load', () => {{\r\n        postHeight(true);\r\n        setTimeout(postHeight, 150);\r\n        setTimeout(postHeight, 700);\r\n        setTimeout(postHeight, 1500);\r\n        setTimeout(postHeight, 2600);\r\n        setTimeout(postHeight, 4200);\r\n    }});\r\n\r\n    window.addEventListener('resize', schedulePostHeight);\r\n\r\n    document.querySelectorAll('img').forEach((image) => {{\r\n        if (!image.complete) {{\r\n            image.addEventListener('load', schedulePostHeight);\r\n            image.addEventListener('error', schedulePostHeight);\r\n        }}\r\n    }});\r\n\r\n    document.querySelectorAll('video').forEach((video) => {{\r\n        video.setAttribute('controls', 'controls');\r\n        video.addEventListener('loadedmetadata', schedulePostHeight);\r\n        video.addEventListener('loadeddata', schedulePostHeight);\r\n        video.addEventListener('durationchange', schedulePostHeight);\r\n        video.addEventListener('canplay', schedulePostHeight);\r\n    }});\r\n\r\n    document.querySelectorAll('iframe').forEach((frame) => {{\r\n        frame.addEventListener('load', () => {{\r\n            schedulePostHeight();\r\n            setTimeout(postHeight, 250);\r\n            setTimeout(postHeight, 900);\r\n        }});\r\n    }});\r\n\r\n    if (document.fonts && document.fonts.ready) {{\r\n        document.fonts.ready.then(postHeight).catch(() => {{}});\r\n    }}\r\n\r\n    window.addEventListener('wheel', (event) => {{\r\n        if (!window.chrome || !window.chrome.webview || Math.abs(event.deltaY) < 0.5) {{\r\n            return;\r\n        }}\r\n\r\n        event.preventDefault();\r\n        wheelAccumulator += event.deltaY;\r\n\r\n        if (wheelFrameHandle) {{\r\n            return;\r\n        }}\r\n\r\n        wheelFrameHandle = requestAnimationFrame(() => {{\r\n            const delta = Math.round(wheelAccumulator);\r\n            wheelAccumulator = 0;\r\n            wheelFrameHandle = 0;\r\n\r\n            if (delta !== 0) {{\r\n                window.chrome.webview.postMessage(`scroll:${{delta}}`);\r\n            }}\r\n        }});\r\n    }}, {{ passive: false }});\r\n\r\n    postHeight(true);\r\n}})();\r\n</script>\r\n</body>\r\n</html>";
	}

	private Control BuildRequirementsCard(StoreGameDetailData data, int cardWidth)
	{
		RoundedPanel roundedPanel = CreateCard(cardWidth);
		TableLayoutPanel stack = CreateCardStack(roundedPanel);
		int cardContentWidth = GetCardContentWidth(roundedPanel);
		AddCardControl(stack, CreateCardTitle("Cấu hình hệ thống", cardContentWidth), 14);
		foreach (StoreProductRequirement requirement in data.Requirements)
		{
			RoundedPanel roundedPanel2 = CreateSubCard(cardContentWidth, 0, new Padding(16, 14, 16, 14));
			roundedPanel2.CornerRadius = 18;
			roundedPanel2.BorderColor = Color.FromArgb(59, 77, 101);
			roundedPanel2.SurfaceColor = Color.FromArgb(18, 25, 35);
			Label label = CreateBlockLabel(requirement.Platform, new Font("Segoe UI Semibold", 10.25f, FontStyle.Bold), AppTheme.PrimaryText, cardContentWidth - roundedPanel2.Padding.Horizontal);
			label.Location = new Point(roundedPanel2.Padding.Left, roundedPanel2.Padding.Top);
			Label label2 = CreateParagraphLabel(requirement.Text, cardContentWidth - roundedPanel2.Padding.Horizontal, new Font("Segoe UI", 9.4f, FontStyle.Regular), AppTheme.SecondaryText);
			label2.Location = new Point(roundedPanel2.Padding.Left, label.Bottom + 10);
			roundedPanel2.Controls.Add(label);
			roundedPanel2.Controls.Add(label2);
			roundedPanel2.Height = label2.Bottom + roundedPanel2.Padding.Bottom;
			AddCardControl(stack, roundedPanel2, 12);
		}
		FinalizeCard(roundedPanel, stack);
		return roundedPanel;
	}

	private Control BuildCustomerReviewsCard(StoreGameDetailData data, int cardWidth)
	{
		RoundedPanel roundedPanel = CreateCard(cardWidth);
		TableLayoutPanel stack = CreateCardStack(roundedPanel);
		int cardContentWidth = GetCardContentWidth(roundedPanel);
		AddCardControl(stack, CreateCardTitle("Đánh giá từ khách hàng", cardContentWidth), 14);
		if (data.CustomerReviews.Count == 0)
		{
			AddCardControl(stack, CreateParagraphLabel("Chưa có review nào trong hệ thống cho sản phẩm này.", cardContentWidth, new Font("Segoe UI", 10f, FontStyle.Regular), AppTheme.SecondaryText), 0);
			FinalizeCard(roundedPanel, stack);
			return roundedPanel;
		}
		foreach (StoreCustomerReview customerReview in data.CustomerReviews)
		{
			AddCardControl(stack, CreateCustomerReviewCard(customerReview, cardContentWidth), 12);
		}
		FinalizeCard(roundedPanel, stack);
		return roundedPanel;
	}

	private Control BuildRelatedProductsCard(StoreGameDetailData data, int cardWidth)
	{
		RoundedPanel roundedPanel = CreateCard(cardWidth);
		TableLayoutPanel stack = CreateCardStack(roundedPanel);
		int cardContentWidth = GetCardContentWidth(roundedPanel);
		Panel headerPanel = new Panel
		{
			Width = cardContentWidth,
			Height = 42,
			Margin = Padding.Empty
		};
		Label titleLabel = CreateCardTitle("Sản phẩm tương tự", Math.Max(120, cardContentWidth - 112));
		titleLabel.Location = new Point(0, 6);
		FlowLayoutPanel navFlow = new FlowLayoutPanel
		{
			AutoSize = true,
			AutoSizeMode = AutoSizeMode.GrowAndShrink,
			WrapContents = false,
			FlowDirection = FlowDirection.LeftToRight,
			Margin = Padding.Empty,
			Padding = Padding.Empty
		};
		Button prevButton = CreateRailNavButton("<");
		Button nextButton = CreateRailNavButton(">");
		navFlow.Controls.Add(prevButton);
		navFlow.Controls.Add(nextButton);
		headerPanel.SizeChanged += delegate
		{
			LayoutHeader();
		};
		headerPanel.Controls.Add(titleLabel);
		headerPanel.Controls.Add(navFlow);
		LayoutHeader();
		AddCardControl(stack, headerPanel, 12);
		int tileGap = 16;
		int num = ((cardContentWidth >= 1020) ? 3 : ((cardContentWidth < 700) ? 1 : 2));
		int tileWidth = ((num == 1) ? Math.Min(360, cardContentWidth) : ((cardContentWidth - tileGap * (num - 1)) / num));
		tileWidth = Math.Clamp(tileWidth, 252, 344);
		int num2 = 286;
		StoreScrollPanel viewport = new StoreScrollPanel
		{
			Width = cardContentWidth,
			Height = num2,
			Margin = Padding.Empty,
			BackColor = AppTheme.CardSurface,
			AutoScroll = true
		};
		Panel rail = new Panel
		{
			Height = num2,
			Location = Point.Empty,
			Margin = Padding.Empty,
			BackColor = AppTheme.CardSurface
		};
		int num3 = 0;
		foreach (StoreGame relatedProduct in data.RelatedProducts)
		{
			Control control = CreateRelatedProductTile(relatedProduct, tileWidth, num2 - 6);
			control.Location = new Point(num3, 0);
			rail.Controls.Add(control);
			num3 += tileWidth + tileGap;
		}
		rail.Width = Math.Max(cardContentWidth, Math.Max(0, num3 - tileGap));
		viewport.Controls.Add(rail);
		viewport.AutoScrollMinSize = new Size(rail.Width, num2 - 4);
		prevButton.Click += delegate
		{
			ScrollRail(-1);
		};
		nextButton.Click += delegate
		{
			ScrollRail(1);
		};
		viewport.Scroll += delegate
		{
			UpdateNavState();
		};
		viewport.SizeChanged += delegate
		{
			UpdateNavState();
		};
		AddCardControl(stack, viewport, 0);
		FinalizeCard(roundedPanel, stack);
		UpdateNavState();
		return roundedPanel;
		void LayoutHeader()
		{
			navFlow.Location = new Point(Math.Max(0, headerPanel.Width - navFlow.Width), 0);
			titleLabel.Width = Math.Max(120, navFlow.Left - 12);
		}
		void ScrollRail(int direction)
		{
			int max = Math.Max(0, rail.Width - viewport.ClientSize.Width);
			int x = Math.Clamp(Math.Max(0, -viewport.AutoScrollPosition.X) + direction * (tileWidth + tileGap), 0, max);
			viewport.AutoScrollPosition = new Point(x, 0);
			UpdateNavState();
		}
		void UpdateNavState()
		{
			int num4 = Math.Max(0, rail.Width - viewport.ClientSize.Width);
			int num5 = Math.Max(0, -viewport.AutoScrollPosition.X);
			bool flag = num4 > 0;
			prevButton.Visible = flag;
			nextButton.Visible = flag;
			ApplyRailNavState(prevButton, flag && num5 > 6);
			ApplyRailNavState(nextButton, flag && num5 < num4 - 6);
		}
	}

	private Control CreateRelatedProductTile(StoreGame game, int tileWidth, int tileHeight)
	{
		RoundedPanel roundedPanel = new RoundedPanel
		{
			Width = tileWidth,
			Height = tileHeight,
			CornerRadius = 20,
			BorderThickness = 1,
			BorderColor = AppTheme.CardBorder,
			SurfaceColor = Color.FromArgb(20, 27, 37),
			Cursor = Cursors.Hand,
			Margin = Padding.Empty
		};
		int num = Math.Max(132, Math.Min(156, (int)((float)tileWidth * 0.42f)));
		Panel panel = new Panel
		{
			Dock = DockStyle.Top,
			Height = num,
			Margin = Padding.Empty,
			BackColor = Color.FromArgb(10, 15, 22)
		};
		PictureBox pictureBox = new PictureBox
		{
			Dock = DockStyle.Fill,
			SizeMode = PictureBoxSizeMode.StretchImage,
			BackColor = Color.FromArgb(10, 15, 22)
		};
		BindPictureAsync(pictureBox, game.PosterImageUrl ?? game.SpotlightImageUrl, PictureBoxSizeMode.StretchImage);
		panel.Controls.Add(pictureBox);
		Label value = new Label
		{
			AutoSize = false,
			BackColor = Color.FromArgb(160, 12, 18, 28),
			ForeColor = Color.White,
			Font = new Font("Segoe UI Semibold", 8.25f, FontStyle.Bold),
			Size = new Size(82, 24),
			Location = new Point(12, 12),
			Text = game.PromoLabel,
			TextAlign = ContentAlignment.MiddleCenter
		};
		panel.Controls.Add(value);
		RoundedPanel roundedPanel2 = new RoundedPanel
		{
			Width = 86,
			Height = 24,
			Location = new Point(Math.Max(12, tileWidth - 110), 12),
			CornerRadius = 12,
			BorderThickness = 0,
			SurfaceColor = Color.FromArgb(150, 24, 33, 48),
			BackColor = Color.Transparent
		};
		roundedPanel2.Controls.Add(new Label
		{
			Dock = DockStyle.Fill,
			Font = new Font("Segoe UI", 8.1f, FontStyle.Bold),
			ForeColor = Color.FromArgb(194, 206, 224),
			TextAlign = ContentAlignment.MiddleCenter,
			Text = game.Category.ToUpperInvariant()
		});
		panel.Controls.Add(roundedPanel2);
		Panel panel2 = new Panel
		{
			Dock = DockStyle.Fill,
			Padding = new Padding(16, 14, 16, 14),
			BackColor = Color.Transparent
		};
		int num2 = tileWidth - panel2.Padding.Horizontal;
		int num3 = tileHeight - num;
		Label label = CreateBlockLabel(game.Title, new Font("Segoe UI Semibold", 10.7f, FontStyle.Bold), AppTheme.PrimaryText, num2);
		label.Height = Math.Min(54, Math.Max(30, label.Height));
		label.Location = new Point(panel2.Padding.Left, panel2.Padding.Top);
		int num4 = Math.Max(label.Bottom + 10, num3 - 58);
		Panel panel3 = new Panel
		{
			Width = num2,
			Height = 1,
			Location = new Point(panel2.Padding.Left, num4 - 8),
			BackColor = Color.FromArgb(44, 57, 74)
		};
		Label label2 = new Label
		{
			AutoSize = false,
			Width = Math.Max(84, num2 - 84),
			Height = 18,
			Font = new Font("Segoe UI", 8.5f, FontStyle.Strikeout),
			ForeColor = AppTheme.MutedText,
			Text = ((game.OriginalPrice > game.SalePrice) ? game.OriginalPriceText : string.Empty),
			TextAlign = ContentAlignment.MiddleLeft,
			Location = new Point(panel2.Padding.Left, num4),
			Visible = (game.OriginalPrice > game.SalePrice)
		};
		RoundedPanel roundedPanel3 = new RoundedPanel
		{
			Width = 58,
			Height = 22,
			Location = new Point(panel2.Padding.Left + num2 - 58, num4 - 2),
			CornerRadius = 11,
			BorderThickness = 0,
			SurfaceColor = ((game.SalePrice <= 0m) ? Color.FromArgb(29, 55, 103) : Color.FromArgb(32, 92, 72)),
			Visible = (game.DiscountPercent > 0 || game.SalePrice <= 0m)
		};
		roundedPanel3.Controls.Add(new Label
		{
			Dock = DockStyle.Fill,
			Font = new Font("Segoe UI Semibold", 8.25f, FontStyle.Bold),
			ForeColor = Color.White,
			TextAlign = ContentAlignment.MiddleCenter,
			Text = ((game.SalePrice <= 0m) ? "FREE" : $"-{game.DiscountPercent}%")
		});
		Label label3 = new Label
		{
			AutoSize = false,
			Width = Math.Max(96, num2 - 96),
			Height = 30,
			Font = new Font("Segoe UI Semibold", 12.5f, FontStyle.Bold),
			ForeColor = ((game.SalePrice <= 0m) ? Color.FromArgb(123, 181, 255) : Color.FromArgb(181, 244, 121)),
			Text = game.SalePriceText,
			TextAlign = ContentAlignment.MiddleLeft,
			Location = new Point(panel2.Padding.Left, num4 + 18)
		};
		Label label4 = new Label
		{
			AutoSize = false,
			Width = 92,
			Height = 20,
			Font = new Font("Segoe UI", 8.6f, FontStyle.Regular),
			ForeColor = AppTheme.MutedText,
			Text = game.PurchaseText,
			TextAlign = ContentAlignment.MiddleRight,
			Location = new Point(panel2.Padding.Left + num2 - 92, num4 + 24)
		};
		panel2.Controls.Add(label4);
		panel2.Controls.Add(label3);
		panel2.Controls.Add(roundedPanel3);
		panel2.Controls.Add(label2);
		panel2.Controls.Add(panel3);
		panel2.Controls.Add(label);
		roundedPanel.Click += delegate
		{
			HandleClick();
		};
		panel.Click += delegate
		{
			HandleClick();
		};
		pictureBox.Click += delegate
		{
			HandleClick();
		};
		panel2.Click += delegate
		{
			HandleClick();
		};
		label.Click += delegate
		{
			HandleClick();
		};
		label3.Click += delegate
		{
			HandleClick();
		};
		label2.Click += delegate
		{
			HandleClick();
		};
		label4.Click += delegate
		{
			HandleClick();
		};
		panel3.Click += delegate
		{
			HandleClick();
		};
		roundedPanel3.Click += delegate
		{
			HandleClick();
		};
		foreach (Control control in roundedPanel3.Controls)
		{
			control.Click += delegate
			{
				HandleClick();
			};
		}
		foreach (Control control2 in roundedPanel2.Controls)
		{
			control2.Click += delegate
			{
				HandleClick();
			};
		}
		roundedPanel.Controls.Add(panel2);
		roundedPanel.Controls.Add(panel);
		return roundedPanel;
		void HandleClick()
		{
			ShowGameAsync(game.GameId);
		}
	}

	private Control CreateCustomerReviewCard(StoreCustomerReview review, int cardWidth)
	{
		RoundedPanel roundedPanel = CreateSubCard(cardWidth, 0, new Padding(16, 14, 16, 14));
		roundedPanel.CornerRadius = 20;
		roundedPanel.BorderColor = Color.FromArgb(59, 77, 101);
		roundedPanel.SurfaceColor = Color.FromArgb(18, 25, 35);
		RoundedPanel roundedPanel2 = new RoundedPanel
		{
			Width = 46,
			Height = 46,
			Location = new Point(16, 16),
			CornerRadius = 18,
			BorderThickness = 0,
			SurfaceColor = Color.FromArgb(29, 55, 103)
		};
		roundedPanel2.Controls.Add(new Label
		{
			Dock = DockStyle.Fill,
			Text = review.AvatarText,
			Font = new Font("Segoe UI Semibold", 13f, FontStyle.Bold),
			ForeColor = Color.White,
			TextAlign = ContentAlignment.MiddleCenter
		});
		int num = roundedPanel2.Right + 16;
		int width = cardWidth - num - 16;
		Label label = CreateBlockLabel(review.DisplayName, new Font("Segoe UI Semibold", 10.25f, FontStyle.Bold), AppTheme.PrimaryText, width);
		label.Location = new Point(num, 14);
		Label label2 = CreateBlockLabel($"{BuildStarGlyphs(review.RatingValue)}  {review.RatingText}  |  {review.CreatedAtText}", new Font("Segoe UI", 8.9f, FontStyle.Regular), AppTheme.SecondaryText, width);
		label2.Location = new Point(num, label.Bottom + 4);
		Label label3 = CreateParagraphLabel(review.ReviewText, cardWidth - 32, new Font("Segoe UI", 9.35f, FontStyle.Regular), AppTheme.SecondaryText);
		label3.Location = new Point(16, Math.Max(78, label2.Bottom + 10));
		roundedPanel.Controls.Add(roundedPanel2);
		roundedPanel.Controls.Add(label);
		roundedPanel.Controls.Add(label2);
		roundedPanel.Controls.Add(label3);
		roundedPanel.Height = label3.Bottom + 16;
		return roundedPanel;
	}

	private static Control CreateBulletRow(string text, int width)
	{
		Panel obj = new Panel
		{
			Width = width,
			Height = 28,
			Margin = Padding.Empty
		};
		Label label = new Label
		{
			AutoSize = false,
			Width = 20,
			Height = 24,
			Location = new Point(0, 0),
			Font = new Font("Segoe UI Semibold", 11f, FontStyle.Bold),
			ForeColor = Color.FromArgb(181, 244, 121),
			Text = "-"
		};
		int width2 = Math.Max(60, width - label.Width - 8);
		Label label2 = CreateParagraphLabel(text, width2, new Font("Segoe UI", 9.5f, FontStyle.Regular), AppTheme.SecondaryText);
		label2.Location = new Point(label.Right + 8, 0);
		obj.Controls.Add(label2);
		obj.Controls.Add(label);
		obj.Height = Math.Max(label.Height, label2.Bottom);
		return obj;
	}

	private static Label CreateCardTitle(string text, int width)
	{
		return new Label
		{
			AutoSize = false,
			Width = width,
			Height = 28,
			Font = new Font("Segoe UI Semibold", 12f, FontStyle.Bold),
			ForeColor = AppTheme.PrimaryText,
			Text = text,
			Margin = Padding.Empty
		};
	}

	private static Label CreateParagraphLabel(string text, int width, Font font, Color color)
	{
		Label obj = new Label
		{
			AutoSize = false,
			Font = font,
			ForeColor = color,
			Margin = Padding.Empty
		};
		SetParagraphLabelText(obj, text, width);
		return obj;
	}

	private static void SetParagraphLabelText(Label label, string text, int width)
	{
		label.Text = text;
		label.Width = Math.Max(10, width);
		label.Height = MeasureTextHeight(text, label.Font, label.Width);
	}

	private static Label CreateBlockLabel(string text, Font font, Color color, int width)
	{
		Label label = new Label
		{
			AutoSize = false,
			Font = font,
			ForeColor = color,
			Width = Math.Max(10, width),
			Margin = Padding.Empty
		};
		label.Text = text;
		label.Height = MeasureTextHeight(text, font, label.Width);
		return label;
	}

	private static RoundedPanel CreateTagChip(string text)
	{
		Font font = new Font("Segoe UI Semibold", 8.5f, FontStyle.Bold);
		return new RoundedPanel
		{
			Width = Math.Max(96, TextRenderer.MeasureText(text, font).Width + 24),
			Height = 30,
			CornerRadius = 15,
			BorderThickness = 1,
			BorderColor = Color.FromArgb(59, 77, 101),
			SurfaceColor = Color.FromArgb(18, 25, 35),
			Margin = new Padding(0, 0, 8, 8),
			Controls = { (Control?)new Label
			{
				Dock = DockStyle.Fill,
				Font = font,
				ForeColor = AppTheme.PrimaryText,
				TextAlign = ContentAlignment.MiddleCenter,
				Text = text
			} }
		};
	}

	private static RoundedPanel CreateCard(int width)
	{
		return CreateSubCard(width, 0, new Padding(18));
	}

	private static Button CreateHtmlToggleButton()
	{
		Button button = new Button();
		button.AutoSize = false;
		button.Size = new Size(118, 36);
		button.Margin = Padding.Empty;
		button.Padding = new Padding(12, 0, 12, 0);
		button.FlatStyle = FlatStyle.Flat;
		button.BackColor = Color.FromArgb(19, 25, 34);
		button.ForeColor = Color.FromArgb(123, 181, 255);
		button.Font = new Font("Segoe UI Semibold", 9.25f, FontStyle.Bold);
		button.TextAlign = ContentAlignment.MiddleCenter;
		button.Cursor = Cursors.Hand;
		button.Visible = false;
		button.Text = "Xem thêm";
		button.FlatAppearance.BorderSize = 1;
		button.FlatAppearance.BorderColor = Color.FromArgb(59, 77, 101);
		return button;
	}

	private static RoundedPanel CreateSubCard(int width, int height, Padding padding)
	{
		return new RoundedPanel
		{
			Width = width,
			Height = height,
			Padding = padding,
			CornerRadius = 24,
			BorderThickness = 1,
			BorderColor = AppTheme.CardBorder,
			SurfaceColor = AppTheme.CardSurface,
			BackColor = Color.Transparent,
			Margin = Padding.Empty
		};
	}

	private static TableLayoutPanel CreateCardStack(RoundedPanel card)
	{
		int cardContentWidth = GetCardContentWidth(card);
		TableLayoutPanel tableLayoutPanel = new TableLayoutPanel
		{
			AutoSize = true,
			AutoSizeMode = AutoSizeMode.GrowAndShrink,
			ColumnCount = 1,
			RowCount = 0,
			Width = cardContentWidth,
			MaximumSize = new Size(cardContentWidth, 0),
			Location = new Point(card.Padding.Left, card.Padding.Top),
			Margin = Padding.Empty
		};
		tableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, cardContentWidth));
		card.Controls.Add(tableLayoutPanel);
		return tableLayoutPanel;
	}

	private static FlowLayoutPanel CreateWrappingFlow(int width)
	{
		FlowLayoutPanel obj = new FlowLayoutPanel
		{
			AutoSize = true,
			AutoSizeMode = AutoSizeMode.GrowAndShrink,
			WrapContents = true,
			FlowDirection = FlowDirection.LeftToRight,
			Width = width,
			MinimumSize = new Size(width, 0),
			MaximumSize = new Size(width, 0),
			Margin = Padding.Empty,
			Padding = Padding.Empty
		};
		StoreUiHelper.EnableDoubleBuffer(obj);
		return obj;
	}

	private static void AddCardControl(TableLayoutPanel stack, Control control, int bottomMargin)
	{
		int row = stack.RowCount++;
		stack.RowStyles.Add(new RowStyle());
		control.Margin = new Padding(0, 0, 0, bottomMargin);
		stack.Controls.Add(control, 0, row);
	}

	private static void FinalizeCard(RoundedPanel card, TableLayoutPanel stack)
	{
		RelayoutCard(card, stack);
	}

	private static void RelayoutCard(RoundedPanel card, TableLayoutPanel stack)
	{
		stack.PerformLayout();
		card.Height = stack.Bottom + card.Padding.Bottom;
		card.Invalidate();
	}

	private void RelayoutHtmlContentSection(Panel container, Panel host, Button toggleButton, RoundedPanel card, TableLayoutPanel stack)
	{
		if (!container.IsDisposed && !host.IsDisposed && !toggleButton.IsDisposed)
		{
			toggleButton.Location = new Point(0, host.Bottom + 12);
			container.Height = host.Height + (toggleButton.Visible ? (toggleButton.Height + 12) : 0);
			RelayoutCard(card, stack);
			contentLayout.PerformLayout();
			contentScrollPanel.Invalidate();
		}
	}

	private static void AddSection(TableLayoutPanel stack, Control control)
	{
		int rowCount = stack.RowCount;
		stack.RowCount++;
		stack.RowStyles.Add(new RowStyle());
		control.Margin = new Padding(0, 0, 0, 18);
		stack.Controls.Add(control, 0, rowCount);
	}

	private static void ClearSection(TableLayoutPanel stack)
	{
		foreach (Control control in stack.Controls)
		{
			control.Dispose();
		}
		stack.Controls.Clear();
		stack.RowStyles.Clear();
		stack.RowCount = 0;
	}

	private static TableLayoutPanel CreateColumnStack()
	{
		return new TableLayoutPanel
		{
			AutoSize = true,
			AutoSizeMode = AutoSizeMode.GrowAndShrink,
			ColumnCount = 1,
			RowCount = 0,
			Margin = Padding.Empty,
			Padding = Padding.Empty
		};
	}

	private static void ConfigureColumnStack(TableLayoutPanel stack, int width)
	{
		stack.Width = width;
		stack.MinimumSize = new Size(width, 0);
		stack.MaximumSize = new Size(width, 0);
		stack.ColumnStyles.Clear();
		stack.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, width));
	}

	private static int GetCardContentWidth(Control control)
	{
		return Math.Max(80, control.Width - control.Padding.Horizontal);
	}

	private static int MeasureTextHeight(string text, Font font, int width)
	{
		Size size = TextRenderer.MeasureText(string.IsNullOrWhiteSpace(text) ? " " : text, font, new Size(Math.Max(10, width), int.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);
		return Math.Max(font.Height + 4, size.Height);
	}

	private static bool ShouldUseEmbeddedHtml(string html, bool preferEmbeddedHtml)
	{
		if (preferEmbeddedHtml)
		{
			return true;
		}
		if (!ContainsHtmlTag(html, "<img") && !ContainsHtmlTag(html, "<video") && !ContainsHtmlTag(html, "<iframe") && !ContainsHtmlTag(html, "<table") && !ContainsHtmlTag(html, "<embed"))
		{
			return ContainsHtmlTag(html, "<object");
		}
		return true;
	}

	private static bool ContainsHtmlTag(string html, string tag)
	{
		return html.IndexOf(tag, StringComparison.OrdinalIgnoreCase) >= 0;
	}

	private static Button CreateActionButton(string text, bool primary)
	{
		Button button = new Button();
		button.Dock = DockStyle.Fill;
		button.Text = text;
		button.Cursor = Cursors.Hand;
		button.FlatStyle = FlatStyle.Flat;
		button.Font = new Font("Segoe UI Semibold", 10.25f, FontStyle.Bold);
		button.ForeColor = Color.White;
		button.BackColor = (primary ? AppTheme.Accent : Color.FromArgb(19, 25, 34));
		button.FlatAppearance.BorderSize = 1;
		button.FlatAppearance.BorderColor = (primary ? AppTheme.Accent : AppTheme.CardBorder);
		return button;
	}

	private static StoreGalleryGlyphButton CreateGalleryButton(string text, Size size, int cornerRadius, float fontSize)
	{
		return new StoreGalleryGlyphButton
		{
			Size = size,
			CornerRadius = cornerRadius,
			IconKind = ((text == "‹") ? StoreGalleryButtonIcon.ChevronLeft : ((text == "›") ? StoreGalleryButtonIcon.ChevronRight : StoreGalleryButtonIcon.Text)),
			BackColor = Color.FromArgb(15, 19, 24),
			HoverBackColor = Color.FromArgb(24, 29, 36),
			PressedBackColor = Color.FromArgb(33, 39, 47),
			ForeColor = Color.FromArgb(244, 247, 250),
			Font = new Font("Segoe UI Symbol", fontSize, FontStyle.Bold),
			Text = text,
			TabStop = false
		};
	}

	private static Button CreateRailNavButton(string text)
	{
		Button button = new Button();
		button.Size = new Size(38, 38);
		button.FlatStyle = FlatStyle.Flat;
		button.BackColor = Color.FromArgb(19, 25, 34);
		button.ForeColor = AppTheme.PrimaryText;
		button.Font = new Font("Segoe UI Symbol", 14f, FontStyle.Bold);
		button.Text = ((text == "<") ? "‹" : "›");
		button.Cursor = Cursors.Hand;
		button.Margin = new Padding(8, 0, 0, 0);
		button.Padding = Padding.Empty;
		button.TextAlign = ContentAlignment.MiddleCenter;
		button.UseCompatibleTextRendering = true;
		button.FlatAppearance.BorderSize = 1;
		button.FlatAppearance.BorderColor = AppTheme.CardBorder;
		return button;
	}

	private static void ApplyRailNavState(Button button, bool enabled)
	{
		button.Enabled = enabled;
		button.BackColor = (enabled ? Color.FromArgb(19, 25, 34) : Color.FromArgb(16, 21, 29));
		button.ForeColor = (enabled ? AppTheme.PrimaryText : AppTheme.MutedText);
		button.FlatAppearance.BorderColor = (enabled ? AppTheme.CardBorder : Color.FromArgb(37, 46, 60));
	}

	private void QueueResponsiveRefresh()
	{
		if (currentDetailData != null && !isLoadingDetail)
		{
			resizeRefreshTimer.Stop();
			resizeRefreshTimer.Start();
		}
	}

	private void ResizeRefreshTimer_Tick(object? sender, EventArgs e)
	{
		resizeRefreshTimer.Stop();
		if (currentDetailData != null && !isLoadingDetail && !base.IsDisposed)
		{
			lastLayoutWidth = contentScrollPanel.ClientSize.Width;
		}
	}

	private static string BuildMetaText(StoreGameDetailData data)
	{
		return ((data.RatingScoreValue > 0m) ? (BuildStarGlyphs(data.RatingScoreValue) + "  " + data.RatingScoreText) : "Chưa có điểm") + "  |  " + data.RatingCountText;
	}

	private static string BuildStarGlyphs(decimal ratingValue)
	{
		int num = (int)Math.Round(Math.Clamp(ratingValue, 0m, 5m), MidpointRounding.AwayFromZero);
		return new string('★', num) + new string('☆', Math.Max(0, 5 - num));
	}

	private void OpenExternalUrl(string? url)
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
			LauncherMessageBox.Show("Không thể mở liên kết này từ launcher.", "NestG Launcher", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
		}
	}

	private async Task<int?> HandleAddToCartAsync(StoreGameDetailData data, StoreProductEdition? selectedEdition, bool includeCheckoutHint)
	{
		_ = 1;
		try
		{
			string accessToken = await DesktopAppContext.AuthSessionManager.GetAccessTokenAsync();
			StorefrontApiClient.StorefrontCartMutationResponse storefrontCartMutationResponse = await storefrontApiClient.AddToCartAsync(data.GameId, selectedEdition?.VersionId, 1, accessToken);
			UpdatedCartItemCount = Math.Max(0, storefrontCartMutationResponse.CartItemCount);
			this.CartItemCountChanged?.Invoke(UpdatedCartItemCount.Value);
			using StoreCartActionDialog storeCartActionDialog = new StoreCartActionDialog(data, selectedEdition, includeCheckoutHint, storefrontCartMutationResponse.Message, UpdatedCartItemCount.Value);
			Form form = FindForm();
			if (((form == null) ? storeCartActionDialog.ShowDialog() : storeCartActionDialog.ShowDialog(form)) == DialogResult.OK)
			{
				this.CartPageRequested?.Invoke();
			}
			return UpdatedCartItemCount;
		}
		catch (Exception ex)
		{
			LauncherMessageBox.Show(ex.Message, "Không thể cập nhật giỏ hàng", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
			return null;
		}
	}

	private async Task HandleReviewSubmitAsync(string message)
	{
		if (currentDetailData == null || isSubmittingReview)
		{
			return;
		}
		string[] payload = message.Substring("review:".Length).Split(':', 3);
		if (payload.Length == 0 || !decimal.TryParse(payload[0], NumberStyles.Number, CultureInfo.InvariantCulture, out var rating))
		{
			LauncherMessageBox.Show("Hãy chọn số sao đánh giá từ 1 đến 5.", "NestG Launcher", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
			await ResetReviewSubmitButtonAsync();
			return;
		}
		int result = 0;
		bool hasPostedScrollY = payload.Length > 1 && int.TryParse(payload[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out result);
		int num = ((!hasPostedScrollY) ? (await CaptureDetailScrollYAsync()) : Math.Max(0, result));
		int postedScrollY = num;
		string reviewText = ((payload.Length > 2) ? Uri.UnescapeDataString(payload[2]) : ((payload.Length > 1 && !hasPostedScrollY) ? Uri.UnescapeDataString(payload[1]) : string.Empty));
		isSubmittingReview = true;
		try
		{
			string text = await DesktopAppContext.AuthSessionManager.GetAccessTokenAsync();
			if (string.IsNullOrWhiteSpace(text))
			{
				LauncherMessageBox.Show("Phiên đăng nhập đã hết hạn. Hãy đăng nhập lại để đánh giá sản phẩm.", "NestG Launcher", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
				await ResetReviewSubmitButtonAsync();
				return;
			}
			pendingDetailScrollY = postedScrollY;
			await storefrontApiClient.SubmitProductReviewAsync(currentDetailData.GameId, rating, reviewText, text);
			await LoadDetailAsync(currentDetailData.GameId);
		}
		catch (Exception ex)
		{
			LauncherMessageBox.Show("Không thể gửi đánh giá lúc này.\n\n" + ex.Message, "NestG Launcher", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
			await ResetReviewSubmitButtonAsync();
		}
		finally
		{
			isSubmittingReview = false;
		}
	}

	private async Task<int> CaptureDetailScrollYAsync()
	{
		if (detailWebView.IsDisposed || detailWebView.CoreWebView2 == null)
		{
			return 0;
		}
		int result;
		return int.TryParse(await detailWebView.ExecuteScriptAsync("Math.round(window.scrollY || document.documentElement.scrollTop || 0)"), NumberStyles.Integer, CultureInfo.InvariantCulture, out result) ? Math.Max(0, result) : 0;
	}

	private async Task ResetReviewSubmitButtonAsync()
	{
		if (!detailWebView.IsDisposed && detailWebView.CoreWebView2 != null)
		{
			await detailWebView.ExecuteScriptAsync("(() => {\r\n    const button = document.querySelector('[data-review-submit]');\r\n    if (button) {\r\n        button.disabled = false;\r\n        button.textContent = button.getAttribute('data-default-label') || 'Gửi đánh giá';\r\n    }\r\n    window.__nestgReview?.finish?.();\r\n})();");
		}
	}

	private static async Task BindPictureAsync(PictureBox pictureBox, string? imageUrl, PictureBoxSizeMode sizeMode)
	{
		if (string.IsNullOrWhiteSpace(imageUrl))
		{
			return;
		}
		Size clientSize = pictureBox.ClientSize;
		if (StoreRemoteImageCache.TryGetPrepared(imageUrl, clientSize, StoreImageResizeMode.Stretch, out Image image) && image != null)
		{
			pictureBox.Image = image;
			pictureBox.SizeMode = sizeMode;
			return;
		}
		Image image2 = await StoreRemoteImageCache.GetPreparedAsync(imageUrl, clientSize, StoreImageResizeMode.Stretch);
		if (!pictureBox.IsDisposed && image2 != null)
		{
			pictureBox.Image = image2;
			pictureBox.SizeMode = sizeMode;
		}
	}

	private static async Task BindGalleryImageAsync(StoreGalleryImageView imageView, string? imageUrl, string? objectPosition, StoreGalleryImageFit fitMode)
	{
		if (string.IsNullOrWhiteSpace(imageUrl))
		{
			return;
		}
		imageView.FitMode = fitMode;
		imageView.ObjectPosition = (string.IsNullOrWhiteSpace(objectPosition) ? "center center" : objectPosition.Trim());
		if (StoreRemoteImageCache.TryGet(imageUrl, out Image image) && image != null)
		{
			imageView.DisplayedImage = image;
			return;
		}
		Image image2 = await StoreRemoteImageCache.GetAsync(imageUrl);
		if (!imageView.IsDisposed && image2 != null)
		{
			imageView.DisplayedImage = image2;
		}
	}
}
