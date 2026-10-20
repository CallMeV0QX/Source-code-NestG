using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using app_desktop;
using app_desktop.CloudRedirect;
using app_desktop.Services;
using app_desktop.Services.CloudRedirect;

namespace UI_Desktop.Store;

internal sealed class StoreLibraryView : UserControl
{
	private sealed class LibraryDetailWebMessage
	{
		public string? Type { get; set; }

		public string? Text { get; set; }
	}

	private const int SidebarDesktopWidth = 334;

	private const int SidebarCompactWidth = 304;

	private const int LayoutStackBreakpoint = 1180;

	private const int DetailGridStackBreakpoint = 980;

	private const int DetailWideBreakpoint = 1320;

	private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
	{
		PropertyNameCaseInsensitive = true
	};

	private readonly StorefrontApiClient storefrontApiClient;

	private readonly GameActivationService activationService;

	private readonly GameActivationLogDownload activationLogDownload = new GameActivationLogDownload();

	private readonly DenuvoFixInstallService denuvoFixInstallService;

	private readonly PypassFixInstallService pypassFixInstallService;

	private readonly OnlineFixInstallService onlineFixInstallService;

	private readonly CancellationTokenSource disposalCancellation = new CancellationTokenSource();

	private readonly DeferredWebViewMessageDispatcher webMessageDispatcher;

	private readonly TableLayoutPanel rootLayout = new TableLayoutPanel();

	private readonly RoundedPanel sidebarPanel = new RoundedPanel();

	private readonly Label sidebarTitleLabel = new Label();

	private readonly Label sidebarSubtitleLabel = new Label();

	private readonly RoundedPanel sidebarSearchPanel = new RoundedPanel();

	private readonly Label sidebarSearchIconLabel = new Label();

	private readonly TextBox sidebarSearchTextBox = new TextBox();

	private readonly StoreScrollPanel sidebarScrollPanel = new StoreScrollPanel();

	private readonly FlowLayoutPanel sidebarFlowPanel = new FlowLayoutPanel();

	private readonly RoundedPanel detailPanel = new RoundedPanel();

	private readonly Panel detailHostPanel = new Panel();

	private readonly StoreScrollPanel detailScrollPanel = new StoreScrollPanel();

	private readonly TableLayoutPanel detailStack = new TableLayoutPanel();

	private readonly WebView2 detailWebView = new WebView2();

	private readonly Dictionary<Guid, StoreLibraryEntryCard> entryCards = new Dictionary<Guid, StoreLibraryEntryCard>();

	private readonly Dictionary<Guid, string> denuvoGuidePayloadJsonByLibraryId = new Dictionary<Guid, string>();

	private readonly DenuvoActivationUiState denuvoActivationUiState = new DenuvoActivationUiState();

	private IReadOnlyList<DesktopLibraryListItemResponse> libraryItems = Array.Empty<DesktopLibraryListItemResponse>();

	private DesktopLibraryDetailResponse? currentDetail;

	private Guid? selectedLibraryId;

	private Guid? watchedLibraryDetailId;

	private string currentFilter = string.Empty;

	private bool isRenderingDetail;

	private bool isActivatingGame;

	private bool isManagingCloudRedirect;

	private bool isApplyingDenuvoFix;

	private bool isApplyingPypassFix;

	private bool isApplyingOnlineFix;

	private CancellationTokenSource? pypassInstallCancellation;

	private int detailRequestId;

	private Task? detailWebViewInitializationTask;

	private CoreWebView2? diagnosticsCoreWebView;

	private CoreWebView2Environment? diagnosticsWebViewEnvironment;

	private readonly DenuvoDeferredUiResults deferredDenuvoUiResults = new DenuvoDeferredUiResults();

	public StoreLibraryView(StorefrontApiClient storefrontApiClient)
	{
		this.storefrontApiClient = storefrontApiClient;
		activationService = new GameActivationService(storefrontApiClient);
		denuvoFixInstallService = new DenuvoFixInstallService(storefrontApiClient);
		pypassFixInstallService = new PypassFixInstallService(storefrontApiClient);
		onlineFixInstallService = new OnlineFixInstallService(storefrontApiClient);
		webMessageDispatcher = new DeferredWebViewMessageDispatcher(TryPostWebMessageAction, HandleUnhandledWebMessageExceptionAsync, disposalCancellation.Token);
		DesktopAppContext.DenuvoActiveRealtimeClient.TokenApplied += HandleDenuvoActiveTokenApplied;
		DesktopAppContext.DenuvoActiveRealtimeClient.RequestCancelled += HandleDenuvoActiveRequestCancelled;
		DesktopAppContext.DenuvoActiveRealtimeClient.CooldownUnlocked += HandleDenuvoActiveCooldownUnlocked;
		DesktopAppContext.DenuvoActiveRealtimeClient.ConnectionStateChanged += HandleDenuvoRealtimeStateChanged;
		base.Disposed += delegate
		{
			DesktopAppContext.DenuvoActiveRealtimeClient.TokenApplied -= HandleDenuvoActiveTokenApplied;
			DesktopAppContext.DenuvoActiveRealtimeClient.RequestCancelled -= HandleDenuvoActiveRequestCancelled;
			DesktopAppContext.DenuvoActiveRealtimeClient.CooldownUnlocked -= HandleDenuvoActiveCooldownUnlocked;
			DesktopAppContext.DenuvoActiveRealtimeClient.ConnectionStateChanged -= HandleDenuvoRealtimeStateChanged;
			disposalCancellation.Cancel();
			pypassInstallCancellation?.Cancel();
			pypassInstallCancellation?.Dispose();
			pypassFixInstallService.Dispose();
			DetachDetailWebViewDiagnostics();
			disposalCancellation.Dispose();
			ClearWatchedLibraryDetail();
		};
		Dock = DockStyle.Fill;
		BackColor = Color.Transparent;
		base.Margin = Padding.Empty;
		ConfigureShell();
		WireEvents();
		ShowStatusCard("Thư viện desktop", "Chọn game đã mua để xem thông tin chi tiết và vào launcher nhanh hơn.");
	}

	public async Task RefreshLibraryAsync(CancellationToken cancellationToken = default(CancellationToken))
	{
		string text = await DesktopAppContext.AuthSessionManager.GetAccessTokenAsync(cancellationToken);
		if (string.IsNullOrWhiteSpace(text))
		{
			sidebarSubtitleLabel.Text = "Phiên đăng nhập đã hết hạn";
			libraryItems = Array.Empty<DesktopLibraryListItemResponse>();
			currentDetail = null;
			selectedLibraryId = null;
			ClearWatchedLibraryDetail();
			RenderSidebarItems(Array.Empty<DesktopLibraryListItemResponse>());
			ShowStatusCard("Không thể tải thư viện", "Hãy đăng nhập lại để đồng bộ dữ liệu game đã mua từ web storefront.");
			return;
		}
		sidebarSubtitleLabel.Text = "Đang đồng bộ dữ liệu mua trên web...";
		ShowStatusCard("Đang tải thư viện", "Launcher đang kết nối backend để lấy danh sách game và version mà bạn đã mua.");
		try
		{
			libraryItems = (await storefrontApiClient.GetLibraryAsync(text, cancellationToken)).Items.OrderByDescending((DesktopLibraryListItemResponse item) => item.AcquiredAt).ThenBy<DesktopLibraryListItemResponse, string>((DesktopLibraryListItemResponse item) => item.GameName, StringComparer.OrdinalIgnoreCase).ToArray();
			sidebarSubtitleLabel.Text = ((libraryItems.Count == 0) ? "Chưa có game nào được cấp" : $"{libraryItems.Count} phiên bản đã được đồng bộ");
			ApplyFilter();
		}
		catch (Exception ex)
		{
			libraryItems = Array.Empty<DesktopLibraryListItemResponse>();
			currentDetail = null;
			selectedLibraryId = null;
			ClearWatchedLibraryDetail();
			RenderSidebarItems(Array.Empty<DesktopLibraryListItemResponse>());
			ShowStatusCard("Không thể tải thư viện", string.IsNullOrWhiteSpace(ex.Message) ? "Đã xảy ra lỗi khi tải thư viện đã mua." : ex.Message);
		}
	}

	private void ConfigureShell()
	{
		StoreUiHelper.EnableDoubleBuffer(this);
		StoreUiHelper.EnableDoubleBuffer(rootLayout);
		StoreUiHelper.EnableDoubleBuffer(sidebarScrollPanel);
		StoreUiHelper.EnableDoubleBuffer(sidebarFlowPanel);
		StoreUiHelper.EnableDoubleBuffer(detailHostPanel);
		StoreUiHelper.EnableDoubleBuffer(detailScrollPanel);
		StoreUiHelper.EnableDoubleBuffer(detailStack);
		base.Controls.Add(rootLayout);
		rootLayout.Dock = DockStyle.Fill;
		rootLayout.Margin = Padding.Empty;
		rootLayout.Padding = Padding.Empty;
		rootLayout.ColumnCount = 2;
		rootLayout.RowCount = 1;
		rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 334f));
		rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
		rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
		rootLayout.Controls.Add(sidebarPanel, 0, 0);
		rootLayout.Controls.Add(detailPanel, 1, 0);
		ConfigureSidebar();
		ConfigureDetailPanel();
		UpdateResponsiveLayout();
	}

	private void ConfigureSidebar()
	{
		sidebarPanel.Dock = DockStyle.Fill;
		sidebarPanel.Margin = new Padding(0, 0, 18, 0);
		sidebarPanel.Padding = new Padding(18);
		sidebarPanel.SurfaceColor = Color.FromArgb(17, 23, 31);
		sidebarPanel.BorderColor = Color.FromArgb(46, 61, 79);
		sidebarPanel.BorderThickness = 1;
		sidebarPanel.CornerRadius = 18;
		sidebarTitleLabel.AutoSize = true;
		sidebarTitleLabel.ForeColor = AppTheme.PrimaryText;
		sidebarTitleLabel.Font = new Font("Segoe UI Semibold", 16.5f, FontStyle.Bold, GraphicsUnit.Point);
		sidebarTitleLabel.Text = "Thư viện";
		sidebarSubtitleLabel.AutoSize = true;
		sidebarSubtitleLabel.ForeColor = AppTheme.SecondaryText;
		sidebarSubtitleLabel.Font = new Font("Segoe UI", 9.6f, FontStyle.Regular, GraphicsUnit.Point);
		sidebarSubtitleLabel.Text = "Đang sẵn sàng đồng bộ";
		sidebarSearchPanel.Height = 44;
		sidebarSearchPanel.Margin = new Padding(0, 16, 0, 14);
		sidebarSearchPanel.Dock = DockStyle.Top;
		StoreUiHelper.ConfigureSearchInput(sidebarSearchPanel, sidebarSearchIconLabel, sidebarSearchTextBox);
		sidebarSearchIconLabel.Text = "⌕";
		sidebarSearchTextBox.PlaceholderText = "Tìm kiếm game hoặc version...";
		StoreUiHelper.RefreshCueBanner(sidebarSearchTextBox);
		sidebarSearchIconLabel.Text = "⌕";
		sidebarSearchTextBox.PlaceholderText = "Tìm kiếm game hoặc version...";
		StoreUiHelper.RefreshCueBanner(sidebarSearchTextBox);
		sidebarSearchIconLabel.Text = "⌕";
		sidebarSearchTextBox.PlaceholderText = "Tìm game hoặc version...";
		sidebarSearchIconLabel.Text = "⌕";
		sidebarSearchTextBox.PlaceholderText = "Tìm kiếm game hoặc version...";
		StoreUiHelper.RefreshCueBanner(sidebarSearchTextBox);
		sidebarScrollPanel.Dock = DockStyle.Fill;
		sidebarScrollPanel.AutoScroll = true;
		sidebarScrollPanel.BackColor = sidebarPanel.SurfaceColor;
		sidebarFlowPanel.Dock = DockStyle.Top;
		sidebarFlowPanel.AutoSize = true;
		sidebarFlowPanel.AutoSizeMode = AutoSizeMode.GrowAndShrink;
		sidebarFlowPanel.FlowDirection = FlowDirection.TopDown;
		sidebarFlowPanel.WrapContents = false;
		sidebarFlowPanel.Margin = Padding.Empty;
		sidebarFlowPanel.Padding = Padding.Empty;
		sidebarScrollPanel.Controls.Add(sidebarFlowPanel);
		TableLayoutPanel tableLayoutPanel = new TableLayoutPanel
		{
			Dock = DockStyle.Fill,
			Margin = Padding.Empty,
			Padding = Padding.Empty,
			ColumnCount = 1,
			RowCount = 4
		};
		tableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
		tableLayoutPanel.RowStyles.Add(new RowStyle());
		tableLayoutPanel.RowStyles.Add(new RowStyle());
		tableLayoutPanel.RowStyles.Add(new RowStyle());
		tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
		tableLayoutPanel.Controls.Add(sidebarTitleLabel, 0, 0);
		tableLayoutPanel.Controls.Add(sidebarSubtitleLabel, 0, 1);
		tableLayoutPanel.Controls.Add(sidebarSearchPanel, 0, 2);
		tableLayoutPanel.Controls.Add(sidebarScrollPanel, 0, 3);
		sidebarPanel.Controls.Add(tableLayoutPanel);
	}

	private void ConfigureDetailPanel()
	{
		detailPanel.Dock = DockStyle.Fill;
		detailPanel.Margin = Padding.Empty;
		detailPanel.Padding = new Padding(22);
		detailPanel.SurfaceColor = Color.FromArgb(15, 21, 29);
		detailPanel.BorderColor = Color.FromArgb(46, 61, 79);
		detailPanel.BorderThickness = 1;
		detailPanel.CornerRadius = 18;
		detailHostPanel.Dock = DockStyle.Fill;
		detailHostPanel.Margin = Padding.Empty;
		detailHostPanel.Padding = Padding.Empty;
		detailHostPanel.BackColor = detailPanel.SurfaceColor;
		detailScrollPanel.Dock = DockStyle.Fill;
		detailScrollPanel.AutoScroll = true;
		detailScrollPanel.BackColor = detailPanel.SurfaceColor;
		detailStack.Dock = DockStyle.Top;
		detailStack.AutoSize = true;
		detailStack.AutoSizeMode = AutoSizeMode.GrowAndShrink;
		detailStack.Margin = Padding.Empty;
		detailStack.Padding = Padding.Empty;
		detailStack.ColumnCount = 1;
		detailStack.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
		detailScrollPanel.Controls.Add(detailStack);
		detailWebView.Dock = DockStyle.Fill;
		detailWebView.Margin = Padding.Empty;
		detailWebView.BackColor = detailPanel.SurfaceColor;
		detailWebView.Visible = false;
		detailHostPanel.Controls.Add(detailWebView);
		detailHostPanel.Controls.Add(detailScrollPanel);
		detailPanel.Controls.Add(detailHostPanel);
	}

	private void WireEvents()
	{
		sidebarSearchTextBox.TextChanged += delegate
		{
			currentFilter = sidebarSearchTextBox.Text.Trim();
			ApplyFilter();
		};
		base.SizeChanged += delegate
		{
			UpdateResponsiveLayout();
		};
		sidebarScrollPanel.SizeChanged += delegate
		{
			UpdateSidebarCardWidths();
		};
		detailScrollPanel.SizeChanged += delegate
		{
			UpdateResponsiveLayout();
		};
	}

	private void UpdateResponsiveLayout()
	{
		int num = Math.Max(720, base.ClientSize.Width);
		bool num2 = num < 1180;
		rootLayout.SuspendLayout();
		rootLayout.Controls.Clear();
		rootLayout.ColumnStyles.Clear();
		rootLayout.RowStyles.Clear();
		if (num2)
		{
			rootLayout.ColumnCount = 1;
			rootLayout.RowCount = 2;
			rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
			rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, Math.Clamp((int)((float)base.ClientSize.Height * 0.34f), 260, 340)));
			rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
			sidebarPanel.Margin = new Padding(0, 0, 0, 18);
			detailPanel.Margin = Padding.Empty;
			rootLayout.Controls.Add(sidebarPanel, 0, 0);
			rootLayout.Controls.Add(detailPanel, 0, 1);
		}
		else
		{
			rootLayout.ColumnCount = 2;
			rootLayout.RowCount = 1;
			rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, (num < 1320) ? 304 : 334));
			rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
			rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
			sidebarPanel.Margin = new Padding(0, 0, 18, 0);
			detailPanel.Margin = Padding.Empty;
			rootLayout.Controls.Add(sidebarPanel, 0, 0);
			rootLayout.Controls.Add(detailPanel, 1, 0);
		}
		rootLayout.ResumeLayout(performLayout: true);
		UpdateSidebarCardWidths();
		if (currentDetail != null && !isRenderingDetail && !detailWebView.Visible)
		{
			RenderDetail(currentDetail);
		}
	}

	private void ApplyFilter()
	{
		IReadOnlyList<DesktopLibraryListItemResponse> readOnlyList2;
		if (!string.IsNullOrWhiteSpace(currentFilter))
		{
			IReadOnlyList<DesktopLibraryListItemResponse> readOnlyList = libraryItems.Where((DesktopLibraryListItemResponse item) => item.GameName.Contains(currentFilter, StringComparison.OrdinalIgnoreCase) || item.VersionName.Contains(currentFilter, StringComparison.OrdinalIgnoreCase) || item.Category.Contains(currentFilter, StringComparison.OrdinalIgnoreCase)).ToArray();
			readOnlyList2 = readOnlyList;
		}
		else
		{
			readOnlyList2 = libraryItems;
		}
		IReadOnlyList<DesktopLibraryListItemResponse> readOnlyList3 = readOnlyList2;
		RenderSidebarItems(readOnlyList3);
		if (readOnlyList3.Count == 0)
		{
			currentDetail = null;
			selectedLibraryId = null;
			ClearWatchedLibraryDetail();
			ShowStatusCard("Không có kết quả", string.IsNullOrWhiteSpace(currentFilter) ? "Thư viện hiện chưa có game nào được cấp vào tài khoản này." : ("Không tìm thấy game hoặc version nào khớp với \"" + currentFilter + "\"."));
		}
		else
		{
			Guid libraryId = ((selectedLibraryId.HasValue && readOnlyList3.Any((DesktopLibraryListItemResponse item) => item.LibraryId == selectedLibraryId.Value)) ? selectedLibraryId.Value : readOnlyList3[0].LibraryId);
			SelectLibraryAsync(libraryId);
		}
	}

	private void RenderSidebarItems(IReadOnlyList<DesktopLibraryListItemResponse> items)
	{
		foreach (StoreLibraryEntryCard value in entryCards.Values)
		{
			value.Dispose();
		}
		entryCards.Clear();
		sidebarFlowPanel.SuspendLayout();
		sidebarFlowPanel.Controls.Clear();
		foreach (DesktopLibraryListItemResponse item in items)
		{
			StoreLibraryEntryCard storeLibraryEntryCard = new StoreLibraryEntryCard(item);
			storeLibraryEntryCard.Selected += async delegate
			{
				await SelectLibraryAsync(item.LibraryId);
			};
			entryCards[item.LibraryId] = storeLibraryEntryCard;
			sidebarFlowPanel.Controls.Add(storeLibraryEntryCard);
		}
		sidebarFlowPanel.ResumeLayout();
		UpdateSidebarCardWidths();
		UpdateSidebarSelectionState();
	}

	private void UpdateSidebarCardWidths()
	{
		int num = sidebarScrollPanel.ClientSize.Width;
		if (sidebarScrollPanel.VerticalScroll.Visible)
		{
			num -= SystemInformation.VerticalScrollBarWidth;
		}
		num = Math.Max(286, num - 4);
		foreach (StoreLibraryEntryCard value in entryCards.Values)
		{
			int width = Math.Max(value.MinimumSize.Width, num - value.Margin.Horizontal - 6);
			value.Width = width;
		}
	}

	private async Task SelectLibraryAsync(Guid libraryId, CancellationToken cancellationToken = default(CancellationToken))
	{
		if (watchedLibraryDetailId.HasValue && watchedLibraryDetailId.Value != libraryId)
		{
			ClearWatchedLibraryDetail();
		}
		selectedLibraryId = libraryId;
		UpdateSidebarSelectionState();
		string text = await DesktopAppContext.AuthSessionManager.GetAccessTokenAsync(cancellationToken);
		if (string.IsNullOrWhiteSpace(text))
		{
			currentDetail = null;
			ClearWatchedLibraryDetail();
			ShowStatusCard("Không thể tải chi tiết", "Phiên đăng nhập đã hết hạn. Hãy đăng nhập lại để tiếp tục.");
			return;
		}
		ShowStatusCard("Đang tải chi tiết", "Launcher đang lấy thông tin game đã mua và dữ liệu storefront liên quan.");
		try
		{
			int requestId = ++detailRequestId;
			DesktopLibraryDetailResponse detail = await storefrontApiClient.GetLibraryDetailAsync(libraryId, text, cancellationToken);
			if (requestId == detailRequestId)
			{
				currentDetail = detail;
				await CacheDenuvoGuidePayloadAsync(detail);
				RenderDetail(detail);
			}
		}
		catch (Exception ex)
		{
			currentDetail = null;
			ClearWatchedLibraryDetail();
			ShowStatusCard("Không thể tải chi tiết", string.IsNullOrWhiteSpace(ex.Message) ? "Đã xảy ra lỗi khi tải chi tiết game trong thư viện." : ex.Message);
		}
	}

	private void UpdateSidebarSelectionState()
	{
		foreach (KeyValuePair<Guid, StoreLibraryEntryCard> entryCard in entryCards)
		{
			entryCard.Value.SetSelected(selectedLibraryId.HasValue && entryCard.Key == selectedLibraryId.Value);
		}
	}

	private void RenderDetail(DesktopLibraryDetailResponse detail)
	{
		isRenderingDetail = true;
		try
		{
			WatchLibraryDetail(detail.LibraryId);
			detailScrollPanel.Visible = false;
			detailWebView.Visible = true;
			RefreshDetailDocumentAsync(detail);
		}
		finally
		{
			isRenderingDetail = false;
		}
	}

	private void WatchLibraryDetail(Guid libraryId)
	{
		if (!(libraryId == Guid.Empty) && !(watchedLibraryDetailId == libraryId))
		{
			ClearWatchedLibraryDetail();
			watchedLibraryDetailId = libraryId;
			DesktopAppContext.DenuvoActiveRealtimeClient.WatchLibraryDetailAsync(libraryId);
		}
	}

	private void ClearWatchedLibraryDetail()
	{
		if (watchedLibraryDetailId.HasValue)
		{
			Guid value = watchedLibraryDetailId.Value;
			watchedLibraryDetailId = null;
			DesktopAppContext.DenuvoActiveRealtimeClient.UnwatchLibraryDetailAsync(value);
		}
	}

	private async Task RefreshDetailDocumentAsync(DesktopLibraryDetailResponse detail)
	{
		Guid currentLibraryId = detail.LibraryId;
		await EnsureDetailWebViewAsync();
		if (!detailWebView.IsDisposed)
		{
			DesktopLibraryDetailResponse? desktopLibraryDetailResponse = currentDetail;
			if (desktopLibraryDetailResponse != null && !(desktopLibraryDetailResponse.LibraryId != currentLibraryId))
			{
				DateTime? adminRequestCooldownUntilUtc = DenuvoFixInstallService.GetAdminRequestCooldownUntilUtc(detail.LibraryId);
				detailWebView.NavigateToString(StoreLibraryDetailHtmlBuilder.BuildDocument(detail, isActivatingGame, isApplyingDenuvoFix, isApplyingPypassFix, isApplyingOnlineFix, adminRequestCooldownUntilUtc, showPypassGuide: true, showDenuvoGuide: true, ResolveDenuvoGuidePayloadJson(detail, adminRequestCooldownUntilUtc)));
			}
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
			AttachDetailWebViewDiagnostics(detailWebView.CoreWebView2);
			detailWebView.CoreWebView2.WebMessageReceived -= DetailWebView_WebMessageReceived;
			detailWebView.CoreWebView2.WebMessageReceived += DetailWebView_WebMessageReceived;
			detailWebView.CoreWebView2.NavigationStarting -= DetailWebView_NavigationStarting;
			detailWebView.CoreWebView2.NavigationStarting += DetailWebView_NavigationStarting;
			detailWebView.CoreWebView2.NavigationCompleted -= DetailWebView_DenuvoNavigationCompleted;
			detailWebView.CoreWebView2.NavigationCompleted += DetailWebView_DenuvoNavigationCompleted;
		}
	}

	private void AttachDetailWebViewDiagnostics(CoreWebView2 coreWebView)
	{
		if (diagnosticsCoreWebView != coreWebView)
		{
			DetachDetailWebViewDiagnostics();
			diagnosticsCoreWebView = coreWebView;
			diagnosticsWebViewEnvironment = coreWebView.Environment;
			diagnosticsCoreWebView.ProcessFailed += DetailWebView_ProcessFailed;
			diagnosticsWebViewEnvironment.BrowserProcessExited += DetailWebView_BrowserProcessExited;
		}
	}

	private void DetachDetailWebViewDiagnostics()
	{
		try
		{
			if (diagnosticsCoreWebView != null)
			{
				diagnosticsCoreWebView.ProcessFailed -= DetailWebView_ProcessFailed;
			}
			if (diagnosticsWebViewEnvironment != null)
			{
				diagnosticsWebViewEnvironment.BrowserProcessExited -= DetailWebView_BrowserProcessExited;
			}
		}
		catch
		{
		}
		finally
		{
			diagnosticsCoreWebView = null;
			diagnosticsWebViewEnvironment = null;
		}
	}

	private static void DetailWebView_ProcessFailed(object? sender, CoreWebView2ProcessFailedEventArgs e)
	{
		try
		{
			DesktopDiagnosticLog.Write("WebView2.LibraryDetail", "ProcessFailed", $"Kind={e.ProcessFailedKind}; Reason={e.Reason}; ExitCode={e.ExitCode}; Description={e.ProcessDescription}; FailureSource={e.FailureSourceModulePath}");
		}
		catch (Exception exception)
		{
			DesktopDiagnosticLog.Write("WebView2.LibraryDetail", "ProcessFailedDetailsUnavailable", null, exception);
		}
	}

	private static void DetailWebView_BrowserProcessExited(object? sender, CoreWebView2BrowserProcessExitedEventArgs e)
	{
		try
		{
			DesktopDiagnosticLog.Write("WebView2.LibraryDetail", "BrowserProcessExited", $"Kind={e.BrowserProcessExitKind}; BrowserProcessId={e.BrowserProcessId}");
		}
		catch (Exception exception)
		{
			DesktopDiagnosticLog.Write("WebView2.LibraryDetail", "BrowserExitDetailsUnavailable", null, exception);
		}
	}

	private void DetailWebView_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
	{
		if (TryGetCopyTextWebMessage(e, out string text))
		{
			CopyTextToClipboard(text);
			return;
		}
		string message;
		try
		{
			message = e.TryGetWebMessageAsString();
		}
		catch
		{
			return;
		}
		if (!string.IsNullOrWhiteSpace(message) && (!message.StartsWith("activation-log:download:", StringComparison.Ordinal) || !(e.Source != "about:blank")))
		{
			webMessageDispatcher.TryDispatch(() => ProcessDetailWebMessageAsync(message));
		}
	}

	private async Task ProcessDetailWebMessageAsync(string message)
	{
		if (message.StartsWith("activation-log:download:", StringComparison.Ordinal))
		{
			DesktopLibraryDetailResponse desktopLibraryDetailResponse = currentDetail;
			if (desktopLibraryDetailResponse == null)
			{
				return;
			}
			await ActivationLogPopupSupport.DownloadAsync(this, activationLogDownload, message.Substring("activation-log:download:".Length), desktopLibraryDetailResponse.LibraryId, async delegate(string script)
			{
				if (detailWebView.CoreWebView2 != null && !detailWebView.IsDisposed)
				{
					await detailWebView.CoreWebView2.ExecuteScriptAsync(script);
				}
			}, disposalCancellation.Token);
		}
		else if ((!isManagingCloudRedirect || (!message.Equals("play", StringComparison.OrdinalIgnoreCase) && !message.StartsWith("cloudsave:", StringComparison.OrdinalIgnoreCase))) && currentDetail != null)
		{
			if (message.Equals("play", StringComparison.OrdinalIgnoreCase))
			{
				await ActivateCurrentGameAsync();
			}
			else if (message.StartsWith("denuvo:", StringComparison.OrdinalIgnoreCase))
			{
				await ApplyCurrentDenuvoFixAsync();
			}
			else if (message.Equals("cloudredirect:manage", StringComparison.OrdinalIgnoreCase))
			{
				await ShowCloudRedirectManagementAsync(currentDetail);
			}
			else if (message.Equals("pypass:cancel", StringComparison.OrdinalIgnoreCase))
			{
				pypassInstallCancellation?.Cancel();
			}
			else if (message.StartsWith("pypass:", StringComparison.OrdinalIgnoreCase))
			{
				await ApplyCurrentPypassFixAsync();
			}
			else if (message.StartsWith("online:", StringComparison.OrdinalIgnoreCase))
			{
				await ApplyCurrentOnlineFixAsync();
			}
			else if (message.StartsWith("open:", StringComparison.OrdinalIgnoreCase))
			{
				OpenWebsite(Uri.UnescapeDataString(message.Substring("open:".Length)));
			}
		}
	}

	private bool TryPostWebMessageAction(Action action)
	{
		if (base.IsDisposed || base.Disposing || !base.IsHandleCreated || disposalCancellation.IsCancellationRequested)
		{
			return false;
		}
		try
		{
			BeginInvoke(action);
			return true;
		}
		catch (InvalidOperationException) when (base.IsDisposed || base.Disposing || !base.IsHandleCreated)
		{
			return false;
		}
	}

	private Task HandleUnhandledWebMessageExceptionAsync(Exception exception)
	{
		DesktopDiagnosticLog.Write("WebView2.LibraryDetail", "UnhandledWebMessageException", null, exception);
		if (base.IsDisposed || base.Disposing || disposalCancellation.IsCancellationRequested)
		{
			return Task.CompletedTask;
		}
		LauncherMessageBox.Show(this, DesktopUserMessage.FromException(exception, "Không thể hoàn tất thao tác trong thư viện. Vui lòng thử lại."), "NestG Launcher", MessageBoxButtons.OK, MessageBoxIcon.Hand);
		return Task.CompletedTask;
	}

	private static bool TryGetCopyTextWebMessage(CoreWebView2WebMessageReceivedEventArgs e, out string text)
	{
		text = string.Empty;
		try
		{
			LibraryDetailWebMessage libraryDetailWebMessage = JsonSerializer.Deserialize<LibraryDetailWebMessage>(e.WebMessageAsJson, JsonOptions);
			if (libraryDetailWebMessage == null || !string.Equals(libraryDetailWebMessage.Type, "copyText", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(libraryDetailWebMessage.Text))
			{
				return false;
			}
			text = libraryDetailWebMessage.Text;
			return true;
		}
		catch
		{
			return false;
		}
	}

	private static void CopyTextToClipboard(string text)
	{
		try
		{
			Clipboard.SetText(text);
		}
		catch
		{
		}
	}

	private void DetailWebView_NavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
	{
		if (e.IsUserInitiated && !e.Uri.StartsWith("about:blank", StringComparison.OrdinalIgnoreCase) && !e.Uri.StartsWith("data:text/html", StringComparison.OrdinalIgnoreCase))
		{
			e.Cancel = true;
			OpenWebsite(e.Uri);
		}
	}

	private void AddDetailSection(Control control)
	{
		control.Margin = new Padding(0, 0, 0, 18);
		control.Dock = DockStyle.Top;
		detailStack.RowStyles.Add(new RowStyle());
		detailStack.Controls.Add(control, 0, detailStack.RowCount);
		detailStack.RowCount++;
	}

	private Control CreateHeroSection(DesktopLibraryDetailResponse detail)
	{
		RoundedPanel roundedPanel = CreateSectionShell();
		roundedPanel.Padding = new Padding(16);
		roundedPanel.Height = 372;
		TableLayoutPanel tableLayoutPanel = new TableLayoutPanel
		{
			Dock = DockStyle.Fill,
			Margin = Padding.Empty,
			Padding = Padding.Empty,
			ColumnCount = 2,
			RowCount = 1
		};
		tableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 46f));
		tableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 54f));
		tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
		PictureBox pictureBox = new PictureBox
		{
			Dock = DockStyle.Fill,
			Margin = Padding.Empty,
			BackColor = Color.FromArgb(10, 15, 22),
			SizeMode = PictureBoxSizeMode.StretchImage
		};
		LoadDetailArtworkAsync(pictureBox, detail);
		Panel infoPanel = new Panel
		{
			Dock = DockStyle.Fill,
			Margin = new Padding(22, 0, 0, 0),
			BackColor = Color.Transparent
		};
		Label badgeLabel = new Label
		{
			AutoSize = false,
			BackColor = Color.FromArgb(154, 180, 37, 42),
			ForeColor = Color.FromArgb(255, 240, 240),
			Font = new Font("Segoe UI Semibold", 9.2f, FontStyle.Bold, GraphicsUnit.Point),
			Text = "ĐÃ SỞ HỮU",
			TextAlign = ContentAlignment.MiddleCenter,
			Bounds = new Rectangle(0, 0, 114, 34)
		};
		Label titleLabel = new Label
		{
			AutoSize = false,
			ForeColor = AppTheme.PrimaryText,
			Font = new Font("Segoe UI Semibold", 22f, FontStyle.Bold, GraphicsUnit.Point),
			Text = detail.GameName
		};
		Label subtitleLabel = new Label
		{
			AutoSize = false,
			ForeColor = AppTheme.SecondaryText,
			Font = new Font("Segoe UI", 10.7f, FontStyle.Regular, GraphicsUnit.Point),
			Text = (string.IsNullOrWhiteSpace(detail.ShortDescription) ? "Sản phẩm đã được cấp vào thư viện launcher của bạn." : StripHtml(detail.ShortDescription))
		};
		FlowLayoutPanel chipFlow = new FlowLayoutPanel
		{
			AutoSize = true,
			AutoSizeMode = AutoSizeMode.GrowAndShrink,
			WrapContents = true,
			Margin = Padding.Empty,
			Padding = Padding.Empty,
			Location = new Point(0, 0)
		};
		chipFlow.Controls.Add(CreateChip(detail.VersionName, Color.FromArgb(58, 66, 88), Color.FromArgb(248, 248, 252)));
		chipFlow.Controls.Add(CreateChip("Order " + detail.OrderCode, Color.FromArgb(32, 71, 61), Color.FromArgb(198, 241, 223)));
		chipFlow.Controls.Add(CreateChip(detail.AcquiredAtText, Color.FromArgb(52, 43, 72), Color.FromArgb(221, 211, 252)));
		TableLayoutPanel metaGrid = new TableLayoutPanel
		{
			Margin = Padding.Empty,
			Padding = Padding.Empty,
			ColumnCount = 2,
			RowCount = 4,
			Width = 420,
			Height = 120
		};
		metaGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140f));
		metaGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
		metaGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 30f));
		metaGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 30f));
		metaGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 30f));
		metaGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 30f));
		AddMetaRow(metaGrid, 0, "Giá hiện tại", detail.CurrentPriceText, AppTheme.PrimaryText);
		AddMetaRow(metaGrid, 1, "Khuyến mãi", string.IsNullOrWhiteSpace(detail.DiscountText) ? "Đã mua" : detail.DiscountText, Color.FromArgb(73, 209, 164));
		AddMetaRow(metaGrid, 2, "Đánh giá", detail.RatingScoreText + " • " + detail.RatingCountText, AppTheme.PrimaryText);
		AddMetaRow(metaGrid, 3, "Trạng thái", detail.StatusText, Color.FromArgb(240, 218, 134));
		GradientButton gradientButton = new GradientButton
		{
			Text = "Chơi ngay",
			Width = 184,
			StartColor = Color.FromArgb(224, 56, 62),
			EndColor = Color.FromArgb(171, 22, 32),
			CornerRadius = 18,
			Font = new Font("Segoe UI Semibold", 11.5f, FontStyle.Bold, GraphicsUnit.Point)
		};
		gradientButton.Click += async delegate
		{
			await ActivateGameAsync(detail);
		};
		Button button = new Button
		{
			Text = "Trang sản phẩm",
			Width = 152,
			Height = 56,
			FlatStyle = FlatStyle.Flat,
			BackColor = Color.FromArgb(27, 36, 48),
			ForeColor = AppTheme.PrimaryText,
			Font = new Font("Segoe UI Semibold", 10.6f, FontStyle.Bold, GraphicsUnit.Point),
			Cursor = Cursors.Hand,
			Enabled = !string.IsNullOrWhiteSpace(detail.Website)
		};
		button.FlatAppearance.BorderColor = Color.FromArgb(64, 81, 103);
		button.FlatAppearance.BorderSize = 1;
		button.Click += delegate
		{
			OpenWebsite(detail.Website);
		};
		FlowLayoutPanel actionFlow = new FlowLayoutPanel
		{
			AutoSize = true,
			AutoSizeMode = AutoSizeMode.GrowAndShrink,
			WrapContents = false,
			Margin = Padding.Empty,
			Padding = Padding.Empty
		};
		actionFlow.Controls.Add(gradientButton);
		actionFlow.Controls.Add(button);
		infoPanel.Controls.Add(actionFlow);
		infoPanel.Controls.Add(metaGrid);
		infoPanel.Controls.Add(chipFlow);
		infoPanel.Controls.Add(subtitleLabel);
		infoPanel.Controls.Add(titleLabel);
		infoPanel.Controls.Add(badgeLabel);
		infoPanel.Resize += delegate
		{
			badgeLabel.Location = new Point(0, 0);
			titleLabel.Location = new Point(0, badgeLabel.Bottom + 18);
			titleLabel.Size = new Size(Math.Max(240, infoPanel.ClientSize.Width - 4), 84);
			subtitleLabel.Location = new Point(0, titleLabel.Bottom + 10);
			subtitleLabel.Size = new Size(Math.Max(240, infoPanel.ClientSize.Width - 4), 68);
			chipFlow.Location = new Point(0, subtitleLabel.Bottom + 12);
			chipFlow.Width = Math.Max(240, infoPanel.ClientSize.Width - 4);
			metaGrid.Location = new Point(0, chipFlow.Bottom + 16);
			metaGrid.Width = Math.Max(320, infoPanel.ClientSize.Width - 6);
			actionFlow.Location = new Point(0, metaGrid.Bottom + 20);
		};
		tableLayoutPanel.Controls.Add(pictureBox, 0, 0);
		tableLayoutPanel.Controls.Add(infoPanel, 1, 0);
		roundedPanel.Controls.Add(tableLayoutPanel);
		return roundedPanel;
	}

	private Control CreateFactsSection(DesktopLibraryDetailResponse detail)
	{
		RoundedPanel roundedPanel = CreateSectionShell("Tổng quan sở hữu", "Thông tin nhanh, package và tag đồng bộ từ detail bên web.");
		TableLayoutPanel tableLayoutPanel = new TableLayoutPanel
		{
			Dock = DockStyle.Top,
			AutoSize = true,
			AutoSizeMode = AutoSizeMode.GrowAndShrink,
			Margin = Padding.Empty,
			Padding = Padding.Empty,
			ColumnCount = 2
		};
		tableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48f));
		tableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52f));
		RoundedPanel roundedPanel2 = CreateInnerCard();
		roundedPanel2.Padding = new Padding(16);
		roundedPanel2.AutoSize = true;
		roundedPanel2.AutoSizeMode = AutoSizeMode.GrowAndShrink;
		TableLayoutPanel tableLayoutPanel2 = new TableLayoutPanel
		{
			Dock = DockStyle.Top,
			AutoSize = true,
			AutoSizeMode = AutoSizeMode.GrowAndShrink,
			Margin = Padding.Empty,
			Padding = Padding.Empty,
			ColumnCount = 2
		};
		tableLayoutPanel2.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 136f));
		tableLayoutPanel2.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
		List<DesktopLibraryFactResponse> list;
		if (detail.Facts.Count <= 0)
		{
			int num = 1;
			list = new List<DesktopLibraryFactResponse>(num);
			CollectionsMarshal.SetCount(list, num);
			CollectionsMarshal.AsSpan(list)[0] = new DesktopLibraryFactResponse
			{
				Label = "Trạng thái",
				Value = "Đã sở hữu"
			};
		}
		else
		{
			list = detail.Facts;
		}
		List<DesktopLibraryFactResponse> list2 = list;
		for (int i = 0; i < list2.Count; i++)
		{
			tableLayoutPanel2.RowStyles.Add(new RowStyle(SizeType.AutoSize));
			Label control = new Label
			{
				AutoSize = true,
				ForeColor = AppTheme.SecondaryText,
				Font = new Font("Segoe UI", 9.2f, FontStyle.Regular, GraphicsUnit.Point),
				Text = list2[i].Label,
				Margin = new Padding(0, 0, 12, 10)
			};
			Label control2 = new Label
			{
				AutoSize = true,
				MaximumSize = new Size(Math.Max(180, GetDetailContentWidth() / 2), 0),
				ForeColor = AppTheme.PrimaryText,
				Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold, GraphicsUnit.Point),
				Text = list2[i].Value,
				Margin = new Padding(0, 0, 0, 10)
			};
			tableLayoutPanel2.Controls.Add(control, 0, i);
			tableLayoutPanel2.Controls.Add(control2, 1, i);
		}
		roundedPanel2.Controls.Add(tableLayoutPanel2);
		RoundedPanel roundedPanel3 = CreateInnerCard();
		roundedPanel3.Padding = new Padding(16);
		roundedPanel3.AutoSize = true;
		roundedPanel3.AutoSizeMode = AutoSizeMode.GrowAndShrink;
		Label label = CreateSectionCaption("Gói sản phẩm");
		label.Location = new Point(16, 16);
		FlowLayoutPanel flowLayoutPanel = new FlowLayoutPanel
		{
			AutoSize = true,
			AutoSizeMode = AutoSizeMode.GrowAndShrink,
			FlowDirection = FlowDirection.TopDown,
			WrapContents = false,
			Margin = Padding.Empty,
			Padding = Padding.Empty,
			Location = new Point(16, 48)
		};
		foreach (string item in detail.PackageItems.DefaultIfEmpty("Không có thông tin gói sản phẩm bổ sung."))
		{
			flowLayoutPanel.Controls.Add(CreateBulletLabel(item));
		}
		Label label2 = CreateSectionCaption("Tag nổi bật");
		label2.Location = new Point(16, flowLayoutPanel.Bottom + 18);
		FlowLayoutPanel flowLayoutPanel2 = new FlowLayoutPanel
		{
			AutoSize = true,
			AutoSizeMode = AutoSizeMode.GrowAndShrink,
			WrapContents = true,
			Margin = Padding.Empty,
			Padding = Padding.Empty,
			Location = new Point(16, label2.Bottom + 10)
		};
		foreach (string item2 in detail.SummaryTags.Take(10))
		{
			flowLayoutPanel2.Controls.Add(CreateChip(item2, Color.FromArgb(42, 59, 82), Color.FromArgb(228, 235, 245)));
		}
		roundedPanel3.Controls.Add(flowLayoutPanel2);
		roundedPanel3.Controls.Add(label2);
		roundedPanel3.Controls.Add(flowLayoutPanel);
		roundedPanel3.Controls.Add(label);
		tableLayoutPanel.Controls.Add(roundedPanel2, 0, 0);
		tableLayoutPanel.Controls.Add(roundedPanel3, 1, 0);
		tableLayoutPanel.Location = new Point(18, GetSectionContentTop(roundedPanel));
		tableLayoutPanel.Width = Math.Max(420, GetDetailContentWidth() - 44);
		roundedPanel.Controls.Add(tableLayoutPanel);
		return roundedPanel;
	}

	private Control CreateDescriptionSection(DesktopLibraryDetailResponse detail)
	{
		RoundedPanel roundedPanel = CreateSectionShell("Mô tả game", "Desktop trích xuất và rút gọn nội dung từ trang game detail trên web.");
		RoundedPanel roundedPanel2 = CreateInnerCard();
		roundedPanel2.Padding = new Padding(18, 16, 18, 18);
		roundedPanel2.AutoSize = true;
		roundedPanel2.AutoSizeMode = AutoSizeMode.GrowAndShrink;
		string[] array = new string[3]
		{
			StripHtml(detail.ShortDescription),
			StripHtml(detail.AboutTheGameHtml),
			StripHtml(detail.DetailedDescriptionHtml)
		}.Where((string text) => !string.IsNullOrWhiteSpace(text)).Distinct<string>(StringComparer.OrdinalIgnoreCase).Take(3)
			.ToArray();
		if (array.Length == 0)
		{
			array = new string[1] { "Launcher hiện chưa có thêm mô tả chi tiết cho sản phẩm này." };
		}
		FlowLayoutPanel flowLayoutPanel = new FlowLayoutPanel
		{
			Dock = DockStyle.Top,
			AutoSize = true,
			AutoSizeMode = AutoSizeMode.GrowAndShrink,
			FlowDirection = FlowDirection.TopDown,
			WrapContents = false,
			Margin = Padding.Empty,
			Padding = Padding.Empty
		};
		string[] array2 = array;
		foreach (string text2 in array2)
		{
			flowLayoutPanel.Controls.Add(CreateParagraphLabel(text2));
		}
		roundedPanel2.Controls.Add(flowLayoutPanel);
		roundedPanel2.Location = new Point(18, GetSectionContentTop(roundedPanel));
		roundedPanel2.Width = Math.Max(420, GetDetailContentWidth() - 44);
		roundedPanel.Controls.Add(roundedPanel2);
		return roundedPanel;
	}

	private Control CreateEditionsSection(DesktopLibraryDetailResponse detail)
	{
		RoundedPanel roundedPanel = CreateSectionShell("Phiên bản và quyền sở hữu", "Bản phiên bản đã mua được highlight để đối chiếu nhanh với detail bên web.");
		FlowLayoutPanel flowLayoutPanel = new FlowLayoutPanel
		{
			Dock = DockStyle.Top,
			AutoSize = true,
			AutoSizeMode = AutoSizeMode.GrowAndShrink,
			WrapContents = true,
			Margin = Padding.Empty,
			Padding = Padding.Empty
		};
		foreach (DesktopLibraryEditionResponse edition in detail.Editions)
		{
			flowLayoutPanel.Controls.Add(CreateEditionCard(edition));
		}
		flowLayoutPanel.Location = new Point(18, GetSectionContentTop(roundedPanel));
		flowLayoutPanel.Width = Math.Max(420, GetDetailContentWidth() - 44);
		roundedPanel.Controls.Add(flowLayoutPanel);
		return roundedPanel;
	}

	private Control CreateRequirementsSection(DesktopLibraryDetailResponse detail)
	{
		RoundedPanel roundedPanel = CreateSectionShell("Yêu cầu hệ thống", "Dữ liệu requirement được render lại từ thông tin Steam/web.");
		FlowLayoutPanel flowLayoutPanel = new FlowLayoutPanel
		{
			Dock = DockStyle.Top,
			AutoSize = true,
			AutoSizeMode = AutoSizeMode.GrowAndShrink,
			WrapContents = true,
			Margin = Padding.Empty,
			Padding = Padding.Empty
		};
		foreach (DesktopLibraryRequirementResponse requirement in detail.Requirements)
		{
			flowLayoutPanel.Controls.Add(CreateRequirementCard(requirement));
		}
		flowLayoutPanel.Location = new Point(18, GetSectionContentTop(roundedPanel));
		flowLayoutPanel.Width = Math.Max(420, GetDetailContentWidth() - 44);
		roundedPanel.Controls.Add(flowLayoutPanel);
		return roundedPanel;
	}

	private Control CreateReviewsSection(DesktopLibraryDetailResponse detail)
	{
		RoundedPanel roundedPanel = CreateSectionShell("Đánh giá người chơi", "Review được trích từ dữ liệu hiện có để launcher có trải nghiệm gần với web.");
		FlowLayoutPanel flowLayoutPanel = new FlowLayoutPanel
		{
			Dock = DockStyle.Top,
			AutoSize = true,
			AutoSizeMode = AutoSizeMode.GrowAndShrink,
			FlowDirection = FlowDirection.TopDown,
			WrapContents = false,
			Margin = Padding.Empty,
			Padding = Padding.Empty
		};
		List<DesktopLibraryReviewResponse> list;
		if (detail.CustomerReviews.Count <= 0)
		{
			int num = 1;
			list = new List<DesktopLibraryReviewResponse>(num);
			CollectionsMarshal.SetCount(list, num);
			CollectionsMarshal.AsSpan(list)[0] = new DesktopLibraryReviewResponse
			{
				DisplayName = "NestG",
				AvatarText = "N",
				RatingText = "-",
				CreatedAtText = string.Empty,
				ReviewText = "Chưa có review nào cho sản phẩm này."
			};
		}
		else
		{
			list = detail.CustomerReviews;
		}
		foreach (DesktopLibraryReviewResponse item in list.Take(4))
		{
			flowLayoutPanel.Controls.Add(CreateReviewCard(item));
		}
		flowLayoutPanel.Location = new Point(18, GetSectionContentTop(roundedPanel));
		flowLayoutPanel.Width = Math.Max(420, GetDetailContentWidth() - 44);
		roundedPanel.Controls.Add(flowLayoutPanel);
		return roundedPanel;
	}

	private RoundedPanel CreateSectionShell(string? title = null, string? subtitle = null)
	{
		RoundedPanel roundedPanel = new RoundedPanel
		{
			BackColor = Color.Transparent,
			SurfaceColor = Color.FromArgb(17, 24, 34),
			BorderColor = Color.FromArgb(46, 61, 79),
			BorderThickness = 1,
			CornerRadius = 18,
			Padding = new Padding(18),
			AutoSize = true,
			AutoSizeMode = AutoSizeMode.GrowAndShrink
		};
		if (string.IsNullOrWhiteSpace(title))
		{
			return roundedPanel;
		}
		Label label = new Label
		{
			AutoSize = true,
			ForeColor = AppTheme.PrimaryText,
			Font = new Font("Segoe UI Semibold", 15f, FontStyle.Bold, GraphicsUnit.Point),
			Text = title,
			Margin = Padding.Empty,
			Location = new Point(18, 18)
		};
		roundedPanel.Controls.Add(label);
		if (!string.IsNullOrWhiteSpace(subtitle))
		{
			Label value = new Label
			{
				AutoSize = true,
				MaximumSize = new Size(Math.Max(320, GetDetailContentWidth() - 60), 0),
				ForeColor = AppTheme.SecondaryText,
				Font = new Font("Segoe UI", 9.2f, FontStyle.Regular, GraphicsUnit.Point),
				Text = subtitle,
				Margin = Padding.Empty,
				Location = new Point(18, label.Bottom + 6)
			};
			roundedPanel.Controls.Add(value);
		}
		return roundedPanel;
	}

	private RoundedPanel CreateInnerCard()
	{
		return new RoundedPanel
		{
			BackColor = Color.Transparent,
			SurfaceColor = Color.FromArgb(20, 28, 39),
			BorderColor = Color.FromArgb(52, 66, 84),
			BorderThickness = 1,
			CornerRadius = 14,
			Margin = Padding.Empty
		};
	}

	private Label CreateSectionCaption(string text)
	{
		return new Label
		{
			AutoSize = true,
			ForeColor = AppTheme.PrimaryText,
			Font = new Font("Segoe UI Semibold", 11f, FontStyle.Bold, GraphicsUnit.Point),
			Text = text,
			Margin = Padding.Empty
		};
	}

	private Label CreateParagraphLabel(string text)
	{
		return new Label
		{
			AutoSize = true,
			MaximumSize = new Size(Math.Max(520, GetDetailContentWidth() - 92), 0),
			ForeColor = AppTheme.SecondaryText,
			Font = new Font("Segoe UI", 10f, FontStyle.Regular, GraphicsUnit.Point),
			Text = text,
			Margin = new Padding(0, 0, 0, 14)
		};
	}

	private Label CreateBulletLabel(string text)
	{
		return new Label
		{
			AutoSize = true,
			MaximumSize = new Size(Math.Max(240, GetDetailContentWidth() / 2 - 80), 0),
			ForeColor = AppTheme.SecondaryText,
			Font = new Font("Segoe UI", 9.4f, FontStyle.Regular, GraphicsUnit.Point),
			Text = "• " + text,
			Margin = new Padding(0, 0, 0, 10)
		};
	}

	private Label CreateChip(string text, Color background, Color foreground)
	{
		return new Label
		{
			AutoSize = true,
			BackColor = background,
			ForeColor = foreground,
			Font = new Font("Segoe UI Semibold", 8.9f, FontStyle.Bold, GraphicsUnit.Point),
			Padding = new Padding(10, 6, 10, 6),
			Margin = new Padding(0, 0, 10, 10),
			Text = text
		};
	}

	private Control CreateEditionCard(DesktopLibraryEditionResponse edition)
	{
		RoundedPanel roundedPanel = CreateInnerCard();
		roundedPanel.Padding = new Padding(16);
		roundedPanel.Size = new Size(250, 170);
		roundedPanel.Margin = new Padding(0, 0, 14, 14);
		roundedPanel.SurfaceColor = (edition.IsOwned ? Color.FromArgb(31, 24, 29) : Color.FromArgb(20, 28, 39));
		roundedPanel.BorderColor = (edition.IsOwned ? Color.FromArgb(203, 66, 74) : Color.FromArgb(52, 66, 84));
		Label label = CreateChip(edition.IsOwned ? "Bạn đã sở hữu" : "Tham chiếu", Color.FromArgb(edition.IsOwned ? 154 : 84, 184, 40, 49), Color.FromArgb(255, 239, 240));
		label.Location = new Point(16, 16);
		Label value = new Label
		{
			AutoSize = false,
			Location = new Point(16, 48),
			Size = new Size(218, 40),
			ForeColor = AppTheme.PrimaryText,
			Font = new Font("Segoe UI Semibold", 10.8f, FontStyle.Bold, GraphicsUnit.Point),
			Text = edition.Name
		};
		Label value2 = new Label
		{
			AutoSize = false,
			Location = new Point(16, 92),
			Size = new Size(218, 48),
			ForeColor = AppTheme.SecondaryText,
			Font = new Font("Segoe UI", 8.8f, FontStyle.Regular, GraphicsUnit.Point),
			Text = StripHtml(edition.Note)
		};
		Label value3 = new Label
		{
			AutoSize = false,
			Location = new Point(16, 142),
			Size = new Size(218, 24),
			ForeColor = (edition.IsOwned ? Color.FromArgb(255, 124, 124) : AppTheme.Accent),
			Font = new Font("Segoe UI Semibold", 10.2f, FontStyle.Bold, GraphicsUnit.Point),
			Text = edition.CurrentPriceText
		};
		roundedPanel.Controls.Add(value3);
		roundedPanel.Controls.Add(value2);
		roundedPanel.Controls.Add(value);
		roundedPanel.Controls.Add(label);
		return roundedPanel;
	}

	private Control CreateRequirementCard(DesktopLibraryRequirementResponse requirement)
	{
		RoundedPanel roundedPanel = CreateInnerCard();
		roundedPanel.Padding = new Padding(16);
		roundedPanel.Size = new Size(Math.Max(280, GetDetailContentWidth() / 2 - 22), 196);
		roundedPanel.Margin = new Padding(0, 0, 14, 14);
		Label value = new Label
		{
			AutoSize = true,
			ForeColor = AppTheme.PrimaryText,
			Font = new Font("Segoe UI Semibold", 11.2f, FontStyle.Bold, GraphicsUnit.Point),
			Text = requirement.Platform,
			Location = new Point(16, 14)
		};
		Label value2 = new Label
		{
			AutoSize = false,
			Location = new Point(16, 44),
			Size = new Size(roundedPanel.Width - 32, roundedPanel.Height - 60),
			ForeColor = AppTheme.SecondaryText,
			Font = new Font("Segoe UI", 8.8f, FontStyle.Regular, GraphicsUnit.Point),
			Text = NormalizeRequirementText(requirement.Html)
		};
		roundedPanel.Controls.Add(value2);
		roundedPanel.Controls.Add(value);
		return roundedPanel;
	}

	private Control CreateReviewCard(DesktopLibraryReviewResponse review)
	{
		RoundedPanel roundedPanel = CreateInnerCard();
		roundedPanel.Padding = new Padding(16);
		roundedPanel.Width = Math.Max(520, GetDetailContentWidth() - 34);
		roundedPanel.AutoSize = true;
		roundedPanel.AutoSizeMode = AutoSizeMode.GrowAndShrink;
		roundedPanel.Margin = new Padding(0, 0, 0, 14);
		RoundedPanel roundedPanel2 = new RoundedPanel
		{
			Size = new Size(40, 40),
			SurfaceColor = Color.FromArgb(44, 57, 78),
			BorderColor = Color.FromArgb(64, 79, 103),
			BorderThickness = 1,
			CornerRadius = 20,
			Location = new Point(16, 16)
		};
		roundedPanel2.Controls.Add(new Label
		{
			Dock = DockStyle.Fill,
			TextAlign = ContentAlignment.MiddleCenter,
			ForeColor = Color.White,
			Font = new Font("Segoe UI Semibold", 11f, FontStyle.Bold, GraphicsUnit.Point),
			Text = review.AvatarText
		});
		Label value = new Label
		{
			AutoSize = true,
			Location = new Point(68, 16),
			ForeColor = AppTheme.PrimaryText,
			Font = new Font("Segoe UI Semibold", 10.2f, FontStyle.Bold, GraphicsUnit.Point),
			Text = review.DisplayName
		};
		Label value2 = new Label
		{
			AutoSize = true,
			Location = new Point(68, 40),
			ForeColor = AppTheme.SecondaryText,
			Font = new Font("Segoe UI", 8.8f, FontStyle.Regular, GraphicsUnit.Point),
			Text = review.RatingText + " • " + review.CreatedAtText
		};
		Label value3 = new Label
		{
			AutoSize = true,
			MaximumSize = new Size(Math.Max(480, GetDetailContentWidth() - 84), 0),
			Location = new Point(16, 70),
			ForeColor = AppTheme.SecondaryText,
			Font = new Font("Segoe UI", 9.2f, FontStyle.Regular, GraphicsUnit.Point),
			Text = (string.IsNullOrWhiteSpace(review.ReviewText) ? "Người dùng chưa để lại nội dung chi tiết." : review.ReviewText)
		};
		roundedPanel.Controls.Add(value3);
		roundedPanel.Controls.Add(value2);
		roundedPanel.Controls.Add(value);
		roundedPanel.Controls.Add(roundedPanel2);
		return roundedPanel;
	}

	private void AddMetaRow(TableLayoutPanel grid, int rowIndex, string labelText, string valueText, Color valueColor)
	{
		grid.Controls.Add(new Label
		{
			AutoSize = true,
			ForeColor = AppTheme.SecondaryText,
			Font = new Font("Segoe UI", 9.1f, FontStyle.Regular, GraphicsUnit.Point),
			Text = labelText,
			Margin = new Padding(0, 4, 12, 0)
		}, 0, rowIndex);
		grid.Controls.Add(new Label
		{
			AutoSize = true,
			ForeColor = valueColor,
			Font = new Font("Segoe UI Semibold", 9.6f, FontStyle.Bold, GraphicsUnit.Point),
			Text = valueText,
			Margin = new Padding(0, 4, 0, 0)
		}, 1, rowIndex);
	}

	private static int GetSectionContentTop(Control section)
	{
		int num = 18;
		foreach (Control control in section.Controls)
		{
			num = Math.Max(num, control.Bottom);
		}
		return num + 8;
	}

	private async Task LoadDetailArtworkAsync(PictureBox pictureBox, DesktopLibraryDetailResponse detail)
	{
		StoreGame game = CreatePreviewGame(detail);
		using Bitmap fallbackImage = StoreArtworkFactory.CreateBanner(game, new Size(Math.Max(320, pictureBox.Width), Math.Max(240, pictureBox.Height)));
		pictureBox.Image = new Bitmap(fallbackImage);
		if (string.IsNullOrWhiteSpace(detail.CoverImageUrl))
		{
			return;
		}
		Size targetSize = new Size(Math.Max(320, pictureBox.Width), Math.Max(240, pictureBox.Height));
		if (StoreRemoteImageCache.TryGetPrepared(detail.CoverImageUrl, targetSize, StoreImageResizeMode.Stretch, out Image image) && image != null)
		{
			pictureBox.Image = image;
			return;
		}
		Image image2 = await StoreRemoteImageCache.GetPreparedAsync(detail.CoverImageUrl, targetSize, StoreImageResizeMode.Stretch);
		if (!pictureBox.IsDisposed)
		{
			pictureBox.Image = image2 ?? pictureBox.Image;
		}
	}

	private StoreGame CreatePreviewGame(DesktopLibraryDetailResponse detail)
	{
		StoreGamePalette storeGamePalette = StoreGamePalette.FromSeed($"{detail.GameId:N}:{detail.GameName}:{detail.VersionName}");
		StoreGame storeGame = new StoreGame();
		storeGame.GameId = detail.GameId;
		storeGame.Title = detail.GameName;
		storeGame.Subtitle = detail.VersionName;
		storeGame.Category = detail.SummaryTags.FirstOrDefault() ?? "Library";
		storeGame.Genres = ((detail.SummaryTags.Count > 0) ? ((IReadOnlyList<string>)detail.SummaryTags.Take(3).ToArray()) : ((IReadOnlyList<string>)new string[1] { "Library" }));
		storeGame.BadgeText = "Đã sở hữu";
		storeGame.PosterImageUrl = detail.CoverImageUrl;
		storeGame.SpotlightImageUrl = detail.CoverImageUrl;
		storeGame.OriginalPrice = 0m;
		storeGame.SalePrice = 0m;
		storeGame.Likes = 0;
		storeGame.Dislikes = 0;
		storeGame.PurchaseCount = 0;
		storeGame.PromoLabel = "PLAY";
		storeGame.IsTrending = false;
		storeGame.PrimaryColor = storeGamePalette.PrimaryColor;
		storeGame.SecondaryColor = storeGamePalette.SecondaryColor;
		storeGame.AccentColor = storeGamePalette.AccentColor;
		return storeGame;
	}

	private void ShowStatusCard(string title, string message)
	{
		detailWebView.Visible = false;
		detailScrollPanel.Visible = true;
		detailStack.SuspendLayout();
		detailStack.Controls.Clear();
		detailStack.RowStyles.Clear();
		detailStack.RowCount = 0;
		RoundedPanel roundedPanel = CreateSectionShell(title, message);
		roundedPanel.Height = 220;
		roundedPanel.Controls.Add(new Label
		{
			AutoSize = true,
			MaximumSize = new Size(Math.Max(420, GetDetailContentWidth() - 80), 0),
			ForeColor = AppTheme.SecondaryText,
			Font = new Font("Segoe UI", 10.2f, FontStyle.Regular, GraphicsUnit.Point),
			Text = message,
			Location = new Point(18, GetSectionContentTop(roundedPanel))
		});
		AddDetailSection(roundedPanel);
		detailStack.ResumeLayout(performLayout: true);
	}

	private Task ActivateCurrentGameAsync()
	{
		if (currentDetail != null)
		{
			return ActivateGameAsync(currentDetail);
		}
		return Task.CompletedTask;
	}

	private Task ApplyCurrentDenuvoFixAsync()
	{
		if (currentDetail != null)
		{
			return ApplyDenuvoFixAsync(currentDetail);
		}
		return Task.CompletedTask;
	}

	private Task ApplyCurrentPypassFixAsync()
	{
		if (currentDetail != null)
		{
			return ApplyPypassFixAsync(currentDetail);
		}
		return Task.CompletedTask;
	}

	private Task ApplyCurrentOnlineFixAsync()
	{
		if (currentDetail != null)
		{
			return ApplyOnlineFixAsync(currentDetail);
		}
		return Task.CompletedTask;
	}

	private void HandleDenuvoActiveTokenApplied(object? sender, DesktopDenuvoActiveTokenAppliedEventArgs payload)
	{
		if (!base.IsDisposed && currentDetail != null && !(currentDetail.LibraryId != payload.LibraryId))
		{
			payload.Handled = true;
			if (base.InvokeRequired)
			{
				BeginInvoke(Dispatch);
			}
			else
			{
				Dispatch();
			}
		}
		void Dispatch()
		{
			HandleDenuvoTokenAppliedUiAsync(payload);
		}
	}

	private async Task HandleDenuvoTokenAppliedUiAsync(DesktopDenuvoActiveTokenAppliedEventArgs payload)
	{
		StoreUiHelper.RestoreWindowToForeground(FindForm());
		await ShowDenuvoTokenAppliedPopupAsync(payload);
	}

	private void HandleDenuvoActiveRequestCancelled(object? sender, DesktopDenuvoActiveRequestCancelledEventArgs payload)
	{
		if (!base.IsDisposed && currentDetail != null && !(currentDetail.LibraryId != payload.LibraryId))
		{
			payload.Handled = true;
			if (base.InvokeRequired)
			{
				BeginInvoke(Dispatch);
			}
			else
			{
				Dispatch();
			}
		}
		void Dispatch()
		{
			ShowDenuvoCancelledPopupAsync(payload);
		}
	}

	private void HandleDenuvoActiveCooldownUnlocked(object? sender, DesktopDenuvoActiveCooldownUnlockedEventArgs payload)
	{
		if (base.IsDisposed)
		{
			return;
		}
		if (base.InvokeRequired)
		{
			BeginInvoke(delegate
			{
				HandleDenuvoActiveCooldownUnlocked(sender, payload);
			});
			return;
		}
		denuvoGuidePayloadJsonByLibraryId.Remove(payload.LibraryId);
		if (currentDetail != null && !(currentDetail.LibraryId != payload.LibraryId))
		{
			payload.Handled = true;
			SetDenuvoUnlockedStateAsync();
		}
	}

	private async Task SetActivationLoadingStateAsync(bool isLoading)
	{
		await EnsureDetailWebViewAsync();
		if (!detailWebView.IsDisposed && detailWebView.CoreWebView2 != null)
		{
			detailWebView.CoreWebView2.PostWebMessageAsString(isLoading ? "activation:start" : "activation:finish");
		}
	}

	private async Task SetDenuvoLoadingStateAsync(bool isLoading)
	{
		await EnsureDetailWebViewAsync();
		if (!detailWebView.IsDisposed && detailWebView.CoreWebView2 != null)
		{
			detailWebView.CoreWebView2.PostWebMessageAsString(isLoading ? "denuvo:start" : "denuvo:finish");
		}
	}

	private async Task SetDenuvoAppliedStateAsync(DateTime? cooldownUntilUtc = null)
	{
		DesktopLibraryDetailResponse expectedDetail = currentDetail;
		int expectedDocument = detailRequestId;
		await EnsureDetailWebViewAsync();
		if (!disposalCancellation.IsCancellationRequested && currentDetail == expectedDetail && detailRequestId == expectedDocument && !detailWebView.IsDisposed && detailWebView.CoreWebView2 != null)
		{
			string webMessageAsString = (cooldownUntilUtc.HasValue ? $"denuvo:cooldown:{cooldownUntilUtc.Value.ToUniversalTime():O}" : "denuvo:applied");
			detailWebView.CoreWebView2.PostWebMessageAsString(webMessageAsString);
		}
	}

	private async Task SetDenuvoUnlockedStateAsync()
	{
		await EnsureDetailWebViewAsync();
		if (!detailWebView.IsDisposed && detailWebView.CoreWebView2 != null)
		{
			detailWebView.CoreWebView2.PostWebMessageAsString("denuvo:unlocked");
		}
	}

	private async Task SetPypassLoadingStateAsync(bool isLoading)
	{
		await EnsureDetailWebViewAsync();
		if (!detailWebView.IsDisposed && detailWebView.CoreWebView2 != null)
		{
			detailWebView.CoreWebView2.PostWebMessageAsString(isLoading ? "pypass:start" : "pypass:finish");
		}
	}

	private async Task SetPypassAppliedStateAsync()
	{
		await EnsureDetailWebViewAsync();
		if (!detailWebView.IsDisposed && detailWebView.CoreWebView2 != null)
		{
			detailWebView.CoreWebView2.PostWebMessageAsString("pypass:applied");
		}
	}

	private async Task SetOnlineLoadingStateAsync(bool isLoading)
	{
		await EnsureDetailWebViewAsync();
		if (!detailWebView.IsDisposed && detailWebView.CoreWebView2 != null)
		{
			detailWebView.CoreWebView2.PostWebMessageAsString(isLoading ? "online:start" : "online:finish");
		}
	}

	private async Task SetOnlineAppliedStateAsync()
	{
		await EnsureDetailWebViewAsync();
		if (!detailWebView.IsDisposed && detailWebView.CoreWebView2 != null)
		{
			detailWebView.CoreWebView2.PostWebMessageAsString("online:applied");
		}
	}

	private async Task ActivateGameAsync(DesktopLibraryDetailResponse detail)
	{
		if (isActivatingGame)
		{
			DesktopDiagnosticLog.Write("CloudRedirectActivation.LegacyShell", "DuplicateRequestIgnored", $"LibraryId={detail.LibraryId}");
			return;
		}
		isActivatingGame = true;
		bool activationUiEntered = false;
		GameActivationProgressQueue progress = null;
		GameActivationLog log = new GameActivationLog(detail, DesktopAppContext.Settings.CurrentVersion);
		activationLogDownload.SetCurrent(log);
		try
		{
			DesktopDiagnosticLog.Write("CloudRedirectActivation.LegacyShell", "Requested", $"LibraryId={detail.LibraryId}; SteamAppId={detail.SteamAppId}");
			base.UseWaitCursor = true;
			activationUiEntered = true;
			await SetActivationLoadingStateAsync(isLoading: true);
			log.Add("Stage=WindowsArchitecture");
			GameActivationPreflight.ValidateWindowsArchitecture(Environment.Is64BitOperatingSystem);
			log.Add("Stage=Authentication");
			string accessToken = await DesktopAppContext.AuthSessionManager.GetAccessTokenAsync(disposalCancellation.Token);
			if (string.IsNullOrWhiteSpace(accessToken))
			{
				throw new InvalidOperationException("Phiên đăng nhập đã hết hạn. Hãy đăng nhập lại để active game.");
			}
			GameActivationProgress progress2 = new GameActivationProgress(GameActivationProgressStage.Checking, "Đang kiểm tra hệ thống...", null);
			await ShowGameActivationProgressPopupAsync(detail, progress2);
			progress = new GameActivationProgressQueue((GameActivationProgress update) => ShowGameActivationProgressPopupAsync(detail, update), disposalCancellation.Token);
			GameActivationResult result = await activationService.ActivateAsync(detail, accessToken, disposalCancellation.Token, progress, log);
			await progress.FlushAsync();
			if (result.SteamInstall.ActivationSucceeded)
			{
				await TryReportDenuvoActiveActivationCompletedAsync(detail, accessToken);
			}
			DesktopDiagnosticLog.Write("CloudRedirectActivation.LegacyShell", "Completed", $"LibraryId={detail.LibraryId}; HealthReady={result.SteamInstall.CloudRedirectHealth?.IsReady}; SteamRestarted={result.SteamInstall.IsSteamRestarted}; RolledBack={result.SteamInstall.WasRolledBack}");
			await ShowActivationResultPopupAsync(detail, result);
		}
		catch (OperationCanceledException) when (disposalCancellation.IsCancellationRequested)
		{
			log.Finish("CANCELLED");
			DesktopDiagnosticLog.Write("CloudRedirectActivation.LegacyShell", "CancelledDuringShutdown", $"LibraryId={detail.LibraryId}");
		}
		catch (Exception ex2)
		{
			log.Fail(ex2);
			if (progress != null)
			{
				await progress.FlushAsync();
			}
			DesktopDiagnosticLog.Write("CloudRedirectActivation.LegacyShell", "Failed", $"LibraryId={detail.LibraryId}; ErrorType={ex2.GetType().Name}");
			await ShowActivationErrorPopupAsync(detail, GameActivationUserMessage.FromException(ex2), null, log);
		}
		finally
		{
			if (progress != null)
			{
				await progress.FlushAsync();
			}
			if (activationUiEntered)
			{
				try
				{
					await SetActivationLoadingStateAsync(isLoading: false);
				}
				catch (Exception exception)
				{
					DesktopDiagnosticLog.Write("CloudRedirectActivation.LegacyShell", "ResetUiFailed", $"LibraryId={detail.LibraryId}", exception);
				}
			}
			base.UseWaitCursor = false;
			isActivatingGame = false;
		}
	}

	private Task ShowActivationResultPopupAsync(DesktopLibraryDetailResponse detail, GameActivationResult result)
	{
		SteamInstallResult steamInstall = result.SteamInstall;
		CloudRedirectActivationUiState cloudRedirectActivationUiState = CloudRedirectActivationUiPolicy.Resolve(steamInstall);
		GameActivationLog log = ((activationLogDownload.CurrentLog?.Id == result.ActivationLogId) ? activationLogDownload.CurrentLog : null);
		switch (cloudRedirectActivationUiState)
		{
		case CloudRedirectActivationUiState.AttemptFailed:
			return ShowActivationErrorPopupAsync(detail, steamInstall.FailureMessage, null, log);
		case CloudRedirectActivationUiState.VerificationPending:
			return ShowActivationPopupAsync(new
			{
				state = "warning",
				gameName = detail.GameName,
				title = "Chưa hoàn tất Active",
				badgeText = "Chờ kiểm tra",
				metaText = BuildActivationModalMeta(detail),
				summary = steamInstall.RecoveryMessage,
				metrics = Array.Empty<object>(),
				paths = Array.Empty<object>(),
				messages = new string[1] { "Hãy kiểm tra Steam rồi bấm ACTIVE để tiếp tục lần đang chờ." },
				technicalDetails = BuildActivationTechnicalDetails(result),
				primaryActionText = "Đã hiểu"
			});
		default:
			if (steamInstall.AutoRecoveryAttempted && !steamInstall.RollbackFailed)
			{
				bool steamRecovered = steamInstall.SteamRecovered;
				return ShowActivationPopupAsync(new
				{
					state = (steamRecovered ? "warning" : "error"),
					gameName = detail.GameName,
					title = (steamRecovered ? "Steam đã chạy lại, ACTIVE chưa hoàn tất" : "Steam chưa khởi động được"),
					badgeText = (steamRecovered ? "Đã tự phục hồi" : "Phục hồi chưa hoàn tất"),
					metaText = BuildActivationModalMeta(detail),
					summary = steamInstall.RecoveryMessage,
					metrics = Array.Empty<object>(),
					paths = Array.Empty<object>(),
					messages = new string[1] { "ACTIVE chưa thành công. Cần kiểm tra nguyên nhân lỗi trước lần ACTIVE tiếp theo." },
					technicalDetails = BuildActivationTechnicalDetails(result),
					primaryActionText = "Đã hiểu"
				});
			}
			switch (cloudRedirectActivationUiState)
			{
			case CloudRedirectActivationUiState.RollbackFailed:
				return ShowActivationPopupAsync(new
				{
					state = "error",
					gameName = detail.GameName,
					title = (steamInstall.SteamRecovered ? "Steam đã chạy lại, rollback chưa hoàn tất" : "Chưa khôi phục xong cài đặt"),
					badgeText = "Rollback chưa hoàn tất",
					metaText = BuildActivationModalMeta(detail),
					summary = "Launcher chưa khôi phục đầy đủ các file của lần ACTIVE lỗi. Giữ nguyên backup và kiểm tra chẩn đoán trước khi ACTIVE lại.",
					metrics = new[]
					{
						new
						{
							label = "Trạng thái",
							value = "Cần khôi phục",
							tone = "error"
						},
						new
						{
							label = "Phiên bản",
							value = (string.IsNullOrWhiteSpace(detail.VersionName) ? "Mặc định" : detail.VersionName),
							tone = "default"
						}
					},
					paths = Array.Empty<object>(),
					messages = new string[1] { "Mở chẩn đoán để kiểm tra backup và các file chưa được khôi phục." },
					technicalDetails = BuildActivationTechnicalDetails(result),
					primaryActionText = "Đã hiểu"
				});
			case CloudRedirectActivationUiState.HealthFailed:
				return ShowActivationPopupAsync(new
				{
					state = "error",
					gameName = detail.GameName,
					title = "Cloud Save không thể khởi tạo",
					badgeText = "Khởi tạo thất bại",
					metaText = BuildActivationModalMeta(detail),
					summary = "Launcher đã xác nhận Cloud Save gặp lỗi. Không mở game cho đến khi kiểm tra hoặc repair Cloud Save hoàn tất.",
					metrics = new[]
					{
						new
						{
							label = "Trạng thái",
							value = "Cloud Save lỗi",
							tone = "error"
						},
						new
						{
							label = "Phiên bản",
							value = (string.IsNullOrWhiteSpace(detail.VersionName) ? "Mặc định" : detail.VersionName),
							tone = "default"
						}
					},
					paths = Array.Empty<object>(),
					messages = new string[1] { "Hãy thử Active lại. Nếu lỗi tiếp tục, mở Chẩn đoán Cloud Save để kiểm tra." },
					technicalDetails = BuildActivationTechnicalDetails(result),
					primaryActionText = "Đã hiểu"
				});
			default:
			{
				bool flag = cloudRedirectActivationUiState == CloudRedirectActivationUiState.Success;
				string text = (flag ? "success" : "warning");
				string title = ((cloudRedirectActivationUiState == CloudRedirectActivationUiState.RolledBack) ? "Không thể hoàn tất Active" : (flag ? "Kích hoạt thành công" : (cloudRedirectActivationUiState switch
				{
					CloudRedirectActivationUiState.NeedsSteamRestart => "Cần mở lại Steam", 
					CloudRedirectActivationUiState.NeedsCloudCheck => "Cần kiểm tra Cloud Save", 
					_ => "Chưa tìm thấy Steam", 
				})));
				string summary = ((cloudRedirectActivationUiState == CloudRedirectActivationUiState.RolledBack) ? "Launcher không thể hoàn tất sửa lỗi và đã khôi phục trạng thái trước đó. Save game không nằm trong backup của launcher." : (flag ? "Đã sửa lỗi và kích hoạt game thành công." : (cloudRedirectActivationUiState switch
				{
					CloudRedirectActivationUiState.NeedsSteamRestart => "File đã được copy, bạn hãy mở lại Steam rồi thử game.", 
					CloudRedirectActivationUiState.NeedsCloudCheck => "Launcher chưa thể xác nhận Cloud Save hoạt động sau khi Steam khởi động.", 
					_ => "Launcher chưa phát hiện Steam trên máy này.", 
				})));
				string value = ((cloudRedirectActivationUiState == CloudRedirectActivationUiState.RolledBack) ? "Đã khôi phục" : (flag ? "Hoàn tất" : (cloudRedirectActivationUiState switch
				{
					CloudRedirectActivationUiState.NeedsSteamRestart => "Cần mở Steam", 
					CloudRedirectActivationUiState.NeedsCloudCheck => "Kiểm tra Cloud", 
					_ => "Cần kiểm tra", 
				})));
				return ShowActivationPopupAsync(new
				{
					state = text,
					gameName = detail.GameName,
					title = title,
					badgeText = (flag ? "Hoàn tất" : "Cần kiểm tra"),
					metaText = BuildActivationModalMeta(detail),
					summary = summary,
					metrics = new[]
					{
						new
						{
							label = "Trạng thái",
							value = value,
							tone = text
						},
						new
						{
							label = "Phiên bản",
							value = (string.IsNullOrWhiteSpace(detail.VersionName) ? "Chưa xác định" : detail.VersionName.Trim()),
							tone = "default"
						}
					},
					paths = Array.Empty<object>(),
					messages = BuildActivationResultMessages(cloudRedirectActivationUiState),
					technicalDetails = (flag ? Array.Empty<string>() : BuildActivationTechnicalDetails(result)),
					primaryActionText = (flag ? "Hoàn tất" : "Đã hiểu"),
					compactSuccess = flag
				});
			}
			}
		}
	}

	private Task ShowActivationErrorPopupAsync(DesktopLibraryDetailResponse detail, string errorMessage, string? technicalError = null, GameActivationLog? log = null)
	{
		string text = (string.IsNullOrWhiteSpace(errorMessage) ? "Không thể kích hoạt game lúc này." : errorMessage.Trim());
		return ShowActivationPopupAsync(new
		{
			state = "error",
			gameName = detail.GameName,
			title = "Không thể kích hoạt",
			badgeText = "Cần thử lại",
			metaText = BuildActivationModalMeta(detail),
			summary = text,
			metrics = new[]
			{
				new
				{
					label = "Trạng thái",
					value = "Cần thử lại",
					tone = "error"
				},
				new
				{
					label = "Phiên bản",
					value = (string.IsNullOrWhiteSpace(detail.VersionName) ? "Chưa xác định" : detail.VersionName.Trim()),
					tone = "default"
				}
			},
			paths = Array.Empty<object>(),
			messages = new string[2]
			{
				text,
				(log != null) ? "Bạn có thể tải file log để gửi cho bộ phận hỗ trợ." : "Hãy chờ game tải xong, kiểm tra kết nối và thử lại."
			},
			fullSummary = true,
			singleCloseAction = true,
			downloadLogId = ((log != null && log.IsFailed) ? log.Id : null),
			technicalDetails = ((log != null || string.IsNullOrWhiteSpace(technicalError)) ? Array.Empty<string>() : new string[1] { technicalError.Trim() }),
			primaryActionText = "Kiểm tra lại"
		});
	}

	private static string[] BuildActivationResultMessages(CloudRedirectActivationUiState outcome)
	{
		return outcome switch
		{
			CloudRedirectActivationUiState.Success => new string[1] { "Bạn có thể mở game từ Steam." }, 
			CloudRedirectActivationUiState.RolledBack => new string[1] { "Bạn có thể thử Active lại. Nếu lỗi tiếp tục, mở CLOUD SAVE để chẩn đoán." }, 
			CloudRedirectActivationUiState.NeedsCloudCheck => new string[1] { "Hãy thử Active lại. Nếu lỗi tiếp tục, mở CLOUD SAVE để chẩn đoán." }, 
			CloudRedirectActivationUiState.HealthFailed => new string[1] { "Hãy thử Active lại. Nếu lỗi tiếp tục, mở CLOUD SAVE để chẩn đoán." }, 
			CloudRedirectActivationUiState.NeedsSteamRestart => new string[1] { "Hãy mở lại Steam rồi kiểm tra game trong thư viện." }, 
			_ => new string[1] { "Hãy mở Steam một lần, sau đó bấm Active lại." }, 
		};
	}

	private static string[] BuildActivationTechnicalDetails(GameActivationResult result)
	{
		SteamInstallResult steamInstall = result.SteamInstall;
		List<string> list = new List<string>();
		if (!string.IsNullOrWhiteSpace(steamInstall.SteamDirectory))
		{
			list.Add("Steam directory: " + steamInstall.SteamDirectory);
		}
		if (!string.IsNullOrWhiteSpace(steamInstall.CloudRedirectTransactionPath))
		{
			list.Add("Transaction: " + steamInstall.CloudRedirectTransactionPath);
		}
		if (!string.IsNullOrWhiteSpace(steamInstall.CloudRedirectBackupDirectory))
		{
			list.Add("Backup: " + steamInstall.CloudRedirectBackupDirectory);
		}
		CloudRedirectReadinessResult cloudRedirectReadiness = steamInstall.CloudRedirectReadiness;
		if ((object)cloudRedirectReadiness != null)
		{
			list.Add($"Readiness: {cloudRedirectReadiness.State} / {cloudRedirectReadiness.RecommendedAction}");
			list.AddRange(cloudRedirectReadiness.Issues.Select((string issue) => "Readiness issue: " + issue));
		}
		CloudRedirectHealthResult cloudRedirectHealth = steamInstall.CloudRedirectHealth;
		if ((object)cloudRedirectHealth != null)
		{
			list.Add($"Health: ready={cloudRedirectHealth.IsReady}, definitiveFailure={cloudRedirectHealth.IsDefinitiveFailure}");
			list.Add("Health message: " + cloudRedirectHealth.Message);
		}
		if (steamInstall.RollbackFailed && !string.IsNullOrWhiteSpace(steamInstall.RollbackError))
		{
			list.Add("Rollback error: " + steamInstall.RollbackError);
		}
		list.AddRange(steamInstall.Messages.Select((string message) => "Message: " + message));
		return list.Where((string item) => !string.IsNullOrWhiteSpace(item)).Distinct<string>(StringComparer.OrdinalIgnoreCase).ToArray();
	}

	private static string BuildActivationModalMeta(DesktopLibraryDetailResponse detail)
	{
		List<string> list = new List<string>();
		if (!string.IsNullOrWhiteSpace(detail.VersionName))
		{
			list.Add(detail.VersionName.Trim());
		}
		if (!string.IsNullOrWhiteSpace(detail.OrderCode))
		{
			list.Add("Order " + detail.OrderCode.Trim());
		}
		if (list.Count != 0)
		{
			return string.Join(" | ", list);
		}
		return "Launcher đang xử lý kích hoạt.";
	}

	private async Task TryReportDenuvoActiveActivationCompletedAsync(DesktopLibraryDetailResponse detail, string accessToken)
	{
		DesktopLibraryDenuvoFixResponse denuvoFix = detail.DenuvoFix;
		if (denuvoFix == null || !denuvoFix.HasDenuvoFix)
		{
			return;
		}
		try
		{
			await storefrontApiClient.PostDenuvoActiveGameActivationCompletedForLibraryAsync(detail.LibraryId, new DesktopDenuvoActiveClientStateRequest
			{
				ClientVersion = $"{DesktopAppContext.Settings.CurrentVersion}+{DesktopAppContext.Settings.CurrentBuildNumber}",
				Message = "Người dùng đã hoàn tất bước Active trong launcher."
			}, accessToken);
			await DesktopAppContext.DenuvoActiveRealtimeClient.ProcessPendingAsync();
		}
		catch
		{
		}
	}

	private void ShowActivationDialog(StoreActivationResultDialog dialog)
	{
		using (dialog)
		{
			Form form = FindForm();
			if (form == null)
			{
				dialog.ShowDialog();
			}
			else
			{
				dialog.ShowDialog(form);
			}
		}
	}

	private async Task ApplyDenuvoFixAsync(DesktopLibraryDetailResponse detail)
	{
		if (isApplyingDenuvoFix)
		{
			return;
		}
		isApplyingDenuvoFix = true;
		long uiGeneration = denuvoActivationUiState.Begin(detail.LibraryId);
		deferredDenuvoUiResults.Clear(detail.LibraryId);
		base.UseWaitCursor = true;
		CancellationToken cancellationToken = disposalCancellation.Token;
		await SetDenuvoLoadingStateAsync(isLoading: true);
		DenuvoProgressQueue progress = new DenuvoProgressQueue((DenuvoInstallProgress update) => (!denuvoActivationUiState.CanShowLoading(detail.LibraryId, uiGeneration) || currentDetail != detail) ? Task.CompletedTask : ShowDenuvoInstallProgressPopupAsync(detail, update), cancellationToken);
		try
		{
			string accessToken = await DesktopAppContext.AuthSessionManager.GetAccessTokenAsync(cancellationToken);
			if (string.IsNullOrWhiteSpace(accessToken))
			{
				throw new InvalidOperationException("Phiên đăng nhập đã hết hạn. Hãy đăng nhập lại để nạp game.");
			}
			DateTime? adminRequestCooldownUntilUtc = DenuvoFixInstallService.GetAdminRequestCooldownUntilUtc(detail.LibraryId);
			if (adminRequestCooldownUntilUtc.HasValue)
			{
				DateTime valueOrDefault = adminRequestCooldownUntilUtc.GetValueOrDefault();
				throw new InvalidOperationException(DenuvoFixInstallService.BuildAdminRequestCooldownMessage(valueOrDefault));
			}
			await ShowDenuvoInstallProgressPopupAsync(detail);
			DenuvoFixInstallResult result = await denuvoFixInstallService.InstallAsync(detail, accessToken, FindForm(), cancellationToken, progress);
			Guid? activeRequestId = result.ActiveRequestId;
			if (activeRequestId.HasValue)
			{
				Guid valueOrDefault2 = activeRequestId.GetValueOrDefault();
				if (valueOrDefault2 != Guid.Empty)
				{
					denuvoActivationUiState.BindRequest(detail.LibraryId, uiGeneration, valueOrDefault2);
				}
			}
			await progress.FlushAsync();
			if (currentDetail == detail && denuvoActivationUiState.CanShowLoading(detail.LibraryId, uiGeneration))
			{
				await SetDenuvoAppliedStateAsync(null);
			}
			activeRequestId = result.ActiveRequestId;
			if (activeRequestId.HasValue)
			{
				Guid valueOrDefault3 = activeRequestId.GetValueOrDefault();
				await ReplayDeferredDenuvoResultAsync(detail, valueOrDefault3);
			}
			if (denuvoActivationUiState.CanShowLoading(detail.LibraryId, uiGeneration))
			{
				await ShowDenuvoWaitingAdminPopupAsync(detail, result);
			}
			await DesktopAppContext.DenuvoActiveRealtimeClient.ProcessPendingAsync();
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
		}
		catch (Exception ex2)
		{
			await progress.FlushAsync();
			deferredDenuvoUiResults.Clear(detail.LibraryId);
			if (currentDetail == detail && denuvoActivationUiState.TryFail(detail.LibraryId, uiGeneration))
			{
				await ShowDenuvoErrorPopupAsync(detail, string.IsNullOrWhiteSpace(ex2.Message) ? "Không thể nạp game Denuvo lúc này." : ex2.Message, (ex2 as DenuvoRollbackException)?.BackupDirectory);
			}
		}
		finally
		{
			if (!base.IsDisposed)
			{
				await SetDenuvoLoadingStateAsync(isLoading: false);
			}
			base.UseWaitCursor = false;
			isApplyingDenuvoFix = false;
		}
	}

	private async Task ApplyPypassFixAsync(DesktopLibraryDetailResponse detail)
	{
		if (isApplyingPypassFix)
		{
			return;
		}
		isApplyingPypassFix = true;
		base.UseWaitCursor = true;
		await SetPypassLoadingStateAsync(isLoading: true);
		CancellationTokenSource operationCancellation = (pypassInstallCancellation = new CancellationTokenSource());
		try
		{
			string accessToken = await DesktopAppContext.AuthSessionManager.GetAccessTokenAsync();
			if (string.IsNullOrWhiteSpace(accessToken))
			{
				throw new InvalidOperationException("Phiên đăng nhập đã hết hạn. Hãy đăng nhập lại để chạy Active-2.");
			}
			await ShowPypassInstallProgressPopupAsync(detail);
			Progress<PypassFixInstallProgress> progress = new Progress<PypassFixInstallProgress>(delegate(PypassFixInstallProgress item)
			{
				ShowPypassInstallProgressPopupAsync(detail, item);
			});
			PypassFixInstallResult result = await pypassFixInstallService.InstallAsync(detail, accessToken, FindForm(), operationCancellation.Token, progress);
			await SetPypassAppliedStateAsync();
			await ShowPypassSuccessPopupAsync(detail, result);
			currentDetail = detail;
		}
		catch (OperationCanceledException)
		{
			await ShowPypassErrorPopupAsync(detail, "Đã hủy tải package Pypass.");
		}
		catch (Exception ex2)
		{
			await ShowPypassErrorPopupAsync(detail, string.IsNullOrWhiteSpace(ex2.Message) ? "Không thể chạy Active-2 lúc này." : ex2.Message);
		}
		finally
		{
			await SetPypassLoadingStateAsync(isLoading: false);
			base.UseWaitCursor = false;
			isApplyingPypassFix = false;
			if (pypassInstallCancellation == operationCancellation)
			{
				pypassInstallCancellation = null;
			}
			operationCancellation.Dispose();
		}
	}

	private async Task ApplyOnlineFixAsync(DesktopLibraryDetailResponse detail)
	{
		if (isApplyingOnlineFix)
		{
			return;
		}
		isApplyingOnlineFix = true;
		base.UseWaitCursor = true;
		await SetOnlineLoadingStateAsync(isLoading: true);
		try
		{
			string accessToken = await DesktopAppContext.AuthSessionManager.GetAccessTokenAsync();
			if (string.IsNullOrWhiteSpace(accessToken))
			{
				throw new InvalidOperationException("Phiên đăng nhập đã hết hạn. Hãy đăng nhập lại để nạp game.");
			}
			await ShowOnlineInstallProgressPopupAsync(detail);
			OnlineFixInstallResult result = await onlineFixInstallService.InstallAsync(detail, accessToken, FindForm());
			await SetOnlineAppliedStateAsync();
			await ShowOnlineSuccessPopupAsync(detail, result);
			currentDetail = detail;
		}
		catch (Exception ex)
		{
			await ShowOnlineErrorPopupAsync(detail, string.IsNullOrWhiteSpace(ex.Message) ? "Không thể nạp game lúc này." : ex.Message);
		}
		finally
		{
			await SetOnlineLoadingStateAsync(isLoading: false);
			base.UseWaitCursor = false;
			isApplyingOnlineFix = false;
		}
	}

	private Task ShowDenuvoInstallProgressPopupAsync(DesktopLibraryDetailResponse detail, DenuvoInstallProgress? update = null)
	{
		if (currentDetail != detail || !denuvoActivationUiState.CanShowLoading(detail.LibraryId, denuvoActivationUiState.Capture(detail.LibraryId)))
		{
			return Task.CompletedTask;
		}
		return ShowActivationPopupAsync(new
		{
			state = "loading",
			gameName = detail.GameName,
			title = "Đang yêu cầu admin nạp game",
			badgeText = "Đang xử lý",
			metaText = BuildDenuvoModalMeta(detail),
			summary = "Launcher đang tải file fix và giải nén vào thư mục game. Vui lòng giữ launcher mở trong lúc xử lý.",
			metrics = new[]
			{
				new
				{
					label = "Trạng thái",
					value = (update?.Message ?? "Đang tải file fix"),
					tone = "default"
				},
				new
				{
					label = "Phiên bản",
					value = (string.IsNullOrWhiteSpace(detail.VersionName) ? "Mặc định" : detail.VersionName),
					tone = "default"
				}
			},
			paths = Array.Empty<object>(),
			messages = new string[3] { "Không tắt launcher trong quá trình tải và giải nén.", "Sau khi cài local xong, launcher sẽ báo admin để cấp token.", "Nếu cần hỗ trợ nhanh, gọi admin: 0947709029." },
			primaryActionText = "Để launcher chạy",
			loaderText = (((object)update != null && update.TotalFiles > 0) ? $"{update.Message}: {update.CompletedFiles}/{update.TotalFiles} file" : (update?.Message ?? "Đang tải file fix, kiểm tra hash và giải nén vào thư mục game..."))
		});
	}

	private Task ShowDenuvoWaitingAdminPopupAsync(DesktopLibraryDetailResponse detail, DenuvoFixInstallResult result)
	{
		if (currentDetail != detail || !denuvoActivationUiState.TryShowWaiting(detail.LibraryId, result.ActiveRequestId, null))
		{
			return Task.CompletedTask;
		}
		List<string> source = new List<string> { "Nếu bạn chưa thấy thành công thì hãy gọi cho admin trước 15 phút.", "Lưu ý nên gọi admin trước khi nạp game.", "NestG cảm ơn." };
		return ShowActivationPopupAsync(new
		{
			state = "loading",
			gameName = detail.GameName,
			title = "Đang chờ admin nạp game",
			badgeText = "Yêu cầu đã gửi",
			metaText = BuildDenuvoModalMeta(detail),
			summary = "Game đang được nạp và xử lý. Vui lòng đợi admin cấp token, hoặc gọi admin: 0947709029. Không tắt launcher trong lúc chờ.",
			metrics = new[]
			{
				new
				{
					label = "Trạng thái",
					value = "Chờ admin",
					tone = "warning"
				},
				new
				{
					label = "Revision",
					value = (result.ConfigRevisionNumber?.ToString() ?? "-"),
					tone = "default"
				},
				new
				{
					label = "Package",
					value = (result.PackageVersion ?? "-"),
					tone = "default"
				},
				new
				{
					label = "File",
					value = $"{Math.Max(0, result.WrittenFiles.Count)} file",
					tone = "default"
				}
			},
			paths = new[]
			{
				new
				{
					label = "Thư mục game",
					value = result.TargetDirectory
				},
				new
				{
					label = "Exe admin cấu hình",
					value = (result.MainExeRelativePath ?? "-")
				},
				new
				{
					label = "Config",
					value = (result.ConfigFullPath ?? "-")
				},
				new
				{
					label = "File chay game",
					value = (result.GameExePath ?? "-")
				},
				new
				{
					label = "Backup",
					value = result.BackupDirectory
				},
				new
				{
					label = "Log",
					value = result.LogFilePath
				}
			},
			messages = source.Distinct<string>(StringComparer.OrdinalIgnoreCase).ToArray(),
			primaryActionText = "Ẩn thông báo",
			loaderText = "Đang giữ kết nối realtime để nhận token từ admin...",
			lockClose = true,
			zaloQr = true
		});
	}

	private Task ShowDenuvoTokenAppliedPopupAsync(DesktopDenuvoActiveTokenAppliedEventArgs payload)
	{
		DesktopLibraryDetailResponse desktopLibraryDetailResponse = currentDetail;
		if (desktopLibraryDetailResponse == null || desktopLibraryDetailResponse.LibraryId != payload.LibraryId)
		{
			return Task.CompletedTask;
		}
		return ShowDenuvoTokenAppliedPopupCoreAsync(desktopLibraryDetailResponse, payload);
	}

	private async Task ShowDenuvoTokenAppliedPopupCoreAsync(DesktopLibraryDetailResponse detail, DesktopDenuvoActiveTokenAppliedEventArgs payload)
	{
		if (currentDetail == detail && denuvoActivationUiState.IsAwaitingRequest(detail.LibraryId))
		{
			deferredDenuvoUiResults.Remember(payload);
		}
		else if (currentDetail == detail && denuvoActivationUiState.TryComplete(detail.LibraryId, payload.ActiveRequestId))
		{
			long generation = denuvoActivationUiState.Capture(detail.LibraryId);
			int document = detailRequestId;
			await SetDenuvoAppliedStateAsync(payload.CooldownUntilUtc);
			DesktopDenuvoGuideResponse guide = await TryLoadDenuvoGuideAsync();
			if (!disposalCancellation.IsCancellationRequested && currentDetail == detail && document == detailRequestId && denuvoActivationUiState.IsCurrentTerminal(detail.LibraryId, generation, payload.ActiveRequestId))
			{
				object obj = BuildDenuvoTokenAppliedPopupPayload(detail, payload.GameName, payload.GameExePath, guide);
				denuvoGuidePayloadJsonByLibraryId[detail.LibraryId] = JsonSerializer.Serialize(obj, JsonOptions);
				await ShowActivationPopupAsync(obj);
			}
		}
	}

	private async Task ShowCloudRedirectManagementAsync(DesktopLibraryDetailResponse detail)
	{
		if (isManagingCloudRedirect || isActivatingGame)
		{
			return;
		}
		isManagingCloudRedirect = true;
		try
		{
			await ShowCloudRedirectManagementCoreAsync(detail);
		}
		finally
		{
			isManagingCloudRedirect = false;
		}
	}

	private async Task ShowCloudRedirectManagementCoreAsync(DesktopLibraryDetailResponse detail)
	{
		using CloudRedirectManagementForm dialog = new CloudRedirectManagementForm(await Task.Run((Func<CloudRedirectRuntimeStatus>)activationService.GetCloudRedirectStatus));
		if (dialog.ShowDialog(this) != DialogResult.OK)
		{
			return;
		}
		if (dialog.SelectedAction == CloudRedirectManagementAction.Repair)
		{
			isManagingCloudRedirect = false;
			await ActivateGameAsync(detail);
			return;
		}
		string text = ((dialog.SelectedAction == CloudRedirectManagementAction.Disable) ? "tắt CloudRedirect" : "rollback thay đổi CloudRedirect gần nhất");
		if (LauncherMessageBox.Show(this, "Launcher sẽ đóng/mở lại Steam để " + text + ".\n\nHãy đóng game và sao lưu save trước khi tiếp tục. Backup launcher không bao gồm save game.", "Xác nhận CloudRedirect", MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation) != DialogResult.Yes)
		{
			return;
		}
		isManagingCloudRedirect = true;
		base.UseWaitCursor = true;
		try
		{
			CloudRedirectManagementResult cloudRedirectManagementResult = ((dialog.SelectedAction != CloudRedirectManagementAction.Disable) ? (await activationService.RollbackLatestCloudRedirectAsync()) : (await activationService.DisableCloudRedirectAsync()));
			CloudRedirectManagementResult cloudRedirectManagementResult2 = cloudRedirectManagementResult;
			LauncherMessageBox.Show(this, $"{cloudRedirectManagementResult2.Message}\n\nBackup: {cloudRedirectManagementResult2.BackupDirectory}\nSteam restart: {(cloudRedirectManagementResult2.IsSteamRestarted ? "đã hoàn tất" : "hãy mở lại Steam thủ công")}", "CloudRedirect", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
		}
		catch (Exception ex)
		{
			LauncherMessageBox.Show(this, ex.Message, "Không thể cập nhật CloudRedirect", MessageBoxButtons.OK, MessageBoxIcon.Hand);
		}
		finally
		{
			base.UseWaitCursor = false;
			isManagingCloudRedirect = false;
		}
	}

	private static object BuildDenuvoTokenAppliedPopupPayload(DesktopLibraryDetailResponse detail, string? gameName, string? gameExePath, DesktopDenuvoGuideResponse? guide)
	{
		bool flag = !string.IsNullOrWhiteSpace(gameExePath);
		return new
		{
			state = "success",
			gameName = (string.IsNullOrWhiteSpace(gameName) ? detail.GameName : gameName.Trim()),
			title = "Nạp game thành công",
			badgeText = "Hoàn tất",
			metaText = BuildDenuvoModalMeta(detail),
			summary = (flag ? "Copy đường dẫn tạo shortcut bên dưới rồi làm theo hướng dẫn." : "Nạp game thành công nhưng chưa có đường dẫn tạo shortcut."),
			metrics = Array.Empty<object>(),
			paths = BuildDenuvoTokenAppliedPaths(gameExePath),
			messages = Array.Empty<string>(),
			primaryActionText = "Hoàn tất",
			compactSuccess = true,
			guide = guide,
			minimizeToGuide = true
		};
	}

	private static object[] BuildDenuvoTokenAppliedPaths(string? gameExePath)
	{
		if (!string.IsNullOrWhiteSpace(gameExePath))
		{
			return new object[1]
			{
				new
				{
					label = "Đường dẫn tạo shortcut",
					value = gameExePath,
					actionText = "Copy link",
					actionValue = gameExePath
				}
			};
		}
		return Array.Empty<object>();
	}

	private async Task CacheDenuvoGuidePayloadAsync(DesktopLibraryDetailResponse detail)
	{
		DenuvoFixCooldownGuideState cooldownState = DenuvoFixInstallService.GetAdminRequestCooldownGuideState(detail.LibraryId);
		if ((object)cooldownState == null)
		{
			denuvoGuidePayloadJsonByLibraryId.Remove(detail.LibraryId);
		}
		else if (!denuvoGuidePayloadJsonByLibraryId.ContainsKey(detail.LibraryId))
		{
			DesktopDenuvoGuideResponse guide = await TryLoadDenuvoGuideAsync();
			object value = BuildDenuvoTokenAppliedPopupPayload(detail, cooldownState.GameName, cooldownState.GameExePath, guide);
			denuvoGuidePayloadJsonByLibraryId[detail.LibraryId] = JsonSerializer.Serialize(value, JsonOptions);
		}
	}

	private string? ResolveDenuvoGuidePayloadJson(DesktopLibraryDetailResponse detail, DateTime? cooldownUntilUtc)
	{
		if (!cooldownUntilUtc.HasValue)
		{
			denuvoGuidePayloadJsonByLibraryId.Remove(detail.LibraryId);
			return null;
		}
		if (denuvoGuidePayloadJsonByLibraryId.TryGetValue(detail.LibraryId, out string value))
		{
			return value;
		}
		DenuvoFixCooldownGuideState adminRequestCooldownGuideState = DenuvoFixInstallService.GetAdminRequestCooldownGuideState(detail.LibraryId);
		if ((object)adminRequestCooldownGuideState == null)
		{
			return null;
		}
		value = JsonSerializer.Serialize(BuildDenuvoTokenAppliedPopupPayload(detail, adminRequestCooldownGuideState.GameName, adminRequestCooldownGuideState.GameExePath, null), JsonOptions);
		denuvoGuidePayloadJsonByLibraryId[detail.LibraryId] = value;
		return value;
	}

	private async Task<DesktopDenuvoGuideResponse?> TryLoadDenuvoGuideAsync()
	{
		try
		{
			return await storefrontApiClient.GetDenuvoGuideAsync();
		}
		catch
		{
			return null;
		}
	}

	private static string[] BuildDenuvoTokenAppliedMessages(DesktopDenuvoActiveTokenAppliedEventArgs payload)
	{
		List<string> list = (from message in payload.Messages
			where !string.IsNullOrWhiteSpace(message)
			select message.Trim()).ToList();
		if (string.IsNullOrWhiteSpace(payload.GameExePath))
		{
			list.Add("Chưa có link file chạy game. Kiểm tra MainExeRelativePath trong admin.");
		}
		else
		{
			list.Add("Hãy mở game bằng link file chạy game hiện trong popup.");
		}
		return list.Distinct<string>(StringComparer.OrdinalIgnoreCase).ToArray();
	}

	private Task ShowDenuvoErrorPopupAsync(DesktopLibraryDetailResponse detail, string errorMessage, string? backupDirectory = null)
	{
		string text = (string.IsNullOrWhiteSpace(errorMessage) ? "Không thể nạp game Denuvo lúc này." : errorMessage.Trim());
		return ShowActivationPopupAsync(new
		{
			state = "error",
			gameName = detail.GameName,
			title = ((backupDirectory == null) ? "Không thể nạp game Denuvo" : "Chưa khôi phục xong Denuvo"),
			badgeText = "Cần kiểm tra",
			metaText = BuildDenuvoModalMeta(detail),
			summary = text,
			metrics = new[]
			{
				new
				{
					label = "Trạng thái",
					value = ((backupDirectory == null) ? "Cần thử lại" : "Rollback chưa hoàn tất"),
					tone = "error"
				},
				new
				{
					label = "Phiên bản",
					value = (string.IsNullOrWhiteSpace(detail.VersionName) ? "Mặc định" : detail.VersionName),
					tone = "default"
				}
			},
			paths = ((backupDirectory == null) ? Array.Empty<object>() : new object[1]
			{
				new
				{
					label = "Backup",
					value = backupDirectory
				}
			}),
			messages = new string[2]
			{
				text,
				(backupDirectory == null) ? "Hãy kiểm tra kết nối, quyền ghi thư mục game và thử lại sau." : "Giữ nguyên backup và kiểm tra các file chưa được khôi phục trước khi nạp lại."
			},
			primaryActionText = "Đóng"
		});
	}

	private Task ShowDenuvoCancelledPopupAsync(DesktopDenuvoActiveRequestCancelledEventArgs payload)
	{
		DesktopLibraryDetailResponse desktopLibraryDetailResponse = currentDetail;
		if (desktopLibraryDetailResponse == null || desktopLibraryDetailResponse.LibraryId != payload.LibraryId)
		{
			return Task.CompletedTask;
		}
		if (denuvoActivationUiState.IsAwaitingRequest(desktopLibraryDetailResponse.LibraryId))
		{
			deferredDenuvoUiResults.Remember(payload);
			return Task.CompletedTask;
		}
		if (!denuvoActivationUiState.TryComplete(desktopLibraryDetailResponse.LibraryId, payload.ActiveRequestId))
		{
			return Task.CompletedTask;
		}
		string text = (string.IsNullOrWhiteSpace(payload.Reason) ? "Admin đã hủy yêu cầu." : payload.Reason.Trim());
		return ShowActivationPopupAsync(new
		{
			state = "error",
			gameName = payload.GameName,
			title = "Yêu cầu đã bị hủy",
			badgeText = "Admin đã hủy",
			metaText = BuildDenuvoModalMeta(desktopLibraryDetailResponse),
			summary = "Admin đã hủy yêu cầu nạp game. Lý do: " + text,
			metrics = new[]
			{
				new
				{
					label = "Trạng thái",
					value = "Đã hủy",
					tone = "error"
				},
				new
				{
					label = "Phiên bản",
					value = (string.IsNullOrWhiteSpace(desktopLibraryDetailResponse.VersionName) ? "Mặc định" : desktopLibraryDetailResponse.VersionName),
					tone = "default"
				}
			},
			paths = Array.Empty<object>(),
			messages = new string[1] { "Bạn có thể kiểm tra lại thông tin game hoặc liên hệ admin nếu cần." },
			primaryActionText = "Đóng"
		});
	}

	private Task ShowPypassInstallProgressPopupAsync(DesktopLibraryDetailResponse detail, PypassFixInstallProgress? progress = null)
	{
		int? num = progress?.Percentage;
		string text;
		if (num.HasValue)
		{
			int valueOrDefault = num.GetValueOrDefault();
			text = $"Đang tải {valueOrDefault}%";
		}
		else
		{
			text = progress?.Stage switch
			{
				"checking" => "Đang kiểm tra", 
				"preparing_proxy" => "Server đang chuẩn bị", 
				"installing" => "Đang giải nén", 
				"completed" => "Đã hoàn tất", 
				_ => "Đang tải package", 
			};
		}
		string value = text;
		string loaderText = (string.IsNullOrWhiteSpace(progress?.Message) ? "Đang tải package và cài vào thư mục game..." : progress.Message);
		return ShowActivationPopupAsync(new
		{
			state = "loading",
			gameName = detail.GameName,
			title = "Đang chạy Active-2",
			badgeText = "Active-2",
			metaText = BuildPypassModalMeta(detail),
			summary = ((progress?.Stage == "downloading_direct") ? "Launcher đang tải ZIP trực tiếp từ Google Drive rồi sẽ backup và giải nén vào thư mục game." : "Launcher đang tải package, kiểm tra ZIP và giải nén vào thư mục game. Vui lòng giữ launcher mở."),
			metrics = new[]
			{
				new
				{
					label = "Trạng thái",
					value = value,
					tone = "default"
				},
				new
				{
					label = "Dung lượng",
					value = FormatPypassDownloadProgress(progress),
					tone = "default"
				},
				new
				{
					label = "Tốc độ",
					value = FormatPypassDownloadSpeed(progress),
					tone = "default"
				},
				new
				{
					label = "Kết nối",
					value = (((object)progress != null && progress.ConnectionCount > 0) ? progress.ConnectionCount.ToString() : "--"),
					tone = "default"
				}
			},
			paths = Array.Empty<object>(),
			messages = new string[3] { "Nếu hủy hoặc mất mạng, launcher sẽ tiếp tục phần đã tải ở lần chạy Active-2 kế tiếp.", "File bị ghi đè sẽ được backup trong LocalAppData.", "Active-2 chỉ chạy khi Steam đã cài game hoàn tất và thư mục game có trên 10 file." },
			primaryActionText = "Hủy tải",
			loaderText = loaderText,
			progressPercentage = progress?.Percentage,
			progressIndeterminate = (progress?.IsIndeterminate ?? false),
			minimalLoading = true,
			lockClose = true,
			cancelAction = "pypass:cancel"
		});
	}

	private static string FormatPypassDownloadProgress(PypassFixInstallProgress? progress)
	{
		if ((object)progress == null || progress.BytesReceived <= 0)
		{
			return "--";
		}
		string text = FormatPypassBytes(progress.BytesReceived);
		long? totalBytes = progress.TotalBytes;
		if (!totalBytes.HasValue || totalBytes.GetValueOrDefault() <= 0)
		{
			return text;
		}
		return text + " / " + FormatPypassBytes(progress.TotalBytes.Value);
	}

	private static string FormatPypassDownloadSpeed(PypassFixInstallProgress? progress)
	{
		if ((object)progress == null || progress.SpeedBytesPerSecond <= 0)
		{
			return "--";
		}
		return FormatPypassBytes(progress.SpeedBytesPerSecond) + "/s";
	}

	private static string FormatPypassBytes(long bytes)
	{
		string[] array = new string[4] { "B", "KB", "MB", "GB" };
		double num = Math.Max(0L, bytes);
		int num2 = 0;
		while (num >= 1024.0 && num2 < array.Length - 1)
		{
			num /= 1024.0;
			num2++;
		}
		return $"{num:0.##} {array[num2]}";
	}

	private Task ShowPypassSuccessPopupAsync(DesktopLibraryDetailResponse detail, PypassFixInstallResult result)
	{
		return ShowActivationPopupAsync(new
		{
			state = "success",
			gameName = detail.GameName,
			title = "Active-2 thành công",
			badgeText = "Hoàn tất",
			metaText = BuildPypassModalMeta(detail),
			summary = "Bạn có thể chơi game. NestG cảm ơn.",
			metrics = Array.Empty<object>(),
			paths = (string.IsNullOrWhiteSpace(result.GameExePath) ? Array.Empty<object>() : new object[1]
			{
				new
				{
					label = "File chay game",
					value = result.GameExePath
				}
			}),
			messages = ((!string.IsNullOrWhiteSpace(result.GameExePath)) ? new string[1] { "Hãy mở game bằng link file chạy game hiện trong popup." } : new string[1] { "Chưa có link file chạy game. Kiểm tra MainExeRelativePath trong admin." }),
			primaryActionText = "Hoàn tất",
			compactSuccess = true
		});
	}

	private Task ShowPypassErrorPopupAsync(DesktopLibraryDetailResponse detail, string errorMessage)
	{
		string text = (string.IsNullOrWhiteSpace(errorMessage) ? "Không thể chạy Active-2 lúc này." : errorMessage.Trim());
		return ShowActivationPopupAsync(new
		{
			state = "error",
			gameName = detail.GameName,
			title = "Không thể chạy Active-2",
			badgeText = "Cần kiểm tra",
			metaText = BuildPypassModalMeta(detail),
			summary = text,
			metrics = new[]
			{
				new
				{
					label = "Trạng thái",
					value = "Cần thử lại",
					tone = "error"
				},
				new
				{
					label = "Phiên bản",
					value = (string.IsNullOrWhiteSpace(detail.VersionName) ? "Mặc định" : detail.VersionName),
					tone = "default"
				}
			},
			paths = Array.Empty<object>(),
			messages = new string[2] { text, "Hãy kiểm tra kết nối, quyền ghi thư mục game và thử lại sau." },
			primaryActionText = "Đóng"
		});
	}

	private Task ShowOnlineInstallProgressPopupAsync(DesktopLibraryDetailResponse detail)
	{
		return ShowActivationPopupAsync(new
		{
			state = "loading",
			gameName = detail.GameName,
			title = "Đang nạp game",
			badgeText = "Nạp game",
			metaText = BuildOnlineModalMeta(detail),
			summary = "Launcher đang tải package và giải nén vào thư mục game Steam.",
			metrics = new[]
			{
				new
				{
					label = "Trạng thái",
					value = "Đang nạp game",
					tone = "default"
				},
				new
				{
					label = "Phiên bản",
					value = (string.IsNullOrWhiteSpace(detail.VersionName) ? "Mặc định" : detail.VersionName),
					tone = "default"
				}
			},
			paths = Array.Empty<object>(),
			messages = new string[1] { "Không tắt launcher trong quá trình nạp game." },
			primaryActionText = "Để launcher chạy",
			loaderText = "Đang tải và giải nén package...",
			minimalLoading = true
		});
	}

	private Task ShowOnlineSuccessPopupAsync(DesktopLibraryDetailResponse detail, OnlineFixInstallResult result)
	{
		return ShowActivationPopupAsync(new
		{
			state = "success",
			gameName = detail.GameName,
			title = "Nạp game thành công",
			badgeText = "Hoàn tất",
			metaText = BuildOnlineModalMeta(detail),
			summary = "Game đã được nạp thành công.",
			metrics = Array.Empty<object>(),
			paths = Array.Empty<object>(),
			messages = Array.Empty<string>(),
			primaryActionText = "Hoàn tất",
			compactSuccess = true
		});
	}

	private Task ShowOnlineErrorPopupAsync(DesktopLibraryDetailResponse detail, string errorMessage)
	{
		string text = (string.IsNullOrWhiteSpace(errorMessage) ? "Không thể nạp game lúc này." : errorMessage.Trim());
		return ShowActivationPopupAsync(new
		{
			state = "error",
			gameName = detail.GameName,
			title = "Không thể nạp game",
			badgeText = "Cần kiểm tra",
			metaText = BuildOnlineModalMeta(detail),
			summary = text,
			metrics = new[]
			{
				new
				{
					label = "Trạng thái",
					value = "Cần thử lại",
					tone = "error"
				},
				new
				{
					label = "Phiên bản",
					value = (string.IsNullOrWhiteSpace(detail.VersionName) ? "Mặc định" : detail.VersionName),
					tone = "default"
				}
			},
			paths = Array.Empty<object>(),
			messages = new string[2] { text, "Hãy kiểm tra kết nối, quyền ghi thư mục game và thử lại sau." },
			primaryActionText = "Đóng"
		});
	}

	private Task ShowGameActivationProgressPopupAsync(DesktopLibraryDetailResponse detail, GameActivationProgress progress)
	{
		if (progress.Stage == GameActivationProgressStage.Completed)
		{
			return Task.CompletedTask;
		}
		int overallPercentage = progress.OverallPercentage;
		return ShowActivationPopupAsync(new
		{
			state = "loading",
			gameName = detail.GameName,
			title = "Đang Active game",
			badgeText = "Tự động",
			metaText = BuildActivationModalMeta(detail),
			summary = "Launcher đang kích hoạt game. Vui lòng giữ Launcher mở.",
			metrics = new[]
			{
				new
				{
					label = "Trạng thái",
					value = progress.Message,
					tone = "default"
				},
				new
				{
					label = "Phiên bản",
					value = (string.IsNullOrWhiteSpace(detail.VersionName) ? "Chưa xác định" : detail.VersionName.Trim()),
					tone = "default"
				}
			},
			paths = Array.Empty<object>(),
			messages = Array.Empty<string>(),
			primaryActionText = "Đang xử lý",
			loaderText = progress.Message,
			minimalLoading = true,
			lockClose = true,
			progressPercentage = overallPercentage
		});
	}

	private async Task CloseActivationPopupAsync()
	{
		await EnsureDetailWebViewAsync();
		if (!detailWebView.IsDisposed && detailWebView.CoreWebView2 != null)
		{
			await detailWebView.CoreWebView2.ExecuteScriptAsync("window.__nestgActivation?.close(true);");
		}
	}

	private async Task ShowActivationPopupAsync(object payload)
	{
		DesktopLibraryDetailResponse expectedDetail = currentDetail;
		int expectedDocument = detailRequestId;
		await EnsureDetailWebViewAsync();
		if (!disposalCancellation.IsCancellationRequested && currentDetail == expectedDetail && detailRequestId == expectedDocument && !detailWebView.IsDisposed && detailWebView.CoreWebView2 != null)
		{
			await detailWebView.CoreWebView2.ExecuteScriptAsync("window.__nestgActivation?.show(" + JsonSerializer.Serialize(payload, JsonOptions) + ");");
		}
	}

	private static string BuildDenuvoModalMeta(DesktopLibraryDetailResponse detail)
	{
		List<string> list = new List<string>();
		if (!string.IsNullOrWhiteSpace(detail.VersionName))
		{
			list.Add(detail.VersionName.Trim());
		}
		if (!string.IsNullOrWhiteSpace(detail.OrderCode))
		{
			list.Add("Order " + detail.OrderCode.Trim());
		}
		if (list.Count != 0)
		{
			return string.Join(" | ", list);
		}
		return "Launcher đang xử lý Denuvo Fix.";
	}

	private static string BuildPypassModalMeta(DesktopLibraryDetailResponse detail)
	{
		List<string> list = new List<string>();
		if (!string.IsNullOrWhiteSpace(detail.VersionName))
		{
			list.Add(detail.VersionName.Trim());
		}
		if (!string.IsNullOrWhiteSpace(detail.OrderCode))
		{
			list.Add("Order " + detail.OrderCode.Trim());
		}
		if (list.Count != 0)
		{
			return string.Join(" | ", list);
		}
		return "Launcher đang xử lý Active-2.";
	}

	private static string BuildOnlineModalMeta(DesktopLibraryDetailResponse detail)
	{
		List<string> list = new List<string>();
		if (!string.IsNullOrWhiteSpace(detail.VersionName))
		{
			list.Add(detail.VersionName.Trim());
		}
		if (!string.IsNullOrWhiteSpace(detail.OrderCode))
		{
			list.Add("Order " + detail.OrderCode.Trim());
		}
		if (list.Count != 0)
		{
			return string.Join(" | ", list);
		}
		return "Launcher đang nạp game.";
	}

	private void OpenWebsite(string? website)
	{
		if (string.IsNullOrWhiteSpace(website))
		{
			return;
		}
		try
		{
			Process.Start(new ProcessStartInfo
			{
				FileName = website,
				UseShellExecute = true
			});
		}
		catch
		{
			LauncherMessageBox.Show("Không mở được liên kết trang sản phẩm lúc này.", "NestG Launcher", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
		}
	}

	private int GetDetailContentWidth()
	{
		int num = detailScrollPanel.ClientSize.Width;
		if (detailScrollPanel.VerticalScroll.Visible)
		{
			num -= SystemInformation.VerticalScrollBarWidth;
		}
		return Math.Max(640, num - 8);
	}

	private static string NormalizeRequirementText(string html)
	{
		return Regex.Replace(StripHtml(html), "\\s{2,}", " ").Trim();
	}

	private static string StripHtml(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return string.Empty;
		}
		return Regex.Replace(Regex.Replace(Regex.Replace(Regex.Replace(WebUtility.HtmlDecode(Regex.Replace(Regex.Replace(Regex.Replace(value, "<br\\s*/?>", "\n", RegexOptions.IgnoreCase), "<hr.*?>", "\n", RegexOptions.IgnoreCase), "<.*?>", " ")), "\\s+\n", "\n"), "\n\\s+", "\n"), "[ \t]{2,}", " "), "\n{3,}", "\n\n").Trim();
	}

	private void HandleDenuvoRealtimeStateChanged(object? sender, DesktopDenuvoRealtimeState state)
	{
		if (base.IsDisposed || base.Disposing || !base.IsHandleCreated || disposalCancellation.IsCancellationRequested)
		{
			return;
		}
		if (base.InvokeRequired)
		{
			try
			{
				BeginInvoke(delegate
				{
					HandleDenuvoRealtimeStateChanged(sender, state);
				});
				return;
			}
			catch (InvalidOperationException) when (base.IsDisposed || base.Disposing || !base.IsHandleCreated)
			{
				return;
			}
		}
		UpdateDenuvoRealtimeStatusAsync(state);
	}

	private async Task UpdateDenuvoRealtimeStatusAsync(DesktopDenuvoRealtimeState state)
	{
		if (disposalCancellation.IsCancellationRequested)
		{
			return;
		}
		DesktopLibraryDetailResponse desktopLibraryDetailResponse = currentDetail;
		if (desktopLibraryDetailResponse == null || (state.LibraryId.HasValue && state.LibraryId != desktopLibraryDetailResponse.LibraryId) || detailWebView.IsDisposed || detailWebView.CoreWebView2 == null)
		{
			return;
		}
		try
		{
			await detailWebView.CoreWebView2.ExecuteScriptAsync("window.__nestgDenuvoRealtime?.update(" + JsonSerializer.Serialize(state, JsonOptions) + ");");
		}
		catch (Exception) when (base.IsDisposed || base.Disposing || disposalCancellation.IsCancellationRequested)
		{
		}
		catch (InvalidOperationException)
		{
		}
		catch (COMException)
		{
		}
	}

	private async void DetailWebView_DenuvoNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs args)
	{
		if (args.IsSuccess)
		{
			await UpdateDenuvoRealtimeStatusAsync(DesktopAppContext.DenuvoActiveRealtimeClient.CurrentState);
		}
	}

	private async Task ReplayDeferredDenuvoResultAsync(DesktopLibraryDetailResponse detail, Guid requestId)
	{
		EventArgs eventArgs = deferredDenuvoUiResults.Take(detail.LibraryId, requestId);
		if (currentDetail == detail && !disposalCancellation.IsCancellationRequested)
		{
			if (eventArgs is DesktopDenuvoActiveTokenAppliedEventArgs payload)
			{
				await ShowDenuvoTokenAppliedPopupAsync(payload);
			}
			else if (eventArgs is DesktopDenuvoActiveRequestCancelledEventArgs payload2)
			{
				await ShowDenuvoCancelledPopupAsync(payload2);
			}
		}
	}
}
