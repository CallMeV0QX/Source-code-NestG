using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using UI_Desktop.Auth;
using app_desktop;
using app_desktop.CloudRedirect;
using app_desktop.Models;
using app_desktop.Services;
using app_desktop.Services.CloudRedirect;
using app_desktop.Services.CloudSave;
using app_desktop.Services.Fix54;
using app_desktop.Services.SteamConnection;
using app_desktop.Services.SteamExclusions;
using app_desktop.Services.Warp;
using app_desktop.Services.WindowsUpdate;

namespace UI_Desktop.Store;

internal sealed class StoreWebHomeForm : Form
{
	private sealed record ActivationPopupPayload(string State, string GameName, string Title, string BadgeText, string MetaText, string Summary, IReadOnlyList<ActivationMetricPayload> Metrics, IReadOnlyList<ActivationPathPayload> Paths, IReadOnlyList<string> Messages, string PrimaryActionText, int? AutoCloseMilliseconds = null, string? LoaderText = null, bool MinimalLoading = false, bool CompactSuccess = false, DesktopDenuvoGuideResponse? Guide = null, bool LockClose = false, bool ZaloQr = false, bool MinimizeToGuide = false, string? CancelAction = null, int? ProgressPercentage = null, bool ProgressIndeterminate = false, IReadOnlyList<string>? TechnicalDetails = null, bool SingleCloseAction = false, bool FullSummary = false, string? DownloadLogId = null);

	private sealed record ActivationMetricPayload(string Label, string Value, string Tone);

	private sealed record ActivationPathPayload(string Label, string Value, string? ActionText = null, string? ActionValue = null);

	private sealed record LauncherUpdateDialogPayload(Guid DesktopAppVersionId, bool IsForceUpdate, string CurrentVersionText, string Version, int BuildNumber, string ReleaseChannel, string SizeText, string? ReleaseNotes);

	private sealed class WebHomeMessage
	{
		public string? DocumentId { get; set; }

		public string? RequestId { get; set; }

		public bool? Enabled { get; set; }

		public string? Type { get; set; }

		public string? GameId { get; set; }

		public string? VersionId { get; set; }

		public string? ComboId { get; set; }

		public decimal? ExpectedPrice { get; set; }

		public string? TierCode { get; set; }

		public string? OpeningId { get; set; }

		public string? DesktopAppVersionId { get; set; }

		public string? CartItemId { get; set; }

		public string? ComboCartItemId { get; set; }

		public string? LibraryId { get; set; }

		public string? Amount { get; set; }

		public string? OrderCode { get; set; }

		public string? PromoCode { get; set; }

		public string? PaymentMethod { get; set; }

		public string? Body { get; set; }

		public string? ClientMessageId { get; set; }

		public string? MessageId { get; set; }

		public string? Text { get; set; }

		public int? Page { get; set; }

		public int? PageSize { get; set; }

		public int? CatalogGeneration { get; set; }

		public StoreCatalogQuery? CatalogQuery { get; set; }

		public bool ResetCatalog { get; set; }

		public int? SlotNumber { get; set; }

		public int? ScrollY { get; set; }
	}

	private enum StoreSurface
	{
		Unknown,
		Home,
		GameDetail,
		ComboDetail,
		Cart,
		Library,
		LibraryDetail,
		Events,
		Profile,
		OrderHistory,
		Wallet
	}

	private CancellationTokenSource? catalogRequestCancellation;

	private StoreCatalogQuery currentCatalogQuery = new StoreCatalogQuery(null, null, null, "default", null);

	private int catalogGeneration = 1;

	private const double RestoredWindowWidthRatio = 0.8;

	private const double RestoredWindowHeightRatio = 0.88;

	private static readonly Size BaselineRestoredClientSize = new Size(1520, 900);

	private const int WmNcButtonDown = 161;

	private const int WmNcHitTest = 132;

	private const int HtClient = 1;

	private const int HtCaption = 2;

	private const int HtLeft = 10;

	private const int HtRight = 11;

	private const int HtTop = 12;

	private const int HtTopLeft = 13;

	private const int HtTopRight = 14;

	private const int HtBottom = 15;

	private const int HtBottomLeft = 16;

	private const int HtBottomRight = 17;

	private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
	{
		PropertyNameCaseInsensitive = true
	};

	private readonly StorefrontApiClient storefrontApiClient;

	private readonly DesktopSupportApiClient supportApiClient;

	private readonly StoreSupportChatPopup supportPopup;

	private readonly DesktopNotificationApiClient notificationApiClient;

	private readonly DesktopPackageUpdateService packageUpdateService;

	private readonly GameActivationService activationService;

	private readonly GameActivationLogDownload activationLogDownload = new GameActivationLogDownload();

	private readonly CloudSaveService cloudSaveService;

	private readonly WarpClientService warpClientService = new WarpClientService();

	private readonly SemaphoreSlim warpOperationGate = new SemaphoreSlim(1, 1);

	private WarpConnectionStatus warpStatus = new WarpConnectionStatus(WarpConnectionKind.Unknown, IsConnected: false, SwitchLocked: false, null);

	private bool isWarpCommandBusy;

	private readonly WarpOnboardingSession warpOnboarding = new WarpOnboardingSession();

	private bool isShowingStartupNotification;

	private bool warpOnboardingDocumentReady;

	private bool hasFreshWarpStatus;

	private string? warpOnboardingDocumentId;

	private string? warpOnboardingNavigationDocumentId;

	private ulong warpOnboardingNavigationId;

	private readonly WubPackageStore wubPackageStore = new WubPackageStore();

	private readonly WindowsUpdateBlockerService windowsUpdateBlockerService;

	private readonly SemaphoreSlim windowsUpdateOperationGate = new SemaphoreSlim(1, 1);

	private WindowsUpdateBlockStatus windowsUpdateStatus = new WindowsUpdateBlockStatus(WindowsUpdateBlockKind.Unavailable, null, null, null, null);

	private bool isWindowsUpdateCommandBusy;

	private IReadOnlyList<CloudSaveRestoreVersion> currentCloudSaveRestoreVersions = Array.Empty<CloudSaveRestoreVersion>();

	private readonly DenuvoFixInstallService denuvoFixInstallService;

	private readonly PypassFixInstallService pypassFixInstallService;

	private readonly OnlineFixInstallService onlineFixInstallService;

	private readonly SteamExclusionOwnership steamExclusionOwnership = new SteamExclusionOwnership();

	private readonly SteamExclusionToggleService steamExclusionToggleService;

	private readonly SteamExclusionToggleStateStore steamExclusionStateStore = new SteamExclusionToggleStateStore(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NestGLauncher", "SteamExclusions"));

	private SteamExclusionToggleState steamExclusionState = new SteamExclusionToggleState();

	private readonly WebView2 homeWebView = new WebView2();

	private readonly System.Windows.Forms.Timer launcherUpdateRefreshTimer = new System.Windows.Forms.Timer
	{
		Interval = 120000
	};

	private readonly System.Windows.Forms.Timer warpStatusRefreshTimer = new System.Windows.Forms.Timer
	{
		Interval = 15000
	};

	private readonly CancellationTokenSource disposalCancellation = new CancellationTokenSource();

	private readonly DeferredWebViewMessageDispatcher webMessageDispatcher;

	private readonly Dictionary<Guid, string> denuvoGuidePayloadJsonByLibraryId = new Dictionary<Guid, string>();

	private readonly DenuvoActivationUiState denuvoActivationUiState = new DenuvoActivationUiState();

	private readonly HashSet<Guid> shownPopupNotificationIds = new HashSet<Guid>();

	private StorePageData? currentPageData;

	private StoreGameDetailData? currentDetailData;

	private StoreComboDetailData? currentComboDetailData;

	private DesktopEventPageResponse? currentEventData;

	private DesktopLibraryListResponse? currentLibraryData;

	private DesktopLibraryDetailResponse? currentLibraryDetail;

	private Guid? watchedLibraryDetailId;

	private DesktopAccountResponse? currentAccountData;

	private DesktopCartResponse? currentCartData;

	private DesktopAppUpdateInfo? availableDesktopUpdate;

	private IReadOnlyList<DesktopNotificationItem> desktopNotifications = Array.Empty<DesktopNotificationItem>();

	private StoreSurface currentSurface;

	private StoreWebHomeContext currentContext = new StoreWebHomeContext("Người dùng NestG", "—", 0, IsAuthenticated: false, string.Empty, IsWalletBalanceAvailable: false, IsCartCountAvailable: false);

	private Task? webViewInitializationTask;

	private string? pendingHomeStateJson;

	private int? pendingDetailScrollY;

	private int? pendingCartScrollY;

	private bool isLoadingHome;

	private bool isLoadingDetail;

	private bool isAddingToCart;

	private bool isLoadingProfile;

	private bool isLoadingCart;

	private bool isLoadingLibrary;

	private bool isOpeningLibraryItem;

	private bool isLoadingEvents;

	private bool isCreatingEventCheckout;

	private bool isActivatingLibraryGame;

	private bool isCloudSavingLibraryGame;

	private bool isManagingCloudRedirect;

	private bool isApplyingLibraryDenuvoFix;

	private bool isApplyingLibraryPypassFix;

	private bool isApplyingLibraryOnlineFix;

	private CancellationTokenSource? pypassInstallCancellation;

	private bool isRefreshingLauncherUpdate;

	private bool isStartingLauncherUpdate;

	private string? dismissedLauncherUpdateKey;

	private string? lastShownOptionalLauncherUpdateKey;

	private bool isCreatingWalletTopUp;

	private bool isHandlingWalletTopUpPaid;

	private bool isSubmittingReview;

	private string? activeWalletTopUpOrderCode;

	private string? completedWalletTopUpOrderCode;

	private string? activeOrderCheckoutOrderCode;

	private string? completedOrderCheckoutOrderCode;

	private bool isHandlingOrderCheckoutPaid;

	private bool isNavigatingToLogin;

	private bool isSigningOut;

	private bool denuvoCloseStarted;

	private bool denuvoCloseReady;

	private DesktopCheckoutPaymentResponse? activeOrderCheckoutPayment;

	private string orderCheckoutReturnTarget = "home";

	private Guid? activeSupportConversationId;

	private DesktopSupportConversationResponse? currentSupportConversation;

	private readonly SupportChatPopupTrigger supportPopupTrigger = new SupportChatPopupTrigger();

	private CoreWebView2? diagnosticsCoreWebView;

	private CoreWebView2Environment? diagnosticsWebViewEnvironment;

	private readonly DenuvoDeferredUiResults deferredDenuvoUiResults = new DenuvoDeferredUiResults();

	private readonly Fix54Service fix54Service = new Fix54Service();

	private readonly StoreFix54OperationLifetime fix54Lifetime = new StoreFix54OperationLifetime();

	private string? fix54DocumentId;

	private bool fix54WorkActive;

	private long fix54UiRevision;

	private StoreFix54Feedback? fix54TerminalFeedback;

	private readonly SteamConnectionCoordinator steamConnectionCoordinator = SteamConnectionCoordinator.Shared;

	private readonly SemaphoreSlim steamConnectionUiGate = new SemaphoreSlim(1, 1);

	private bool steamConnectionSubscribed;

	private bool ShouldStopStartup
	{
		get
		{
			if (!base.IsDisposed && !base.Disposing && !disposalCancellation.IsCancellationRequested)
			{
				return isNavigatingToLogin;
			}
			return true;
		}
	}

	private bool isFixing54 => fix54Lifetime.IsRunning;

	private bool isStoppingFix54 => fix54Lifetime.IsStopping;

	private bool ShouldSuppressFix54Feedback
	{
		get
		{
			if (!isStoppingFix54 && !isStartingLauncherUpdate && !isSigningOut && !denuvoCloseStarted)
			{
				return ShouldStopStartup;
			}
			return true;
		}
	}

	private void CancelCatalogRequest()
	{
		catalogRequestCancellation?.Cancel();
		catalogRequestCancellation = null;
	}

	private string NormalizeCapturedCatalogState(string stateJson)
	{
		JsonObject jsonObject = JsonNode.Parse(stateJson, null).AsObject();
		StoreCatalogQuery storeCatalogQuery = jsonObject["catalogQuery"]?.Deserialize<StoreCatalogQuery>(JsonOptions)?.Bounded();
		if ((object)storeCatalogQuery != null && storeCatalogQuery != currentCatalogQuery)
		{
			StorePageData storePageData = currentPageData;
			if (storePageData != null)
			{
				currentCatalogQuery = storeCatalogQuery;
				storePageData.Games = Array.Empty<StoreGame>();
				storePageData.CatalogPage = 0;
				storePageData.CatalogRenderedCount = 0;
				storePageData.CatalogTotalItems = 0;
				storePageData.CatalogHasMore = true;
				storePageData.CatalogNextPage = 1;
				jsonObject["catalogHasMore"] = true;
				jsonObject["catalogNextPage"] = 1;
				jsonObject["catalogTotalCount"] = 0;
			}
		}
		return jsonObject.ToJsonString(JsonOptions);
	}

	private async Task LoadCatalogPageAsync(WebHomeMessage message, string source)
	{
		if (currentSurface != StoreSurface.Home || currentPageData == null || !StoreSupportContact.IsTrustedAction(source, message.DocumentId, warpOnboardingDocumentId))
		{
			return;
		}
		string requestId = message.RequestId;
		if (requestId == null)
		{
			return;
		}
		int length = requestId.Length;
		if (length <= 0 || length > 80)
		{
			return;
		}
		int? num = message.CatalogGeneration;
		if (!num.HasValue)
		{
			return;
		}
		int generation = num.GetValueOrDefault();
		if (generation < 1 || generation > 100000000)
		{
			return;
		}
		num = message.Page;
		if (!num.HasValue)
		{
			return;
		}
		int page = num.GetValueOrDefault();
		if (page < 1 || page > 1000000)
		{
			return;
		}
		StoreCatalogQuery storeCatalogQuery = (message.CatalogQuery ?? new StoreCatalogQuery(null, null, null, "default", null)).Bounded();
		if (message.ResetCatalog)
		{
			if (page != 1 || generation < catalogGeneration || (generation == catalogGeneration && storeCatalogQuery != currentCatalogQuery))
			{
				return;
			}
			currentCatalogQuery = storeCatalogQuery;
			catalogGeneration = generation;
			currentPageData.Games = Array.Empty<StoreGame>();
			currentPageData.CatalogPage = 0;
			currentPageData.CatalogRenderedCount = 0;
			currentPageData.CatalogTotalItems = 0;
			currentPageData.CatalogHasMore = true;
			currentPageData.CatalogNextPage = 1;
		}
		else if (generation != catalogGeneration || storeCatalogQuery != currentCatalogQuery || page != currentPageData.CatalogNextPage || !currentPageData.CatalogHasMore)
		{
			return;
		}
		CancelCatalogRequest();
		CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(disposalCancellation.Token);
		string documentId;
		StorePageData pageDataOwner;
		try
		{
			catalogRequestCancellation = cancellation;
			documentId = warpOnboardingDocumentId;
			pageDataOwner = currentPageData;
			try
			{
				StorefrontHomeCatalogPageResponse storefrontHomeCatalogPageResponse = await storefrontApiClient.GetDesktopCatalogPageAsync(page, storeCatalogQuery, cancellation.Token);
				if (!IsCurrent())
				{
					return;
				}
				if (storefrontHomeCatalogPageResponse.Page != page || storefrontHomeCatalogPageResponse.Games.Count > 24 || storefrontHomeCatalogPageResponse.HasMore != storefrontHomeCatalogPageResponse.NextPage.HasValue || (storefrontHomeCatalogPageResponse.HasMore && storefrontHomeCatalogPageResponse.Games.Count == 0))
				{
					goto IL_03bc;
				}
				num = storefrontHomeCatalogPageResponse.NextPage;
				if (num.HasValue)
				{
					int valueOrDefault = num.GetValueOrDefault();
					if (valueOrDefault != page + 1)
					{
						goto IL_03bc;
					}
				}
				StoreCatalogPageData data = StoreCatalogPageData.FromApi(storefrontHomeCatalogPageResponse);
				if (message.ResetCatalog)
				{
					currentPageData.Games = Array.Empty<StoreGame>();
					currentPageData.CatalogPage = 0;
					currentPageData.CatalogRenderedCount = 0;
				}
				currentPageData.CatalogTotalItems = data.TotalCount;
				currentPageData.CatalogHasMore = data.HasMore;
				currentPageData.CatalogNextPage = data.NextPage;
				AppendCatalogPageData(data);
				var value = new
				{
					documentId = documentId,
					RequestId = message.RequestId,
					catalogGeneration = generation,
					ResetCatalog = message.ResetCatalog,
					html = StoreWebHomeHtmlBuilder.BuildCatalogCardsFragment(data.Games),
					HasMore = data.HasMore,
					NextPage = data.NextPage,
					Page = data.Page,
					PageSize = data.PageSize,
					TotalCount = data.TotalCount
				};
				await ExecuteScriptAsync("window.__nestgHome?.appendCatalogPage(" + JsonSerializer.Serialize(value, JsonOptions) + ");");
				DesktopDiagnosticLog.Write("Storefront", "CatalogPage", $"Page={page}; Cards={data.Games.Count}; Total={data.TotalCount}");
				goto end_IL_032a;
				IL_03bc:
				throw new InvalidOperationException("Dữ liệu phân trang không hợp lệ. Vui lòng thử lại.");
				end_IL_032a:;
			}
			catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
			{
			}
			catch (Exception ex2)
			{
				if (!IsCurrent())
				{
					return;
				}
				DesktopDiagnosticLog.Write("Storefront", "CatalogPageFailed", $"Page={page}; Type={ex2.GetType().Name}; HResult=0x{ex2.HResult:X8}");
				var value2 = new
				{
					documentId = documentId,
					RequestId = message.RequestId,
					catalogGeneration = generation,
					page = page,
					ResetCatalog = message.ResetCatalog,
					message = DesktopUserMessage.FromException(ex2, "Không thể tải thêm game. Vui lòng thử lại.")
				};
				await ExecuteScriptAsync("window.__nestgHome?.catalogPageFailed(" + JsonSerializer.Serialize(value2, JsonOptions) + ");");
			}
			finally
			{
				if (catalogRequestCancellation == cancellation)
				{
					catalogRequestCancellation = null;
				}
			}
		}
		finally
		{
			if (cancellation != null)
			{
				((IDisposable)cancellation).Dispose();
			}
		}
		bool IsCurrent()
		{
			if (!cancellation.IsCancellationRequested && currentSurface == StoreSurface.Home && documentId == warpOnboardingDocumentId && generation == catalogGeneration && pageDataOwner == currentPageData)
			{
				return catalogRequestCancellation == cancellation;
			}
			return false;
		}
	}

	private async Task RestoreCatalogCardsAsync()
	{
		StorePageData storePageData = currentPageData;
		if (storePageData == null)
		{
			return;
		}
		string documentId = warpOnboardingDocumentId;
		foreach (StoreGame[] item in storePageData.Games.Skip(24).Chunk(24))
		{
			if (currentSurface != StoreSurface.Home || documentId != warpOnboardingDocumentId || disposalCancellation.IsCancellationRequested)
			{
				return;
			}
			var value = new
			{
				documentId = documentId,
				html = StoreWebHomeHtmlBuilder.BuildCatalogCardsFragment(item)
			};
			await ExecuteScriptAsync("window.__nestgHome?.restoreCatalogPage(" + JsonSerializer.Serialize(value, JsonOptions) + ");");
		}
	}

	private async Task RestorePendingHomeStateAsync(ulong navigationId, bool success)
	{
		if (!success || navigationId != warpOnboardingNavigationId)
		{
			return;
		}
		string stateJson = pendingHomeStateJson;
		if (stateJson != null && stateJson.Length > 0 && currentSurface == StoreSurface.Home && currentPageData != null && currentDetailData == null && currentLibraryDetail == null)
		{
			string documentId = warpOnboardingDocumentId;
			pendingHomeStateJson = null;
			await RestoreCatalogCardsAsync();
			if (currentSurface == StoreSurface.Home && !(documentId != warpOnboardingDocumentId) && navigationId == warpOnboardingNavigationId)
			{
				await ExecuteScriptAsync("(() => { const state = " + stateJson + "; const documentId = " + JsonSerializer.Serialize(documentId) + "; let attempts = 0; const restore = () => {\n                if (document.body.dataset.catalogDocumentId !== documentId) return;\n                if (window.__nestgHome?.restoreState) { window.__nestgHome.restoreState(state); return; }\n                if (++attempts < 40) window.setTimeout(restore, 50);\n            }; restore(); })();");
			}
		}
	}

	public StoreWebHomeForm()
	{
		string log = Path.Combine(AppContext.BaseDirectory, "startup.log");
		File.AppendAllText(log, $"[{DateTime.Now}] StoreWebHomeForm: enter ctor\n");
		storefrontApiClient = new StorefrontApiClient(DesktopAppContext.Settings.ApiBaseUrl);
		supportApiClient = new DesktopSupportApiClient(DesktopAppContext.Settings.ApiBaseUrl);
		supportPopup = new StoreSupportChatPopup((string body, Guid clientMessageId) => SendSupportMessageAsync(body, clientMessageId.ToString("D")), MarkSupportReadCoreAsync);
		notificationApiClient = new DesktopNotificationApiClient(DesktopAppContext.Settings.ApiBaseUrl);
		packageUpdateService = new DesktopPackageUpdateService(DesktopAppContext.Settings.ApiBaseUrl, DesktopAppContext.Settings.StorageFolderName);
		activationService = new GameActivationService(storefrontApiClient);
		cloudSaveService = new CloudSaveService(DesktopAppContext.Settings.StorageFolderName, DesktopAppContext.Settings.GoogleDriveClientId, null, null, null, DesktopAppContext.Settings.GoogleDriveClientSecret);
		windowsUpdateBlockerService = new WindowsUpdateBlockerService(null, null, wubPackageStore);
		denuvoFixInstallService = new DenuvoFixInstallService(storefrontApiClient);
		pypassFixInstallService = new PypassFixInstallService(storefrontApiClient);
		onlineFixInstallService = new OnlineFixInstallService(storefrontApiClient);
		steamExclusionToggleService = new SteamExclusionToggleService(new SteamGameFolderDiscovery(), new WindowsDefenderExclusionClient(new DefenderPowerShellRunner()), steamExclusionOwnership);
		File.AppendAllText(log, $"[{DateTime.Now}] StoreWebHomeForm: loading steamExclusionState...\n");
		steamExclusionState = steamExclusionStateStore.Load();
		webMessageDispatcher = new DeferredWebViewMessageDispatcher(TryPostWebMessageAction, HandleUnhandledWebMessageExceptionAsync, disposalCancellation.Token);
		launcherUpdateRefreshTimer.Tick += async delegate
		{
			await RefreshLauncherUpdateAsync(showOptionalPopup: false);
		};
		warpStatusRefreshTimer.Tick += async delegate
		{
			await RefreshWarpStatusAsync();
			await RefreshWindowsUpdateStatusAsync();
		};
		File.AppendAllText(log, $"[{DateTime.Now}] StoreWebHomeForm: binding realtime events...\n");
		DesktopAppContext.PaymentRealtimeClient.PaymentStatusUpdated += HandlePaymentStatusUpdated;
		DesktopAppContext.DenuvoActiveRealtimeClient.TokenApplied += HandleDenuvoActiveTokenApplied;
		DesktopAppContext.DenuvoActiveRealtimeClient.RequestCancelled += HandleDenuvoActiveRequestCancelled;
		DesktopAppContext.DenuvoActiveRealtimeClient.CooldownUnlocked += HandleDenuvoActiveCooldownUnlocked;
		DesktopAppContext.DenuvoActiveRealtimeClient.ConnectionStateChanged += HandleDenuvoRealtimeStateChanged;
		DesktopAppContext.SupportRealtimeClient.ConversationUpdated += HandleSupportConversationUpdated;
		DesktopAppContext.SupportRealtimeClient.MessageReceived += HandleSupportMessageReceived;
		DesktopAppContext.SupportRealtimeClient.MessageRead += HandleSupportMessageRead;
		DesktopAppContext.SupportRealtimeClient.Reconnected += HandleSupportRealtimeReconnected;
		File.AppendAllText(log, $"[{DateTime.Now}] StoreWebHomeForm: calling InitializeShell()...\n");
		InitializeShell();
		File.AppendAllText(log, $"[{DateTime.Now}] StoreWebHomeForm: exit ctor\n");
	}

	protected override async void OnShown(EventArgs e)
	{
		string log = Path.Combine(AppContext.BaseDirectory, "startup.log");
		File.AppendAllText(log, $"[{DateTime.Now}] StoreWebHomeForm.OnShown: entered\n");
		base.OnShown(e);
		File.AppendAllText(log, $"[{DateTime.Now}] StoreWebHomeForm.OnShown: base.OnShown done, calling EnsureHomeWebViewWithRecoveryAsync...\n");
		if (!(await EnsureHomeWebViewWithRecoveryAsync()) || ShouldStopStartup)
		{
			File.AppendAllText(log, $"[{DateTime.Now}] StoreWebHomeForm.OnShown: EnsureHomeWebViewWithRecoveryAsync failed or ShouldStopStartup\n");
			return;
		}
		File.AppendAllText(log, $"[{DateTime.Now}] StoreWebHomeForm.OnShown: WebView ready, starting payment client...\n");
		try
		{
			await DesktopAppContext.PaymentRealtimeClient.StartAsync(disposalCancellation.Token);
		}
		catch
		{
		}
		if (ShouldStopStartup)
		{
			File.AppendAllText(log, $"[{DateTime.Now}] StoreWebHomeForm.OnShown: ShouldStopStartup after PaymentRealtimeClient\n");
			return;
		}
		RenderLoading();
		File.AppendAllText(log, $"[{DateTime.Now}] StoreWebHomeForm.OnShown: calling LoadHomeAsync...\n");
		await LoadHomeAsync();
		File.AppendAllText(log, $"[{DateTime.Now}] StoreWebHomeForm.OnShown: LoadHomeAsync done\n");
		if (ShouldStopStartup)
		{
			File.AppendAllText(log, $"[{DateTime.Now}] StoreWebHomeForm.OnShown: ShouldStopStartup after LoadHomeAsync\n");
			return;
		}
		await RefreshWarpStatusAsync();
		if (ShouldStopStartup)
		{
			return;
		}
		await RefreshWindowsUpdateStatusAsync();
		if (ShouldStopStartup)
		{
			return;
		}
		warpStatusRefreshTimer.Start();
		await StartSupportRealtimeAsync();
		if (!ShouldStopStartup)
		{
			await RefreshLauncherUpdateAsync(showOptionalPopup: true);
			if (!ShouldStopStartup)
			{
				launcherUpdateRefreshTimer.Start();
			}
		}
		File.AppendAllText(log, $"[{DateTime.Now}] StoreWebHomeForm.OnShown: fully completed!\n");
	}

	protected override void OnFormClosing(FormClosingEventArgs e)
	{
		string log = Path.Combine(AppContext.BaseDirectory, "startup.log");
		File.AppendAllText(log, $"[{DateTime.Now}] StoreWebHomeForm.OnFormClosing: CloseReason={e.CloseReason}, Cancel={e.Cancel}, denuvoCloseReady={denuvoCloseReady}, denuvoCloseStarted={denuvoCloseStarted}\n");
		base.OnFormClosing(e);
		if (e.Cancel || denuvoCloseReady)
		{
			return;
		}
		e.Cancel = true;
		if (denuvoCloseStarted)
		{
			return;
		}
		denuvoCloseStarted = true;
		BeginInvoke(async delegate
		{
			await StopFix54Async();
			await StopDenuvoRealtimeAsync();
			await StopSteamConnectionForCloseAsync();
			denuvoCloseReady = true;
			if (!base.IsDisposed && !base.Disposing)
			{
				Close();
			}
		});
	}

	private static async Task StopDenuvoRealtimeAsync()
	{
		using CancellationTokenSource deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5.0));
		try
		{
			await DesktopAppContext.DenuvoActiveRealtimeClient.StopAsync(deadline.Token);
		}
		catch
		{
		}
	}

	protected override void OnFormClosed(FormClosedEventArgs e)
	{
		DetachSteamConnection();
		warpOnboarding.Dismiss();
		DesktopAppContext.PaymentRealtimeClient.PaymentStatusUpdated -= HandlePaymentStatusUpdated;
		DesktopAppContext.DenuvoActiveRealtimeClient.TokenApplied -= HandleDenuvoActiveTokenApplied;
		DesktopAppContext.DenuvoActiveRealtimeClient.RequestCancelled -= HandleDenuvoActiveRequestCancelled;
		DesktopAppContext.DenuvoActiveRealtimeClient.CooldownUnlocked -= HandleDenuvoActiveCooldownUnlocked;
		DesktopAppContext.DenuvoActiveRealtimeClient.ConnectionStateChanged -= HandleDenuvoRealtimeStateChanged;
		DesktopAppContext.SupportRealtimeClient.ConversationUpdated -= HandleSupportConversationUpdated;
		DesktopAppContext.SupportRealtimeClient.MessageReceived -= HandleSupportMessageReceived;
		DesktopAppContext.SupportRealtimeClient.MessageRead -= HandleSupportMessageRead;
		DesktopAppContext.SupportRealtimeClient.Reconnected -= HandleSupportRealtimeReconnected;
		ClearWatchedLibraryDetail();
		launcherUpdateRefreshTimer.Stop();
		launcherUpdateRefreshTimer.Dispose();
		warpStatusRefreshTimer.Stop();
		warpStatusRefreshTimer.Dispose();
		disposalCancellation.Cancel();
		supportPopup.Dispose();
		pypassInstallCancellation?.Cancel();
		pypassInstallCancellation?.Dispose();
		pypassFixInstallService.Dispose();
		disposalCancellation.Dispose();
		DetachHomeWebViewDiagnostics();
		homeWebView.Dispose();
		base.OnFormClosed(e);
	}

	private void InitializeShell()
	{
		StoreUiHelper.ApplyWindowIcon(this);
		base.AutoScaleMode = AutoScaleMode.Dpi;
		BackColor = Color.FromArgb(7, 13, 20);
		Rectangle workingArea = Screen.FromPoint(System.Windows.Forms.Cursor.Position).WorkingArea;
		base.ClientSize = CalculateRestoredClientSize(workingArea.Size);
		MinimumSize = new Size(Math.Min(760, workingArea.Width), Math.Min(540, workingArea.Height));
		base.MaximizedBounds = workingArea;
		base.FormBorderStyle = FormBorderStyle.None;
		base.StartPosition = FormStartPosition.CenterScreen;
		base.WindowState = FormWindowState.Maximized;
		Text = "NestG Store";
		homeWebView.Dock = DockStyle.Fill;
		homeWebView.Margin = Padding.Empty;
		homeWebView.DefaultBackgroundColor = Color.FromArgb(7, 13, 20);
		base.Controls.Add(homeWebView);
	}

	internal static Size CalculateRestoredClientSize(Size workingArea)
	{
		int val = Math.Max(BaselineRestoredClientSize.Width, (int)Math.Round((double)workingArea.Width * 0.8));
		return new Size(height: Math.Min(Math.Max(BaselineRestoredClientSize.Height, (int)Math.Round((double)workingArea.Height * 0.88)), workingArea.Height), width: Math.Min(val, workingArea.Width));
	}

	private Task EnsureHomeWebViewAsync()
	{
		if (webViewInitializationTask == null)
		{
			webViewInitializationTask = EnsureHomeWebViewCoreAsync();
		}
		return webViewInitializationTask;
	}

	private async Task EnsureHomeWebViewCoreAsync()
	{
		await homeWebView.EnsureCoreWebView2Async();
		if (!homeWebView.IsDisposed && homeWebView.CoreWebView2 != null)
		{
			CoreWebView2Settings settings = homeWebView.CoreWebView2.Settings;
			settings.AreDefaultContextMenusEnabled = false;
			settings.AreDevToolsEnabled = false;
			settings.AreBrowserAcceleratorKeysEnabled = true;
			settings.IsStatusBarEnabled = false;
			settings.IsZoomControlEnabled = true;
			AttachHomeWebViewDiagnostics(homeWebView.CoreWebView2);
			DesktopDiagnosticLog.Write("WebView2", "StorefrontInitialized", "Runtime=" + homeWebView.CoreWebView2.Environment.BrowserVersionString);
			string text = Path.Combine(AppContext.BaseDirectory, "Assets");
			if (Directory.Exists(text))
			{
				homeWebView.CoreWebView2.SetVirtualHostNameToFolderMapping("nestg-assets.local", text, CoreWebView2HostResourceAccessKind.Allow);
			}
			homeWebView.CoreWebView2.WebMessageReceived -= HomeWebView_WebMessageReceived;
			homeWebView.CoreWebView2.WebMessageReceived += HomeWebView_WebMessageReceived;
			await homeWebView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(StoreWarpHeaderHtml.Script);
			await homeWebView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(StoreWindowsUpdateHeaderHtml.Script);
			await homeWebView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(StoreSteamConnectionHeaderHtml.Script);
			await homeWebView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(StoreSupportContactHeaderHtml.Script);
			homeWebView.CoreWebView2.NavigationStarting -= HomeWebView_NavigationStarting;
			homeWebView.CoreWebView2.NavigationStarting += HomeWebView_NavigationStarting;
			homeWebView.CoreWebView2.NavigationCompleted -= HomeWebView_NavigationCompleted;
			homeWebView.CoreWebView2.NavigationCompleted += HomeWebView_NavigationCompleted;
		}
	}

	private void AttachHomeWebViewDiagnostics(CoreWebView2 coreWebView)
	{
		if (diagnosticsCoreWebView != coreWebView)
		{
			DetachHomeWebViewDiagnostics();
			diagnosticsCoreWebView = coreWebView;
			diagnosticsWebViewEnvironment = coreWebView.Environment;
			diagnosticsCoreWebView.ProcessFailed += HomeWebView_ProcessFailed;
			diagnosticsWebViewEnvironment.BrowserProcessExited += HomeWebView_BrowserProcessExited;
		}
	}

	private void DetachHomeWebViewDiagnostics()
	{
		try
		{
			if (diagnosticsCoreWebView != null)
			{
				diagnosticsCoreWebView.ProcessFailed -= HomeWebView_ProcessFailed;
			}
			if (diagnosticsWebViewEnvironment != null)
			{
				diagnosticsWebViewEnvironment.BrowserProcessExited -= HomeWebView_BrowserProcessExited;
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

	private static void HomeWebView_ProcessFailed(object? sender, CoreWebView2ProcessFailedEventArgs e)
	{
		try
		{
			DesktopDiagnosticLog.Write("WebView2", "ProcessFailed", $"Kind={e.ProcessFailedKind}; Reason={e.Reason}; ExitCode={e.ExitCode}; Description={e.ProcessDescription}; FailureSource={e.FailureSourceModulePath}");
		}
		catch (Exception exception)
		{
			DesktopDiagnosticLog.Write("WebView2", "ProcessFailedDetailsUnavailable", null, exception);
		}
	}

	private static void HomeWebView_BrowserProcessExited(object? sender, CoreWebView2BrowserProcessExitedEventArgs e)
	{
		try
		{
			DesktopDiagnosticLog.Write("WebView2", "BrowserProcessExited", $"Kind={e.BrowserProcessExitKind}; BrowserProcessId={e.BrowserProcessId}");
		}
		catch (Exception exception)
		{
			DesktopDiagnosticLog.Write("WebView2", "BrowserExitDetailsUnavailable", null, exception);
		}
	}

	private async Task<bool> EnsureHomeWebViewWithRecoveryAsync()
	{
		while (!base.IsDisposed && !disposalCancellation.IsCancellationRequested)
		{
			try
			{
				await EnsureHomeWebViewAsync();
				return !homeWebView.IsDisposed && homeWebView.CoreWebView2 != null;
			}
			catch (Exception exception)
			{
				webViewInitializationTask = null;
				if (await LauncherMessageBox.ShowAsync(this, DesktopUserMessage.FromException(exception, "Không thể mở giao diện cửa hàng. Hãy kiểm tra hoặc cài lại Microsoft Edge WebView2 Runtime, sau đó thử lại."), "Không thể mở WebView2", MessageBoxButtons.RetryCancel, MessageBoxIcon.Exclamation) != DialogResult.Retry)
				{
					Close();
					return false;
				}
			}
		}
		return false;
	}

	private void RenderLoading()
	{
		if (!homeWebView.IsDisposed)
		{
			warpOnboardingDocumentId = null;
			warpOnboardingDocumentReady = false;
			homeWebView.NavigateToString(StoreWebHomeHtmlBuilder.BuildLoadingDocument());
		}
	}

	private void RenderError(string message)
	{
		if (!homeWebView.IsDisposed)
		{
			warpOnboardingDocumentId = null;
			warpOnboardingDocumentReady = false;
			homeWebView.NavigateToString(StoreWebHomeHtmlBuilder.BuildErrorDocument(message));
		}
	}

	private void RenderHome()
	{
		if (!homeWebView.IsDisposed && currentPageData != null)
		{
			currentDetailData = null;
			currentComboDetailData = null;
			ClearCurrentLibraryDetail();
			currentSurface = StoreSurface.Home;
			CancelCatalogRequest();
			catalogGeneration = 1;
			warpOnboardingDocumentId = Guid.NewGuid().ToString("N");
			warpOnboardingDocumentReady = false;
			StorePageData? pageData = currentPageData;
			StoreWebHomeContext context = currentContext;
			DesktopAccountResponse? account = currentAccountData;
			string? text = warpOnboardingDocumentId;
			string text2 = pendingHomeStateJson;
			string text3 = StoreWebHomeHtmlBuilder.BuildDocument(pageData, context, account, text, text2 != null && text2.Length > 0);
			int byteCount = Encoding.UTF8.GetByteCount(text3);
			DesktopDiagnosticLog.Write("Storefront", "HomeDocument", $"HtmlBytes={byteCount}; LoadedGames={currentPageData.Games.Count}; InitialCards={Math.Min(24, currentPageData.Games.Count)}");
			if (byteCount > 1048576)
			{
				throw new InvalidOperationException("Nội dung cửa hàng quá lớn. Vui lòng thử lại hoặc liên hệ hỗ trợ.");
			}
			homeWebView.NavigateToString(text3);
		}
	}

	private void RenderDetail(StoreGameDetailData data, bool isAuthenticated)
	{
		if (!homeWebView.IsDisposed)
		{
			currentDetailData = data;
			currentComboDetailData = null;
			ClearCurrentLibraryDetail();
			currentSurface = StoreSurface.GameDetail;
			homeWebView.NavigateToString(StoreGameDetailHtmlBuilder.BuildDocument(data, isAuthenticated, pendingDetailScrollY, showInlineBackLink: true, currentContext));
		}
	}

	private void RenderComboDetail(StoreComboDetailData data)
	{
		if (!homeWebView.IsDisposed)
		{
			currentDetailData = null;
			currentComboDetailData = data;
			ClearCurrentLibraryDetail();
			currentSurface = StoreSurface.ComboDetail;
			homeWebView.NavigateToString(StoreComboDetailHtmlBuilder.BuildDocument(data, currentContext));
		}
	}

	private void RenderLibrary(DesktopLibraryListResponse library)
	{
		if (!homeWebView.IsDisposed)
		{
			currentDetailData = null;
			ClearCurrentLibraryDetail();
			pendingDetailScrollY = null;
			currentLibraryData = library;
			currentSurface = StoreSurface.Library;
			steamExclusionState = steamExclusionState with
			{
				CanRemove = HasManagedSteamExclusions()
			};
			homeWebView.NavigateToString(StoreLibraryHtmlBuilder.BuildDocument(library, currentPageData, currentContext, steamExclusionState));
		}
	}

	private bool HasManagedSteamExclusions()
	{
		try
		{
			return steamExclusionOwnership.ReadPaths().Count > 0;
		}
		catch
		{
			return true;
		}
	}

	private async Task UpdateSteamExclusionControlAsync()
	{
		try
		{
			if (currentSurface == StoreSurface.Library && !homeWebView.IsDisposed)
			{
				await ExecuteScriptAsync("window.__nestgDefender?.update(" + JsonSerializer.Serialize(steamExclusionState, JsonOptions) + ");");
			}
		}
		catch (Exception ex) when (((ex is InvalidOperationException || ex is COMException || ex is OperationCanceledException) ? 1 : 0) != 0)
		{
		}
	}

	private async Task SetSteamExclusionsAsync(bool enabled)
	{
		if (currentSurface != StoreSurface.Library || steamExclusionState.Busy)
		{
			return;
		}
		steamExclusionState = steamExclusionState with
		{
			Busy = true,
			Message = "Đang chuẩn bị thư mục và yêu cầu quyền quản trị…"
		};
		try
		{
			_ = 2;
			try
			{
				await UpdateSteamExclusionControlAsync();
				SteamExclusionToggleResult result = await steamExclusionToggleService.RunAsync(enabled, async delegate(IReadOnlyList<string> paths, bool enable, CancellationToken cancellationToken)
				{
					if (base.IsDisposed || disposalCancellation.IsCancellationRequested || currentSurface != StoreSurface.Library)
					{
						return false;
					}
					using SteamExclusionConsentDialog dialog = new SteamExclusionConsentDialog(paths, enable);
					bool num = await dialog.ShowAsync(this, cancellationToken) == DialogResult.Yes;
					if (num)
					{
						steamExclusionState = steamExclusionState with
						{
							Message = "Đang chờ quyền Windows, xử lý và xác minh ngoại lệ…"
						};
						UpdateSteamExclusionControlAsync();
					}
					return num;
				}, disposalCancellation.Token);
				DesktopDiagnosticLog.Write("SteamExclusions", "OperationCompleted", $"RequestedEnabled={enabled}; Outcome={result.Outcome}");
				steamExclusionState = steamExclusionState.Complete(result, HasManagedSteamExclusions());
				if (currentSurface == StoreSurface.Library)
				{
					await ShowToastAsync(result.CompactMessage);
				}
				if (!steamExclusionStateStore.Save(steamExclusionState, result))
				{
					steamExclusionState = steamExclusionState with
					{
						Message = steamExclusionState.Message + " Không lưu được lịch sử hiển thị."
					};
				}
			}
			catch (OperationCanceledException) when (disposalCancellation.IsCancellationRequested)
			{
			}
			catch (Exception exception)
			{
				DesktopDiagnosticLog.Write("SteamExclusions", "OperationFailed", $"RequestedEnabled={enabled}", exception);
				SteamExclusionToggleResult result2 = new SteamExclusionToggleResult(SteamExclusionToggleOutcome.Failed);
				steamExclusionState = steamExclusionState.Complete(result2, HasManagedSteamExclusions());
				steamExclusionStateStore.Save(steamExclusionState, result2);
			}
		}
		finally
		{
			steamExclusionState = steamExclusionState with
			{
				Busy = false
			};
			if (!disposalCancellation.IsCancellationRequested)
			{
				await UpdateSteamExclusionControlAsync();
			}
		}
	}

	private async Task RefreshWarpStatusAsync()
	{
		if (base.IsDisposed || disposalCancellation.IsCancellationRequested)
		{
			return;
		}
		bool acquired = false;
		try
		{
			int num = 0;
			try
			{
				acquired = await warpOperationGate.WaitAsync(0, disposalCancellation.Token);
				if (!acquired || isWarpCommandBusy)
				{
					return;
				}
				warpStatus = await warpClientService.GetStatusAsync(disposalCancellation.Token);
				hasFreshWarpStatus = true;
				await UpdateWarpHeaderAsync();
			}
			catch (OperationCanceledException) when (disposalCancellation.IsCancellationRequested)
			{
			}
			catch
			{
				num = 1;
			}
			if (num == 1)
			{
				warpStatus = new WarpConnectionStatus(WarpConnectionKind.Unavailable, IsConnected: false, SwitchLocked: false, null);
				hasFreshWarpStatus = true;
				await UpdateWarpHeaderAsync();
			}
		}
		finally
		{
			if (acquired)
			{
				warpOperationGate.Release();
			}
		}
	}

	private async Task SetWarpEnabledAsync(bool enabled)
	{
		if (isWarpCommandBusy || base.IsDisposed || disposalCancellation.IsCancellationRequested)
		{
			return;
		}
		isWarpCommandBusy = true;
		await UpdateWarpHeaderAsync();
		bool acquired = false;
		try
		{
			try
			{
				await warpOperationGate.WaitAsync(disposalCancellation.Token);
				acquired = true;
				warpStatus = await warpClientService.SetEnabledAsync(enabled, disposalCancellation.Token);
				WarpConnectionKind kind = warpStatus.Kind;
				bool flag = ((kind == WarpConnectionKind.Disconnected || kind == WarpConnectionKind.Connected) ? true : false);
				bool flag2 = flag && warpStatus.IsConnected == enabled;
				DesktopDiagnosticLog.Write("WARP", flag2 ? "SwitchConfirmed" : "SwitchUnconfirmed", $"RequestedEnabled={enabled}; State={warpStatus.Kind}");
			}
			catch (OperationCanceledException) when (disposalCancellation.IsCancellationRequested)
			{
			}
			catch
			{
				warpStatus = new WarpConnectionStatus(WarpConnectionKind.Unavailable, IsConnected: false, SwitchLocked: false, null);
				DesktopDiagnosticLog.Write("WARP", "SwitchFailed", $"RequestedEnabled={enabled}");
			}
		}
		finally
		{
			if (acquired)
			{
				warpOperationGate.Release();
			}
			isWarpCommandBusy = false;
			if (!disposalCancellation.IsCancellationRequested)
			{
				await UpdateWarpHeaderAsync();
			}
		}
		if (!disposalCancellation.IsCancellationRequested)
		{
			if (enabled && warpStatus.Kind == WarpConnectionKind.NotInstalled)
			{
				await InstallWarpAsync();
				return;
			}
			WarpConnectionKind kind = warpStatus.Kind;
			bool flag = ((kind == WarpConnectionKind.Disconnected || kind == WarpConnectionKind.Connected) ? true : false);
			bool flag3 = flag && warpStatus.IsConnected == enabled;
			await ShowToastAsync((!flag3) ? warpStatus.Detail : (enabled ? "WARP đã kết nối cho cả máy." : "WARP đã ngắt kết nối."));
		}
	}

	private async Task InstallWarpAsync()
	{
		if (isWarpCommandBusy || base.IsDisposed || disposalCancellation.IsCancellationRequested)
		{
			return;
		}
		isWarpCommandBusy = true;
		await UpdateWarpHeaderAsync();
		bool acquired = false;
		try
		{
			try
			{
				await warpOperationGate.WaitAsync(disposalCancellation.Token);
				acquired = true;
				warpStatus = await warpClientService.GetStatusAsync(disposalCancellation.Token);
				if (warpStatus.Kind != 0)
				{
					goto end_IL_0151;
				}
				using (WarpInstallDialog dialog = new WarpInstallDialog(new WarpInstallerService(warpClientService), warpClientService, homeWebView.CoreWebView2?.Environment))
				{
					await dialog.ShowAsync(this, disposalCancellation.Token);
					WarpInstallResult result = dialog.Result;
					if ((object)result != null)
					{
						DesktopDiagnosticLog.Write("WARP", "InstallCompleted", $"Outcome={result.Outcome}");
						WarpInstallOutcome outcome = result.Outcome;
						if ((uint)outcome <= 2u)
						{
							await ShowToastAsync(result.Message);
						}
					}
					warpStatus = await warpClientService.GetStatusAsync(disposalCancellation.Token);
				}
				goto end_IL_00ca;
				end_IL_0151:;
			}
			catch (OperationCanceledException) when (disposalCancellation.IsCancellationRequested)
			{
				goto end_IL_00ca;
			}
			catch
			{
				warpStatus = new WarpConnectionStatus(WarpConnectionKind.Unavailable, IsConnected: false, SwitchLocked: false, null);
				DesktopDiagnosticLog.Write("WARP", "InstallFailed");
				goto end_IL_00ca;
			}
			end_IL_00ca:;
		}
		finally
		{
			if (acquired)
			{
				warpOperationGate.Release();
			}
			isWarpCommandBusy = false;
			if (!disposalCancellation.IsCancellationRequested)
			{
				await UpdateWarpHeaderAsync();
			}
		}
	}

	private async Task UpdateWarpHeaderAsync()
	{
		var value = new
		{
			Kind = warpStatus.Kind.ToString(),
			Connected = (warpStatus.Kind == WarpConnectionKind.Connected && warpStatus.IsConnected),
			CanToggle = warpStatus.CanToggle,
			Busy = isWarpCommandBusy,
			Label = warpStatus.Label,
			Detail = warpStatus.Detail
		};
		await ExecuteScriptAsync("window.__nestgWarp?.setState(" + JsonSerializer.Serialize(value, JsonOptions) + ");");
		await UpdateWarpOnboardingAsync();
	}

	private Task UpdateWarpOnboardingAsync()
	{
		if (ShouldStopStartup || !hasFreshWarpStatus)
		{
			return Task.CompletedTask;
		}
		warpOnboarding.Observe(warpStatus, warpOnboardingDocumentReady && !isShowingStartupNotification && currentSurface == StoreSurface.Home && warpOnboardingDocumentId != null && warpOnboardingDocumentId == warpOnboardingNavigationDocumentId && !isWarpCommandBusy);
		var value = new
		{
			DocumentId = warpOnboardingDocumentId,
			Phase = warpOnboarding.Phase.ToString(),
			Kind = warpStatus.Kind.ToString(),
			Connected = warpStatus.IsConnected,
			CanToggle = warpStatus.CanToggle,
			Busy = (isWarpCommandBusy || isShowingStartupNotification),
			Unconfirmed = (warpStatus.UnconfirmedRequest == true),
			Detail = warpStatus.Detail
		};
		return ExecuteScriptAsync("window.__nestgWarpOnboarding?.setState(" + JsonSerializer.Serialize(value, JsonOptions) + ");");
	}

	private async Task RefreshWindowsUpdateStatusAsync()
	{
		if (base.IsDisposed || disposalCancellation.IsCancellationRequested)
		{
			return;
		}
		bool acquired = false;
		try
		{
			int num = 0;
			try
			{
				acquired = await windowsUpdateOperationGate.WaitAsync(0, disposalCancellation.Token);
				if (!acquired || isWindowsUpdateCommandBusy)
				{
					return;
				}
				windowsUpdateStatus = await windowsUpdateBlockerService.GetStatusAsync(disposalCancellation.Token);
				await UpdateWindowsUpdateHeaderAsync();
			}
			catch (OperationCanceledException) when (disposalCancellation.IsCancellationRequested)
			{
			}
			catch
			{
				num = 1;
			}
			if (num == 1)
			{
				windowsUpdateStatus = new WindowsUpdateBlockStatus(WindowsUpdateBlockKind.Unavailable, null, null, null, null);
				await UpdateWindowsUpdateHeaderAsync();
			}
		}
		finally
		{
			if (acquired)
			{
				windowsUpdateOperationGate.Release();
			}
		}
	}

	private async Task SetWindowsUpdateBlockedAsync(bool blocked)
	{
		if (isWindowsUpdateCommandBusy || base.IsDisposed || disposalCancellation.IsCancellationRequested)
		{
			return;
		}
		isWindowsUpdateCommandBusy = true;
		await UpdateWindowsUpdateHeaderAsync();
		bool acquired = false;
		try
		{
			await windowsUpdateOperationGate.WaitAsync(disposalCancellation.Token);
			acquired = true;
			WindowsUpdateSwitchResult windowsUpdateSwitchResult = await new WindowsUpdateSwitchFlow(windowsUpdateBlockerService, PrepareWubForSwitchAsync).ApplyAsync(blocked, disposalCancellation.Token);
			windowsUpdateStatus = windowsUpdateSwitchResult.Status;
			if (!windowsUpdateSwitchResult.PackageReady)
			{
				await ShowToastAsync("WUB chưa sẵn sàng. Windows Update chưa đổi.");
				return;
			}
			if (!windowsUpdateSwitchResult.CommandAttempted)
			{
				if (windowsUpdateStatus.IsBlocked != blocked)
				{
					await ShowToastAsync(windowsUpdateStatus.Detail);
				}
				return;
			}
			bool flag = windowsUpdateStatus.CanToggle && windowsUpdateStatus.IsBlocked == blocked;
			DesktopDiagnosticLog.Write("WindowsUpdate", flag ? "SwitchConfirmed" : "SwitchUnconfirmed", $"RequestedBlocked={blocked}; State={windowsUpdateStatus.Kind}; Outcome={windowsUpdateStatus.LastOutcome}");
			if (!disposalCancellation.IsCancellationRequested)
			{
				await ShowToastAsync((!flag) ? ((windowsUpdateStatus.LastOutcome == WubCommandOutcome.Cancelled) ? "Bạn đã hủy yêu cầu quyền quản trị. Windows Update chưa đổi." : windowsUpdateStatus.Detail) : (blocked ? "Windows Update đã được chặn." : "Windows Update đã được bật lại."));
			}
		}
		catch (OperationCanceledException) when (disposalCancellation.IsCancellationRequested)
		{
		}
		catch
		{
			windowsUpdateStatus = new WindowsUpdateBlockStatus(WindowsUpdateBlockKind.Unavailable, null, null, null, null);
			DesktopDiagnosticLog.Write("WindowsUpdate", "SwitchFailed", $"RequestedBlocked={blocked}");
			if (!disposalCancellation.IsCancellationRequested)
			{
				await ShowToastAsync("Không xác minh được trạng thái Windows Update. Hãy kiểm tra WUB.");
			}
		}
		finally
		{
			if (acquired)
			{
				windowsUpdateOperationGate.Release();
			}
			isWindowsUpdateCommandBusy = false;
			if (!disposalCancellation.IsCancellationRequested)
			{
				await UpdateWindowsUpdateHeaderAsync();
			}
		}
	}

	private async Task<bool> PrepareWubForSwitchAsync(CancellationToken cancellationToken)
	{
		using WubSetupDialog dialog = new WubSetupDialog(wubPackageStore, homeWebView.CoreWebView2?.Environment, null, autoDownload: true, autoCloseOnReady: true);
		await dialog.ShowAsync(this, cancellationToken);
		return dialog.Imported;
	}

	private Task UpdateWindowsUpdateHeaderAsync()
	{
		var value = new
		{
			Kind = windowsUpdateStatus.Kind.ToString(),
			Blocked = (windowsUpdateStatus.IsBlocked == true),
			CanToggle = windowsUpdateStatus.CanStartToggle,
			Busy = isWindowsUpdateCommandBusy,
			Label = windowsUpdateStatus.Label,
			Detail = windowsUpdateStatus.Detail
		};
		return ExecuteScriptAsync("window.__nestgWindowsUpdate?.setState(" + JsonSerializer.Serialize(value, JsonOptions) + ");");
	}

	private void RenderEvents(DesktopEventPageResponse events)
	{
		if (!homeWebView.IsDisposed)
		{
			currentDetailData = null;
			ClearCurrentLibraryDetail();
			pendingDetailScrollY = null;
			currentEventData = events;
			currentSurface = StoreSurface.Events;
			homeWebView.NavigateToString(StoreEventHtmlBuilder.BuildDocument(currentContext, events));
		}
	}

	private void RenderLibraryDetail(DesktopLibraryDetailResponse detail)
	{
		if (!homeWebView.IsDisposed)
		{
			currentDetailData = null;
			string text = BeginFix54Document();
			pendingDetailScrollY = null;
			currentLibraryDetail = detail;
			currentCloudSaveRestoreVersions = Array.Empty<CloudSaveRestoreVersion>();
			currentSurface = StoreSurface.LibraryDetail;
			WatchLibraryDetail(detail.LibraryId);
			DateTime? adminRequestCooldownUntilUtc = DenuvoFixInstallService.GetAdminRequestCooldownUntilUtc(detail.LibraryId);
			homeWebView.NavigateToString(StoreLibraryDetailHtmlBuilder.BuildDocument(detail, currentContext, isActivatingLibraryGame, isApplyingLibraryDenuvoFix, isApplyingLibraryPypassFix, isApplyingLibraryOnlineFix, adminRequestCooldownUntilUtc, showPypassGuide: true, showDenuvoGuide: true, ResolveDenuvoGuidePayloadJson(detail, adminRequestCooldownUntilUtc), isCloudSavingLibraryGame, text, isFixing54));
		}
	}

	private void RenderProfile(DesktopAccountResponse account)
	{
		if (!homeWebView.IsDisposed)
		{
			currentDetailData = null;
			ClearCurrentLibraryDetail();
			pendingDetailScrollY = null;
			currentSurface = StoreSurface.Profile;
			homeWebView.NavigateToString(StoreProfileHtmlBuilder.BuildDocument(account, currentContext));
		}
	}

	private void RenderOrderHistory(DesktopAccountResponse account)
	{
		if (!homeWebView.IsDisposed)
		{
			currentDetailData = null;
			ClearCurrentLibraryDetail();
			pendingDetailScrollY = null;
			currentSurface = StoreSurface.OrderHistory;
			homeWebView.NavigateToString(StoreOrderHistoryHtmlBuilder.BuildDocument(account, currentContext));
		}
	}

	private void RenderWalletTopUp(DesktopAccountResponse account)
	{
		if (!homeWebView.IsDisposed)
		{
			currentDetailData = null;
			ClearCurrentLibraryDetail();
			pendingDetailScrollY = null;
			currentSurface = StoreSurface.Wallet;
			homeWebView.NavigateToString(StoreWalletTopUpHtmlBuilder.BuildDocument(account, currentContext));
		}
	}

	private void RenderCart(DesktopCartResponse cart)
	{
		if (!homeWebView.IsDisposed)
		{
			currentDetailData = null;
			ClearCurrentLibraryDetail();
			pendingDetailScrollY = null;
			currentCartData = cart;
			currentSurface = StoreSurface.Cart;
			homeWebView.NavigateToString(StoreCartHtmlBuilder.BuildDocument(cart, currentContext, currentAccountData));
		}
	}

	private void ClearCurrentLibraryDetail()
	{
		CancelFix54ForNavigation();
		currentLibraryDetail = null;
		currentCloudSaveRestoreVersions = Array.Empty<CloudSaveRestoreVersion>();
		ClearWatchedLibraryDetail();
	}

	private void WatchLibraryDetail(Guid libraryId)
	{
		if (!(libraryId == Guid.Empty) && !(watchedLibraryDetailId == libraryId))
		{
			ClearWatchedLibraryDetail();
			watchedLibraryDetailId = libraryId;
			DesktopAppContext.DenuvoActiveRealtimeClient.WatchLibraryDetailAsync(libraryId, disposalCancellation.Token);
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

	private async Task LoadHomeAsync()
	{
		if (isLoadingHome)
		{
			return;
		}
		isLoadingHome = true;
		try
		{
			CancellationToken cancellationToken = disposalCancellation.Token;
			bool hadSession = DesktopAppContext.AuthSessionManager.CurrentSession != null;
			string accessToken = await DesktopAppContext.AuthSessionManager.GetAccessTokenAsync(cancellationToken);
			if (hadSession && string.IsNullOrWhiteSpace(accessToken))
			{
				await NavigateToLoginForExpiredSessionAsync();
				return;
			}
			AuthSession session = DesktopAppContext.AuthSessionManager.CurrentSession;
			CancelCatalogRequest();
			pendingHomeStateJson = null;
			currentCatalogQuery = new StoreCatalogQuery(null, null, null, "default", null);
			Task<StorefrontHomeResponse> homeTask = storefrontApiClient.GetDesktopHomeAsync(accessToken, cancellationToken);
			Task<IReadOnlyDictionary<Guid, string>> salePortraitImagesTask = TryGetSalePortraitImagesAsync(cancellationToken);
			Task<IReadOnlyList<DesktopHomeBannerSlotResponse>> heroBannerSlotsTask = TryGetDesktopHomeBannerSlotsAsync(cancellationToken);
			Task<IReadOnlyList<StoreComboCardResponse>> comboCardsTask = TryGetComboCardsAsync(cancellationToken);
			await Task.WhenAll(homeTask, salePortraitImagesTask, heroBannerSlotsTask, comboCardsTask);
			currentPageData = StorePageData.FromApi(await homeTask, await salePortraitImagesTask, await heroBannerSlotsTask, await comboCardsTask);
			string userName = (string.IsNullOrWhiteSpace(session?.DisplayName) ? "Người dùng NestG" : session.DisplayName.Trim());
			string walletText = currentContext.WalletBalanceText;
			int cartCount = currentContext.CartItemCount;
			bool isAuthenticated = !string.IsNullOrWhiteSpace(accessToken);
			bool isWalletBalanceAvailable = !isAuthenticated || currentContext.IsWalletBalanceAvailable;
			bool isCartCountAvailable = !isAuthenticated || currentContext.IsCartCountAvailable;
			if (isAuthenticated)
			{
				int? num = await TryGetCartCountAsync(accessToken, cancellationToken);
				if (num.HasValue)
				{
					cartCount = num.Value;
					isCartCountAvailable = true;
				}
				DesktopAccountResponse desktopAccountResponse = await TryGetAccountAsync(accessToken, cancellationToken);
				if (desktopAccountResponse != null)
				{
					currentAccountData = desktopAccountResponse;
					userName = (string.IsNullOrWhiteSpace(desktopAccountResponse.DisplayName) ? userName : desktopAccountResponse.DisplayName.Trim());
					walletText = (string.IsNullOrWhiteSpace(desktopAccountResponse.WalletBalanceText) ? walletText : desktopAccountResponse.WalletBalanceText.Trim());
					isWalletBalanceAvailable = !string.IsNullOrWhiteSpace(desktopAccountResponse.WalletBalanceText);
				}
			}
			currentContext = new StoreWebHomeContext(userName, walletText, cartCount, isAuthenticated, DesktopAppContext.Settings.CurrentVersion, isWalletBalanceAvailable, isCartCountAvailable, currentContext.NotificationUnreadCount);
			RenderHome();
		}
		catch (OperationCanceledException) when (disposalCancellation.IsCancellationRequested)
		{
		}
		catch (Exception ex2)
		{
			if (await TryHandleExpiredSessionAsync(ex2))
			{
				return;
			}
			DesktopDiagnosticLog.Write("Storefront", "HomeLoadFailed", $"Type={ex2.GetType().Name}; HResult=0x{ex2.HResult:X8}");
			RenderError(DesktopUserMessage.FromException(ex2, "Không thể tải cửa hàng. Hãy kiểm tra kết nối mạng rồi thử lại."));
		}
		finally
		{
			isLoadingHome = false;
		}
	}

	private async Task<int?> TryGetCartCountAsync(string accessToken, CancellationToken cancellationToken)
	{
		try
		{
			return await storefrontApiClient.GetCartItemCountAsync(accessToken, cancellationToken);
		}
		catch
		{
			return null;
		}
	}

	private async Task<DesktopAccountResponse?> TryGetAccountAsync(string accessToken, CancellationToken cancellationToken)
	{
		try
		{
			return await storefrontApiClient.GetAccountAsync(accessToken, cancellationToken);
		}
		catch
		{
			return null;
		}
	}

	private async Task<IReadOnlyDictionary<Guid, string>> TryGetSalePortraitImagesAsync(CancellationToken cancellationToken)
	{
		try
		{
			return await storefrontApiClient.GetSalePortraitImagesAsync(cancellationToken);
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			throw;
		}
		catch
		{
			return new Dictionary<Guid, string>();
		}
	}

	private async Task<IReadOnlyList<DesktopHomeBannerSlotResponse>> TryGetDesktopHomeBannerSlotsAsync(CancellationToken cancellationToken)
	{
		try
		{
			return await storefrontApiClient.GetDesktopHomeBannerSlotsAsync(cancellationToken);
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			throw;
		}
		catch
		{
			return Array.Empty<DesktopHomeBannerSlotResponse>();
		}
	}

	private async Task RefreshLauncherUpdateAsync(bool showOptionalPopup)
	{
		if (isRefreshingLauncherUpdate || isStartingLauncherUpdate)
		{
			return;
		}
		isRefreshingLauncherUpdate = true;
		try
		{
			string accessToken = await DesktopAppContext.AuthSessionManager.GetAccessTokenAsync(disposalCancellation.Token);
			if (string.IsNullOrWhiteSpace(accessToken))
			{
				availableDesktopUpdate = null;
				return;
			}
			DesktopAppSettings settings = DesktopAppContext.Settings;
			DesktopNotificationFeed desktopNotificationFeed = await notificationApiClient.GetFeedAsync(accessToken, settings.Platform, settings.ReleaseChannel, settings.CurrentVersion, settings.CurrentBuildNumber, disposalCancellation.Token);
			StoreWebHomeForm storeWebHomeForm = this;
			DesktopAppUpdateInfo update = desktopNotificationFeed.Update;
			storeWebHomeForm.availableDesktopUpdate = ((update != null && update.IsUpdateAvailable) ? update : null);
			desktopNotifications = desktopNotificationFeed.Notifications;
			currentContext = currentContext with
			{
				NotificationUnreadCount = Math.Max(0, desktopNotificationFeed.UnreadCount)
			};
			await UpdateNotificationBadgeAsync(currentContext.NotificationUnreadCount);
			if (showOptionalPopup)
			{
				await ShowPopupNotificationsAsync(accessToken);
			}
			if (availableDesktopUpdate != null)
			{
				await TryApplyLauncherPackageMetadataAsync(availableDesktopUpdate, disposalCancellation.Token);
				string a = BuildLauncherUpdateKey(availableDesktopUpdate);
				if (availableDesktopUpdate.IsForceUpdate)
				{
					await ShowLauncherUpdateDialogAsync(availableDesktopUpdate);
				}
				else if (showOptionalPopup && !string.Equals(a, dismissedLauncherUpdateKey, StringComparison.OrdinalIgnoreCase) && !string.Equals(a, lastShownOptionalLauncherUpdateKey, StringComparison.OrdinalIgnoreCase))
				{
					lastShownOptionalLauncherUpdateKey = a;
					await ShowLauncherUpdateDialogAsync(availableDesktopUpdate);
				}
			}
		}
		catch (OperationCanceledException) when (disposalCancellation.IsCancellationRequested)
		{
		}
		catch
		{
		}
		finally
		{
			isRefreshingLauncherUpdate = false;
		}
	}

	private async Task TryApplyLauncherPackageMetadataAsync(DesktopAppUpdateInfo update, CancellationToken cancellationToken)
	{
		try
		{
			DesktopLauncherPackageMetadata desktopLauncherPackageMetadata = await packageUpdateService.GetPackageMetadataAsync(update, cancellationToken);
			if ((object)desktopLauncherPackageMetadata != null)
			{
				update.FileName = (string.IsNullOrWhiteSpace(desktopLauncherPackageMetadata.FileName) ? update.FileName : desktopLauncherPackageMetadata.FileName.Trim());
				update.Size = ((desktopLauncherPackageMetadata.Size > 0) ? desktopLauncherPackageMetadata.Size : update.Size);
				update.SizeText = (string.IsNullOrWhiteSpace(desktopLauncherPackageMetadata.SizeText) ? update.SizeText : desktopLauncherPackageMetadata.SizeText.Trim());
				if (!string.IsNullOrWhiteSpace(desktopLauncherPackageMetadata.DownloadUrl))
				{
					update.DownloadUrl = desktopLauncherPackageMetadata.DownloadUrl.Trim();
				}
			}
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			throw;
		}
		catch
		{
		}
	}

	private async Task StartLauncherPackageUpdateAsync()
	{
		if (isStartingLauncherUpdate || isSigningOut || isNavigatingToLogin || denuvoCloseStarted)
		{
			return;
		}
		DesktopAppUpdateInfo update = availableDesktopUpdate;
		if (update != null && update.IsUpdateAvailable)
		{
			isStartingLauncherUpdate = true;
			launcherUpdateRefreshTimer.Stop();
			try
			{
				try
				{
					await StopFix54Async();
					await SetLauncherUpdateStatusAsync("Đang chuẩn bị tải gói cập nhật launcher...", isBusy: true);
					Progress<string> progress = new Progress<string>(delegate(string status)
					{
						SetLauncherUpdateStatusAsync(status, isBusy: true);
					});
					DesktopPackageUpdateStageResult stageResult = await packageUpdateService.PreparePackageUpdateAsync(update, progress, disposalCancellation.Token).ConfigureAwait(continueOnCapturedContext: true);
					await SetLauncherUpdateStatusAsync("Đang dừng hỗ trợ Steam trước khi cập nhật launcher...", isBusy: true);
					await steamConnectionCoordinator.SetEnabledAsync(enabled: false).WaitAsync(TimeSpan.FromSeconds(120.0));
					await SetLauncherUpdateStatusAsync("Đang mở trình cập nhật. Launcher sẽ tự khởi động lại sau khi hoàn tất.", isBusy: true);
					SteamConnectionUiState state = steamConnectionCoordinator.GetState();
					if (state.Enabled || state.Busy || state.CleanupPending)
					{
						throw new InvalidOperationException("Bộ hỗ trợ Steam chưa dừng được. Vui lòng tắt lại trước khi cập nhật.");
					}
					packageUpdateService.StartUpdater(stageResult, Environment.ProcessId);
					Close();
				}
				catch (OperationCanceledException) when (disposalCancellation.IsCancellationRequested)
				{
				}
				catch (Exception exception)
				{
					if (!base.IsDisposed)
					{
						launcherUpdateRefreshTimer.Start();
						await SetLauncherUpdateStatusAsync("Không thể cập nhật launcher: " + UserError(exception), isBusy: false, isError: true);
					}
				}
				return;
			}
			finally
			{
				isStartingLauncherUpdate = false;
				if (!denuvoCloseStarted && !isNavigatingToLogin && !isSigningOut)
				{
					fix54Lifetime.AllowNewOperations();
				}
			}
		}
		await SetLauncherUpdateStatusAsync("Không tìm thấy bản cập nhật hợp lệ.", isBusy: false, isError: true);
	}

	private Task ShowLauncherUpdateDialogAsync(DesktopAppUpdateInfo update)
	{
		DesktopAppSettings settings = DesktopAppContext.Settings;
		LauncherUpdateDialogPayload value = new LauncherUpdateDialogPayload(update.DesktopAppVersionId, update.IsForceUpdate, FormatDesktopVersion(settings.CurrentVersion, settings.CurrentBuildNumber), string.IsNullOrWhiteSpace(update.Version) ? "Mới nhất" : update.Version.Trim(), Math.Max(0, update.BuildNumber), FormatReleaseChannel(update.ReleaseChannel), ResolveSizeText(update), string.IsNullOrWhiteSpace(update.ReleaseNotes) ? null : update.ReleaseNotes.Trim());
		return ExecuteScriptAsync("window.__nestgLauncherUpdate?.show(" + JsonSerializer.Serialize(value, JsonOptions) + ");");
	}

	private Task SetLauncherUpdateStatusAsync(string message, bool isBusy, bool isError = false)
	{
		var value = new
		{
			Message = message,
			IsBusy = isBusy,
			IsError = isError
		};
		return ExecuteScriptAsync("window.__nestgLauncherUpdate?.setStatus(" + JsonSerializer.Serialize(value, JsonOptions) + ");");
	}

	private static string BuildLauncherUpdateKey(DesktopAppUpdateInfo update)
	{
		if (!(update.DesktopAppVersionId != Guid.Empty))
		{
			return $"{update.Platform}:{update.ReleaseChannel}:{update.Version}:{update.BuildNumber}";
		}
		return update.DesktopAppVersionId.ToString("D");
	}

	private static string FormatDesktopVersion(string version, int buildNumber)
	{
		return $"{(string.IsNullOrWhiteSpace(version) ? "không xác định" : version.Trim())} (build {Math.Max(0, buildNumber)})";
	}

	private static string FormatReleaseChannel(string? releaseChannel)
	{
		string text = releaseChannel?.Trim().ToLowerInvariant();
		switch (text)
		{
		default:
			if (text.Length <= 0)
			{
				break;
			}
			return text;
		case "stable":
			return "Ổn định";
		case "preview":
			return "Xem trước";
		case "beta":
			return "Beta";
		case null:
			break;
		}
		return "Ổn định";
	}

	private async Task<IReadOnlyList<StoreComboCardResponse>> TryGetComboCardsAsync(CancellationToken cancellationToken)
	{
		try
		{
			return await storefrontApiClient.GetComboCardsAsync(cancellationToken);
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			throw;
		}
		catch
		{
			return Array.Empty<StoreComboCardResponse>();
		}
	}

	private static string ResolveSizeText(DesktopAppUpdateInfo update)
	{
		if (!string.IsNullOrWhiteSpace(update.SizeText))
		{
			return update.SizeText.Trim();
		}
		if (update.Size <= 0)
		{
			return "Đang cập nhật";
		}
		return FormatFileSize(update.Size);
	}

	private static string FormatFileSize(long bytes)
	{
		string[] array = new string[5] { "B", "KB", "MB", "GB", "TB" };
		double num = Math.Max(0L, bytes);
		int num2 = 0;
		while (num >= 1024.0 && num2 < array.Length - 1)
		{
			num /= 1024.0;
			num2++;
		}
		return $"{num:0.##} {array[num2]}";
	}

	private void HomeWebView_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
	{
		string message;
		string stringMessage = (TryGetStringWebMessage(e, out message) ? message : null);
		string webMessageAsJson;
		try
		{
			webMessageAsJson = e.WebMessageAsJson;
		}
		catch
		{
			webMessageAsJson = null;
		}
		if (!string.IsNullOrWhiteSpace(stringMessage) || !string.IsNullOrWhiteSpace(webMessageAsJson))
		{
			string source = e.Source;
			webMessageDispatcher.TryDispatch(() => ProcessHomeWebMessageAsync(stringMessage, webMessageAsJson, source));
		}
	}

	private async Task ProcessHomeWebMessageAsync(string? stringMessage, string? webMessageAsJson, string source)
	{
		if (stringMessage?.StartsWith("fix54:", StringComparison.Ordinal) ?? false)
		{
			await HandleFix54WebMessageAsync(stringMessage, source);
			return;
		}
		if (stringMessage?.StartsWith("activation-log:download:", StringComparison.Ordinal) ?? false)
		{
			if (source == "about:blank" && currentSurface == StoreSurface.LibraryDetail)
			{
				DesktopLibraryDetailResponse desktopLibraryDetailResponse = currentLibraryDetail;
				if (desktopLibraryDetailResponse != null)
				{
					await ActivationLogPopupSupport.DownloadAsync(this, activationLogDownload, stringMessage.Substring("activation-log:download:".Length), desktopLibraryDetailResponse.LibraryId, ExecuteScriptAsync, disposalCancellation.Token);
				}
			}
			return;
		}
		if (currentLibraryDetail != null && !string.IsNullOrWhiteSpace(stringMessage) && IsLibraryDetailWebMessage(stringMessage))
		{
			if (!stringMessage.StartsWith("cloudsave:", StringComparison.OrdinalIgnoreCase) || string.Equals(source, "about:blank", StringComparison.OrdinalIgnoreCase))
			{
				await HandleLibraryDetailWebMessageAsync(stringMessage);
			}
			return;
		}
		if (!string.IsNullOrWhiteSpace(stringMessage) && IsDetailWebMessage(stringMessage))
		{
			await HandleDetailWebMessageAsync(stringMessage);
			return;
		}
		WebHomeMessage message;
		try
		{
			message = (string.IsNullOrWhiteSpace(webMessageAsJson) ? null : JsonSerializer.Deserialize<WebHomeMessage>(webMessageAsJson, JsonOptions));
		}
		catch
		{
			return;
		}
		if (message == null || string.IsNullOrWhiteSpace(message.Type) || await HandleSteamConnectionUiAsync(message, source) || await HandleSupportContactUiAsync(message, source))
		{
			return;
		}
		string type = message.Type;
		if ((type == "startWarpOnboarding" || type == "dismissWarpOnboarding") ? true : false)
		{
			if (warpOnboardingDocumentReady && message.DocumentId == warpOnboardingDocumentId && WarpOnboardingSession.IsTrustedAction(source, currentSurface == StoreSurface.Home, message.Type))
			{
				if (message.Type == "startWarpOnboarding")
				{
					warpOnboarding.StartGuide();
				}
				else
				{
					warpOnboarding.Dismiss();
				}
				await UpdateWarpOnboardingAsync();
			}
			return;
		}
		if (message.Type.Equals("copyText", StringComparison.OrdinalIgnoreCase))
		{
			CopyTextToClipboard(message.Text);
			return;
		}
		if (message.Type.Equals("openNotifications", StringComparison.OrdinalIgnoreCase))
		{
			await ShowNotificationCenterAsync();
			return;
		}
		if (message.Type.Equals("startLauncherUpdate", StringComparison.OrdinalIgnoreCase))
		{
			await StartLauncherPackageUpdateAsync();
			return;
		}
		if (message.Type.Equals("dismissLauncherUpdate", StringComparison.OrdinalIgnoreCase))
		{
			DesktopAppUpdateInfo desktopAppUpdateInfo = availableDesktopUpdate;
			if (desktopAppUpdateInfo != null)
			{
				dismissedLauncherUpdateKey = BuildLauncherUpdateKey(desktopAppUpdateInfo);
			}
			return;
		}
		if (message.Type.Equals("exitLauncherUpdate", StringComparison.OrdinalIgnoreCase))
		{
			Close();
			return;
		}
		if (message.Type.Equals("openWalletTopup", StringComparison.OrdinalIgnoreCase))
		{
			await CaptureHomeStateAsync();
			await LoadWalletTopUpAsync();
			return;
		}
		if (message.Type.Equals("openWalletTopupModal", StringComparison.OrdinalIgnoreCase))
		{
			await OpenWalletTopUpModalAsync();
			return;
		}
		if (message.Type.Equals("submitWalletTopup", StringComparison.OrdinalIgnoreCase))
		{
			await BeginWalletTopUpAsync(message.Amount);
			return;
		}
		if (message.Type.Equals("refreshWalletTopupStatus", StringComparison.OrdinalIgnoreCase))
		{
			await RefreshWalletTopUpStatusAsync(message.OrderCode);
			return;
		}
		if (message.Type.Equals("closeWalletTopupPopup", StringComparison.OrdinalIgnoreCase))
		{
			activeWalletTopUpOrderCode = null;
			completedWalletTopUpOrderCode = null;
			await LoadWalletTopUpAsync();
			return;
		}
		if (message.Type.Equals("confirmWalletTopupSuccess", StringComparison.OrdinalIgnoreCase))
		{
			activeWalletTopUpOrderCode = null;
			completedWalletTopUpOrderCode = null;
			await LoadProfileAsync();
			return;
		}
		if (message.Type.Equals("confirmWalletTopupSuccessHome", StringComparison.OrdinalIgnoreCase))
		{
			activeWalletTopUpOrderCode = null;
			completedWalletTopUpOrderCode = null;
			await ExecuteScriptAsync("window.__nestgWalletTopUp?.close();");
			return;
		}
		if (message.Type.Equals("openSupportChat", StringComparison.OrdinalIgnoreCase))
		{
			await OpenSupportChatAsync();
			return;
		}
		if (message.Type.Equals("setSteamExclusions", StringComparison.OrdinalIgnoreCase))
		{
			if (string.Equals(source, "about:blank", StringComparison.OrdinalIgnoreCase))
			{
				bool? enabled = message.Enabled;
				if (enabled.HasValue)
				{
					bool valueOrDefault = enabled == true;
					await SetSteamExclusionsAsync(valueOrDefault);
				}
			}
			return;
		}
		if (message.Type.Equals("setWarpEnabled", StringComparison.OrdinalIgnoreCase))
		{
			if (currentSurface != 0 && string.Equals(source, "about:blank", StringComparison.OrdinalIgnoreCase))
			{
				bool? enabled = message.Enabled;
				if (enabled.HasValue)
				{
					bool valueOrDefault2 = enabled == true;
					await SetWarpEnabledAsync(valueOrDefault2);
				}
			}
			return;
		}
		if (message.Type.Equals("installWarp", StringComparison.OrdinalIgnoreCase))
		{
			if (currentSurface != 0 && string.Equals(source, "about:blank", StringComparison.OrdinalIgnoreCase))
			{
				await InstallWarpAsync();
			}
			return;
		}
		if (message.Type.Equals("setWindowsUpdateBlocked", StringComparison.OrdinalIgnoreCase))
		{
			if (currentSurface != 0 && string.Equals(source, "about:blank", StringComparison.OrdinalIgnoreCase))
			{
				bool? enabled = message.Enabled;
				if (enabled.HasValue)
				{
					bool valueOrDefault3 = enabled == true;
					await SetWindowsUpdateBlockedAsync(valueOrDefault3);
				}
			}
			return;
		}
		if (message.Type.Equals("closeSupportChat", StringComparison.OrdinalIgnoreCase))
		{
			supportPopup.HideChat();
			return;
		}
		if (message.Type.Equals("sendSupportMessage", StringComparison.OrdinalIgnoreCase))
		{
			await SendSupportMessageAsync(message.Body, message.ClientMessageId);
			return;
		}
		if (message.Type.Equals("markSupportRead", StringComparison.OrdinalIgnoreCase))
		{
			await MarkSupportReadAsync(message.MessageId);
			return;
		}
		if (message.Type.Equals("openProfile", StringComparison.OrdinalIgnoreCase) || message.Type.Equals("refreshProfile", StringComparison.OrdinalIgnoreCase))
		{
			await CaptureHomeStateAsync();
			await LoadProfileAsync();
			return;
		}
		if (message.Type.Equals("openEvents", StringComparison.OrdinalIgnoreCase))
		{
			await CaptureHomeStateAsync();
			await LoadEventsAsync();
			return;
		}
		if (message.Type.Equals("createChestEventCheckout", StringComparison.OrdinalIgnoreCase))
		{
			await CreateChestEventCheckoutAsync(message.TierCode, message.PaymentMethod);
			return;
		}
		if (message.Type.Equals("openNightMarketSlot", StringComparison.OrdinalIgnoreCase))
		{
			await OpenNightMarketSlotAsync(message.SlotNumber);
			return;
		}
		if (message.Type.Equals("createNightMarketPurchaseCheckout", StringComparison.OrdinalIgnoreCase))
		{
			await CreateNightMarketPurchaseCheckoutAsync(message.OpeningId, message.PaymentMethod);
			return;
		}
		if (message.Type.Equals("revealChestEventReward", StringComparison.OrdinalIgnoreCase))
		{
			await RevealChestEventRewardAsync(message.OpeningId);
			return;
		}
		if (message.Type.Equals("confirmChestEventReward", StringComparison.OrdinalIgnoreCase))
		{
			await ConfirmAndActivateChestEventRewardAsync(message.OpeningId);
			return;
		}
		if (message.Type.Equals("revealNightMarketOffer", StringComparison.OrdinalIgnoreCase))
		{
			await RevealNightMarketOfferAsync(message.OpeningId);
			return;
		}
		if (message.Type.Equals("openOrderHistory", StringComparison.OrdinalIgnoreCase) || message.Type.Equals("refreshOrderHistory", StringComparison.OrdinalIgnoreCase))
		{
			await CaptureHomeStateAsync();
			await LoadOrderHistoryAsync();
			return;
		}
		if (message.Type.Equals("openOrderCheckout", StringComparison.OrdinalIgnoreCase))
		{
			await OpenOrderCheckoutAsync(message.OrderCode);
			return;
		}
		if (message.Type.Equals("refreshOrderCheckoutStatus", StringComparison.OrdinalIgnoreCase))
		{
			await RefreshOrderCheckoutStatusAsync(message.OrderCode);
			return;
		}
		if (message.Type.Equals("payOrderCheckoutWithWallet", StringComparison.OrdinalIgnoreCase))
		{
			await PayOrderCheckoutWithWalletAsync(message.OrderCode);
			return;
		}
		if (message.Type.Equals("closeOrderCheckoutPopup", StringComparison.OrdinalIgnoreCase) || message.Type.Equals("confirmOrderCheckoutSuccess", StringComparison.OrdinalIgnoreCase))
		{
			activeOrderCheckoutOrderCode = null;
			activeOrderCheckoutPayment = null;
			if (orderCheckoutReturnTarget == "cart")
			{
				await LoadCartAsync();
			}
			else if (orderCheckoutReturnTarget == "orders")
			{
				await LoadOrderHistoryAsync();
			}
			return;
		}
		if (message.Type.Equals("openCart", StringComparison.OrdinalIgnoreCase))
		{
			await CaptureHomeStateAsync();
			await LoadCartAsync();
			return;
		}
		if (message.Type.Equals("applyCartPromo", StringComparison.OrdinalIgnoreCase))
		{
			await ApplyCartPromoAsync(message.PromoCode, message.ScrollY);
			return;
		}
		if (message.Type.Equals("openLibrary", StringComparison.OrdinalIgnoreCase) || message.Type.Equals("refreshLibrary", StringComparison.OrdinalIgnoreCase))
		{
			await CaptureHomeStateAsync();
			await LoadLibraryAsync();
			return;
		}
		if (message.Type.Equals("openLibraryItem", StringComparison.OrdinalIgnoreCase))
		{
			await OpenLibraryItemAsync(message.LibraryId);
			return;
		}
		if (message.Type.Equals("removeCartItem", StringComparison.OrdinalIgnoreCase))
		{
			await RemoveCartItemAsync(message.CartItemId, message.ScrollY);
			return;
		}
		if (message.Type.Equals("removeComboCartItem", StringComparison.OrdinalIgnoreCase))
		{
			await RemoveComboCartItemAsync(message.ComboCartItemId, message.ScrollY);
			return;
		}
		if (message.Type.Equals("clearCart", StringComparison.OrdinalIgnoreCase))
		{
			await ClearCartAsync(message.ScrollY);
			return;
		}
		if (message.Type.Equals("checkoutCart", StringComparison.OrdinalIgnoreCase))
		{
			await CheckoutCartAsync(message.PaymentMethod);
			return;
		}
		type = message.Type;
		if (type == null)
		{
			return;
		}
		switch (type.Length)
		{
		case 8:
			switch (type[4])
			{
			case 'm':
				if (type == "minimize")
				{
					base.WindowState = FormWindowState.Minimized;
				}
				break;
			case 'H':
				if (type == "backHome")
				{
					pendingDetailScrollY = null;
					currentDetailData = null;
					currentComboDetailData = null;
					ClearCurrentLibraryDetail();
					if (currentPageData == null)
					{
						RenderLoading();
						await LoadHomeAsync();
					}
					else
					{
						RenderHome();
					}
				}
				break;
			case 'G':
				if (type == "openGame")
				{
					await OpenGameDetailAsync(message.GameId, message.VersionId);
				}
				break;
			case 'o':
				if (type == "buyCombo")
				{
					await AddComboToCartAsync(message.ComboId, openCartAfterAdd: true);
				}
				break;
			case 'C':
				if (type == "openCart")
				{
					await ShowToastAsync("Giỏ hàng sẽ được đưa vào WebView shell sau khi trang chủ mới ổn định.");
				}
				break;
			}
			break;
		case 14:
			switch (type[0])
			{
			case 't':
				if (type == "toggleMaximize")
				{
					base.WindowState = ((base.WindowState != FormWindowState.Maximized) ? FormWindowState.Maximized : FormWindowState.Normal);
				}
				break;
			case 'a':
				if (type == "addComboToCart")
				{
					await AddComboToCartAsync(message.ComboId, openCartAfterAdd: false);
				}
				break;
			}
			break;
		case 5:
			switch (type[0])
			{
			case 'c':
				if (type == "close")
				{
					Close();
				}
				break;
			case 'r':
				if (type == "retry")
				{
					RenderLoading();
					await LoadHomeAsync();
				}
				break;
			}
			break;
		case 9:
			switch (type[0])
			{
			case 'o':
				if (type == "openCombo")
				{
					await OpenComboDetailAsync(message.ComboId);
				}
				break;
			case 'a':
				if (type == "addToCart")
				{
					await AddToCartAsync(message.GameId, message.VersionId);
				}
				break;
			}
			break;
		case 11:
			switch (type[4])
			{
			case 'L':
				if (type == "openLibrary")
				{
					await ShowToastAsync("Thư viện sẽ được nối vào WebView shell ở bước tiếp theo.");
				}
				break;
			case 'A':
				if (type == "openAccount")
				{
					await ShowToastAsync("Tài khoản và ví sẽ được nối vào WebView shell ở bước tiếp theo.");
				}
				break;
			case 'P':
				if (type == "openProfile")
				{
					await ShowToastAsync("Trang cá nhân đang được nối vào WebView shell mới.");
				}
				break;
			}
			break;
		case 15:
			switch (type[0])
			{
			case 'l':
				if (type == "loadCatalogPage")
				{
					await LoadCatalogPageAsync(message, source);
				}
				break;
			case 'o':
				if (type == "openWalletTopup")
				{
					await ShowToastAsync("Trang nạp ví đang được nối vào WebView shell mới.");
				}
				break;
			}
			break;
		case 10:
			if (type == "dragWindow")
			{
				BeginWindowDrag();
			}
			break;
		case 6:
			if (type == "buyNow")
			{
				await BuyNowAsync(message.GameId, message.VersionId, message.ExpectedPrice, message.PaymentMethod);
			}
			break;
		case 16:
			if (type == "openOrderHistory")
			{
				await LoadOrderHistoryAsync();
			}
			break;
		case 7:
			if (type == "signOut")
			{
				await SignOutAsync();
			}
			break;
		case 12:
		case 13:
			break;
		}
	}

	private async Task StartSupportRealtimeAsync()
	{
		_ = 2;
		try
		{
			string accessToken = await DesktopAppContext.AuthSessionManager.GetAccessTokenAsync(disposalCancellation.Token);
			if (!string.IsNullOrWhiteSpace(accessToken))
			{
				try
				{
					await DesktopAppContext.SupportRealtimeClient.StartAsync(disposalCancellation.Token);
				}
				catch (OperationCanceledException) when (disposalCancellation.IsCancellationRequested)
				{
					return;
				}
				catch
				{
				}
				await SynchronizeSupportConversationStateAsync(accessToken);
			}
		}
		catch (OperationCanceledException) when (disposalCancellation.IsCancellationRequested)
		{
		}
		catch
		{
		}
	}

	private async Task SynchronizeSupportConversationStateAsync(string? accessToken = null)
	{
		if (accessToken == null)
		{
			accessToken = await DesktopAppContext.AuthSessionManager.GetAccessTokenAsync(disposalCancellation.Token);
		}
		if (string.IsNullOrWhiteSpace(accessToken))
		{
			return;
		}
		DesktopSupportConversationResponse conversation = (currentSupportConversation = await supportApiClient.GetCurrentStateAsync(accessToken, disposalCancellation.Token));
		activeSupportConversationId = conversation?.ConversationId;
		if (conversation == null)
		{
			return;
		}
		await ExecuteScriptAsync("window.__nestgSupport?.updateConversation(" + JsonSerializer.Serialize(conversation, JsonOptions) + ");");
		Guid currentUserId = DesktopAppContext.AuthSessionManager.CurrentSession?.UserId ?? Guid.Empty;
		if (supportPopupTrigger.TryPresentUnread(conversation, currentUserId, supportPopup.IsChatOpen))
		{
			await OpenSupportChatAsync();
		}
		try
		{
			await DesktopAppContext.SupportRealtimeClient.JoinConversationAsync(conversation.ConversationId, disposalCancellation.Token);
		}
		catch (OperationCanceledException) when (disposalCancellation.IsCancellationRequested)
		{
		}
		catch
		{
		}
	}

	private async Task OpenSupportChatAsync()
	{
		try
		{
			await supportPopup.ShowChatAsync(this);
			string text = await DesktopAppContext.AuthSessionManager.GetAccessTokenAsync(disposalCancellation.Token);
			if (string.IsNullOrWhiteSpace(text))
			{
				await SetSupportErrorAsync("Bạn cần đăng nhập để chat hỗ trợ.");
				return;
			}
			DesktopSupportConversationDetailResponse desktopSupportConversationDetailResponse = await supportApiClient.GetCurrentAsync(text, disposalCancellation.Token);
			activeSupportConversationId = desktopSupportConversationDetailResponse.Conversation.ConversationId;
			currentSupportConversation = desktopSupportConversationDetailResponse.Conversation;
			supportPopup.LoadChat(desktopSupportConversationDetailResponse);
			try
			{
				await DesktopAppContext.SupportRealtimeClient.JoinConversationAsync(desktopSupportConversationDetailResponse.Conversation.ConversationId, disposalCancellation.Token);
			}
			catch (OperationCanceledException) when (disposalCancellation.IsCancellationRequested)
			{
			}
			catch
			{
			}
		}
		catch (OperationCanceledException) when (disposalCancellation.IsCancellationRequested)
		{
		}
		catch (Exception exception)
		{
			string text2 = UserError(exception, "Không thể tải cuộc trò chuyện hỗ trợ. Vui lòng thử lại.");
			if (!supportPopup.IsChatOpen)
			{
				await ShowUserMessageAsync(text2, MessageBoxIcon.Hand);
			}
			else
			{
				await SetSupportErrorAsync(text2);
			}
		}
	}

	private async Task SendSupportMessageAsync(string? body, string? clientMessageIdText)
	{
		string normalizedBody = (string.IsNullOrWhiteSpace(body) ? null : body.Trim());
		Guid result;
		Guid clientMessageId = (Guid.TryParse(clientMessageIdText, out result) ? result : Guid.NewGuid());
		if (string.IsNullOrWhiteSpace(normalizedBody))
		{
			await FailSupportMessageAsync(clientMessageId, "Nội dung tin nhắn không được để trống.");
			return;
		}
		try
		{
			string accessToken = await DesktopAppContext.AuthSessionManager.GetAccessTokenAsync(disposalCancellation.Token);
			if (string.IsNullOrWhiteSpace(accessToken))
			{
				await FailSupportMessageAsync(clientMessageId, "Bạn cần đăng nhập để chat hỗ trợ.");
				return;
			}
			if (!activeSupportConversationId.HasValue)
			{
				DesktopSupportConversationDetailResponse desktopSupportConversationDetailResponse = await supportApiClient.GetCurrentAsync(accessToken, disposalCancellation.Token);
				activeSupportConversationId = desktopSupportConversationDetailResponse.Conversation.ConversationId;
				supportPopup.LoadChat(desktopSupportConversationDetailResponse);
			}
			DesktopSupportMutationResponse desktopSupportMutationResponse = await supportApiClient.SendMessageAsync(activeSupportConversationId.Value, normalizedBody, clientMessageId, accessToken, disposalCancellation.Token);
			activeSupportConversationId = desktopSupportMutationResponse.Conversation.ConversationId;
			currentSupportConversation = desktopSupportMutationResponse.Conversation;
			supportPopup.Confirm(clientMessageId, desktopSupportMutationResponse);
			await MarkSupportReadCoreAsync(desktopSupportMutationResponse.SupportMessage?.MessageId);
		}
		catch (OperationCanceledException) when (disposalCancellation.IsCancellationRequested)
		{
		}
		catch (Exception exception)
		{
			await FailSupportMessageAsync(clientMessageId, UserError(exception, "Không thể gửi tin nhắn. Vui lòng thử lại."));
		}
	}

	private Task MarkSupportReadAsync(string? messageIdText)
	{
		Guid result;
		Guid? messageId = (Guid.TryParse(messageIdText, out result) ? new Guid?(result) : ((Guid?)null));
		return MarkSupportReadCoreAsync(messageId);
	}

	private async Task MarkSupportReadCoreAsync(Guid? messageId)
	{
		if (!supportPopup.IsChatOpen || !activeSupportConversationId.HasValue)
		{
			return;
		}
		try
		{
			string text = await DesktopAppContext.AuthSessionManager.GetAccessTokenAsync(disposalCancellation.Token);
			if (!string.IsNullOrWhiteSpace(text))
			{
				await ApplySupportReadStateAsync(await supportApiClient.MarkReadAsync(activeSupportConversationId.Value, messageId, text, disposalCancellation.Token));
			}
		}
		catch
		{
		}
	}

	private Task SetSupportErrorAsync(string message)
	{
		supportPopup.SetError(message);
		return Task.CompletedTask;
	}

	private Task FailSupportMessageAsync(Guid clientMessageId, string message)
	{
		supportPopup.Fail(clientMessageId, message);
		return Task.CompletedTask;
	}

	private void HandleSupportConversationUpdated(object? sender, DesktopSupportConversationUpdatedPayload payload)
	{
		RunOnUiThreadAsync(async delegate
		{
			if (payload.Conversation != null)
			{
				Guid userId = payload.Conversation.UserId;
				Guid? obj = DesktopAppContext.AuthSessionManager.CurrentSession?.UserId;
				if (!(userId != obj))
				{
					int previousUnread = ((currentSupportConversation?.ConversationId == payload.Conversation.ConversationId) ? currentSupportConversation.CustomerUnreadCount : 0);
					currentSupportConversation = payload.Conversation;
					activeSupportConversationId = payload.Conversation.ConversationId;
					supportPopup.UpdateConversation(payload.Conversation);
					await ExecuteScriptAsync("window.__nestgSupport?.updateConversation(" + JsonSerializer.Serialize(payload.Conversation, JsonOptions) + ");");
					Guid currentUserId = DesktopAppContext.AuthSessionManager.CurrentSession?.UserId ?? Guid.Empty;
					if (payload.Conversation.CustomerUnreadCount > previousUnread && supportPopupTrigger.TryPresentUnread(payload.Conversation, currentUserId, supportPopup.IsChatOpen))
					{
						await OpenSupportChatAsync();
					}
				}
			}
		});
	}

	private void HandleSupportMessageReceived(object? sender, DesktopSupportMessageReceivedPayload payload)
	{
		RunOnUiThreadAsync(async delegate
		{
			Guid currentUserId = DesktopAppContext.AuthSessionManager.CurrentSession?.UserId ?? Guid.Empty;
			if (!(payload.Message.ConversationId == Guid.Empty) && !(payload.Conversation.UserId != currentUserId) && !payload.Message.IsInternal && !payload.Message.IsDeleted)
			{
				if (!activeSupportConversationId.HasValue)
				{
					activeSupportConversationId = payload.Message.ConversationId;
				}
				currentSupportConversation = payload.Conversation;
				supportPopup.Receive(payload);
				await ExecuteScriptAsync("window.__nestgSupport?.updateConversation(" + JsonSerializer.Serialize(payload.Conversation, JsonOptions) + ");");
				if (supportPopupTrigger.TryPresentMessage(payload, currentUserId, supportPopup.IsChatOpen))
				{
					await OpenSupportChatAsync();
				}
			}
		});
	}

	private void HandleSupportMessageRead(object? sender, DesktopSupportMessageReadPayload payload)
	{
		RunOnUiThreadAsync(() => ApplySupportReadStateAsync(payload));
	}

	private Task ApplySupportReadStateAsync(DesktopSupportMessageReadPayload payload)
	{
		if (currentSupportConversation?.ConversationId != payload.ConversationId)
		{
			return Task.CompletedTask;
		}
		currentSupportConversation.CustomerUnreadCount = payload.CustomerUnreadCount;
		currentSupportConversation.AdminUnreadCount = payload.AdminUnreadCount;
		supportPopup.UpdateConversation(currentSupportConversation);
		return ExecuteScriptAsync("window.__nestgSupport?.updateConversation(" + JsonSerializer.Serialize(currentSupportConversation, JsonOptions) + ");");
	}

	private void HandleSupportRealtimeReconnected(object? sender, EventArgs e)
	{
		RunOnUiThreadAsync(async delegate
		{
			_ = 1;
			try
			{
				bool wasOpen = supportPopup.IsChatOpen;
				await SynchronizeSupportConversationStateAsync();
				if (wasOpen)
				{
					await OpenSupportChatAsync();
				}
			}
			catch (OperationCanceledException) when (disposalCancellation.IsCancellationRequested)
			{
			}
			catch
			{
			}
		});
	}

	private void HandleDenuvoActiveTokenApplied(object? sender, DesktopDenuvoActiveTokenAppliedEventArgs payload)
	{
		payload.Handled = true;
		RunOnUiThreadAsync(async delegate
		{
			StoreUiHelper.RestoreWindowToForeground(this);
			if (currentLibraryDetail == null || currentLibraryDetail.LibraryId != payload.LibraryId)
			{
				await ShowToastAsync(payload.GameName + " đã nạp game thành công.");
			}
			else
			{
				await ShowDenuvoFixTokenAppliedPopupAsync(currentLibraryDetail, payload);
			}
		});
	}

	private void HandleDenuvoActiveRequestCancelled(object? sender, DesktopDenuvoActiveRequestCancelledEventArgs payload)
	{
		payload.Handled = true;
		RunOnUiThreadAsync(async delegate
		{
			if (currentLibraryDetail == null || currentLibraryDetail.LibraryId != payload.LibraryId)
			{
				await ShowToastAsync("Yêu cầu nạp " + payload.GameName + " đã bị hủy. " + payload.Reason);
			}
			else
			{
				await ShowDenuvoFixCancelledPopupAsync(currentLibraryDetail, payload);
			}
		});
	}

	private void HandleDenuvoActiveCooldownUnlocked(object? sender, DesktopDenuvoActiveCooldownUnlockedEventArgs payload)
	{
		payload.Handled = true;
		RunOnUiThreadAsync(async delegate
		{
			denuvoGuidePayloadJsonByLibraryId.Remove(payload.LibraryId);
			if (currentLibraryDetail == null || currentLibraryDetail.LibraryId != payload.LibraryId)
			{
				await ShowToastAsync(payload.Message);
			}
			else
			{
				await SetLibraryDenuvoUnlockedStateAsync();
				await ShowToastAsync(payload.Message);
			}
		});
	}

	private Task RunOnUiThreadAsync(Func<Task> action)
	{
		if (base.IsDisposed)
		{
			return Task.CompletedTask;
		}
		if (!base.InvokeRequired)
		{
			return action();
		}
		BeginInvoke(async delegate
		{
			try
			{
				await action();
			}
			catch
			{
			}
		});
		return Task.CompletedTask;
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
		DesktopDiagnosticLog.Write("WebView2", "UnhandledWebMessageException", null, exception);
		if (base.IsDisposed || base.Disposing || disposalCancellation.IsCancellationRequested)
		{
			return Task.CompletedTask;
		}
		return ShowUserMessageAsync(UserError(exception, "Không thể hoàn tất thao tác trong launcher. Vui lòng thử lại."), MessageBoxIcon.Hand);
	}

	private static bool TryGetStringWebMessage(CoreWebView2WebMessageReceivedEventArgs e, out string message)
	{
		try
		{
			message = e.TryGetWebMessageAsString();
			return !string.IsNullOrWhiteSpace(message);
		}
		catch
		{
			message = string.Empty;
			return false;
		}
	}

	private static void CopyTextToClipboard(string? text)
	{
		if (string.IsNullOrWhiteSpace(text))
		{
			return;
		}
		try
		{
			Clipboard.SetText(text);
		}
		catch
		{
		}
	}

	private static bool IsDetailWebMessage(string message)
	{
		if (!message.Equals("backHome", StringComparison.OrdinalIgnoreCase) && !message.StartsWith("gallery:", StringComparison.OrdinalIgnoreCase) && !message.StartsWith("game:", StringComparison.OrdinalIgnoreCase) && !message.StartsWith("open:", StringComparison.OrdinalIgnoreCase) && !message.StartsWith("review:", StringComparison.OrdinalIgnoreCase))
		{
			return message.StartsWith("cart:", StringComparison.OrdinalIgnoreCase);
		}
		return true;
	}

	private static bool IsLibraryDetailWebMessage(string message)
	{
		if (!message.Equals("play", StringComparison.OrdinalIgnoreCase) && !message.Equals("cloudsave:backup", StringComparison.OrdinalIgnoreCase) && !message.Equals("cloudsave:versions", StringComparison.OrdinalIgnoreCase) && !message.StartsWith("cloudsave:restore:", StringComparison.OrdinalIgnoreCase) && !message.Equals("cloudredirect:manage", StringComparison.OrdinalIgnoreCase) && !message.StartsWith("denuvo:", StringComparison.OrdinalIgnoreCase) && !message.StartsWith("pypass:", StringComparison.OrdinalIgnoreCase) && !message.StartsWith("online:", StringComparison.OrdinalIgnoreCase))
		{
			return message.StartsWith("open:", StringComparison.OrdinalIgnoreCase);
		}
		return true;
	}

	private async Task HandleLibraryDetailWebMessageAsync(string message)
	{
		if (isFixing54 && !message.StartsWith("open:", StringComparison.OrdinalIgnoreCase))
		{
			await ShowToastAsync("FIX 54 đang chạy. Hãy chờ hoàn tất hoặc bấm Hủy trước khi chạy tác vụ khác.");
		}
		else if ((!isManagingCloudRedirect || (!message.Equals("play", StringComparison.OrdinalIgnoreCase) && !message.StartsWith("cloudsave:", StringComparison.OrdinalIgnoreCase))) && currentLibraryDetail != null)
		{
			if (message.Equals("play", StringComparison.OrdinalIgnoreCase))
			{
				await ActivateCurrentLibraryGameAsync();
			}
			else if (message.Equals("cloudsave:backup", StringComparison.OrdinalIgnoreCase))
			{
				await BackupCurrentLibraryGameAsync();
			}
			else if (message.Equals("cloudsave:versions", StringComparison.OrdinalIgnoreCase))
			{
				await OpenCurrentLibraryRestoreVersionsAsync();
			}
			else if (message.StartsWith("cloudsave:restore:", StringComparison.OrdinalIgnoreCase))
			{
				await RestoreCurrentLibraryGameAsync(message.Substring("cloudsave:restore:".Length));
			}
			else if (message.Equals("denuvo:apply", StringComparison.OrdinalIgnoreCase) || message.Equals("denuvo:update", StringComparison.OrdinalIgnoreCase))
			{
				await ApplyCurrentLibraryDenuvoFixAsync();
			}
			else if (message.Equals("cloudredirect:manage", StringComparison.OrdinalIgnoreCase))
			{
				await ShowCloudRedirectManagementAsync(currentLibraryDetail);
			}
			else if (message.Equals("pypass:cancel", StringComparison.OrdinalIgnoreCase))
			{
				pypassInstallCancellation?.Cancel();
			}
			else if (message.StartsWith("pypass:", StringComparison.OrdinalIgnoreCase))
			{
				await ApplyCurrentLibraryPypassFixAsync();
			}
			else if (message.StartsWith("online:", StringComparison.OrdinalIgnoreCase))
			{
				await ApplyCurrentLibraryOnlineFixAsync();
			}
			else if (message.StartsWith("open:", StringComparison.OrdinalIgnoreCase))
			{
				OpenExternalUrl(Uri.UnescapeDataString(message.Substring("open:".Length)));
			}
		}
	}

	private async Task HandleDetailWebMessageAsync(string message)
	{
		if (message.Equals("backHome", StringComparison.OrdinalIgnoreCase))
		{
			pendingDetailScrollY = null;
			currentDetailData = null;
			if (currentPageData == null)
			{
				RenderLoading();
				await LoadHomeAsync();
			}
			else
			{
				RenderHome();
			}
		}
		else
		{
			if (currentDetailData == null)
			{
				return;
			}
			if (message.StartsWith("gallery:", StringComparison.OrdinalIgnoreCase))
			{
				if (int.TryParse(message.Substring("gallery:".Length), out var result))
				{
					await ShowGalleryLightboxAsync(result);
				}
			}
			else if (message.StartsWith("game:", StringComparison.OrdinalIgnoreCase))
			{
				if (Guid.TryParse(message.Substring("game:".Length), out var result2))
				{
					await LoadGameDetailAsync(result2, null, showLoadingPopup: true, null);
				}
			}
			else if (message.StartsWith("open:", StringComparison.OrdinalIgnoreCase))
			{
				string stringToUnescape = message.Substring("open:".Length);
				OpenExternalUrl(Uri.UnescapeDataString(stringToUnescape));
			}
			else if (message.StartsWith("review:", StringComparison.OrdinalIgnoreCase))
			{
				await HandleDetailReviewSubmitAsync(message);
			}
			else if (message.StartsWith("cart:", StringComparison.OrdinalIgnoreCase))
			{
				StoreGameDetailData storeGameDetailData = currentDetailData;
				string[] array = message.Substring("cart:".Length).Split(':');
				int result3;
				int value = ((array.Length != 0 && int.TryParse(array[0], out result3)) ? result3 : GetActiveEditionIndex(storeGameDetailData));
				bool flag = array.Length > 1 && (array[1] == "1" || array[1].Equals("true", StringComparison.OrdinalIgnoreCase));
				string paymentMethod = ((array.Length > 2) ? array[2] : null);
				StoreProductEdition storeProductEdition = ((storeGameDetailData.Editions.Count == 0) ? null : storeGameDetailData.Editions[Math.Clamp(value, 0, storeGameDetailData.Editions.Count - 1)]);
				if (flag)
				{
					await BuyNowAsync(storeGameDetailData.GameId, storeProductEdition?.VersionId, storeProductEdition?.CurrentPrice, paymentMethod);
				}
				else
				{
					await HandleDetailAddToCartAsync(storeGameDetailData, storeProductEdition, flag);
				}
			}
		}
	}

	private void AppendCatalogPageData(StoreCatalogPageData pageData)
	{
		if (currentPageData != null && pageData.Games.Count != 0)
		{
			StoreGame[] array = (from game in currentPageData.Games.Concat(pageData.Games)
				group game by game.GameId into @group
				select @group.First()).ToArray();
			int val = ((pageData.Page > 0 && pageData.PageSize > 0) ? (pageData.Page * pageData.PageSize) : (currentPageData.CatalogRenderedCount + pageData.Games.Count));
			currentPageData.Games = array;
			currentPageData.CatalogPage = Math.Max(currentPageData.CatalogPage, pageData.Page);
			currentPageData.CatalogPageSize = ((pageData.PageSize > 0) ? pageData.PageSize : currentPageData.CatalogPageSize);
			currentPageData.CatalogTotalItems = ((pageData.TotalCount > 0) ? pageData.TotalCount : currentPageData.CatalogTotalItems);
			currentPageData.CatalogHasMore = pageData.HasMore;
			currentPageData.CatalogNextPage = pageData.NextPage;
			currentPageData.CatalogRenderedCount = Math.Min(array.Length, Math.Max(currentPageData.CatalogRenderedCount, val));
		}
	}

	private void HomeWebView_NavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
	{
		if (currentSurface != StoreSurface.Home)
		{
			CancelCatalogRequest();
		}
		if (!e.IsUserInitiated)
		{
			ResetWarpOnboardingDocument(e.NavigationId);
			return;
		}
		if (e.Uri.StartsWith("about:blank", StringComparison.OrdinalIgnoreCase) || e.Uri.StartsWith("data:text/html", StringComparison.OrdinalIgnoreCase))
		{
			ResetWarpOnboardingDocument(e.NavigationId);
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
			ShowToastAsync("Không thể mở liên kết này.");
		}
	}

	private void ResetWarpOnboardingDocument(ulong navigationId)
	{
		warpOnboardingNavigationId = navigationId;
		warpOnboardingNavigationDocumentId = warpOnboardingDocumentId;
		warpOnboardingDocumentReady = false;
		warpOnboarding.DocumentChanged();
	}

	private async void HomeWebView_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
	{
		string completedDocumentId = warpOnboardingNavigationDocumentId;
		if (homeWebView.IsDisposed || homeWebView.CoreWebView2 == null)
		{
			return;
		}
		await RestorePendingHomeStateAsync(e.NavigationId, e.IsSuccess);
		if (e.IsSuccess)
		{
			await UpdateDenuvoRealtimeStatusAsync(DesktopAppContext.DenuvoActiveRealtimeClient.CurrentState);
		}
		await UpdateWarpHeaderAsync();
		await RefreshWarpStatusAsync();
		if (e.IsSuccess && e.NavigationId == warpOnboardingNavigationId && completedDocumentId != null && completedDocumentId == warpOnboardingDocumentId)
		{
			warpOnboardingDocumentReady = true;
			await UpdateWarpOnboardingAsync();
		}
		await UpdateWindowsUpdateHeaderAsync();
		await RefreshWindowsUpdateStatusAsync();
		int? num = null;
		int? num2 = pendingDetailScrollY;
		if (num2.HasValue)
		{
			int valueOrDefault = num2.GetValueOrDefault();
			if (currentDetailData != null)
			{
				num = valueOrDefault;
				pendingDetailScrollY = null;
				goto IL_03ec;
			}
		}
		num2 = pendingCartScrollY;
		if (num2.HasValue)
		{
			int valueOrDefault2 = num2.GetValueOrDefault();
			if (currentCartData != null)
			{
				num = valueOrDefault2;
				pendingCartScrollY = null;
			}
		}
		goto IL_03ec;
		IL_03ec:
		if (num.HasValue)
		{
			string script = string.Format(CultureInfo.InvariantCulture, "(() => {{ const targetY = Math.max(0, {0}); requestAnimationFrame(() => window.scrollTo({{ top: targetY, left: 0 }})); }})();", num.Value);
			await ExecuteScriptAsync(script);
		}
		if (currentSurface == StoreSurface.Home && currentSupportConversation != null)
		{
			await ExecuteScriptAsync("window.__nestgSupport?.updateConversation(" + JsonSerializer.Serialize(currentSupportConversation, JsonOptions) + ");");
		}
		DesktopAppUpdateInfo desktopAppUpdateInfo = availableDesktopUpdate;
		if (desktopAppUpdateInfo != null && desktopAppUpdateInfo.IsUpdateAvailable && desktopAppUpdateInfo.IsForceUpdate && !isStartingLauncherUpdate)
		{
			await ShowLauncherUpdateDialogAsync(desktopAppUpdateInfo);
		}
	}

	private async Task OpenGameDetailAsync(string? rawGameId, string? rawVersionId = null)
	{
		if (!Guid.TryParse(rawGameId, out var gameId))
		{
			await HideLoadingPopupAsync();
			await ShowToastAsync("Game không hợp lệ.");
		}
		else
		{
			Guid result;
			Guid? preferredVersionId = (Guid.TryParse(rawVersionId, out result) ? new Guid?(result) : ((Guid?)null));
			await CaptureHomeStateAsync();
			await LoadGameDetailAsync(gameId, null, showLoadingPopup: true, preferredVersionId);
		}
	}

	private async Task OpenComboDetailAsync(string? rawComboId)
	{
		if (!Guid.TryParse(rawComboId, out var comboId))
		{
			await ShowToastAsync("Combo không hợp lệ.");
			return;
		}
		await CaptureHomeStateAsync();
		if (isLoadingDetail)
		{
			return;
		}
		isLoadingDetail = true;
		await ShowLoadingPopupAsync("Đang tải Combo Game", "NestG đang đồng bộ thiết kế và danh sách game trong combo.");
		try
		{
			RenderComboDetail(StoreComboDetailData.FromApi(await storefrontApiClient.GetComboDetailAsync(comboId, disposalCancellation.Token)));
		}
		catch (OperationCanceledException) when (disposalCancellation.IsCancellationRequested)
		{
		}
		catch (Exception exception)
		{
			if (currentPageData != null)
			{
				RenderHome();
				await ShowToastAsync(UserError(exception, "Không thể tải chi tiết combo."));
			}
			else
			{
				RenderError(UserError(exception, "Không thể tải chi tiết combo."));
			}
		}
		finally
		{
			await HideLoadingPopupAsync();
			isLoadingDetail = false;
		}
	}

	private async Task CaptureHomeStateAsync()
	{
		if (currentSurface != StoreSurface.Home || homeWebView.IsDisposed || homeWebView.CoreWebView2 == null || currentDetailData != null)
		{
			return;
		}
		pendingHomeStateJson = null;
		CancelCatalogRequest();
		try
		{
			string text = await homeWebView.CoreWebView2.ExecuteScriptAsync("window.__nestgHome?.captureState ? window.__nestgHome.captureState() : null;");
			if (string.IsNullOrWhiteSpace(text) || text.Equals("null", StringComparison.OrdinalIgnoreCase) || text.Equals("undefined", StringComparison.OrdinalIgnoreCase))
			{
				return;
			}
			using (JsonDocument.Parse(text))
			{
				pendingHomeStateJson = NormalizeCapturedCatalogState(text);
			}
		}
		catch
		{
			pendingHomeStateJson = null;
		}
	}

	private async Task LoadGameDetailAsync(Guid gameId, int? restoreScrollY = null, bool showLoadingPopup = true, Guid? preferredVersionId = null)
	{
		if (isLoadingDetail)
		{
			return;
		}
		isLoadingDetail = true;
		StoreGameDetailData previousDetail = currentDetailData;
		bool openingFromLibrary = currentSurface == StoreSurface.Library;
		string libraryOpenError = null;
		pendingDetailScrollY = restoreScrollY;
		bool usesInlineLoadingPopup = showLoadingPopup && (openingFromLibrary || currentPageData != null || currentDetailData != null);
		if (usesInlineLoadingPopup)
		{
			await ShowLoadingPopupAsync("Đang tải chi tiết game", "NestG đang đồng bộ thông tin, hình ảnh và phiên bản sản phẩm.");
		}
		else if (showLoadingPopup)
		{
			RenderLoading();
		}
		try
		{
			CancellationToken cancellationToken = disposalCancellation.Token;
			string accessToken = await DesktopAppContext.AuthSessionManager.GetAccessTokenAsync(cancellationToken);
			StoreGameDetailData detailData = StoreGameDetailData.FromApi(await storefrontApiClient.GetProductDetailAsync(gameId, accessToken, cancellationToken));
			detailData.PreferredVersionId = preferredVersionId;
			if (openingFromLibrary)
			{
				await StoreWebViewNavigation.NavigateAsync(homeWebView.CoreWebView2, delegate
				{
					RenderDetail(detailData, !string.IsNullOrWhiteSpace(accessToken));
				}, cancellationToken, null);
			}
			else
			{
				RenderDetail(detailData, !string.IsNullOrWhiteSpace(accessToken));
			}
		}
		catch (OperationCanceledException) when (disposalCancellation.IsCancellationRequested)
		{
		}
		catch (Exception exception)
		{
			pendingDetailScrollY = null;
			if (openingFromLibrary && currentSurface == StoreSurface.Library)
			{
				libraryOpenError = UserError(exception, "Không thể tải chi tiết game. Vui lòng thử lại.");
			}
			else
			{
				if (previousDetail != null)
				{
					RenderDetail(previousDetail, currentContext.IsAuthenticated);
					await ShowUserMessageAsync(UserError(exception, "Không thể tải chi tiết game lúc này."), MessageBoxIcon.Exclamation);
					return;
				}
				if (currentPageData != null)
				{
					RenderHome();
					await ShowToastAsync(UserError(exception, "Không thể tải chi tiết game. Vui lòng thử lại."));
					return;
				}
				RenderError(UserError(exception, "Không thể tải chi tiết game. Vui lòng thử lại."));
			}
		}
		finally
		{
			isLoadingDetail = false;
			if (usesInlineLoadingPopup)
			{
				await HideLoadingPopupAsync();
			}
		}
		if (libraryOpenError != null)
		{
			await ShowToastAsync(libraryOpenError);
		}
	}

	private async Task AddToCartAsync(string? rawGameId, string? rawVersionId = null)
	{
		if (isAddingToCart)
		{
			return;
		}
		if (Guid.TryParse(rawGameId, out var gameId))
		{
			Guid? versionId = (Guid.TryParse(rawVersionId, out var result) ? new Guid?(result) : ((Guid?)null));
			isAddingToCart = true;
			try
			{
				try
				{
					string accessToken = await DesktopAppContext.AuthSessionManager.GetAccessTokenAsync(disposalCancellation.Token);
					if (string.IsNullOrWhiteSpace(accessToken))
					{
						await ShowToastAsync("Bạn cần đăng nhập để thêm game vào giỏ hàng.");
						return;
					}
					versionId = await ResolvePurchaseVersionIdAsync(gameId, versionId, accessToken);
					StorefrontApiClient.StorefrontCartMutationResponse result2 = await storefrontApiClient.AddToCartAsync(gameId, versionId, 1, accessToken, disposalCancellation.Token);
					await UpdateCartCountAsync(result2.CartItemCount);
					await ShowToastAsync(result2.Message);
				}
				catch (OperationCanceledException) when (disposalCancellation.IsCancellationRequested)
				{
				}
				catch (Exception exception)
				{
					await ShowToastAsync(UserError(exception));
				}
				return;
			}
			finally
			{
				isAddingToCart = false;
			}
		}
		await ShowToastAsync("Game không hợp lệ.");
	}

	private async Task BuyNowAsync(string? rawGameId, string? rawVersionId = null, decimal? expectedPrice = null, string? paymentMethod = null)
	{
		if (!Guid.TryParse(rawGameId, out var result))
		{
			await ShowToastAsync("Game không hợp lệ.");
			return;
		}
		Guid result2;
		Guid? versionId = (Guid.TryParse(rawVersionId, out result2) ? new Guid?(result2) : ((Guid?)null));
		await BuyNowAsync(result, versionId, expectedPrice, paymentMethod);
	}

	private async Task AddComboToCartAsync(string? rawComboId, bool openCartAfterAdd)
	{
		if (isAddingToCart)
		{
			return;
		}
		if (Guid.TryParse(rawComboId, out var comboId))
		{
			isAddingToCart = true;
			try
			{
				try
				{
					string text = await DesktopAppContext.AuthSessionManager.GetAccessTokenAsync(disposalCancellation.Token);
					if (string.IsNullOrWhiteSpace(text))
					{
						await ShowToastAsync("Bạn cần đăng nhập để thêm combo vào giỏ hàng.");
						return;
					}
					StorefrontApiClient.StorefrontCartMutationResponse result = await storefrontApiClient.AddComboToCartAsync(comboId, text, disposalCancellation.Token);
					await UpdateCartCountAsync(result.CartItemCount);
					if (openCartAfterAdd)
					{
						await LoadCartAsync();
						return;
					}
					await ExecuteScriptAsync("window.__nestgCombo?.notify(" + JsonSerializer.Serialize(result.Message) + ", 'success');");
					await ShowToastAsync(result.Message);
				}
				catch (OperationCanceledException) when (disposalCancellation.IsCancellationRequested)
				{
				}
				catch (Exception exception)
				{
					string message = UserError(exception);
					await ExecuteScriptAsync("window.__nestgCombo?.notify(" + JsonSerializer.Serialize(message) + ", 'error');");
					await ShowToastAsync(message);
				}
				return;
			}
			finally
			{
				isAddingToCart = false;
			}
		}
		await ShowToastAsync("Combo không hợp lệ.");
	}

	private async Task BuyNowAsync(Guid gameId, Guid? versionId, decimal? expectedPrice = null, string? paymentMethod = null)
	{
		if (isAddingToCart)
		{
			return;
		}
		string normalizedPaymentMethod = (string.Equals(paymentMethod?.Trim(), "wallet", StringComparison.OrdinalIgnoreCase) ? "wallet" : "sepay");
		isAddingToCart = true;
		try
		{
			string accessToken = await DesktopAppContext.AuthSessionManager.GetAccessTokenAsync(disposalCancellation.Token);
			if (string.IsNullOrWhiteSpace(accessToken))
			{
				await ShowToastAsync("Bạn cần đăng nhập để mua game.");
				return;
			}
			await ShowToastAsync("Đang tạo thanh toán cho game đã chọn...");
			versionId = await ResolvePurchaseVersionIdAsync(gameId, versionId, accessToken);
			DesktopCheckoutPaymentResponse desktopCheckoutPaymentResponse = await storefrontApiClient.CreateSingleItemCheckoutAsync(gameId, versionId, expectedPrice, null, accessToken, normalizedPaymentMethod, disposalCancellation.Token);
			if (expectedPrice.HasValue && decimal.Round(desktopCheckoutPaymentResponse.TotalAmount, 2, MidpointRounding.AwayFromZero) != decimal.Round(expectedPrice.Value, 2, MidpointRounding.AwayFromZero))
			{
				throw new InvalidOperationException("Giá thanh toán (" + desktopCheckoutPaymentResponse.TotalAmountText + ") không khớp giá đang hiển thị. Vui lòng tải lại cửa hàng trước khi tạo mã QR mới.");
			}
			activeOrderCheckoutOrderCode = desktopCheckoutPaymentResponse.OrderCode;
			orderCheckoutReturnTarget = "home";
			await ShowOrderCheckoutPopupAsync(desktopCheckoutPaymentResponse);
		}
		catch (OperationCanceledException) when (disposalCancellation.IsCancellationRequested)
		{
		}
		catch (Exception exception)
		{
			await ShowToastAsync(UserError(exception));
		}
		finally
		{
			isAddingToCart = false;
		}
	}

	private async Task LoadCartAsync(string? promoCode = null, bool preserveCurrentPromo = true)
	{
		if (isLoadingCart)
		{
			return;
		}
		isLoadingCart = true;
		await ShowLoadingPopupAsync("Đang tải giỏ hàng", "NestG đang đồng bộ sản phẩm và tổng thanh toán.");
		try
		{
			string text = await DesktopAppContext.AuthSessionManager.GetAccessTokenAsync(disposalCancellation.Token);
			if (string.IsNullOrWhiteSpace(text))
			{
				await NavigateToLoginForExpiredSessionAsync();
				return;
			}
			string text2 = promoCode;
			if (text2 == null && preserveCurrentPromo)
			{
				text2 = currentCartData?.PromoCode;
			}
			Task<DesktopCartResponse> cartTask = storefrontApiClient.GetCartAsync(text, text2, disposalCancellation.Token);
			Task<DesktopAccountResponse?> accountTask = TryGetAccountAsync(text, disposalCancellation.Token);
			await Task.WhenAll(cartTask, accountTask);
			DesktopCartResponse cart = await cartTask;
			currentAccountData = await accountTask;
			if (currentAccountData != null)
			{
				UpdateAccountContext(currentAccountData);
			}
			currentContext = currentContext with
			{
				CartItemCount = Math.Max(0, cart.TotalQuantity),
				IsCartCountAvailable = true
			};
			RenderCart(cart);
		}
		catch (OperationCanceledException) when (disposalCancellation.IsCancellationRequested)
		{
		}
		catch (Exception exception)
		{
			if (!(await TryHandleExpiredSessionAsync(exception)))
			{
				await ShowToastAsync(DesktopUserMessage.FromException(exception, "Không thể tải giỏ hàng. Vui lòng thử lại."));
			}
		}
		finally
		{
			await HideLoadingPopupAsync();
			isLoadingCart = false;
		}
	}

	private async Task<Guid> ResolvePurchaseVersionIdAsync(Guid gameId, Guid? requestedVersionId, string accessToken)
	{
		if (requestedVersionId.HasValue)
		{
			Guid valueOrDefault = requestedVersionId.GetValueOrDefault();
			if (valueOrDefault != Guid.Empty)
			{
				return valueOrDefault;
			}
		}
		StoreGameDetailData? storeGameDetailData = currentDetailData;
		Guid? guid;
		if (storeGameDetailData != null && storeGameDetailData.GameId == gameId)
		{
			guid = currentDetailData.GetActiveEdition()?.VersionId;
			if (guid.HasValue)
			{
				Guid valueOrDefault2 = guid.GetValueOrDefault();
				if (valueOrDefault2 != Guid.Empty)
				{
					return valueOrDefault2;
				}
			}
		}
		StorefrontProductDetailResponse storefrontProductDetailResponse = await storefrontApiClient.GetProductDetailAsync(gameId, accessToken, disposalCancellation.Token);
		guid = (storefrontProductDetailResponse.Editions.FirstOrDefault((StorefrontProductEditionResponse item) => item.IsActive) ?? storefrontProductDetailResponse.Editions.FirstOrDefault())?.VersionId;
		if (guid.HasValue)
		{
			Guid valueOrDefault3 = guid.GetValueOrDefault();
			if (!(valueOrDefault3 == Guid.Empty))
			{
				return valueOrDefault3;
			}
		}
		throw new InvalidOperationException("Không thể xác định phiên bản khớp với giá đang hiển thị. Vui lòng mở chi tiết game và thử lại.");
	}

	private async Task ShowPopupNotificationsAsync(string accessToken)
	{
		foreach (DesktopNotificationItem notification in (from item in desktopNotifications
			where item.IsPopup && !item.IsRead
			where !string.Equals(item.Type, "app_update", StringComparison.OrdinalIgnoreCase)
			where shownPopupNotificationIds.Add(item.NotificationId)
			orderby item.IsPinned descending, item.PublishedAt ?? item.CreatedAt descending
			select item).Take(3))
		{
			if (base.IsDisposed || base.Disposing || isNavigatingToLogin)
			{
				return;
			}
			bool hasAction = notification.GameId.HasValue || !string.IsNullOrWhiteSpace(notification.ActionUrl);
			isShowingStartupNotification = true;
			DialogResult result;
			try
			{
				await UpdateWarpOnboardingAsync();
				result = await LauncherMessageBox.ShowAsync(this, string.IsNullOrWhiteSpace(notification.Message) ? "NestG Launcher có một thông báo mới." : notification.Message.Trim(), string.IsNullOrWhiteSpace(notification.Title) ? "Thông báo NestG Launcher" : notification.Title.Trim(), hasAction ? MessageBoxButtons.YesNo : MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
			}
			finally
			{
				isShowingStartupNotification = false;
				if (!ShouldStopStartup)
				{
					await UpdateWarpOnboardingAsync();
				}
			}
			if (base.IsDisposed || base.Disposing || isNavigatingToLogin)
			{
				return;
			}
			notification.IsRead = true;
			try
			{
				await notificationApiClient.MarkReadAsync(accessToken, notification.NotificationId, disposalCancellation.Token);
			}
			catch
			{
			}
			if (hasAction && result == DialogResult.Yes)
			{
				await OpenNotificationActionAsync(notification);
			}
		}
		int num = desktopNotifications.Count((DesktopNotificationItem item) => !item.IsRead);
		currentContext = currentContext with
		{
			NotificationUnreadCount = num
		};
		await UpdateNotificationBadgeAsync(num);
	}

	private async Task ShowNotificationCenterAsync()
	{
		if (desktopNotifications.Count == 0)
		{
			await LauncherMessageBox.ShowAsync(this, "Bạn chưa có thông báo nào từ NestG Launcher.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
			return;
		}
		string[] value = (from item in (from item in desktopNotifications
				orderby item.IsPinned descending, item.PublishedAt ?? item.CreatedAt descending
				select item).Take(10)
			select (item.IsRead ? "" : "• ") + item.Title + "\n" + item.Message).ToArray();
		await LauncherMessageBox.ShowAsync(this, string.Join("\n\n", value), "Thông báo NestG Launcher", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
		if (base.IsDisposed || base.Disposing || isNavigatingToLogin)
		{
			return;
		}
		Guid[] unreadIds = (from item in desktopNotifications
			where !item.IsRead
			select item.NotificationId).ToArray();
		if (unreadIds.Length == 0)
		{
			return;
		}
		string text = await DesktopAppContext.AuthSessionManager.GetAccessTokenAsync(disposalCancellation.Token);
		if (!string.IsNullOrWhiteSpace(text))
		{
			try
			{
				await notificationApiClient.MarkAllReadAsync(text, (IReadOnlyCollection<Guid>)(object)unreadIds, disposalCancellation.Token);
			}
			catch
			{
			}
		}
		foreach (DesktopNotificationItem item in desktopNotifications.Where((DesktopNotificationItem item) => unreadIds.Contains(item.NotificationId)))
		{
			item.IsRead = true;
			item.ReadAt = DateTime.Now;
		}
		currentContext = currentContext with
		{
			NotificationUnreadCount = 0
		};
		await UpdateNotificationBadgeAsync(0);
	}

	private async Task OpenNotificationActionAsync(DesktopNotificationItem notification)
	{
		Guid? gameId = notification.GameId;
		if (gameId.HasValue)
		{
			Guid gameId2 = gameId.GetValueOrDefault();
			if (gameId2 != Guid.Empty)
			{
				await CaptureHomeStateAsync();
				await LoadGameDetailAsync(gameId2, null, showLoadingPopup: true, null);
				return;
			}
		}
		if (!string.IsNullOrWhiteSpace(notification.ActionUrl))
		{
			OpenExternalUrl(notification.ActionUrl);
		}
	}

	private Task UpdateNotificationBadgeAsync(int unreadCount)
	{
		int value = Math.Max(0, unreadCount);
		return ExecuteScriptAsync("document.querySelectorAll('[data-notification-count]').forEach(element => {" + $"element.hidden = {value} <= 0; element.textContent = String({value});" + "});");
	}

	private async Task LoadEventsAsync()
	{
		if (isLoadingEvents)
		{
			return;
		}
		isLoadingEvents = true;
		RenderLoading();
		try
		{
			string text = await DesktopAppContext.AuthSessionManager.GetAccessTokenAsync(disposalCancellation.Token);
			if (string.IsNullOrWhiteSpace(text))
			{
				await NavigateToLoginForExpiredSessionAsync();
				return;
			}
			RenderEvents(await storefrontApiClient.GetEventsAsync(text, disposalCancellation.Token));
		}
		catch (OperationCanceledException) when (disposalCancellation.IsCancellationRequested)
		{
		}
		catch (Exception exception)
		{
			if (!(await TryHandleExpiredSessionAsync(exception)))
			{
				RenderError(DesktopUserMessage.FromException(exception, "Không thể tải sự kiện. Vui lòng thử lại."));
			}
		}
		finally
		{
			isLoadingEvents = false;
		}
	}

	private async Task CreateChestEventCheckoutAsync(string? tierCode, string? paymentMethod)
	{
		if (string.IsNullOrWhiteSpace(tierCode))
		{
			await ShowEventToastAsync("Loại rương không hợp lệ.");
			return;
		}
		await RunEventCheckoutAsync((string token, CancellationToken cancellationToken) => storefrontApiClient.CreateChestEventCheckoutAsync(tierCode.Trim(), NormalizeEventPaymentMethod(paymentMethod), Guid.NewGuid().ToString("N"), token, cancellationToken));
	}

	private async Task OpenNightMarketSlotAsync(int? slotNumber)
	{
		bool flag = !slotNumber.HasValue;
		if (!flag)
		{
			bool flag2;
			switch (slotNumber)
			{
			default:
				flag2 = true;
				break;
			case null:
			case 1:
			case 2:
			case 3:
			case 4:
			case 5:
			case 6:
				flag2 = false;
				break;
			}
			flag = flag2;
		}
		if (flag)
		{
			await ShowEventToastAsync("Ô Night Market không hợp lệ.");
		}
		else
		{
			if (isCreatingEventCheckout)
			{
				return;
			}
			isCreatingEventCheckout = true;
			base.UseWaitCursor = true;
			bool openingSuccessStarted = false;
			try
			{
				string text = await DesktopAppContext.AuthSessionManager.GetAccessTokenAsync(disposalCancellation.Token);
				if (string.IsNullOrWhiteSpace(text))
				{
					throw new InvalidOperationException("Phiên đăng nhập đã hết hạn.");
				}
				string text2 = JsonSerializer.Serialize(new
				{
					EventActionType = "night_market_opening",
					ReferenceId = (await storefrontApiClient.OpenNightMarketSlotAsync(slotNumber.Value, Guid.NewGuid().ToString("N"), text, disposalCancellation.Token)).OpeningId,
					IsFree = true
				}, JsonOptions);
				await ExecuteScriptAsync("window.__nestgEvent?.freeOpeningSuccess(" + text2 + ");");
				openingSuccessStarted = true;
			}
			catch (OperationCanceledException) when (disposalCancellation.IsCancellationRequested)
			{
			}
			catch (Exception exception)
			{
				if (!openingSuccessStarted)
				{
					await StopEventPaymentProcessingAsync();
				}
				await ShowEventToastAsync(UserError(exception));
			}
			finally
			{
				base.UseWaitCursor = false;
				isCreatingEventCheckout = false;
			}
		}
	}

	private async Task CreateNightMarketPurchaseCheckoutAsync(string? rawOpeningId, string? paymentMethod)
	{
		if (!Guid.TryParse(rawOpeningId, out var openingId))
		{
			await ShowEventToastAsync("Ưu đãi Night Market không hợp lệ.");
			return;
		}
		await RunEventCheckoutAsync((string token, CancellationToken cancellationToken) => storefrontApiClient.CreateNightMarketPurchaseCheckoutAsync(openingId, NormalizeEventPaymentMethod(paymentMethod), Guid.NewGuid().ToString("N"), token, cancellationToken));
	}

	private async Task RunEventCheckoutAsync(Func<string, CancellationToken, Task<DesktopEventCheckoutResponse>> checkoutFactory)
	{
		if (isCreatingEventCheckout)
		{
			return;
		}
		isCreatingEventCheckout = true;
		base.UseWaitCursor = true;
		bool paymentSuccessStarted = false;
		try
		{
			string accessToken = await DesktopAppContext.AuthSessionManager.GetAccessTokenAsync(disposalCancellation.Token);
			if (string.IsNullOrWhiteSpace(accessToken))
			{
				throw new InvalidOperationException("Phiên đăng nhập đã hết hạn.");
			}
			DesktopEventCheckoutResponse result = await checkoutFactory(accessToken, disposalCancellation.Token);
			if (result.Checkout.IsWalletPayment && result.Checkout.IsPaid)
			{
				await PlayEventPaymentSuccessAsync(result);
				paymentSuccessStarted = true;
				currentAccountData = await TryGetAccountAsync(accessToken, disposalCancellation.Token);
				if (currentAccountData != null)
				{
					UpdateAccountContext(currentAccountData);
				}
				return;
			}
			StorePaymentQrDialog dialog = new StorePaymentQrDialog(storefrontApiClient, result.Checkout, closeAfterPaymentSuccess: true);
			try
			{
				await DeferredModalDialog.ShowAsync(this, () => dialog.ShowDialog(this));
				if (base.IsDisposed || base.Disposing)
				{
					return;
				}
				if (dialog.IsPaid)
				{
					await PlayEventPaymentSuccessAsync(result);
					paymentSuccessStarted = true;
					return;
				}
				await LoadEventsAsync();
			}
			finally
			{
				if (dialog != null)
				{
					((IDisposable)dialog).Dispose();
				}
			}
		}
		catch (OperationCanceledException) when (disposalCancellation.IsCancellationRequested)
		{
		}
		catch (Exception exception)
		{
			if (!paymentSuccessStarted)
			{
				await StopEventPaymentProcessingAsync();
			}
			await ShowEventToastAsync(UserError(exception));
		}
		finally
		{
			base.UseWaitCursor = false;
			isCreatingEventCheckout = false;
		}
	}

	private Task PlayEventPaymentSuccessAsync(DesktopEventCheckoutResponse result)
	{
		string text = JsonSerializer.Serialize(new { result.EventActionType, result.ReferenceId }, JsonOptions);
		return ExecuteScriptAsync("window.__nestgEvent?.paymentSuccess(" + text + ");");
	}

	private Task StopEventPaymentProcessingAsync()
	{
		return ExecuteScriptAsync("window.__nestgEvent?.paymentFailed();");
	}

	private async Task RevealChestEventRewardAsync(string? rawOpeningId)
	{
		if (!Guid.TryParse(rawOpeningId, out var openingId))
		{
			await ShowEventToastAsync("Lượt mở rương không hợp lệ.");
			return;
		}
		try
		{
			string text = await DesktopAppContext.AuthSessionManager.GetAccessTokenAsync(disposalCancellation.Token);
			if (string.IsNullOrWhiteSpace(text))
			{
				throw new InvalidOperationException("Phiên đăng nhập đã hết hạn.");
			}
			DesktopChestOpeningResponse obj = await storefrontApiClient.GetChestEventOpeningAsync(openingId, markRevealed: true, text, disposalCancellation.Token);
			if (obj.Reward == null)
			{
				throw new InvalidOperationException("Phần thưởng chưa sẵn sàng. Vui lòng kiểm tra lại trạng thái thanh toán.");
			}
			string text2 = JsonSerializer.Serialize(obj, JsonOptions);
			await ExecuteScriptAsync("window.__nestgEvent?.showChestReward(" + text2 + ");");
		}
		catch (OperationCanceledException) when (disposalCancellation.IsCancellationRequested)
		{
		}
		catch (Exception exception)
		{
			await ShowEventToastAsync(UserError(exception));
		}
	}

	private async Task ConfirmAndActivateChestEventRewardAsync(string? rawOpeningId)
	{
		if (!Guid.TryParse(rawOpeningId, out var openingId))
		{
			await ShowEventToastAsync("Phần thưởng không hợp lệ.");
			return;
		}
		base.UseWaitCursor = true;
		try
		{
			string accessToken = await DesktopAppContext.AuthSessionManager.GetAccessTokenAsync(disposalCancellation.Token);
			if (string.IsNullOrWhiteSpace(accessToken))
			{
				throw new InvalidOperationException("Phiên đăng nhập đã hết hạn.");
			}
			DesktopChestOpeningResponse desktopChestOpeningResponse = await storefrontApiClient.ConfirmChestEventOpeningAsync(openingId, accessToken, disposalCancellation.Token);
			if (desktopChestOpeningResponse.Reward == null || desktopChestOpeningResponse.Reward.LibraryId == Guid.Empty)
			{
				throw new InvalidOperationException("Game chưa được thêm vào thư viện.");
			}
			DesktopLibraryDetailResponse detail = await storefrontApiClient.GetLibraryDetailAsync(desktopChestOpeningResponse.Reward.LibraryId, accessToken, disposalCancellation.Token);
			Task task = WaitForNextWebNavigationAsync();
			RenderLibraryDetail(detail);
			await task;
			await ActivateLibraryGameAsync(detail);
		}
		catch (OperationCanceledException) when (disposalCancellation.IsCancellationRequested)
		{
		}
		catch (Exception exception)
		{
			await LauncherMessageBox.ShowAsync(this, UserError(exception, "Không thể xác nhận phần thưởng. Vui lòng thử lại."), "Không thể xác nhận phần thưởng", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
		}
		finally
		{
			base.UseWaitCursor = false;
		}
	}

	private async Task RevealNightMarketOfferAsync(string? rawOpeningId)
	{
		if (!Guid.TryParse(rawOpeningId, out var openingId))
		{
			await ShowEventToastAsync("Lượt mở thẻ không hợp lệ.");
			return;
		}
		try
		{
			string text = await DesktopAppContext.AuthSessionManager.GetAccessTokenAsync(disposalCancellation.Token);
			if (string.IsNullOrWhiteSpace(text))
			{
				throw new InvalidOperationException("Phiên đăng nhập đã hết hạn.");
			}
			await storefrontApiClient.MarkNightMarketOpeningRevealedAsync(openingId, text, disposalCancellation.Token);
			await LoadEventsAsync();
		}
		catch (OperationCanceledException) when (disposalCancellation.IsCancellationRequested)
		{
		}
		catch (Exception exception)
		{
			await ShowEventToastAsync(UserError(exception));
		}
	}

	private async Task WaitForNextWebNavigationAsync()
	{
		if (homeWebView.CoreWebView2 == null)
		{
			return;
		}
		TaskCompletionSource completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		homeWebView.CoreWebView2.NavigationCompleted += Handler;
		try
		{
			await Task.WhenAny(completion.Task, Task.Delay(2000, disposalCancellation.Token));
		}
		finally
		{
			homeWebView.CoreWebView2.NavigationCompleted -= Handler;
		}
		void Handler(object? sender, CoreWebView2NavigationCompletedEventArgs args)
		{
			completion.TrySetResult();
		}
	}

	private Task ShowEventToastAsync(string message)
	{
		return ExecuteScriptAsync("window.__nestgEvent?.toast(" + JsonSerializer.Serialize(message, JsonOptions) + ");");
	}

	private static string NormalizeEventPaymentMethod(string? paymentMethod)
	{
		if (!string.Equals(paymentMethod, "wallet", StringComparison.OrdinalIgnoreCase))
		{
			return "sepay";
		}
		return "wallet";
	}

	private async Task ApplyCartPromoAsync(string? promoCode, int? scrollY)
	{
		pendingCartScrollY = Math.Max(0, scrollY.GetValueOrDefault());
		await LoadCartAsync(promoCode?.Trim() ?? string.Empty, preserveCurrentPromo: false);
	}

	private async Task LoadLibraryAsync()
	{
		if (isLoadingLibrary)
		{
			return;
		}
		isLoadingLibrary = true;
		await ShowLoadingPopupAsync("Đang tải thư viện", "NestG đang đồng bộ game bạn đã mua.");
		try
		{
			string text = await DesktopAppContext.AuthSessionManager.GetAccessTokenAsync(disposalCancellation.Token);
			if (string.IsNullOrWhiteSpace(text))
			{
				await NavigateToLoginForExpiredSessionAsync();
				return;
			}
			DesktopLibraryListResponse library = await storefrontApiClient.GetLibraryAsync(text, disposalCancellation.Token);
			await HideLoadingPopupAsync();
			RenderLibrary(library);
		}
		catch (OperationCanceledException) when (disposalCancellation.IsCancellationRequested)
		{
		}
		catch (Exception exception)
		{
			await HideLoadingPopupAsync();
			if (!(await TryHandleExpiredSessionAsync(exception)))
			{
				await ShowToastAsync(DesktopUserMessage.FromException(exception, "Không thể tải thư viện. Vui lòng thử lại."));
			}
		}
		finally
		{
			isLoadingLibrary = false;
		}
	}

	private async Task OpenLibraryItemAsync(string? rawLibraryId)
	{
		if (isOpeningLibraryItem)
		{
			return;
		}
		if (!Guid.TryParse(rawLibraryId, out var libraryId))
		{
			await HideLoadingPopupAsync();
			await ShowToastAsync("Game trong thư viện không hợp lệ.");
			return;
		}
		isOpeningLibraryItem = true;
		bool renderingDetail = false;
		Exception openError = null;
		try
		{
			await ShowLoadingPopupAsync("Đang mở game", "NestG đang tải chi tiết game trong thư viện.");
			string text = await DesktopAppContext.AuthSessionManager.GetAccessTokenAsync(disposalCancellation.Token);
			if (string.IsNullOrWhiteSpace(text))
			{
				await NavigateToLoginForExpiredSessionAsync();
				return;
			}
			DesktopLibraryDetailResponse detail = await storefrontApiClient.GetLibraryDetailAsync(libraryId, text, disposalCancellation.Token);
			await CacheDenuvoGuidePayloadAsync(detail);
			disposalCancellation.Token.ThrowIfCancellationRequested();
			renderingDetail = true;
			await StoreWebViewNavigation.NavigateAsync(homeWebView.CoreWebView2, delegate
			{
				RenderLibraryDetail(detail);
			}, disposalCancellation.Token, null);
		}
		catch (OperationCanceledException) when (disposalCancellation.IsCancellationRequested)
		{
		}
		catch (Exception ex2)
		{
			if (renderingDetail)
			{
				DesktopLibraryListResponse library = currentLibraryData;
				if (library != null && !homeWebView.IsDisposed)
				{
					await StoreWebViewNavigation.NavigateAsync(homeWebView.CoreWebView2, delegate
					{
						RenderLibrary(library);
					}, disposalCancellation.Token, null);
				}
			}
			openError = ex2;
		}
		finally
		{
			isOpeningLibraryItem = false;
			await HideLoadingPopupAsync();
		}
		bool flag = openError != null;
		if (flag)
		{
			flag = !(await TryHandleExpiredSessionAsync(openError));
		}
		if (flag)
		{
			await ShowToastAsync(DesktopUserMessage.FromException(openError, "Không thể tải chi tiết thư viện. Vui lòng thử lại."));
		}
	}

	private async Task RemoveCartItemAsync(string? rawCartItemId, int? scrollY)
	{
		if (isLoadingCart || !Guid.TryParse(rawCartItemId, out var cartItemId))
		{
			return;
		}
		isLoadingCart = true;
		await ShowLoadingPopupAsync("Đang cập nhật giỏ hàng", "NestG đang xóa sản phẩm đã chọn.");
		try
		{
			string text = await DesktopAppContext.AuthSessionManager.GetAccessTokenAsync(disposalCancellation.Token);
			if (string.IsNullOrWhiteSpace(text))
			{
				throw new InvalidOperationException("Phiên đăng nhập đã hết hạn.");
			}
			StorefrontApiClient.StorefrontCartMutationResponse storefrontCartMutationResponse = await storefrontApiClient.RemoveCartItemAsync(cartItemId, currentCartData?.PromoCode, text, disposalCancellation.Token);
			currentContext = currentContext with
			{
				CartItemCount = Math.Max(0, storefrontCartMutationResponse.CartItemCount),
				IsCartCountAvailable = true
			};
		}
		catch (OperationCanceledException) when (disposalCancellation.IsCancellationRequested)
		{
			return;
		}
		catch (Exception exception)
		{
			await ShowToastAsync(UserError(exception));
			return;
		}
		finally
		{
			await HideLoadingPopupAsync();
			isLoadingCart = false;
		}
		pendingCartScrollY = Math.Max(0, scrollY.GetValueOrDefault());
		await LoadCartAsync();
	}

	private async Task RemoveComboCartItemAsync(string? rawComboCartItemId, int? scrollY)
	{
		if (isLoadingCart || !Guid.TryParse(rawComboCartItemId, out var comboCartItemId))
		{
			return;
		}
		isLoadingCart = true;
		await ShowLoadingPopupAsync("Đang cập nhật giỏ hàng", "NestG đang xóa combo đã chọn.");
		try
		{
			string text = await DesktopAppContext.AuthSessionManager.GetAccessTokenAsync(disposalCancellation.Token);
			if (string.IsNullOrWhiteSpace(text))
			{
				throw new InvalidOperationException("Phiên đăng nhập đã hết hạn.");
			}
			StorefrontApiClient.StorefrontCartMutationResponse storefrontCartMutationResponse = await storefrontApiClient.RemoveComboCartItemAsync(comboCartItemId, currentCartData?.PromoCode, text, disposalCancellation.Token);
			currentContext = currentContext with
			{
				CartItemCount = Math.Max(0, storefrontCartMutationResponse.CartItemCount),
				IsCartCountAvailable = true
			};
		}
		catch (OperationCanceledException) when (disposalCancellation.IsCancellationRequested)
		{
			return;
		}
		catch (Exception exception)
		{
			await ShowToastAsync(UserError(exception));
			return;
		}
		finally
		{
			await HideLoadingPopupAsync();
			isLoadingCart = false;
		}
		pendingCartScrollY = Math.Max(0, scrollY.GetValueOrDefault());
		await LoadCartAsync();
	}

	private async Task ClearCartAsync(int? scrollY)
	{
		if (isLoadingCart || currentCartData == null || (currentCartData.Items.Count == 0 && currentCartData.ComboItems.Count == 0))
		{
			return;
		}
		isLoadingCart = true;
		await ShowLoadingPopupAsync("Đang làm trống giỏ hàng", "NestG đang xóa các sản phẩm trong giỏ.");
		try
		{
			string accessToken = await DesktopAppContext.AuthSessionManager.GetAccessTokenAsync(disposalCancellation.Token);
			if (string.IsNullOrWhiteSpace(accessToken))
			{
				throw new InvalidOperationException("Phiên đăng nhập đã hết hạn.");
			}
			DesktopCartLineItemResponse[] array = currentCartData.Items.ToArray();
			foreach (DesktopCartLineItemResponse desktopCartLineItemResponse in array)
			{
				await storefrontApiClient.RemoveCartItemAsync(desktopCartLineItemResponse.CartItemId, currentCartData.PromoCode, accessToken, disposalCancellation.Token);
			}
			DesktopComboCartLineItemResponse[] array2 = currentCartData.ComboItems.ToArray();
			foreach (DesktopComboCartLineItemResponse desktopComboCartLineItemResponse in array2)
			{
				await storefrontApiClient.RemoveComboCartItemAsync(desktopComboCartLineItemResponse.ComboCartItemId, currentCartData.PromoCode, accessToken, disposalCancellation.Token);
			}
			currentContext = currentContext with
			{
				CartItemCount = 0,
				IsCartCountAvailable = true
			};
		}
		catch (OperationCanceledException) when (disposalCancellation.IsCancellationRequested)
		{
			return;
		}
		catch (Exception exception)
		{
			await ShowToastAsync(UserError(exception));
			return;
		}
		finally
		{
			await HideLoadingPopupAsync();
			isLoadingCart = false;
		}
		pendingCartScrollY = Math.Max(0, scrollY.GetValueOrDefault());
		await LoadCartAsync();
	}

	private async Task CheckoutCartAsync(string? paymentMethod = null)
	{
		if (isLoadingCart || currentCartData == null || currentCartData.IsEmpty)
		{
			return;
		}
		string normalizedPaymentMethod = (string.Equals(paymentMethod?.Trim(), "wallet", StringComparison.OrdinalIgnoreCase) ? "wallet" : "sepay");
		isLoadingCart = true;
		if (!(normalizedPaymentMethod == "wallet"))
		{
			await ShowLoadingPopupAsync("Đang tạo thanh toán", "NestG đang chuẩn bị mã QR cho đơn hàng.");
		}
		else
		{
			await ShowLoadingPopupAsync("Đang thanh toán bằng ví", "NestG đang kiểm tra số dư và hoàn tất đơn hàng.");
		}
		try
		{
			string text = await DesktopAppContext.AuthSessionManager.GetAccessTokenAsync(disposalCancellation.Token);
			if (string.IsNullOrWhiteSpace(text))
			{
				throw new InvalidOperationException("Phiên đăng nhập đã hết hạn.");
			}
			decimal expectedTotalAmount = currentCartData.TotalAmount;
			DesktopCheckoutPaymentResponse checkout = await storefrontApiClient.CreateCheckoutAsync(currentCartData.PromoCode, expectedTotalAmount, text, normalizedPaymentMethod, disposalCancellation.Token);
			if (decimal.Round(checkout.TotalAmount, 2, MidpointRounding.AwayFromZero) != decimal.Round(expectedTotalAmount, 2, MidpointRounding.AwayFromZero))
			{
				throw new InvalidOperationException("Tổng thanh toán đã thay đổi. Vui lòng tải lại giỏ hàng trước khi thanh toán.");
			}
			activeOrderCheckoutOrderCode = checkout.OrderCode;
			orderCheckoutReturnTarget = "cart";
			await HideLoadingPopupAsync();
			await ShowOrderCheckoutPopupAsync(checkout);
		}
		catch (OperationCanceledException) when (disposalCancellation.IsCancellationRequested)
		{
		}
		catch (Exception exception)
		{
			await HideLoadingPopupAsync();
			await ShowToastAsync(UserError(exception, "Không thể tạo thanh toán. Vui lòng thử lại."));
		}
		finally
		{
			isLoadingCart = false;
		}
	}

	private async Task LoadProfileAsync()
	{
		if (isLoadingProfile)
		{
			return;
		}
		isLoadingProfile = true;
		await ShowLoadingPopupAsync("Đang tải hồ sơ", "NestG đang đồng bộ thông tin tài khoản, ví và hoạt động gần đây.");
		try
		{
			string text = await DesktopAppContext.AuthSessionManager.GetAccessTokenAsync(disposalCancellation.Token);
			if (string.IsNullOrWhiteSpace(text))
			{
				await NavigateToLoginForExpiredSessionAsync();
				return;
			}
			DesktopAccountResponse desktopAccountResponse = await storefrontApiClient.GetAccountAsync(text, disposalCancellation.Token);
			currentContext = currentContext with
			{
				UserName = (string.IsNullOrWhiteSpace(desktopAccountResponse.DisplayName) ? currentContext.UserName : desktopAccountResponse.DisplayName.Trim()),
				WalletBalanceText = (string.IsNullOrWhiteSpace(desktopAccountResponse.WalletBalanceText) ? currentContext.WalletBalanceText : desktopAccountResponse.WalletBalanceText.Trim()),
				IsAuthenticated = true,
				IsWalletBalanceAvailable = !string.IsNullOrWhiteSpace(desktopAccountResponse.WalletBalanceText)
			};
			RenderProfile(desktopAccountResponse);
		}
		catch (OperationCanceledException) when (disposalCancellation.IsCancellationRequested)
		{
		}
		catch (Exception exception)
		{
			if (!(await TryHandleExpiredSessionAsync(exception)))
			{
				await ShowToastAsync(DesktopUserMessage.FromException(exception, "Không thể tải trang cá nhân. Vui lòng thử lại."));
			}
		}
		finally
		{
			await HideLoadingPopupAsync();
			isLoadingProfile = false;
		}
	}

	private async Task LoadOrderHistoryAsync()
	{
		if (isLoadingProfile)
		{
			return;
		}
		isLoadingProfile = true;
		await ShowLoadingPopupAsync("Đang tải lịch sử mua hàng", "NestG đang đồng bộ đơn hàng và trạng thái thanh toán của bạn.");
		try
		{
			string text = await DesktopAppContext.AuthSessionManager.GetAccessTokenAsync(disposalCancellation.Token);
			if (string.IsNullOrWhiteSpace(text))
			{
				await NavigateToLoginForExpiredSessionAsync();
				return;
			}
			DesktopAccountResponse account = (currentAccountData = await storefrontApiClient.GetAccountAsync(text, disposalCancellation.Token));
			UpdateAccountContext(account);
			RenderOrderHistory(account);
		}
		catch (OperationCanceledException) when (disposalCancellation.IsCancellationRequested)
		{
		}
		catch (Exception exception)
		{
			if (!(await TryHandleExpiredSessionAsync(exception)))
			{
				await ShowToastAsync(DesktopUserMessage.FromException(exception, "Không thể tải lịch sử mua hàng. Vui lòng thử lại."));
			}
		}
		finally
		{
			await HideLoadingPopupAsync();
			isLoadingProfile = false;
		}
	}

	private async Task OpenOrderCheckoutAsync(string? orderCode)
	{
		if (string.IsNullOrWhiteSpace(orderCode))
		{
			return;
		}
		await ShowLoadingPopupAsync("Đang mở thanh toán", "NestG đang tải lại mã QR và trạng thái đơn hàng.");
		try
		{
			string text = await DesktopAppContext.AuthSessionManager.GetAccessTokenAsync(disposalCancellation.Token);
			if (string.IsNullOrWhiteSpace(text))
			{
				throw new InvalidOperationException("Phiên đăng nhập đã hết hạn.");
			}
			DesktopCheckoutPaymentResponse checkout = await storefrontApiClient.GetCheckoutAsync(orderCode.Trim(), text, disposalCancellation.Token);
			await HideLoadingPopupAsync();
			activeOrderCheckoutOrderCode = checkout.OrderCode;
			orderCheckoutReturnTarget = "orders";
			await ShowOrderCheckoutPopupAsync(checkout);
		}
		catch (OperationCanceledException) when (disposalCancellation.IsCancellationRequested)
		{
		}
		catch (Exception exception)
		{
			await HideLoadingPopupAsync();
			await LauncherMessageBox.ShowAsync(this, UserError(exception, "Không thể mở chi tiết giao dịch. Vui lòng thử lại."), "Không thể mở chi tiết giao dịch", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
			await LoadOrderHistoryAsync();
		}
	}

	private async Task PayOrderCheckoutWithWalletAsync(string? orderCode)
	{
		if (isAddingToCart || string.IsNullOrWhiteSpace(orderCode))
		{
			return;
		}
		isAddingToCart = true;
		await ShowLoadingPopupAsync("Đang thanh toán bằng ví", "NestG đang kiểm tra số dư và hoàn tất đơn hàng.");
		try
		{
			string accessToken = await DesktopAppContext.AuthSessionManager.GetAccessTokenAsync(disposalCancellation.Token);
			if (string.IsNullOrWhiteSpace(accessToken))
			{
				throw new InvalidOperationException("Phiên đăng nhập đã hết hạn.");
			}
			DesktopCheckoutPaymentResponse checkout = await storefrontApiClient.PayCheckoutWithWalletAsync(orderCode.Trim(), accessToken, disposalCancellation.Token);
			activeOrderCheckoutOrderCode = checkout.OrderCode;
			completedOrderCheckoutOrderCode = (checkout.IsPaid ? checkout.OrderCode : completedOrderCheckoutOrderCode);
			activeOrderCheckoutPayment = checkout;
			currentAccountData = await TryGetAccountAsync(accessToken, disposalCancellation.Token);
			if (currentAccountData != null)
			{
				UpdateAccountContext(currentAccountData);
			}
			await ShowOrderCheckoutPopupAsync(checkout);
			await ShowToastAsync(checkout.Message);
		}
		catch (OperationCanceledException) when (disposalCancellation.IsCancellationRequested)
		{
		}
		catch (Exception exception)
		{
			await ShowToastAsync(UserError(exception, "Không thể thanh toán bằng ví. Vui lòng thử lại."));
		}
		finally
		{
			await HideLoadingPopupAsync();
			isAddingToCart = false;
		}
	}

	private async Task RefreshOrderCheckoutStatusAsync(string? orderCode)
	{
		if (string.IsNullOrWhiteSpace(orderCode))
		{
			await ShowToastAsync("Không tìm thấy mã đơn hàng để kiểm tra.");
			return;
		}
		try
		{
			string text = await DesktopAppContext.AuthSessionManager.GetAccessTokenAsync(disposalCancellation.Token);
			if (string.IsNullOrWhiteSpace(text))
			{
				throw new InvalidOperationException("Phiên đăng nhập đã hết hạn.");
			}
			DesktopCheckoutStatusResponse status = await storefrontApiClient.GetCheckoutStatusAsync(orderCode.Trim(), text, disposalCancellation.Token);
			await UpdateOrderCheckoutStatusPopupAsync(status);
			if (status.IsPaid)
			{
				await HandleOrderCheckoutPaidAsync(status);
			}
		}
		catch (OperationCanceledException) when (disposalCancellation.IsCancellationRequested)
		{
		}
		catch (Exception exception)
		{
			await ShowToastAsync(UserError(exception, "Không thể kiểm tra trạng thái. Vui lòng thử lại."));
			await UpdateOrderCheckoutStatusPopupAsync(new DesktopCheckoutStatusResponse
			{
				OrderCode = orderCode.Trim(),
				StatusTitle = "Chưa kiểm tra được",
				StatusDescription = UserError(exception, "Chưa thể kiểm tra trạng thái."),
				IsPaid = false
			});
		}
	}

	private async Task LoadWalletTopUpAsync()
	{
		if (isLoadingProfile)
		{
			return;
		}
		isLoadingProfile = true;
		await ShowLoadingPopupAsync("Đang tải ví", "NestG đang đồng bộ số dư, hạn mức nạp và các mức nạp nhanh.");
		try
		{
			string text = await DesktopAppContext.AuthSessionManager.GetAccessTokenAsync(disposalCancellation.Token);
			if (string.IsNullOrWhiteSpace(text))
			{
				await HideLoadingPopupAsync();
				await ShowToastAsync("Phiên đăng nhập đã hết hạn. Hãy đăng nhập lại để nạp ví.");
				return;
			}
			DesktopAccountResponse account = await storefrontApiClient.GetAccountAsync(text, disposalCancellation.Token);
			UpdateAccountContext(account);
			RenderWalletTopUp(account);
		}
		catch (OperationCanceledException) when (disposalCancellation.IsCancellationRequested)
		{
		}
		catch (Exception exception)
		{
			await ShowToastAsync(UserError(exception, "Không thể tải trang nạp ví. Vui lòng thử lại."));
		}
		finally
		{
			await HideLoadingPopupAsync();
			isLoadingProfile = false;
		}
	}

	private async Task OpenWalletTopUpModalAsync()
	{
		try
		{
			string text = await DesktopAppContext.AuthSessionManager.GetAccessTokenAsync(disposalCancellation.Token);
			if (string.IsNullOrWhiteSpace(text))
			{
				await ShowToastAsync("Phiên đăng nhập đã hết hạn. Hãy đăng nhập lại để nạp ví.");
				return;
			}
			DesktopAccountResponse desktopAccountResponse = (currentAccountData = await storefrontApiClient.GetAccountAsync(text, disposalCancellation.Token));
			UpdateAccountContext(desktopAccountResponse);
			var value = new
			{
				WalletBalanceText = desktopAccountResponse.WalletBalanceText,
				MinimumTopUpAmount = desktopAccountResponse.MinimumTopUpAmount,
				MaximumTopUpAmount = desktopAccountResponse.MaximumTopUpAmount,
				QuickAmounts = desktopAccountResponse.QuickAmounts.Select((DesktopAccountWalletQuickAmountResponse item) => new { item.Amount, item.AmountText })
			};
			await ExecuteScriptAsync("window.__nestgWalletTopUp?.open(" + JsonSerializer.Serialize(value, JsonOptions) + ");");
		}
		catch (OperationCanceledException) when (disposalCancellation.IsCancellationRequested)
		{
		}
		catch (Exception exception)
		{
			await ShowToastAsync(UserError(exception, "Không thể mở nạp ví. Vui lòng thử lại."));
		}
	}

	private async Task BeginWalletTopUpAsync(string? rawAmount)
	{
		if (isCreatingWalletTopUp)
		{
			return;
		}
		decimal amount = ParseCurrencyValue(rawAmount);
		if (!(amount <= 0m))
		{
			isCreatingWalletTopUp = true;
			await SetWalletTopUpBusyAsync(isBusy: true);
			try
			{
				try
				{
					string accessToken = await DesktopAppContext.AuthSessionManager.GetAccessTokenAsync(disposalCancellation.Token);
					if (string.IsNullOrWhiteSpace(accessToken))
					{
						throw new InvalidOperationException("Phiên đăng nhập đã hết hạn.");
					}
					DesktopAccountResponse desktopAccountResponse = await storefrontApiClient.GetAccountAsync(accessToken, disposalCancellation.Token);
					if (amount < desktopAccountResponse.MinimumTopUpAmount)
					{
						await SetWalletTopUpBusyAsync(isBusy: false, "Số tiền nạp tối thiểu là " + FormatAmount(desktopAccountResponse.MinimumTopUpAmount) + ".", isError: true);
						return;
					}
					if (amount > desktopAccountResponse.MaximumTopUpAmount)
					{
						await SetWalletTopUpBusyAsync(isBusy: false, "Mỗi lần nạp tối đa " + FormatAmount(desktopAccountResponse.MaximumTopUpAmount) + ".", isError: true);
						return;
					}
					DesktopCheckoutPaymentResponse checkout = await storefrontApiClient.CreateWalletTopUpAsync(amount, accessToken, disposalCancellation.Token);
					activeWalletTopUpOrderCode = checkout.OrderCode;
					completedWalletTopUpOrderCode = null;
					await SetWalletTopUpBusyAsync(isBusy: false);
					await ShowWalletTopUpCheckoutPopupAsync(checkout);
					if (checkout.IsPaid)
					{
						await HandleWalletTopUpPaidAsync(checkout.OrderCode);
					}
				}
				catch (OperationCanceledException) when (disposalCancellation.IsCancellationRequested)
				{
				}
				catch (Exception exception)
				{
					await SetWalletTopUpBusyAsync(isBusy: false, UserError(exception, "Không thể tạo phiếu nạp tiền. Vui lòng thử lại."), isError: true);
				}
				return;
			}
			finally
			{
				isCreatingWalletTopUp = false;
			}
		}
		await SetWalletTopUpBusyAsync(isBusy: false, "Vui lòng nhập số tiền cần nạp.", isError: true);
	}

	private async Task RefreshWalletTopUpStatusAsync(string? orderCode)
	{
		if (string.IsNullOrWhiteSpace(orderCode))
		{
			await ShowToastAsync("Không tìm thấy mã phiếu nạp để kiểm tra.");
			return;
		}
		try
		{
			string text = await DesktopAppContext.AuthSessionManager.GetAccessTokenAsync(disposalCancellation.Token);
			if (string.IsNullOrWhiteSpace(text))
			{
				throw new InvalidOperationException("Phiên đăng nhập đã hết hạn.");
			}
			DesktopCheckoutStatusResponse status = await storefrontApiClient.GetCheckoutStatusAsync(orderCode.Trim(), text, disposalCancellation.Token);
			await UpdateWalletTopUpStatusPopupAsync(status);
			if (status.IsPaid)
			{
				await HandleWalletTopUpPaidAsync(status.OrderCode ?? orderCode.Trim());
			}
		}
		catch (OperationCanceledException) when (disposalCancellation.IsCancellationRequested)
		{
		}
		catch (Exception exception)
		{
			await ShowToastAsync(UserError(exception, "Không thể kiểm tra trạng thái. Vui lòng thử lại."));
			await UpdateWalletTopUpStatusPopupAsync(new DesktopCheckoutStatusResponse
			{
				StatusTitle = "Chưa kiểm tra được",
				StatusDescription = UserError(exception, "Chưa thể kiểm tra trạng thái."),
				IsPaid = false
			});
		}
	}

	private void HandlePaymentStatusUpdated(object? sender, DesktopCheckoutStatusResponse status)
	{
		if (status.IsPaid && !string.IsNullOrWhiteSpace(activeOrderCheckoutOrderCode) && string.Equals(status.OrderCode, activeOrderCheckoutOrderCode, StringComparison.OrdinalIgnoreCase) && !base.IsDisposed)
		{
			if (base.InvokeRequired)
			{
				BeginInvoke(delegate
				{
					HandlePaymentStatusUpdated(sender, status);
				});
			}
			else
			{
				UpdateOrderCheckoutStatusPopupAsync(status);
				HandleOrderCheckoutPaidAsync(status);
			}
		}
		else
		{
			if (!status.IsPaid || string.IsNullOrWhiteSpace(activeWalletTopUpOrderCode) || !string.Equals(status.OrderCode, activeWalletTopUpOrderCode, StringComparison.OrdinalIgnoreCase) || base.IsDisposed)
			{
				return;
			}
			if (base.InvokeRequired)
			{
				BeginInvoke(delegate
				{
					HandlePaymentStatusUpdated(sender, status);
				});
			}
			else
			{
				HandleWalletTopUpPaidAsync(status.OrderCode);
			}
		}
	}

	private async Task HandleOrderCheckoutPaidAsync(DesktopCheckoutStatusResponse status)
	{
		if (!status.IsPaid || string.IsNullOrWhiteSpace(status.OrderCode) || !string.Equals(status.OrderCode, activeOrderCheckoutOrderCode, StringComparison.OrdinalIgnoreCase) || string.Equals(status.OrderCode, completedOrderCheckoutOrderCode, StringComparison.OrdinalIgnoreCase) || isHandlingOrderCheckoutPaid || base.IsDisposed)
		{
			return;
		}
		isHandlingOrderCheckoutPaid = true;
		try
		{
			completedOrderCheckoutOrderCode = status.OrderCode;
			await UpdateOrderCheckoutStatusPopupAsync(status);
		}
		catch (OperationCanceledException) when (disposalCancellation.IsCancellationRequested)
		{
		}
		catch (Exception exception)
		{
			await ShowToastAsync(UserError(exception, "Thanh toán đã được xác nhận nhưng chưa thể mở cửa sổ xác thực."));
		}
		finally
		{
			isHandlingOrderCheckoutPaid = false;
		}
	}

	private async Task HandleWalletTopUpPaidAsync(string? orderCode)
	{
		if (string.IsNullOrWhiteSpace(orderCode) || !string.Equals(orderCode, activeWalletTopUpOrderCode, StringComparison.OrdinalIgnoreCase) || string.Equals(orderCode, completedWalletTopUpOrderCode, StringComparison.OrdinalIgnoreCase) || isHandlingWalletTopUpPaid)
		{
			return;
		}
		isHandlingWalletTopUpPaid = true;
		try
		{
			string text = await DesktopAppContext.AuthSessionManager.GetAccessTokenAsync(disposalCancellation.Token);
			if (string.IsNullOrWhiteSpace(text))
			{
				return;
			}
			DesktopAccountResponse desktopAccountResponse = (currentAccountData = await storefrontApiClient.GetAccountAsync(text, disposalCancellation.Token));
			UpdateAccountContext(desktopAccountResponse);
			completedWalletTopUpOrderCode = orderCode;
			await ShowWalletTopUpSuccessPopupAsync(desktopAccountResponse.WalletBalanceText);
		}
		catch (OperationCanceledException) when (disposalCancellation.IsCancellationRequested)
		{
		}
		catch (Exception exception)
		{
			await ShowToastAsync(UserError(exception, "Thanh toán đã được xác nhận nhưng chưa thể tải số dư mới."));
		}
		finally
		{
			isHandlingWalletTopUpPaid = false;
		}
	}

	private void UpdateAccountContext(DesktopAccountResponse account)
	{
		currentContext = currentContext with
		{
			UserName = (string.IsNullOrWhiteSpace(account.DisplayName) ? currentContext.UserName : account.DisplayName.Trim()),
			WalletBalanceText = (string.IsNullOrWhiteSpace(account.WalletBalanceText) ? currentContext.WalletBalanceText : account.WalletBalanceText.Trim()),
			IsAuthenticated = true,
			IsWalletBalanceAvailable = !string.IsNullOrWhiteSpace(account.WalletBalanceText)
		};
		ExecuteScriptAsync("window.__nestgHome?.setWalletBalance(" + JsonSerializer.Serialize(currentContext.WalletBalanceText, JsonOptions) + ");");
	}

	private void HandleDetailCartItemCountChanged(int cartItemCount)
	{
		UpdateCartCountAsync(cartItemCount);
	}

	private void HandleDetailCartPageRequested()
	{
		ShowToastAsync("Giỏ hàng WebView sẽ được nối sau. Số lượng giỏ hàng đã được cập nhật.");
	}

	private async Task ShowGalleryLightboxAsync(int startIndex)
	{
		if (currentDetailData == null)
		{
			return;
		}
		IReadOnlyList<StoreProductGalleryItem> readOnlyList2;
		if (currentDetailData.Gallery.Count <= 0)
		{
			IReadOnlyList<StoreProductGalleryItem> readOnlyList = new _003C_003Ez__ReadOnlySingleElementList<StoreProductGalleryItem>(new StoreProductGalleryItem
			{
				ThumbnailUrl = currentDetailData.CoverImageUrl,
				FullUrl = currentDetailData.CoverImageUrl,
				Alt = currentDetailData.Name,
				MainPosition = "center center"
			});
			readOnlyList2 = readOnlyList;
		}
		else
		{
			readOnlyList2 = currentDetailData.Gallery;
		}
		IReadOnlyList<StoreProductGalleryItem> gallery = readOnlyList2;
		StoreGalleryLightboxForm dialog = new StoreGalleryLightboxForm(gallery, startIndex, currentDetailData.Name);
		try
		{
			await DeferredModalDialog.ShowAsync(this, () => dialog.ShowDialog(this));
		}
		finally
		{
			if (dialog != null)
			{
				((IDisposable)dialog).Dispose();
			}
		}
	}

	private static int GetActiveEditionIndex(StoreGameDetailData data)
	{
		StoreProductEdition activeEdition = data.GetActiveEdition();
		for (int i = 0; i < data.Editions.Count; i++)
		{
			if (data.Editions[i] == activeEdition)
			{
				return i;
			}
		}
		return 0;
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
			ShowUserMessageAsync("Không thể mở liên kết này từ launcher.", MessageBoxIcon.Asterisk);
		}
	}

	private Task ActivateCurrentLibraryGameAsync()
	{
		if (currentLibraryDetail != null)
		{
			return ActivateLibraryGameAsync(currentLibraryDetail);
		}
		return Task.CompletedTask;
	}

	private Task BackupCurrentLibraryGameAsync()
	{
		if (currentLibraryDetail != null)
		{
			return BackupLibraryGameAsync(currentLibraryDetail);
		}
		return Task.CompletedTask;
	}

	private Task OpenCurrentLibraryRestoreVersionsAsync()
	{
		if (currentLibraryDetail != null)
		{
			return OpenLibraryRestoreVersionsAsync(currentLibraryDetail);
		}
		return Task.CompletedTask;
	}

	private async Task OpenLibraryRestoreVersionsAsync(DesktopLibraryDetailResponse detail)
	{
		if (isCloudSavingLibraryGame)
		{
			return;
		}
		isCloudSavingLibraryGame = true;
		IReadOnlyList<CloudSaveRestoreVersion> versions = null;
		try
		{
			await SetLibraryCloudSaveStateAsync(isLoading: true);
			CloudSaveBackupRequest request = CreateCloudSaveRequest(detail);
			await ShowCloudSaveProgressPopupAsync(detail, new CloudSaveProgress(CloudSaveProgressStage.Connecting, "Đang kết nối Google Drive và tìm các phiên bản đã hoàn tất...", 8), isRestore: true);
			versions = await cloudSaveService.ListRestoreVersionsAsync(request, disposalCancellation.Token);
			if (currentLibraryDetail?.LibraryId == detail.LibraryId)
			{
				currentCloudSaveRestoreVersions = versions;
			}
		}
		catch (OperationCanceledException) when (disposalCancellation.IsCancellationRequested)
		{
		}
		catch (Exception exception)
		{
			CloudSaveErrorPresentation cloudSaveErrorPresentation = CloudSaveErrorPresentation.FromException(exception);
			DesktopDiagnosticLog.Write("CloudSave", "RestoreListFailed", $"SteamAppId={detail.SteamAppId}; {string.Join("; ", cloudSaveErrorPresentation.TechnicalDetails)}");
			if (currentLibraryDetail?.LibraryId == detail.LibraryId)
			{
				await ShowCloudSaveErrorPopupAsync(detail, cloudSaveErrorPresentation);
			}
		}
		finally
		{
			isCloudSavingLibraryGame = false;
			if (currentLibraryDetail?.LibraryId == detail.LibraryId)
			{
				await SetLibraryCloudSaveStateAsync(isLoading: false);
			}
		}
		if (versions != null && currentLibraryDetail?.LibraryId == detail.LibraryId)
		{
			await CloseActivationPopupAsync();
			var value = versions.Select((CloudSaveRestoreVersion item) => new
			{
				snapshotId = item.SnapshotId,
				date = item.CompletedAtUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.GetCultureInfo("vi-VN")),
				steamAccountId = item.SteamAccountId,
				fileCount = item.FileCount,
				size = FormatFileSize(item.ArchiveSize)
			}).ToArray();
			await ExecuteScriptAsync("window.__nestgCloudSave?.showVersions(" + JsonSerializer.Serialize(value, JsonOptions) + ");");
		}
	}

	private Task RestoreCurrentLibraryGameAsync(string snapshotId)
	{
		if (currentLibraryDetail != null)
		{
			return RestoreLibraryGameAsync(currentLibraryDetail, snapshotId);
		}
		return Task.CompletedTask;
	}

	private async Task RestoreLibraryGameAsync(DesktopLibraryDetailResponse detail, string snapshotId)
	{
		if (isCloudSavingLibraryGame)
		{
			return;
		}
		isCloudSavingLibraryGame = true;
		bool attemptInProgress = true;
		try
		{
			await SetLibraryCloudSaveStateAsync(isLoading: true);
			CloudSaveBackupRequest request = CreateCloudSaveRequest(detail);
			CloudSaveRestoreVersion selected = currentCloudSaveRestoreVersions.SingleOrDefault((CloudSaveRestoreVersion item) => item.SnapshotId.Equals(snapshotId, StringComparison.Ordinal));
			if ((object)selected == null)
			{
				throw new InvalidOperationException("Phiên bản Cloud Save chưa được chọn hoặc danh sách đã thay đổi.");
			}
			await ShowCloudSaveProgressPopupAsync(detail, new CloudSaveProgress(CloudSaveProgressStage.Connecting, "Đang xác minh bản lưu trên Google Drive...", 5), isRestore: true);
			Progress<CloudSaveProgress> progress = new Progress<CloudSaveProgress>(delegate(CloudSaveProgress update)
			{
				if (attemptInProgress && currentLibraryDetail?.LibraryId == detail.LibraryId)
				{
					ShowCloudSaveProgressPopupAsync(detail, update, isRestore: true);
				}
			});
			CloudSaveRestoreResult cloudSaveRestoreResult = await cloudSaveService.RestoreAsync(request, selected, progress, disposalCancellation.Token);
			attemptInProgress = false;
			DesktopDiagnosticLog.Write("CloudSave", "RestoreApplied", $"LibraryId={detail.LibraryId}; SteamAppId={detail.SteamAppId}; SnapshotId={cloudSaveRestoreResult.SnapshotId}");
			if (currentLibraryDetail?.LibraryId == detail.LibraryId)
			{
				await ShowCloudSaveRestoreAppliedPopupAsync(detail, cloudSaveRestoreResult);
			}
		}
		catch (OperationCanceledException) when (disposalCancellation.IsCancellationRequested)
		{
		}
		catch (Exception exception)
		{
			attemptInProgress = false;
			CloudSaveErrorPresentation cloudSaveErrorPresentation = CloudSaveErrorPresentation.FromException(exception);
			DesktopDiagnosticLog.Write("CloudSave", "RestoreFailed", $"SteamAppId={detail.SteamAppId}; {string.Join("; ", cloudSaveErrorPresentation.TechnicalDetails)}");
			if (currentLibraryDetail?.LibraryId == detail.LibraryId)
			{
				await ShowCloudSaveErrorPopupAsync(detail, cloudSaveErrorPresentation);
			}
		}
		finally
		{
			attemptInProgress = false;
			currentCloudSaveRestoreVersions = Array.Empty<CloudSaveRestoreVersion>();
			isCloudSavingLibraryGame = false;
			if (currentLibraryDetail?.LibraryId == detail.LibraryId)
			{
				await SetLibraryCloudSaveStateAsync(isLoading: false);
			}
		}
	}

	private static CloudSaveBackupRequest CreateCloudSaveRequest(DesktopLibraryDetailResponse detail)
	{
		AuthSession currentSession = DesktopAppContext.AuthSessionManager.CurrentSession;
		if (currentSession == null || currentSession.UserId == Guid.Empty)
		{
			throw new InvalidOperationException("Phiên NestG đã hết hạn. Hãy đăng nhập lại.");
		}
		int? steamAppId = detail.SteamAppId;
		if (!steamAppId.HasValue || steamAppId.GetValueOrDefault() <= 0)
		{
			throw new InvalidOperationException("Game chưa có Steam App ID hợp lệ để dùng Cloud Save.");
		}
		return new CloudSaveBackupRequest(currentSession.UserId, detail.LibraryId, detail.SteamAppId.Value, detail.GameName);
	}

	private async Task BackupLibraryGameAsync(DesktopLibraryDetailResponse detail)
	{
		if (isCloudSavingLibraryGame)
		{
			DesktopDiagnosticLog.Write("CloudSave", "DuplicateRequestIgnored", $"LibraryId={detail.LibraryId}");
			return;
		}
		isCloudSavingLibraryGame = true;
		bool attemptInProgress = true;
		try
		{
			await SetLibraryCloudSaveStateAsync(isLoading: true);
			AuthSession session = DesktopAppContext.AuthSessionManager.CurrentSession;
			if (session == null || session.UserId == Guid.Empty)
			{
				throw new InvalidOperationException("Phiên NestG đã hết hạn. Hãy đăng nhập lại trước khi dùng CLOUD SAVE.");
			}
			int? steamAppId = detail.SteamAppId;
			if (!steamAppId.HasValue || steamAppId.GetValueOrDefault() <= 0)
			{
				throw new InvalidOperationException("Game chưa có Steam App ID hợp lệ để lưu save lên Google Drive.");
			}
			DesktopDiagnosticLog.Write("CloudSave", "Requested", $"LibraryId={detail.LibraryId}; SteamAppId={detail.SteamAppId}; Configured={cloudSaveService.IsConfigured}");
			CloudSaveProgress progress = new CloudSaveProgress(CloudSaveProgressStage.Connecting, cloudSaveService.HasStoredCredential(session.UserId) ? "Đang xác minh kết nối Google Drive..." : "Trình duyệt sẽ mở để bạn kết nối Google Drive...", 8);
			await ShowCloudSaveProgressPopupAsync(detail, progress);
			Progress<CloudSaveProgress> progress2 = new Progress<CloudSaveProgress>(delegate(CloudSaveProgress update)
			{
				if (attemptInProgress && currentLibraryDetail?.LibraryId == detail.LibraryId)
				{
					ShowCloudSaveProgressPopupAsync(detail, update);
				}
			});
			CloudSaveBackupResult cloudSaveBackupResult = await cloudSaveService.BackupAsync(new CloudSaveBackupRequest(session.UserId, detail.LibraryId, detail.SteamAppId.Value, detail.GameName), progress2, disposalCancellation.Token);
			attemptInProgress = false;
			DesktopDiagnosticLog.Write("CloudSave", "Completed", $"LibraryId={detail.LibraryId}; SteamAppId={detail.SteamAppId}; SnapshotId={cloudSaveBackupResult.SnapshotId}; SteamAccountId={cloudSaveBackupResult.SteamAccountId}; Files={cloudSaveBackupResult.FileCount}; Bytes={cloudSaveBackupResult.ArchiveSize}");
			if (currentLibraryDetail?.LibraryId == detail.LibraryId)
			{
				await ShowCloudSaveSuccessPopupAsync(detail, cloudSaveBackupResult);
			}
		}
		catch (OperationCanceledException) when (disposalCancellation.IsCancellationRequested)
		{
			DesktopDiagnosticLog.Write("CloudSave", "CancelledDuringShutdown", $"LibraryId={detail.LibraryId}");
		}
		catch (Exception exception)
		{
			attemptInProgress = false;
			CloudSaveErrorPresentation cloudSaveErrorPresentation = CloudSaveErrorPresentation.FromException(exception);
			DesktopDiagnosticLog.Write("CloudSave", "Failed", $"SteamAppId={detail.SteamAppId}; {string.Join("; ", cloudSaveErrorPresentation.TechnicalDetails)}");
			if (!(currentLibraryDetail?.LibraryId == detail.LibraryId))
			{
				await ShowUserMessageAsync(cloudSaveErrorPresentation.Summary, MessageBoxIcon.Exclamation);
			}
			else
			{
				await ShowCloudSaveErrorPopupAsync(detail, cloudSaveErrorPresentation);
			}
		}
		finally
		{
			attemptInProgress = false;
			isCloudSavingLibraryGame = false;
			if (currentLibraryDetail?.LibraryId == detail.LibraryId)
			{
				await SetLibraryCloudSaveStateAsync(isLoading: false);
			}
		}
	}

	private Task ApplyCurrentLibraryDenuvoFixAsync()
	{
		if (currentLibraryDetail != null)
		{
			return ApplyLibraryDenuvoFixAsync(currentLibraryDetail);
		}
		return Task.CompletedTask;
	}

	private Task ApplyCurrentLibraryPypassFixAsync()
	{
		if (currentLibraryDetail != null)
		{
			return ApplyLibraryPypassFixAsync(currentLibraryDetail);
		}
		return Task.CompletedTask;
	}

	private Task ApplyCurrentLibraryOnlineFixAsync()
	{
		if (currentLibraryDetail != null)
		{
			return ApplyLibraryOnlineFixAsync(currentLibraryDetail);
		}
		return Task.CompletedTask;
	}

	private async Task ActivateLibraryGameAsync(DesktopLibraryDetailResponse detail)
	{
		if (isActivatingLibraryGame)
		{
			DesktopDiagnosticLog.Write("CloudRedirectActivation", "DuplicateRequestIgnored", $"LibraryId={detail.LibraryId}");
			return;
		}
		isActivatingLibraryGame = true;
		bool activationUiEntered = false;
		GameActivationProgressQueue progress = null;
		GameActivationLog log = new GameActivationLog(detail, DesktopAppContext.Settings.CurrentVersion);
		activationLogDownload.SetCurrent(log);
		try
		{
			DesktopDiagnosticLog.Write("CloudRedirectActivation", "Requested", $"LibraryId={detail.LibraryId}; SteamAppId={detail.SteamAppId}");
			base.UseWaitCursor = true;
			activationUiEntered = true;
			await SetLibraryActivationStateAsync(isLoading: true);
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
			DesktopDiagnosticLog.Write("CloudRedirectActivation", "Completed", $"LibraryId={detail.LibraryId}; HealthReady={result.SteamInstall.CloudRedirectHealth?.IsReady}; SteamRestarted={result.SteamInstall.IsSteamRestarted}; RolledBack={result.SteamInstall.WasRolledBack}");
			await ShowActivationResultPopupAsync(detail, result);
		}
		catch (OperationCanceledException) when (disposalCancellation.IsCancellationRequested)
		{
			log.Finish("CANCELLED");
			DesktopDiagnosticLog.Write("CloudRedirectActivation", "CancelledDuringShutdown", $"LibraryId={detail.LibraryId}");
		}
		catch (Exception ex2)
		{
			log.Fail(ex2);
			if (progress != null)
			{
				await progress.FlushAsync();
			}
			DesktopDiagnosticLog.Write("CloudRedirectActivation", "Failed", $"LibraryId={detail.LibraryId}; ErrorType={ex2.GetType().Name}");
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
					await SetLibraryActivationStateAsync(isLoading: false);
				}
				catch (Exception exception)
				{
					DesktopDiagnosticLog.Write("CloudRedirectActivation", "ResetUiFailed", $"LibraryId={detail.LibraryId}", exception);
				}
			}
			base.UseWaitCursor = false;
			isActivatingLibraryGame = false;
		}
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
			}, accessToken, disposalCancellation.Token);
			await DesktopAppContext.DenuvoActiveRealtimeClient.ProcessPendingAsync(disposalCancellation.Token);
		}
		catch (OperationCanceledException) when (disposalCancellation.IsCancellationRequested)
		{
		}
		catch
		{
		}
	}

	private async Task ApplyLibraryDenuvoFixAsync(DesktopLibraryDetailResponse detail)
	{
		if (isApplyingLibraryDenuvoFix)
		{
			return;
		}
		isApplyingLibraryDenuvoFix = true;
		long uiGeneration = denuvoActivationUiState.Begin(detail.LibraryId);
		deferredDenuvoUiResults.Clear(detail.LibraryId);
		base.UseWaitCursor = true;
		await SetLibraryDenuvoStateAsync(isLoading: true);
		DenuvoProgressQueue progress = new DenuvoProgressQueue((DenuvoInstallProgress update) => (!denuvoActivationUiState.CanShowLoading(detail.LibraryId, uiGeneration) || currentLibraryDetail != detail) ? Task.CompletedTask : ShowDenuvoFixInstallProgressPopupAsync(detail, update), disposalCancellation.Token);
		try
		{
			string accessToken = await DesktopAppContext.AuthSessionManager.GetAccessTokenAsync(disposalCancellation.Token);
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
			await ShowDenuvoFixInstallProgressPopupAsync(detail);
			DenuvoFixInstallResult result = await denuvoFixInstallService.InstallAsync(detail, accessToken, this, disposalCancellation.Token, progress);
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
			if (currentLibraryDetail == detail && denuvoActivationUiState.CanShowLoading(detail.LibraryId, uiGeneration))
			{
				await SetLibraryDenuvoAppliedStateAsync(null);
			}
			if (currentLibraryDetail == detail)
			{
				await SetLibraryDenuvoStateAsync(isLoading: false);
			}
			base.UseWaitCursor = false;
			activeRequestId = result.ActiveRequestId;
			if (activeRequestId.HasValue)
			{
				Guid valueOrDefault3 = activeRequestId.GetValueOrDefault();
				await ReplayDeferredDenuvoResultAsync(detail, valueOrDefault3);
			}
			if (denuvoActivationUiState.CanShowLoading(detail.LibraryId, uiGeneration))
			{
				await ShowDenuvoFixWaitingAdminPopupAsync(detail, result);
			}
			await DesktopAppContext.DenuvoActiveRealtimeClient.ProcessPendingAsync(disposalCancellation.Token);
		}
		catch (OperationCanceledException) when (disposalCancellation.IsCancellationRequested)
		{
		}
		catch (Exception ex2)
		{
			await progress.FlushAsync();
			deferredDenuvoUiResults.Clear(detail.LibraryId);
			if (currentLibraryDetail == detail && denuvoActivationUiState.TryFail(detail.LibraryId, uiGeneration))
			{
				await ShowDenuvoFixErrorPopupAsync(detail, (ex2 is DenuvoRollbackException) ? "Chưa khôi phục xong cài đặt Denuvo. Giữ nguyên backup để kiểm tra." : UserError(ex2, "Không thể nạp game Denuvo lúc này."), (ex2 as DenuvoRollbackException)?.BackupDirectory);
			}
		}
		finally
		{
			if (currentLibraryDetail == detail)
			{
				await SetLibraryDenuvoStateAsync(isLoading: false);
			}
			base.UseWaitCursor = false;
			isApplyingLibraryDenuvoFix = false;
		}
	}

	private async Task ApplyLibraryPypassFixAsync(DesktopLibraryDetailResponse detail)
	{
		if (isApplyingLibraryPypassFix)
		{
			return;
		}
		isApplyingLibraryPypassFix = true;
		base.UseWaitCursor = true;
		await SetLibraryPypassStateAsync(isLoading: true);
		CancellationTokenSource operationCancellation = (pypassInstallCancellation = CancellationTokenSource.CreateLinkedTokenSource(disposalCancellation.Token));
		try
		{
			string accessToken = await DesktopAppContext.AuthSessionManager.GetAccessTokenAsync(operationCancellation.Token);
			if (string.IsNullOrWhiteSpace(accessToken))
			{
				throw new InvalidOperationException("Phiên đăng nhập đã hết hạn. Hãy đăng nhập lại để chạy Active-2.");
			}
			await ShowPypassFixInstallProgressPopupAsync(detail);
			Progress<PypassFixInstallProgress> progress = new Progress<PypassFixInstallProgress>(delegate(PypassFixInstallProgress item)
			{
				ShowPypassFixInstallProgressPopupAsync(detail, item);
			});
			PypassFixInstallResult result = await pypassFixInstallService.InstallAsync(detail, accessToken, this, operationCancellation.Token, progress);
			await SetLibraryPypassAppliedStateAsync();
			await SetLibraryPypassStateAsync(isLoading: false);
			base.UseWaitCursor = false;
			isApplyingLibraryPypassFix = false;
			await ShowPypassFixSuccessPopupAsync(detail, result);
		}
		catch (OperationCanceledException) when (disposalCancellation.IsCancellationRequested)
		{
		}
		catch (OperationCanceledException)
		{
			await ShowPypassFixErrorPopupAsync(detail, "Đã hủy tải package Pypass.");
		}
		catch (Exception exception)
		{
			await ShowPypassFixErrorPopupAsync(detail, UserError(exception, "Không thể chạy Active-2 lúc này."));
		}
		finally
		{
			await SetLibraryPypassStateAsync(isLoading: false);
			base.UseWaitCursor = false;
			isApplyingLibraryPypassFix = false;
			if (pypassInstallCancellation == operationCancellation)
			{
				pypassInstallCancellation = null;
			}
			operationCancellation.Dispose();
		}
	}

	private async Task ApplyLibraryOnlineFixAsync(DesktopLibraryDetailResponse detail)
	{
		if (isApplyingLibraryOnlineFix)
		{
			return;
		}
		isApplyingLibraryOnlineFix = true;
		base.UseWaitCursor = true;
		await SetLibraryOnlineStateAsync(isLoading: true);
		try
		{
			string accessToken = await DesktopAppContext.AuthSessionManager.GetAccessTokenAsync(disposalCancellation.Token);
			if (string.IsNullOrWhiteSpace(accessToken))
			{
				throw new InvalidOperationException("Phiên đăng nhập đã hết hạn. Hãy đăng nhập lại để nạp game.");
			}
			await ShowOnlineFixInstallProgressPopupAsync(detail);
			OnlineFixInstallResult result = await onlineFixInstallService.InstallAsync(detail, accessToken, this, disposalCancellation.Token);
			await SetLibraryOnlineAppliedStateAsync();
			await SetLibraryOnlineStateAsync(isLoading: false);
			base.UseWaitCursor = false;
			isApplyingLibraryOnlineFix = false;
			await ShowOnlineFixSuccessPopupAsync(detail, result);
		}
		catch (OperationCanceledException) when (disposalCancellation.IsCancellationRequested)
		{
		}
		catch (Exception exception)
		{
			await ShowOnlineFixErrorPopupAsync(detail, UserError(exception, "Không thể nạp game lúc này."));
		}
		finally
		{
			await SetLibraryOnlineStateAsync(isLoading: false);
			base.UseWaitCursor = false;
			isApplyingLibraryOnlineFix = false;
		}
	}

	private Task ShowActivationResultPopupAsync(DesktopLibraryDetailResponse detail, GameActivationResult result)
	{
		SteamInstallResult steamInstall = result.SteamInstall;
		CloudRedirectActivationUiState cloudRedirectActivationUiState = CloudRedirectActivationUiPolicy.Resolve(steamInstall);
		GameActivationLog log = ((activationLogDownload.CurrentLog?.Id == result.ActivationLogId) ? activationLogDownload.CurrentLog : null);
		object obj;
		switch (cloudRedirectActivationUiState)
		{
		case CloudRedirectActivationUiState.AttemptFailed:
			return ShowActivationErrorPopupAsync(detail, steamInstall.FailureMessage, null, log);
		default:
			obj = "warning";
			break;
		case CloudRedirectActivationUiState.Success:
			obj = "success";
			break;
		}
		string text = (string)obj;
		IReadOnlyList<string> technicalDetails;
		if (cloudRedirectActivationUiState == CloudRedirectActivationUiState.VerificationPending)
		{
			string gameName = detail.GameName;
			string metaText = BuildActivationMeta(detail);
			string recoveryMessage = steamInstall.RecoveryMessage;
			ActivationMetricPayload[] metrics = Array.Empty<ActivationMetricPayload>();
			ActivationPathPayload[] paths = Array.Empty<ActivationPathPayload>();
			_003C_003Ez__ReadOnlySingleElementList<string> messages = new _003C_003Ez__ReadOnlySingleElementList<string>("Hãy kiểm tra Steam rồi bấm ACTIVE để tiếp tục lần đang chờ.");
			technicalDetails = BuildActivationTechnicalDetails(result);
			return ShowActivationPopupAsync(new ActivationPopupPayload("warning", gameName, "Chưa hoàn tất Active", "Chờ kiểm tra", metaText, recoveryMessage, metrics, paths, messages, "Đã hiểu", null, null, MinimalLoading: false, CompactSuccess: false, null, LockClose: false, ZaloQr: false, MinimizeToGuide: false, null, null, ProgressIndeterminate: false, technicalDetails));
		}
		if (steamInstall.AutoRecoveryAttempted && !steamInstall.RollbackFailed)
		{
			bool steamRecovered = steamInstall.SteamRecovered;
			string state = (steamRecovered ? "warning" : "error");
			string gameName2 = detail.GameName;
			string title = (steamRecovered ? "Steam đã chạy lại, ACTIVE chưa hoàn tất" : "Steam chưa khởi động được");
			string badgeText = (steamRecovered ? "Đã tự phục hồi" : "Phục hồi chưa hoàn tất");
			string metaText2 = BuildActivationMeta(detail);
			string recoveryMessage2 = steamInstall.RecoveryMessage;
			_003C_003Ez__ReadOnlySingleElementList<ActivationMetricPayload> metrics2 = new _003C_003Ez__ReadOnlySingleElementList<ActivationMetricPayload>(new ActivationMetricPayload("Trạng thái", steamRecovered ? "Steam đang chạy" : "Cần kiểm tra lỗi", steamRecovered ? "warning" : "error"));
			ActivationPathPayload[] paths2 = Array.Empty<ActivationPathPayload>();
			_003C_003Ez__ReadOnlySingleElementList<string> messages2 = new _003C_003Ez__ReadOnlySingleElementList<string>("ACTIVE chưa thành công. Cần kiểm tra nguyên nhân lỗi trước lần ACTIVE tiếp theo.");
			technicalDetails = BuildActivationTechnicalDetails(result);
			return ShowActivationPopupAsync(new ActivationPopupPayload(state, gameName2, title, badgeText, metaText2, recoveryMessage2, metrics2, paths2, messages2, "Đã hiểu", null, null, MinimalLoading: false, CompactSuccess: false, null, LockClose: false, ZaloQr: false, MinimizeToGuide: false, null, null, ProgressIndeterminate: false, technicalDetails));
		}
		object obj2;
		switch (cloudRedirectActivationUiState)
		{
		case CloudRedirectActivationUiState.RollbackFailed:
		{
			string gameName6 = detail.GameName;
			string title2 = (steamInstall.SteamRecovered ? "Steam đã chạy lại, rollback chưa hoàn tất" : "Chưa khôi phục xong cài đặt");
			string metaText6 = BuildActivationMeta(detail);
			_003C_003Ez__ReadOnlyArray<ActivationMetricPayload> metrics6 = new _003C_003Ez__ReadOnlyArray<ActivationMetricPayload>(new ActivationMetricPayload[2]
			{
				new ActivationMetricPayload("Trạng thái", "Cần khôi phục", "error"),
				new ActivationMetricPayload("Phiên bản", NormalizeActivationValue(detail.VersionName, "Mặc định"), "default")
			});
			ActivationPathPayload[] paths6 = Array.Empty<ActivationPathPayload>();
			_003C_003Ez__ReadOnlySingleElementList<string> messages6 = new _003C_003Ez__ReadOnlySingleElementList<string>("Mở chẩn đoán để kiểm tra backup và các file chưa được khôi phục.");
			technicalDetails = BuildActivationTechnicalDetails(result);
			return ShowActivationPopupAsync(new ActivationPopupPayload("error", gameName6, title2, "Rollback chưa hoàn tất", metaText6, "Launcher chưa khôi phục đầy đủ các file của lần ACTIVE lỗi. Giữ nguyên backup và kiểm tra chẩn đoán trước khi ACTIVE lại.", metrics6, paths6, messages6, "Đã hiểu", null, null, MinimalLoading: false, CompactSuccess: false, null, LockClose: false, ZaloQr: false, MinimizeToGuide: false, null, null, ProgressIndeterminate: false, technicalDetails));
		}
		case CloudRedirectActivationUiState.RolledBack:
		{
			string gameName5 = detail.GameName;
			string metaText5 = BuildActivationMeta(detail);
			_003C_003Ez__ReadOnlyArray<ActivationMetricPayload> metrics5 = new _003C_003Ez__ReadOnlyArray<ActivationMetricPayload>(new ActivationMetricPayload[2]
			{
				new ActivationMetricPayload("Trạng thái", "Đã khôi phục an toàn", "warning"),
				new ActivationMetricPayload("Phiên bản", NormalizeActivationValue(detail.VersionName, "Mặc định"), "default")
			});
			ActivationPathPayload[] paths5 = Array.Empty<ActivationPathPayload>();
			_003C_003Ez__ReadOnlySingleElementList<string> messages5 = new _003C_003Ez__ReadOnlySingleElementList<string>("Kiểm tra nguyên nhân lỗi trước khi ACTIVE lại.");
			technicalDetails = BuildActivationTechnicalDetails(result);
			return ShowActivationPopupAsync(new ActivationPopupPayload("warning", gameName5, "Không thể hoàn tất Active", "Đã khôi phục", metaText5, "Launcher không thể hoàn tất sửa lỗi và đã khôi phục trạng thái trước đó. Save game không nằm trong backup của launcher.", metrics5, paths5, messages5, "Đã hiểu", null, null, MinimalLoading: false, CompactSuccess: false, null, LockClose: false, ZaloQr: false, MinimizeToGuide: false, null, null, ProgressIndeterminate: false, technicalDetails));
		}
		case CloudRedirectActivationUiState.HealthFailed:
		{
			if (steamInstall.BetterOnly)
			{
				return ShowActivationErrorPopupAsync(detail, "Chưa xác nhận được Steam và thành phần kích hoạt. Hãy thử Active lại.", string.Empty);
			}
			string gameName4 = detail.GameName;
			string metaText4 = BuildActivationMeta(detail);
			_003C_003Ez__ReadOnlyArray<ActivationMetricPayload> metrics4 = new _003C_003Ez__ReadOnlyArray<ActivationMetricPayload>(new ActivationMetricPayload[2]
			{
				new ActivationMetricPayload("Trạng thái", "Cloud Save lỗi", "error"),
				new ActivationMetricPayload("Phiên bản", NormalizeActivationValue(detail.VersionName, "Mặc định"), "default")
			});
			ActivationPathPayload[] paths4 = Array.Empty<ActivationPathPayload>();
			_003C_003Ez__ReadOnlySingleElementList<string> messages4 = new _003C_003Ez__ReadOnlySingleElementList<string>("Hãy thử Active lại. Nếu lỗi tiếp tục, mở Chẩn đoán Cloud Save để kiểm tra.");
			technicalDetails = BuildActivationTechnicalDetails(result);
			return ShowActivationPopupAsync(new ActivationPopupPayload("error", gameName4, "Cloud Save không thể khởi tạo", "Khởi tạo thất bại", metaText4, "Launcher đã xác nhận Cloud Save gặp lỗi. Không mở game cho đến khi kiểm tra hoặc repair Cloud Save hoàn tất.", metrics4, paths4, messages4, "Đã hiểu", null, null, MinimalLoading: false, CompactSuccess: false, null, LockClose: false, ZaloQr: false, MinimizeToGuide: false, null, null, ProgressIndeterminate: false, technicalDetails));
		}
		case CloudRedirectActivationUiState.Success:
			return ShowActivationPopupAsync(new ActivationPopupPayload("success", detail.GameName, "Active thành công", "Hoàn tất", string.Empty, "Đã sửa lỗi và kích hoạt game thành công.", new _003C_003Ez__ReadOnlyArray<ActivationMetricPayload>(new ActivationMetricPayload[2]
			{
				new ActivationMetricPayload("Trạng thái", "Hoàn tất", "success"),
				new ActivationMetricPayload("Phiên bản", NormalizeActivationValue(detail.VersionName, "Chưa xác định"), "default")
			}), Array.Empty<ActivationPathPayload>(), Array.Empty<string>(), "Hoàn tất", null, null, MinimalLoading: false, CompactSuccess: true, null, LockClose: false, ZaloQr: false, MinimizeToGuide: false, null, null));
		case CloudRedirectActivationUiState.NeedsCloudCheck:
		{
			string gameName3 = detail.GameName;
			string metaText3 = BuildActivationMeta(detail);
			_003C_003Ez__ReadOnlyArray<ActivationMetricPayload> metrics3 = new _003C_003Ez__ReadOnlyArray<ActivationMetricPayload>(new ActivationMetricPayload[2]
			{
				new ActivationMetricPayload("Trạng thái", "Kiểm tra Cloud", "warning"),
				new ActivationMetricPayload("Phiên bản", NormalizeActivationValue(detail.VersionName, "Mặc định"), "default")
			});
			ActivationPathPayload[] paths3 = Array.Empty<ActivationPathPayload>();
			IReadOnlyList<string> messages3 = BuildActivationMessages("warning", result, string.Empty);
			technicalDetails = BuildActivationTechnicalDetails(result);
			return ShowActivationPopupAsync(new ActivationPopupPayload("warning", gameName3, "Cần kiểm tra Cloud Save", "Cần kiểm tra", metaText3, "Launcher chưa thể xác nhận Cloud Save hoạt động sau khi Steam khởi động.", metrics3, paths3, messages3, "Đã hiểu", null, null, MinimalLoading: false, CompactSuccess: false, null, LockClose: false, ZaloQr: false, MinimizeToGuide: false, null, null, ProgressIndeterminate: false, technicalDetails));
		}
		default:
			obj2 = "Chưa tìm thấy Steam";
			break;
		case CloudRedirectActivationUiState.NeedsSteamRestart:
			obj2 = "Đã copy file, cần mở lại Steam";
			break;
		}
		string title3 = (string)obj2;
		string summary = ((cloudRedirectActivationUiState == CloudRedirectActivationUiState.NeedsSteamRestart) ? "Launcher đã đồng bộ file vào Steam, nhưng chưa thể khởi động lại Steam tự động trên máy này." : "Launcher chưa phát hiện Steam trên máy này nên chưa thay đổi file cài đặt.");
		string value = ((cloudRedirectActivationUiState == CloudRedirectActivationUiState.NeedsSteamRestart) ? "Cần mở lại Steam" : "Cần mở Steam");
		string gameName7 = detail.GameName;
		string badgeText2 = ((text == "success") ? "Steam đã sẵn sàng" : "Cần kiểm tra Steam");
		string metaText7 = BuildActivationMeta(detail);
		_003C_003Ez__ReadOnlyArray<ActivationMetricPayload> metrics7 = new _003C_003Ez__ReadOnlyArray<ActivationMetricPayload>(new ActivationMetricPayload[2]
		{
			new ActivationMetricPayload("Trạng thái", value, text),
			new ActivationMetricPayload("Phiên bản", NormalizeActivationValue(detail.VersionName, "Mặc định"), "default")
		});
		ActivationPathPayload[] paths7 = Array.Empty<ActivationPathPayload>();
		IReadOnlyList<string> messages7 = BuildActivationMessages(text, result, string.Empty);
		string primaryActionText = ((text == "success") ? "Hoàn tất" : "Đã hiểu");
		technicalDetails = BuildActivationTechnicalDetails(result);
		return ShowActivationPopupAsync(new ActivationPopupPayload(text, gameName7, title3, badgeText2, metaText7, summary, metrics7, paths7, messages7, primaryActionText, null, null, MinimalLoading: false, CompactSuccess: false, null, LockClose: false, ZaloQr: false, MinimizeToGuide: false, null, null, ProgressIndeterminate: false, technicalDetails));
	}

	private Task ShowActivationErrorPopupAsync(DesktopLibraryDetailResponse detail, string errorMessage, string? technicalError = null, GameActivationLog? log = null)
	{
		string text = (string.IsNullOrWhiteSpace(errorMessage) ? "Không thể active game lúc này." : errorMessage.Trim());
		string gameName = detail.GameName;
		string metaText = BuildActivationMeta(detail);
		_003C_003Ez__ReadOnlyArray<ActivationMetricPayload> metrics = new _003C_003Ez__ReadOnlyArray<ActivationMetricPayload>(new ActivationMetricPayload[2]
		{
			new ActivationMetricPayload("Trạng thái", "Cần thử lại", "error"),
			new ActivationMetricPayload("Phiên bản", NormalizeActivationValue(detail.VersionName, "Mặc định"), "default")
		});
		ActivationPathPayload[] paths = Array.Empty<ActivationPathPayload>();
		IReadOnlyList<string> messages;
		if (log == null)
		{
			messages = BuildActivationMessages("error", null, text);
		}
		else
		{
			IReadOnlyList<string> readOnlyList = new _003C_003Ez__ReadOnlySingleElementList<string>("Bạn có thể tải file log để gửi cho bộ phận hỗ trợ.");
			messages = readOnlyList;
		}
		IReadOnlyList<string> readOnlyList2;
		if (log == null && !string.IsNullOrWhiteSpace(technicalError))
		{
			IReadOnlyList<string> readOnlyList = new _003C_003Ez__ReadOnlySingleElementList<string>(technicalError.Trim());
			readOnlyList2 = readOnlyList;
		}
		else
		{
			IReadOnlyList<string> readOnlyList = Array.Empty<string>();
			readOnlyList2 = readOnlyList;
		}
		IReadOnlyList<string> technicalDetails = readOnlyList2;
		string downloadLogId = ((log != null && log.IsFailed) ? log.Id : null);
		return ShowActivationPopupAsync(new ActivationPopupPayload("error", gameName, "Không thể kích hoạt game", "Thử lại sau", metaText, text, metrics, paths, messages, "Đóng", null, null, MinimalLoading: false, CompactSuccess: false, null, LockClose: false, ZaloQr: false, MinimizeToGuide: false, null, null, ProgressIndeterminate: false, technicalDetails, SingleCloseAction: true, FullSummary: true, downloadLogId));
	}

	private Task ShowDenuvoFixInstallProgressPopupAsync(DesktopLibraryDetailResponse detail, DenuvoInstallProgress? update = null)
	{
		if (currentLibraryDetail != detail || !denuvoActivationUiState.CanShowLoading(detail.LibraryId, denuvoActivationUiState.Capture(detail.LibraryId)))
		{
			return Task.CompletedTask;
		}
		return ShowActivationPopupAsync(new ActivationPopupPayload("loading", detail.GameName, "Đang yêu cầu admin nạp game", "Đang xử lý", BuildActivationMeta(detail), "Launcher đang tải lại toàn bộ file fix và cài đè vào thư mục game, kể cả khi đã có bản cũ. Vui lòng giữ launcher mở trong lúc xử lý.", new _003C_003Ez__ReadOnlyArray<ActivationMetricPayload>(new ActivationMetricPayload[2]
		{
			new ActivationMetricPayload("Trạng thái", update?.Message ?? "Đang tải file fix", "default"),
			new ActivationMetricPayload("Phiên bản", NormalizeActivationValue(detail.VersionName, "Mặc định"), "default")
		}), Array.Empty<ActivationPathPayload>(), new _003C_003Ez__ReadOnlyArray<string>(new string[3] { "Không tắt launcher trong quá trình tải và giải nén.", "Sau khi cài local xong, launcher sẽ báo admin để cấp token.", "Nếu cần hỗ trợ nhanh, gọi admin: 0947709029." }), "Để launcher chạy", null, ((object)update != null && update.TotalFiles > 0) ? $"{update.Message}: {update.CompletedFiles}/{update.TotalFiles} file" : (update?.Message ?? "Đang tải file fix, kiểm tra hash và giải nén vào thư mục game..."), MinimalLoading: false, CompactSuccess: false, null, LockClose: false, ZaloQr: false, MinimizeToGuide: false, null, null));
	}

	private Task ShowDenuvoFixWaitingAdminPopupAsync(DesktopLibraryDetailResponse detail, DenuvoFixInstallResult result)
	{
		if (currentLibraryDetail != detail || !denuvoActivationUiState.TryShowWaiting(detail.LibraryId, result.ActiveRequestId, null))
		{
			return Task.CompletedTask;
		}
		List<string> source = new List<string> { "Nếu bạn chưa thấy thành công thì hãy gọi cho admin trước 15 phút.", "Lưu ý nên gọi admin trước khi nạp game.", "NestG cảm ơn." };
		return ShowActivationPopupAsync(new ActivationPopupPayload("loading", detail.GameName, "Đang chờ admin nạp game", "Yêu cầu đã gửi", BuildActivationMeta(detail), "Game đang được nạp và xử lý. Vui lòng đợi admin cấp token, hoặc gọi admin: 0947709029. Đóng launcher hoặc mất kết nối sẽ hủy yêu cầu; bạn cần chủ động gửi lại.", new _003C_003Ez__ReadOnlyArray<ActivationMetricPayload>(new ActivationMetricPayload[6]
		{
			new ActivationMetricPayload("Trạng thái", "Chờ admin", "warning"),
			new ActivationMetricPayload("Revision", result.ConfigRevisionNumber?.ToString() ?? "-", "default"),
			new ActivationMetricPayload("Package", result.PackageVersion ?? "-", "default"),
			new ActivationMetricPayload("Nguồn", NormalizeActivationValue(detail.DenuvoFix?.PackageSourceType, "server"), "default"),
			new ActivationMetricPayload("File", FormatDenuvoFileCount(result.WrittenFiles.Count), "default"),
			new ActivationMetricPayload("Ghi đè", FormatDenuvoFileCount(result.OverwrittenFiles.Count), (result.OverwrittenFiles.Count > 0) ? "warning" : "default")
		}), new _003C_003Ez__ReadOnlyArray<ActivationPathPayload>(new ActivationPathPayload[6]
		{
			new ActivationPathPayload("Thư mục game", result.TargetDirectory),
			new ActivationPathPayload("Exe admin cấu hình", result.MainExeRelativePath ?? "-"),
			new ActivationPathPayload("Config", result.ConfigFullPath ?? "-"),
			new ActivationPathPayload("File chay game", result.GameExePath ?? "-"),
			new ActivationPathPayload("Backup", result.BackupDirectory),
			new ActivationPathPayload("Log", result.LogFilePath)
		}), source.Distinct<string>(StringComparer.OrdinalIgnoreCase).ToArray(), "Ẩn thông báo", null, "Đang giữ kết nối realtime để nhận token từ admin...", MinimalLoading: false, CompactSuccess: false, null, LockClose: true, ZaloQr: true, MinimizeToGuide: false, null, null));
	}

	private async Task ShowDenuvoFixTokenAppliedPopupAsync(DesktopLibraryDetailResponse detail, DesktopDenuvoActiveTokenAppliedEventArgs payload)
	{
		if (currentLibraryDetail == detail && denuvoActivationUiState.IsAwaitingRequest(detail.LibraryId))
		{
			deferredDenuvoUiResults.Remember(payload);
		}
		else if (currentLibraryDetail == detail && denuvoActivationUiState.TryComplete(detail.LibraryId, payload.ActiveRequestId))
		{
			long generation = denuvoActivationUiState.Capture(detail.LibraryId);
			string document = fix54DocumentId;
			await SetLibraryDenuvoAppliedStateAsync(payload.CooldownUntilUtc);
			DesktopDenuvoGuideResponse guide = await TryLoadDenuvoGuideAsync();
			if (!disposalCancellation.IsCancellationRequested && currentLibraryDetail == detail && !(document != fix54DocumentId) && denuvoActivationUiState.IsCurrentTerminal(detail.LibraryId, generation, payload.ActiveRequestId))
			{
				ActivationPopupPayload activationPopupPayload = BuildDenuvoTokenAppliedPopupPayload(detail, payload.GameName, payload.GameExePath, guide);
				denuvoGuidePayloadJsonByLibraryId[detail.LibraryId] = JsonSerializer.Serialize(activationPopupPayload, JsonOptions);
				await ShowActivationPopupAsync(activationPopupPayload);
			}
		}
	}

	private async Task ShowCloudRedirectManagementAsync(DesktopLibraryDetailResponse detail)
	{
		if (isManagingCloudRedirect || isActivatingLibraryGame || isCloudSavingLibraryGame)
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
		CloudRedirectManagementForm dialog = new CloudRedirectManagementForm(await Task.Run((Func<CloudRedirectRuntimeStatus>)activationService.GetCloudRedirectStatus, disposalCancellation.Token));
		try
		{
			if (await DeferredModalDialog.ShowAsync(this, () => dialog.ShowDialog(this)) != DialogResult.OK)
			{
				return;
			}
			if (dialog.SelectedAction == CloudRedirectManagementAction.Repair)
			{
				isManagingCloudRedirect = false;
				await ActivateLibraryGameAsync(detail);
				return;
			}
			string text = ((dialog.SelectedAction == CloudRedirectManagementAction.Disable) ? "tắt CloudRedirect" : "rollback thay đổi CloudRedirect gần nhất");
			if (await LauncherMessageBox.ShowAsync(this, "Launcher sẽ đóng/mở lại Steam để " + text + ".\n\nHãy đóng game và sao lưu save trước khi tiếp tục. Backup launcher không bao gồm save game.", "Xác nhận CloudRedirect", MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation) != DialogResult.Yes)
			{
				return;
			}
			isManagingCloudRedirect = true;
			base.UseWaitCursor = true;
			try
			{
				CloudRedirectManagementResult cloudRedirectManagementResult = ((dialog.SelectedAction != CloudRedirectManagementAction.Disable) ? (await activationService.RollbackLatestCloudRedirectAsync(disposalCancellation.Token)) : (await activationService.DisableCloudRedirectAsync(disposalCancellation.Token)));
				CloudRedirectManagementResult cloudRedirectManagementResult2 = cloudRedirectManagementResult;
				await LauncherMessageBox.ShowAsync(this, $"{cloudRedirectManagementResult2.Message}\n\nBackup: {cloudRedirectManagementResult2.BackupDirectory}\nSteam restart: {(cloudRedirectManagementResult2.IsSteamRestarted ? "đã hoàn tất" : "hãy mở lại Steam thủ công")}", "CloudRedirect", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
			}
			catch (OperationCanceledException) when (disposalCancellation.IsCancellationRequested)
			{
			}
			catch (Exception exception)
			{
				await LauncherMessageBox.ShowAsync(this, UserError(exception, "Không thể cập nhật CloudRedirect."), "Không thể cập nhật CloudRedirect", MessageBoxButtons.OK, MessageBoxIcon.Hand);
			}
			finally
			{
				base.UseWaitCursor = false;
				isManagingCloudRedirect = false;
			}
		}
		finally
		{
			if (dialog != null)
			{
				((IDisposable)dialog).Dispose();
			}
		}
	}

	private static ActivationPopupPayload BuildDenuvoTokenAppliedPopupPayload(DesktopLibraryDetailResponse detail, string? gameName, string? gameExePath, DesktopDenuvoGuideResponse? guide)
	{
		bool flag = !string.IsNullOrWhiteSpace(gameExePath);
		return new ActivationPopupPayload("success", string.IsNullOrWhiteSpace(gameName) ? detail.GameName : gameName.Trim(), "Nạp game thành công", "Hoàn tất", BuildActivationMeta(detail), flag ? "Copy đường dẫn tạo shortcut bên dưới rồi làm theo hướng dẫn." : "Nạp game thành công nhưng chưa có đường dẫn tạo shortcut.", Array.Empty<ActivationMetricPayload>(), BuildDenuvoTokenAppliedPaths(gameExePath), Array.Empty<string>(), "Hoàn tất", null, null, MinimalLoading: false, CompactSuccess: true, guide, LockClose: false, ZaloQr: false, MinimizeToGuide: true, null, null);
	}

	private static IReadOnlyList<ActivationPathPayload> BuildDenuvoTokenAppliedPaths(string? gameExePath)
	{
		if (!string.IsNullOrWhiteSpace(gameExePath))
		{
			return new _003C_003Ez__ReadOnlySingleElementList<ActivationPathPayload>(new ActivationPathPayload("Đường dẫn tạo shortcut", gameExePath, "Copy link", gameExePath));
		}
		return Array.Empty<ActivationPathPayload>();
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
			ActivationPopupPayload value = BuildDenuvoTokenAppliedPopupPayload(detail, cooldownState.GameName, cooldownState.GameExePath, guide);
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
			return await storefrontApiClient.GetDenuvoGuideAsync(disposalCancellation.Token);
		}
		catch
		{
			return null;
		}
	}

	private static IReadOnlyList<string> BuildDenuvoTokenAppliedMessages(DesktopDenuvoActiveTokenAppliedEventArgs payload)
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

	private Task ShowDenuvoFixErrorPopupAsync(DesktopLibraryDetailResponse detail, string errorMessage, string? backupDirectory = null)
	{
		string text = (string.IsNullOrWhiteSpace(errorMessage) ? "Không thể nạp game Denuvo lúc này." : errorMessage.Trim());
		string gameName = detail.GameName;
		string title = ((backupDirectory == null) ? "Không thể nạp game Denuvo" : "Chưa khôi phục xong Denuvo");
		string metaText = BuildActivationMeta(detail);
		_003C_003Ez__ReadOnlyArray<ActivationMetricPayload> metrics = new _003C_003Ez__ReadOnlyArray<ActivationMetricPayload>(new ActivationMetricPayload[2]
		{
			new ActivationMetricPayload("Trạng thái", (backupDirectory == null) ? "Cần thử lại" : "Rollback chưa hoàn tất", "error"),
			new ActivationMetricPayload("Phiên bản", NormalizeActivationValue(detail.VersionName, "Mặc định"), "default")
		});
		IReadOnlyList<ActivationPathPayload> paths;
		if (backupDirectory != null)
		{
			IReadOnlyList<ActivationPathPayload> readOnlyList = new _003C_003Ez__ReadOnlySingleElementList<ActivationPathPayload>(new ActivationPathPayload("Backup", backupDirectory));
			paths = readOnlyList;
		}
		else
		{
			IReadOnlyList<ActivationPathPayload> readOnlyList = Array.Empty<ActivationPathPayload>();
			paths = readOnlyList;
		}
		return ShowActivationPopupAsync(new ActivationPopupPayload("error", gameName, title, "Cần kiểm tra", metaText, text, metrics, paths, new _003C_003Ez__ReadOnlyArray<string>(new string[2]
		{
			text,
			(backupDirectory == null) ? "Hãy kiểm tra kết nối, quyền ghi thư mục game và thử lại sau." : "Giữ nguyên backup và kiểm tra các file chưa được khôi phục trước khi nạp lại."
		}), "Đóng", null, null, MinimalLoading: false, CompactSuccess: false, null, LockClose: false, ZaloQr: false, MinimizeToGuide: false, null, null));
	}

	private Task ShowDenuvoFixCancelledPopupAsync(DesktopLibraryDetailResponse detail, DesktopDenuvoActiveRequestCancelledEventArgs payload)
	{
		if (currentLibraryDetail == detail && denuvoActivationUiState.IsAwaitingRequest(detail.LibraryId))
		{
			deferredDenuvoUiResults.Remember(payload);
			return Task.CompletedTask;
		}
		if (currentLibraryDetail != detail || !denuvoActivationUiState.TryComplete(detail.LibraryId, payload.ActiveRequestId))
		{
			return Task.CompletedTask;
		}
		string text = (string.IsNullOrWhiteSpace(payload.Reason) ? "Yêu cầu không còn được xử lý." : payload.Reason.Trim());
		return ShowActivationPopupAsync(new ActivationPopupPayload("error", payload.GameName, "Yêu cầu đã bị hủy", "Yêu cầu đã hủy", BuildActivationMeta(detail), "Yêu cầu nạp game đã bị hủy. Lý do: " + text, new _003C_003Ez__ReadOnlyArray<ActivationMetricPayload>(new ActivationMetricPayload[2]
		{
			new ActivationMetricPayload("Trạng thái", "Đã hủy", "error"),
			new ActivationMetricPayload("Phiên bản", NormalizeActivationValue(detail.VersionName, "Mặc định"), "default")
		}), Array.Empty<ActivationPathPayload>(), new _003C_003Ez__ReadOnlySingleElementList<string>("Khi launcher đã kết nối lại, hãy chủ động gửi yêu cầu nạp game mới."), "Đóng", null, null, MinimalLoading: false, CompactSuccess: false, null, LockClose: false, ZaloQr: false, MinimizeToGuide: false, null, null));
	}

	private Task ShowPypassFixInstallProgressPopupAsync(DesktopLibraryDetailResponse detail, PypassFixInstallProgress? progress = null)
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
		string value2 = FormatPypassFixDownloadProgress(progress);
		string loaderText = (string.IsNullOrWhiteSpace(progress?.Message) ? "Đang tải package và cài vào thư mục game..." : progress.Message);
		return ShowActivationPopupAsync(new ActivationPopupPayload("loading", detail.GameName, "Đang chạy Active-2", "Active-2", BuildActivationMeta(detail), (progress?.Stage == "downloading_direct") ? "Launcher đang tải ZIP trực tiếp từ Google Drive rồi sẽ backup và giải nén vào thư mục game." : "Launcher đang tải package, kiểm tra ZIP và giải nén vào thư mục game. Vui lòng giữ launcher mở.", new _003C_003Ez__ReadOnlyArray<ActivationMetricPayload>(new ActivationMetricPayload[4]
		{
			new ActivationMetricPayload("Trạng thái", value, "default"),
			new ActivationMetricPayload("Dung lượng", value2, "default"),
			new ActivationMetricPayload("Tốc độ", FormatPypassFixDownloadSpeed(progress), "default"),
			new ActivationMetricPayload("Kết nối", ((object)progress != null && progress.ConnectionCount > 0) ? progress.ConnectionCount.ToString() : "--", "default")
		}), Array.Empty<ActivationPathPayload>(), new _003C_003Ez__ReadOnlyArray<string>(new string[3] { "Nếu hủy hoặc mất mạng, launcher sẽ tiếp tục phần đã tải ở lần chạy Active-2 kế tiếp.", "File bị ghi đè sẽ được backup trong LocalAppData.", "Active-2 chỉ chạy khi Steam đã cài game hoàn tất và thư mục game có trên 10 file." }), "Hủy tải", null, loaderText, MinimalLoading: true, CompactSuccess: false, null, LockClose: true, ZaloQr: false, MinimizeToGuide: false, "pypass:cancel", progress?.Percentage, progress?.IsIndeterminate ?? false));
	}

	private static string FormatPypassFixDownloadProgress(PypassFixInstallProgress? progress)
	{
		if ((object)progress == null || progress.BytesReceived <= 0)
		{
			return "--";
		}
		string text = FormatFileSize(progress.BytesReceived);
		long? totalBytes = progress.TotalBytes;
		if (!totalBytes.HasValue || totalBytes.GetValueOrDefault() <= 0)
		{
			return text;
		}
		return text + " / " + FormatFileSize(progress.TotalBytes.Value);
	}

	private static string FormatPypassFixDownloadSpeed(PypassFixInstallProgress? progress)
	{
		if ((object)progress == null || progress.SpeedBytesPerSecond <= 0)
		{
			return "--";
		}
		return FormatFileSize(progress.SpeedBytesPerSecond) + "/s";
	}

	private Task ShowPypassFixSuccessPopupAsync(DesktopLibraryDetailResponse detail, PypassFixInstallResult result)
	{
		string gameName = detail.GameName;
		string metaText = BuildActivationMeta(detail);
		ActivationMetricPayload[] metrics = Array.Empty<ActivationMetricPayload>();
		IReadOnlyList<ActivationPathPayload> paths;
		if (!string.IsNullOrWhiteSpace(result.GameExePath))
		{
			IReadOnlyList<ActivationPathPayload> readOnlyList = new _003C_003Ez__ReadOnlySingleElementList<ActivationPathPayload>(new ActivationPathPayload("File chạy game", result.GameExePath));
			paths = readOnlyList;
		}
		else
		{
			IReadOnlyList<ActivationPathPayload> readOnlyList = Array.Empty<ActivationPathPayload>();
			paths = readOnlyList;
		}
		IReadOnlyList<string> messages;
		if (!string.IsNullOrWhiteSpace(result.GameExePath))
		{
			IReadOnlyList<string> readOnlyList2 = new _003C_003Ez__ReadOnlySingleElementList<string>("Hãy mở game bằng link file chạy game hiện trong popup.");
			messages = readOnlyList2;
		}
		else
		{
			IReadOnlyList<string> readOnlyList2 = new _003C_003Ez__ReadOnlySingleElementList<string>("Chưa có link file chạy game. Kiểm tra MainExeRelativePath trong admin.");
			messages = readOnlyList2;
		}
		return ShowActivationPopupAsync(new ActivationPopupPayload("success", gameName, "Active-2 thành công", "Hoàn tất", metaText, "Bạn có thể chơi game. NestG cảm ơn.", metrics, paths, messages, "Hoàn tất", null, null, MinimalLoading: false, CompactSuccess: true, null, LockClose: false, ZaloQr: false, MinimizeToGuide: false, null, null));
	}

	private Task ShowPypassFixErrorPopupAsync(DesktopLibraryDetailResponse detail, string errorMessage)
	{
		string text = (string.IsNullOrWhiteSpace(errorMessage) ? "Không thể chạy Active-2 lúc này." : errorMessage.Trim());
		return ShowActivationPopupAsync(new ActivationPopupPayload("error", detail.GameName, "Không thể chạy Active-2", "Cần kiểm tra", BuildActivationMeta(detail), text, new _003C_003Ez__ReadOnlyArray<ActivationMetricPayload>(new ActivationMetricPayload[2]
		{
			new ActivationMetricPayload("Trạng thái", "Cần thử lại", "error"),
			new ActivationMetricPayload("Phiên bản", NormalizeActivationValue(detail.VersionName, "Mặc định"), "default")
		}), Array.Empty<ActivationPathPayload>(), new _003C_003Ez__ReadOnlyArray<string>(new string[2] { text, "Hãy kiểm tra kết nối, quyền ghi thư mục game và thử lại sau." }), "Đóng", null, null, MinimalLoading: false, CompactSuccess: false, null, LockClose: false, ZaloQr: false, MinimizeToGuide: false, null, null));
	}

	private Task ShowOnlineFixInstallProgressPopupAsync(DesktopLibraryDetailResponse detail)
	{
		return ShowActivationPopupAsync(new ActivationPopupPayload("loading", detail.GameName, "Đang nạp game", "Nạp game", BuildActivationMeta(detail), "Launcher đang tải package và giải nén vào thư mục game. Vui lòng giữ launcher mở trong lúc xử lý.", new _003C_003Ez__ReadOnlyArray<ActivationMetricPayload>(new ActivationMetricPayload[2]
		{
			new ActivationMetricPayload("Trạng thái", "Đang nạp game", "default"),
			new ActivationMetricPayload("Phiên bản", NormalizeActivationValue(detail.VersionName, "Mặc định"), "default")
		}), Array.Empty<ActivationPathPayload>(), new _003C_003Ez__ReadOnlyArray<string>(new string[3] { "Không tắt launcher trong quá trình nạp game.", "File bị ghi đè sẽ được backup trong LocalAppData.", "Nếu launcher không tự tìm thấy thư mục game, hãy chọn đúng folder cài đặt game." }), "Để launcher chạy", null, "Đang tải file và cài vào thư mục game...", MinimalLoading: true, CompactSuccess: false, null, LockClose: false, ZaloQr: false, MinimizeToGuide: false, null, null));
	}

	private Task ShowOnlineFixSuccessPopupAsync(DesktopLibraryDetailResponse detail, OnlineFixInstallResult result)
	{
		return ShowActivationPopupAsync(new ActivationPopupPayload("success", detail.GameName, "Nạp game thành công", "Hoàn tất", BuildActivationMeta(detail), "Game đã được nạp thành công.", Array.Empty<ActivationMetricPayload>(), Array.Empty<ActivationPathPayload>(), Array.Empty<string>(), "Hoàn tất", null, null, MinimalLoading: false, CompactSuccess: true, null, LockClose: false, ZaloQr: false, MinimizeToGuide: false, null, null));
	}

	private Task ShowOnlineFixErrorPopupAsync(DesktopLibraryDetailResponse detail, string errorMessage)
	{
		string text = (string.IsNullOrWhiteSpace(errorMessage) ? "Không thể nạp game lúc này." : errorMessage.Trim());
		return ShowActivationPopupAsync(new ActivationPopupPayload("error", detail.GameName, "Không thể nạp game", "Cần kiểm tra", BuildActivationMeta(detail), text, new _003C_003Ez__ReadOnlyArray<ActivationMetricPayload>(new ActivationMetricPayload[2]
		{
			new ActivationMetricPayload("Trạng thái", "Cần thử lại", "error"),
			new ActivationMetricPayload("Phiên bản", NormalizeActivationValue(detail.VersionName, "Mặc định"), "default")
		}), Array.Empty<ActivationPathPayload>(), new _003C_003Ez__ReadOnlyArray<string>(new string[2] { text, "Hãy kiểm tra kết nối, quyền ghi thư mục game và thử lại sau." }), "Đóng", null, null, MinimalLoading: false, CompactSuccess: false, null, LockClose: false, ZaloQr: false, MinimizeToGuide: false, null, null));
	}

	private Task ShowGameActivationProgressPopupAsync(DesktopLibraryDetailResponse detail, GameActivationProgress progress)
	{
		if (progress.Stage == GameActivationProgressStage.Completed)
		{
			return Task.CompletedTask;
		}
		int overallPercentage = progress.OverallPercentage;
		string gameName = detail.GameName;
		string metaText = BuildActivationMeta(detail);
		_003C_003Ez__ReadOnlyArray<ActivationMetricPayload> metrics = new _003C_003Ez__ReadOnlyArray<ActivationMetricPayload>(new ActivationMetricPayload[2]
		{
			new ActivationMetricPayload("Trạng thái", progress.Message, "default"),
			new ActivationMetricPayload("Phiên bản", NormalizeActivationValue(detail.VersionName, "Chưa xác định"), "default")
		});
		ActivationPathPayload[] paths = Array.Empty<ActivationPathPayload>();
		string[] messages = Array.Empty<string>();
		string message = progress.Message;
		int? progressPercentage = overallPercentage;
		return ShowActivationPopupAsync(new ActivationPopupPayload("loading", gameName, "Đang Active game", "Tự động", metaText, "Launcher đang kích hoạt game. Vui lòng giữ Launcher mở.", metrics, paths, messages, "Đang xử lý", null, message, MinimalLoading: true, CompactSuccess: false, null, LockClose: true, ZaloQr: false, MinimizeToGuide: false, null, progressPercentage));
	}

	private Task ShowCloudSaveProgressPopupAsync(DesktopLibraryDetailResponse detail, CloudSaveProgress progress, bool isRestore = false)
	{
		string title = progress.Stage switch
		{
			CloudSaveProgressStage.Connecting => "Đang kết nối Google Drive", 
			CloudSaveProgressStage.Listing => "Đang tìm các bản lưu", 
			CloudSaveProgressStage.Downloading => "Đang tải bản lưu đã chọn", 
			CloudSaveProgressStage.Restoring => "Đang khôi phục bản lưu", 
			CloudSaveProgressStage.ClosingGame => "Đang thoát game", 
			CloudSaveProgressStage.WaitingForSync => "Đang chờ đồng bộ save", 
			CloudSaveProgressStage.ClosingSteam => "Đang thoát Steam", 
			CloudSaveProgressStage.Inspecting => isRestore ? "Đang xác minh bản lưu" : "Đang kiểm tra save game", 
			CloudSaveProgressStage.Snapshotting => "Đang tạo snapshot", 
			CloudSaveProgressStage.Uploading => "Đang tải lên Google Drive", 
			CloudSaveProgressStage.Verifying => "Đang xác minh bản backup", 
			_ => "Đang lưu Cloud Save", 
		};
		string gameName = detail.GameName;
		string metaText = BuildActivationMeta(detail);
		string summary = (isRestore ? "Launcher kiểm tra bản lưu và giữ một bản dự phòng save hiện tại trước khi khôi phục." : "Cloud Save giữ provider CloudRedirect và cấu hình hiện tại của bạn.");
		_003C_003Ez__ReadOnlyArray<ActivationMetricPayload> metrics = new _003C_003Ez__ReadOnlyArray<ActivationMetricPayload>(new ActivationMetricPayload[2]
		{
			new ActivationMetricPayload("Trạng thái", progress.Message, "default"),
			new ActivationMetricPayload("Steam App ID", detail.SteamAppId?.ToString(CultureInfo.InvariantCulture) ?? "-", "default")
		});
		ActivationPathPayload[] paths = Array.Empty<ActivationPathPayload>();
		_003C_003Ez__ReadOnlySingleElementList<string> messages = new _003C_003Ez__ReadOnlySingleElementList<string>(isRestore ? "Giữ launcher mở cho đến khi bản lưu được áp dụng xong." : "Giữ launcher mở trong lúc snapshot được tạo và tải lên Drive.");
		string message = progress.Message;
		int? progressPercentage = Math.Clamp(progress.Percentage, 0, 100);
		return ShowActivationPopupAsync(new ActivationPopupPayload("loading", gameName, title, "GOOGLE DRIVE", metaText, summary, metrics, paths, messages, "Đang xử lý", null, message, MinimalLoading: true, CompactSuccess: false, null, LockClose: true, ZaloQr: false, MinimizeToGuide: false, null, progressPercentage));
	}

	private Task ShowCloudSaveSuccessPopupAsync(DesktopLibraryDetailResponse detail, CloudSaveBackupResult result)
	{
		return ShowActivationPopupAsync(new ActivationPopupPayload("success", detail.GameName, "Đã lưu Cloud Save", "GOOGLE DRIVE", BuildActivationMeta(detail), "Snapshot đã được upload, đối chiếu checksum và đánh dấu hoàn tất trên Google Drive.", new _003C_003Ez__ReadOnlyArray<ActivationMetricPayload>(new ActivationMetricPayload[3]
		{
			new ActivationMetricPayload("Trạng thái", "Đã xác minh", "success"),
			new ActivationMetricPayload("Số file", result.FileCount.ToString(CultureInfo.InvariantCulture), "default"),
			new ActivationMetricPayload("Dung lượng", FormatFileSize(result.ArchiveSize), "default")
		}), Array.Empty<ActivationPathPayload>(), new _003C_003Ez__ReadOnlyArray<string>(new string[2]
		{
			"Phiên bản: " + result.SnapshotId,
			$"Steam account: {result.SteamAccountId}"
		}), "Hoàn tất", null, null, MinimalLoading: false, CompactSuccess: true, null, LockClose: false, ZaloQr: false, MinimizeToGuide: false, null, null));
	}

	private Task ShowCloudSaveRestoreAppliedPopupAsync(DesktopLibraryDetailResponse detail, CloudSaveRestoreResult result)
	{
		string gameName = detail.GameName;
		string metaText = BuildActivationMeta(detail);
		_003C_003Ez__ReadOnlyArray<ActivationMetricPayload> metrics = new _003C_003Ez__ReadOnlyArray<ActivationMetricPayload>(new ActivationMetricPayload[3]
		{
			new ActivationMetricPayload("Trạng thái", "Đã áp dụng", "success"),
			new ActivationMetricPayload("Số file", result.FileCount.ToString(CultureInfo.InvariantCulture), "default"),
			new ActivationMetricPayload("Steam account", result.SteamAccountId.ToString(CultureInfo.InvariantCulture), "default")
		});
		ActivationPathPayload[] paths = Array.Empty<ActivationPathPayload>();
		_003C_003Ez__ReadOnlyArray<string> messages = new _003C_003Ez__ReadOnlyArray<string>(new string[2]
		{
			"Phiên bản: " + result.SnapshotId,
			"Mở Steam và game để xác nhận game nạp đúng tiến độ. Giữ bản dự phòng nếu cần quay lại save trước restore."
		});
		IReadOnlyList<string> technicalDetails = new _003C_003Ez__ReadOnlySingleElementList<string>("SafetyBackup=" + result.SafetyBackupPath);
		return ShowActivationPopupAsync(new ActivationPopupPayload("success", gameName, "Đã áp dụng bản lưu", "CLOUD SAVE RESTORE", metaText, "Bản đã chọn đã được kiểm tra và đưa vào CloudRedirect. Save trước restore được giữ trên máy để phục hồi nếu cần.", metrics, paths, messages, "Đã hiểu", null, null, MinimalLoading: false, CompactSuccess: true, null, LockClose: false, ZaloQr: false, MinimizeToGuide: false, null, null, ProgressIndeterminate: false, technicalDetails));
	}

	private Task ShowCloudSaveErrorPopupAsync(DesktopLibraryDetailResponse detail, CloudSaveErrorPresentation error)
	{
		string state = error.State;
		string gameName = detail.GameName;
		string title = error.Title;
		string badgeText = ((error.State == "warning") ? "CHƯA CÓ SAVE" : "CẦN KIỂM TRA");
		string metaText = BuildActivationMeta(detail);
		string summary = error.Summary;
		_003C_003Ez__ReadOnlySingleElementList<ActivationMetricPayload> metrics = new _003C_003Ez__ReadOnlySingleElementList<ActivationMetricPayload>(new ActivationMetricPayload("Trạng thái", error.Status, error.State));
		ActivationPathPayload[] paths = Array.Empty<ActivationPathPayload>();
		_003C_003Ez__ReadOnlySingleElementList<string> messages = new _003C_003Ez__ReadOnlySingleElementList<string>(error.Hint);
		IReadOnlyList<string> technicalDetails = error.TechnicalDetails;
		return ShowActivationPopupAsync(new ActivationPopupPayload(state, gameName, title, badgeText, metaText, summary, metrics, paths, messages, "Đóng", null, null, MinimalLoading: false, CompactSuccess: false, null, LockClose: false, ZaloQr: false, MinimizeToGuide: false, null, null, ProgressIndeterminate: false, technicalDetails, SingleCloseAction: true, FullSummary: true));
	}

	private Task CloseActivationPopupAsync()
	{
		return ExecuteScriptAsync("window.__nestgActivation?.close(true);");
	}

	private Task ShowActivationPopupAsync(ActivationPopupPayload payload)
	{
		return ExecuteScriptAsync("window.__nestgActivation?.show(" + JsonSerializer.Serialize(payload, JsonOptions) + ");");
	}

	private static IReadOnlyList<string> BuildActivationMessages(string state, GameActivationResult? result, string errorMessage)
	{
		List<string> list = new List<string>();
		if (state == "success")
		{
			list.Add("Bạn có thể mở game từ Steam.");
		}
		else if ((object)result != null && result.SteamInstall.IsFound)
		{
			list.Add(result.SteamInstall.IsSteamRestarted ? "Hãy thử Active lại. Nếu lỗi tiếp tục, mở CLOUD SAVE để chẩn đoán." : "Hãy mở lại Steam rồi kiểm tra game trong thư viện.");
		}
		else if (state == "warning")
		{
			list.Add("Hãy cài đặt hoặc mở Steam ít nhất một lần, sau đó bấm Active lại.");
		}
		else
		{
			list.Add(errorMessage);
			list.Add("Xem chi tiết lỗi để biết bước cần xử lý trước khi Active lại.");
		}
		return list.Where((string message) => !string.IsNullOrWhiteSpace(message)).Distinct<string>(StringComparer.OrdinalIgnoreCase).ToArray();
	}

	private static IReadOnlyList<string> BuildActivationTechnicalDetails(GameActivationResult result)
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

	private static string BuildActivationMeta(DesktopLibraryDetailResponse detail)
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
		if (!string.IsNullOrWhiteSpace(detail.AcquiredAtText))
		{
			list.Add(detail.AcquiredAtText.Trim());
		}
		if (list.Count != 0)
		{
			return string.Join(" • ", list);
		}
		return "Launcher đã sẵn sàng hoàn tất kích hoạt.";
	}

	private static string FormatActivationFileCount(int count)
	{
		return $"{Math.Max(0, count)} tệp";
	}

	private static string FormatDenuvoFileCount(int count)
	{
		return $"{Math.Max(0, count)} file";
	}

	private static string NormalizeActivationValue(string? value, string fallback)
	{
		if (!string.IsNullOrWhiteSpace(value))
		{
			return value.Trim();
		}
		return fallback;
	}

	private async Task SetLibraryActivationStateAsync(bool isLoading)
	{
		if (homeWebView.IsDisposed || homeWebView.CoreWebView2 == null)
		{
			return;
		}
		try
		{
			homeWebView.CoreWebView2.PostWebMessageAsString(isLoading ? "activation:start" : "activation:finish");
			await Task.CompletedTask;
		}
		catch
		{
		}
	}

	private async Task SetLibraryCloudSaveStateAsync(bool isLoading)
	{
		if (homeWebView.IsDisposed || homeWebView.CoreWebView2 == null)
		{
			return;
		}
		try
		{
			homeWebView.CoreWebView2.PostWebMessageAsString(isLoading ? "cloudsave:start" : "cloudsave:finish");
			await Task.CompletedTask;
		}
		catch
		{
		}
	}

	private async Task SetLibraryDenuvoStateAsync(bool isLoading)
	{
		if (homeWebView.IsDisposed || homeWebView.CoreWebView2 == null)
		{
			return;
		}
		try
		{
			homeWebView.CoreWebView2.PostWebMessageAsString(isLoading ? "denuvo:start" : "denuvo:finish");
			await Task.CompletedTask;
		}
		catch
		{
		}
	}

	private async Task SetLibraryDenuvoAppliedStateAsync(DateTime? cooldownUntilUtc = null)
	{
		if (homeWebView.IsDisposed || homeWebView.CoreWebView2 == null)
		{
			return;
		}
		try
		{
			string webMessageAsString = (cooldownUntilUtc.HasValue ? $"denuvo:cooldown:{cooldownUntilUtc.Value.ToUniversalTime():O}" : "denuvo:applied");
			homeWebView.CoreWebView2.PostWebMessageAsString(webMessageAsString);
			await Task.CompletedTask;
		}
		catch
		{
		}
	}

	private async Task SetLibraryDenuvoUnlockedStateAsync()
	{
		if (homeWebView.IsDisposed || homeWebView.CoreWebView2 == null)
		{
			return;
		}
		try
		{
			homeWebView.CoreWebView2.PostWebMessageAsString("denuvo:unlocked");
			await Task.CompletedTask;
		}
		catch
		{
		}
	}

	private async Task SetLibraryPypassStateAsync(bool isLoading)
	{
		if (homeWebView.IsDisposed || homeWebView.CoreWebView2 == null)
		{
			return;
		}
		try
		{
			homeWebView.CoreWebView2.PostWebMessageAsString(isLoading ? "pypass:start" : "pypass:finish");
			await Task.CompletedTask;
		}
		catch
		{
		}
	}

	private async Task SetLibraryPypassAppliedStateAsync()
	{
		if (homeWebView.IsDisposed || homeWebView.CoreWebView2 == null)
		{
			return;
		}
		try
		{
			homeWebView.CoreWebView2.PostWebMessageAsString("pypass:applied");
			await Task.CompletedTask;
		}
		catch
		{
		}
	}

	private async Task SetLibraryOnlineStateAsync(bool isLoading)
	{
		if (homeWebView.IsDisposed || homeWebView.CoreWebView2 == null)
		{
			return;
		}
		try
		{
			homeWebView.CoreWebView2.PostWebMessageAsString(isLoading ? "online:start" : "online:finish");
			await Task.CompletedTask;
		}
		catch
		{
		}
	}

	private async Task SetLibraryOnlineAppliedStateAsync()
	{
		if (homeWebView.IsDisposed || homeWebView.CoreWebView2 == null)
		{
			return;
		}
		try
		{
			homeWebView.CoreWebView2.PostWebMessageAsString("online:applied");
			await Task.CompletedTask;
		}
		catch
		{
		}
	}

	private async Task<int?> HandleDetailAddToCartAsync(StoreGameDetailData data, StoreProductEdition? selectedEdition, bool includeCheckoutHint)
	{
		try
		{
			string accessToken = await DesktopAppContext.AuthSessionManager.GetAccessTokenAsync(disposalCancellation.Token);
			StorefrontApiClient.StorefrontCartMutationResponse result = await storefrontApiClient.AddToCartAsync(data.GameId, selectedEdition?.VersionId, 1, accessToken, disposalCancellation.Token);
			int cartItemCount = Math.Max(0, result.CartItemCount);
			await UpdateCartCountAsync(cartItemCount);
			await ShowToastAsync(result.Message);
			return cartItemCount;
		}
		catch (OperationCanceledException) when (disposalCancellation.IsCancellationRequested)
		{
			return null;
		}
		catch (Exception exception)
		{
			await ShowUserMessageAsync(UserError(exception), MessageBoxIcon.Exclamation);
			return null;
		}
	}

	private async Task HandleDetailReviewSubmitAsync(string message)
	{
		if (currentDetailData == null || isSubmittingReview)
		{
			return;
		}
		StoreGameDetailData detail = currentDetailData;
		string[] payload = message.Substring("review:".Length).Split(':', 3);
		if (payload.Length != 0 && decimal.TryParse(payload[0], NumberStyles.Number, CultureInfo.InvariantCulture, out var rating))
		{
			int result = 0;
			bool hasPostedScrollY = payload.Length > 1 && int.TryParse(payload[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out result);
			int num = ((!hasPostedScrollY) ? (await CaptureDetailScrollYAsync()) : Math.Max(0, result));
			int postedScrollY = num;
			string reviewText = ((payload.Length > 2) ? Uri.UnescapeDataString(payload[2]) : ((payload.Length > 1 && !hasPostedScrollY) ? Uri.UnescapeDataString(payload[1]) : string.Empty));
			isSubmittingReview = true;
			try
			{
				try
				{
					string text = await DesktopAppContext.AuthSessionManager.GetAccessTokenAsync(disposalCancellation.Token);
					if (string.IsNullOrWhiteSpace(text))
					{
						await ShowUserMessageAsync("Phiên đăng nhập đã hết hạn. Hãy đăng nhập lại để đánh giá sản phẩm.", MessageBoxIcon.Asterisk);
						await ResetReviewSubmitButtonAsync();
						return;
					}
					await storefrontApiClient.SubmitProductReviewAsync(detail.GameId, rating, reviewText, text, disposalCancellation.Token);
					await LoadGameDetailAsync(detail.GameId, postedScrollY, showLoadingPopup: false, null);
				}
				catch (OperationCanceledException) when (disposalCancellation.IsCancellationRequested)
				{
				}
				catch (Exception exception)
				{
					await ShowUserMessageAsync(UserError(exception, "Không thể gửi đánh giá lúc này. Vui lòng thử lại."), MessageBoxIcon.Exclamation);
					await ResetReviewSubmitButtonAsync();
				}
				return;
			}
			finally
			{
				isSubmittingReview = false;
			}
		}
		await ShowUserMessageAsync("Hãy chọn số sao đánh giá từ 1 đến 5.", MessageBoxIcon.Asterisk);
		await ResetReviewSubmitButtonAsync();
	}

	private async Task<int> CaptureDetailScrollYAsync()
	{
		if (homeWebView.IsDisposed || homeWebView.CoreWebView2 == null)
		{
			return 0;
		}
		int result;
		return int.TryParse(await homeWebView.ExecuteScriptAsync("Math.round(window.scrollY || document.documentElement.scrollTop || 0)"), NumberStyles.Integer, CultureInfo.InvariantCulture, out result) ? Math.Max(0, result) : 0;
	}

	private async Task ResetReviewSubmitButtonAsync()
	{
		if (!homeWebView.IsDisposed && homeWebView.CoreWebView2 != null)
		{
			await homeWebView.ExecuteScriptAsync("(() => {\r\n    const button = document.querySelector('[data-review-submit]');\r\n    if (button) {\r\n        button.disabled = false;\r\n        button.textContent = button.getAttribute('data-default-label') || 'Gửi đánh giá';\r\n    }\r\n    window.__nestgReview?.finish?.();\r\n})();");
		}
	}

	private async Task ShowUserMessageAsync(string message, MessageBoxIcon icon)
	{
		if (currentDetailData == null)
		{
			await ShowToastAsync(message);
		}
		else
		{
			await LauncherMessageBox.ShowAsync(this, message, "NestG Launcher", MessageBoxButtons.OK, icon);
		}
	}

	private async Task UpdateCartCountAsync(int cartItemCount)
	{
		currentContext = currentContext with
		{
			CartItemCount = Math.Max(0, cartItemCount),
			IsCartCountAvailable = true
		};
		await ExecuteScriptAsync($"window.__nestgHome?.setCartCount({currentContext.CartItemCount});");
	}

	private Task ShowToastAsync(string message)
	{
		return ExecuteScriptAsync("window.__nestgHome?.toast(" + JsonSerializer.Serialize(message) + ");");
	}

	private static string UserError(Exception exception, string fallback = "Không thể hoàn tất thao tác. Vui lòng thử lại.")
	{
		return DesktopUserMessage.FromException(exception, fallback);
	}

	private Task SetWalletTopUpBusyAsync(bool isBusy, string message = "", bool isError = false)
	{
		string value = (isBusy ? "true" : "false");
		string value2 = JsonSerializer.Serialize(message, JsonOptions);
		string value3 = (isError ? "true" : "false");
		return ExecuteScriptAsync($"window.__nestgWalletTopUp?.setBusy({value}, {value2}, {value3});");
	}

	private Task ShowOrderCheckoutPopupAsync(DesktopCheckoutPaymentResponse checkout)
	{
		activeOrderCheckoutPayment = checkout;
		if (!string.Equals(completedOrderCheckoutOrderCode, checkout.OrderCode, StringComparison.OrdinalIgnoreCase))
		{
			completedOrderCheckoutOrderCode = null;
		}
		bool isWalletPayment = checkout.IsWalletPayment;
		bool flag = !isWalletPayment && checkout.HasQrCode && !string.IsNullOrWhiteSpace(checkout.QrImageUrl);
		string qrImageUrl = (flag ? checkout.QrImageUrl : string.Empty);
		var value = new
		{
			OrderCode = checkout.OrderCode,
			PageTitle = checkout.PageTitle,
			TotalAmountText = checkout.TotalAmountText,
			BankName = FormatBankDisplayName(checkout.ReceiverBankName, checkout.ReceiverBankShortName),
			AccountNumber = checkout.ReceiverAccountNumber,
			AccountName = checkout.ReceiverAccountName,
			TransferContent = checkout.TransferContent,
			QrImageUrl = qrImageUrl,
			HasQrCode = flag,
			IsWalletPayment = isWalletPayment,
			ExpiresAtText = (checkout.ExpiresAtText ?? "Không giới hạn"),
			StatusTitle = checkout.StatusTitle,
			StatusDescription = checkout.StatusDescription,
			IsPaid = checkout.IsPaid,
			SubtotalText = checkout.SubtotalText,
			DiscountAmountText = checkout.DiscountAmountText,
			PromoCode = checkout.PromoCode,
			Items = checkout.Items.Select((DesktopCheckoutPaymentLineItemResponse item) => new { item.Name, item.VersionName, item.Quantity, item.UnitPriceText, item.LineTotalText })
		};
		return ExecuteScriptAsync("window.__nestgOrderCheckout?.show(" + JsonSerializer.Serialize(value, JsonOptions) + ");");
	}

	private Task UpdateOrderCheckoutStatusPopupAsync(DesktopCheckoutStatusResponse status)
	{
		var value = new { status.OrderCode, status.StatusTitle, status.StatusDescription, status.IsPaid, status.IsExpired };
		return ExecuteScriptAsync("window.__nestgOrderCheckout?.updateStatus(" + JsonSerializer.Serialize(value, JsonOptions) + ");");
	}

	private Task ShowWalletTopUpCheckoutPopupAsync(DesktopCheckoutPaymentResponse checkout)
	{
		var value = new
		{
			OrderCode = checkout.OrderCode,
			TotalAmountText = checkout.TotalAmountText,
			BankName = FormatBankDisplayName(checkout.ReceiverBankName, checkout.ReceiverBankShortName),
			AccountNumber = checkout.ReceiverAccountNumber,
			AccountName = checkout.ReceiverAccountName,
			TransferContent = checkout.TransferContent,
			QrImageUrl = checkout.QrImageUrl,
			ExpiresAtText = (checkout.ExpiresAtText ?? "Không giới hạn"),
			StatusTitle = checkout.StatusTitle,
			StatusDescription = checkout.StatusDescription,
			IsPaid = checkout.IsPaid
		};
		return ExecuteScriptAsync("window.__nestgWalletTopUp?.showQrPopup(" + JsonSerializer.Serialize(value, JsonOptions) + ");");
	}

	private Task UpdateWalletTopUpStatusPopupAsync(DesktopCheckoutStatusResponse status)
	{
		var value = new { status.OrderCode, status.StatusTitle, status.StatusDescription, status.IsPaid, status.IsExpired };
		return ExecuteScriptAsync("window.__nestgWalletTopUp?.updateQrStatus(" + JsonSerializer.Serialize(value, JsonOptions) + ");");
	}

	private Task ShowWalletTopUpSuccessPopupAsync(string walletBalanceText)
	{
		var value = new
		{
			Title = "Bạn đã thanh toán thành công",
			Message = "Tiền đã được cộng vào ví NestG của bạn.",
			WalletBalanceText = walletBalanceText
		};
		return ExecuteScriptAsync("window.__nestgWalletTopUp?.showSuccess(" + JsonSerializer.Serialize(value, JsonOptions) + ");");
	}

	private static string FormatBankDisplayName(string? bankName, string? shortName)
	{
		if (!string.IsNullOrWhiteSpace(shortName) && !string.IsNullOrWhiteSpace(bankName))
		{
			return shortName.Trim() + " - " + bankName.Trim();
		}
		object obj;
		if (string.IsNullOrWhiteSpace(shortName))
		{
			obj = bankName?.Trim();
			if (obj == null)
			{
				return string.Empty;
			}
		}
		else
		{
			obj = shortName.Trim();
		}
		return (string)obj;
	}

	private static decimal ParseCurrencyValue(string? rawText)
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

	private Task ShowLoadingPopupAsync(string title, string message)
	{
		string value = JsonSerializer.Serialize(title, JsonOptions);
		string value2 = JsonSerializer.Serialize(message, JsonOptions);
		return ExecuteScriptAsync($"window.__nestgLoading?.show({value}, {value2});");
	}

	private Task HideLoadingPopupAsync()
	{
		return ExecuteScriptAsync("window.__nestgLoading?.hide();");
	}

	private async Task<bool> TryHandleExpiredSessionAsync(Exception exception)
	{
		if (!(exception is UnauthorizedAccessException) && !DesktopUserMessage.FromException(exception, string.Empty).Contains("Phiên đăng nhập đã hết hạn", StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}
		await NavigateToLoginForExpiredSessionAsync();
		return true;
	}

	private async Task NavigateToLoginForExpiredSessionAsync()
	{
		if (isNavigatingToLogin || isSigningOut || base.IsDisposed || base.Disposing)
		{
			return;
		}
		isNavigatingToLogin = true;
		await StopFix54Async();
		DesktopDiagnosticLog.Write("Authentication", "ExpiredSessionNavigationRequested");
		await HideLoadingPopupAsync();
		await StopDenuvoRealtimeAsync();
		await DesktopAppContext.AuthSessionManager.SignOutAsync();
		await StopSupportRealtimeAsync();
		if (!base.IsDisposed && !base.Disposing)
		{
			await LauncherMessageBox.ShowAsync(this, "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại để tiếp tục.", "Đăng nhập lại", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
			if (!base.IsDisposed && !base.Disposing)
			{
				AuthUiHelper.Navigate(this, DesktopAppContext.CreateLoginForm());
			}
		}
	}

	private async Task SignOutAsync()
	{
		if (isSigningOut || isNavigatingToLogin || denuvoCloseStarted)
		{
			return;
		}
		isSigningOut = true;
		await ShowLoadingPopupAsync("Đang đăng xuất", "NestG đang đóng phiên đăng nhập của bạn.");
		try
		{
			await StopFix54Async();
			await StopDenuvoRealtimeAsync();
			await DesktopAppContext.AuthSessionManager.SignOutAsync();
			await StopSupportRealtimeAsync();
			AuthUiHelper.Navigate(this, DesktopAppContext.CreateLoginForm());
		}
		catch (Exception exception)
		{
			if (!denuvoCloseStarted && !isNavigatingToLogin && !isStartingLauncherUpdate)
			{
				fix54Lifetime.AllowNewOperations();
			}
			await HideLoadingPopupAsync();
			await ShowToastAsync(UserError(exception, "Không thể đăng xuất. Vui lòng thử lại."));
		}
		finally
		{
			isSigningOut = false;
		}
	}

	private async Task StopSupportRealtimeAsync()
	{
		try
		{
			await DesktopAppContext.SupportRealtimeClient.StopAsync();
		}
		catch
		{
		}
		finally
		{
			activeSupportConversationId = null;
			currentSupportConversation = null;
			supportPopupTrigger.Reset();
			supportPopup.ClearChat();
		}
	}

	private async Task ExecuteScriptAsync(string script)
	{
		try
		{
			if (!homeWebView.IsDisposed && homeWebView.CoreWebView2 != null)
			{
				await homeWebView.CoreWebView2.ExecuteScriptAsync(script);
			}
		}
		catch
		{
		}
	}

	protected override void WndProc(ref Message message)
	{
		base.WndProc(ref message);
		if (message.Msg == 132 && base.WindowState == FormWindowState.Normal && ((IntPtr)message.Result).ToInt32() == 1)
		{
			long num = ((IntPtr)message.LParam).ToInt64();
			Point p = new Point((short)(num & 0xFFFF), (short)((num >> 16) & 0xFFFF));
			Point point = PointToClient(p);
			int num2 = Math.Max(6, 8 * base.DeviceDpi / 96);
			bool num3 = point.X <= num2;
			bool flag = point.X >= base.ClientSize.Width - num2;
			bool flag2 = point.Y <= num2;
			bool flag3 = point.Y >= base.ClientSize.Height - num2;
			nint result = (num3 ? (flag2 ? 13 : ((!flag3) ? 10 : 16)) : (flag ? (flag2 ? 14 : ((!flag3) ? 11 : 17)) : (flag2 ? 12 : ((!flag3) ? message.Result : 15))));
			message.Result = result;
		}
	}

	private void BeginWindowDrag()
	{
		if (base.WindowState == FormWindowState.Maximized)
		{
			base.WindowState = FormWindowState.Normal;
		}
		ReleaseCapture();
		SendMessage(base.Handle, 161, 2, 0);
	}

	[DllImport("user32.dll")]
	private static extern bool ReleaseCapture();

	[DllImport("user32.dll")]
	private static extern nint SendMessage(nint hWnd, int msg, int wParam, int lParam);

	private void HandleDenuvoRealtimeStateChanged(object? sender, DesktopDenuvoRealtimeState state)
	{
		RunOnUiThreadAsync(() => UpdateDenuvoRealtimeStatusAsync(state));
	}

	private Task UpdateDenuvoRealtimeStatusAsync(DesktopDenuvoRealtimeState state)
	{
		if (!disposalCancellation.IsCancellationRequested)
		{
			DesktopLibraryDetailResponse desktopLibraryDetailResponse = currentLibraryDetail;
			if (desktopLibraryDetailResponse != null && (!state.LibraryId.HasValue || !(state.LibraryId != desktopLibraryDetailResponse.LibraryId)))
			{
				return ExecuteScriptAsync("window.__nestgDenuvoRealtime?.update(" + JsonSerializer.Serialize(state, JsonOptions) + ");");
			}
		}
		return Task.CompletedTask;
	}

	private async Task ReplayDeferredDenuvoResultAsync(DesktopLibraryDetailResponse detail, Guid requestId)
	{
		EventArgs eventArgs = deferredDenuvoUiResults.Take(detail.LibraryId, requestId);
		if (currentLibraryDetail == detail && !disposalCancellation.IsCancellationRequested)
		{
			if (eventArgs is DesktopDenuvoActiveTokenAppliedEventArgs payload)
			{
				await ShowDenuvoFixTokenAppliedPopupAsync(detail, payload);
			}
			else if (eventArgs is DesktopDenuvoActiveRequestCancelledEventArgs payload2)
			{
				await ShowDenuvoFixCancelledPopupAsync(detail, payload2);
			}
		}
	}

	private string BeginFix54Document()
	{
		CancelFix54ForNavigation();
		return fix54DocumentId = Guid.NewGuid().ToString("N");
	}

	private void CancelFix54ForNavigation()
	{
		fix54DocumentId = null;
		fix54TerminalFeedback = null;
		fix54Lifetime.Cancel();
	}

	private async Task StopFix54Async()
	{
		try
		{
			await fix54Lifetime.StopAsync();
		}
		catch (OperationCanceledException)
		{
		}
	}

	private async Task HandleFix54WebMessageAsync(string message, string source)
	{
		DesktopLibraryDetailResponse detail = currentLibraryDetail;
		if (detail == null || !StoreFix54Message.TryAuthorize(message, source, currentSurface == StoreSurface.LibraryDetail, detail.LibraryId, fix54DocumentId, out var isCancellation))
		{
			return;
		}
		if (isCancellation)
		{
			fix54Lifetime.Cancel();
			if (fix54WorkActive)
			{
				await SetFix54StateAsync(detail.LibraryId, fix54DocumentId, isRunning: true, "Đang hủy và kiểm tra file gốc...", "loading", null, isCancelling: true);
			}
			return;
		}
		if (ShouldSuppressFix54Feedback)
		{
			await SetFix54StateAsync(detail.LibraryId, fix54DocumentId, isRunning: false, string.Empty, "warning", null);
			return;
		}
		if (isFixing54)
		{
			if (!fix54WorkActive)
			{
				StoreFix54Feedback storeFix54Feedback = (fix54TerminalFeedback ?? new StoreFix54Feedback(detail.LibraryId, fix54DocumentId, "warning", string.Empty)).ForDocument(detail.LibraryId, fix54DocumentId);
				await SetFix54StateAsync(storeFix54Feedback.LibraryId, storeFix54Feedback.DocumentId, isRunning: false, storeFix54Feedback.Message, storeFix54Feedback.State, null);
			}
			return;
		}
		if (isStartingLauncherUpdate || isActivatingLibraryGame || isCloudSavingLibraryGame || isManagingCloudRedirect || isApplyingLibraryDenuvoFix || isApplyingLibraryPypassFix || isApplyingLibraryOnlineFix)
		{
			await SetFix54StateAsync(detail.LibraryId, fix54DocumentId, isRunning: false, "Một tác vụ game khác đang chạy. Hãy chờ hoàn tất rồi thử FIX 54.", "warning", null);
			return;
		}
		string documentId = fix54DocumentId;
		Task task = fix54Lifetime.TryStart((CancellationToken token) => RunFix54Async(detail, documentId, token), disposalCancellation.Token);
		if (task != null)
		{
			await task;
		}
	}

	private async Task RunFix54Async(DesktopLibraryDetailResponse detail, string documentId, CancellationToken cancellationToken)
	{
		bool operationActive = true;
		fix54WorkActive = true;
		fix54TerminalFeedback = null;
		string message = "Đang chuẩn bị FIX 54...";
		string state = "error";
		try
		{
			_ = 5;
			try
			{
				await SetFix54StateAsync(detail.LibraryId, documentId, isRunning: true, message, "loading", null);
				int? steamAppId = detail.SteamAppId;
				if (!steamAppId.HasValue || steamAppId.GetValueOrDefault() <= 0)
				{
					throw new InvalidOperationException("Game chưa có Steam App ID hợp lệ để chạy FIX 54.");
				}
				Guid? sessionUser = DesktopAppContext.AuthSessionManager.CurrentSession?.UserId;
				string value = await DesktopAppContext.AuthSessionManager.GetAccessTokenAsync(cancellationToken);
				if (!sessionUser.HasValue || string.IsNullOrWhiteSpace(value))
				{
					throw new InvalidOperationException("Phiên NestG đã hết hạn. Hãy đăng nhập lại trước khi dùng FIX 54.");
				}
				using (await CloudRedirectOperationCoordinator.AcquireAsync(cancellationToken))
				{
					cancellationToken.ThrowIfCancellationRequested();
					value = await DesktopAppContext.AuthSessionManager.GetAccessTokenAsync(cancellationToken);
					if (string.IsNullOrWhiteSpace(value))
					{
						goto IL_0399;
					}
					AuthSession? currentSession = DesktopAppContext.AuthSessionManager.CurrentSession;
					bool num;
					if (currentSession == null)
					{
						num = sessionUser.HasValue;
					}
					else
					{
						Guid userId = currentSession.UserId;
						Guid? guid = sessionUser;
						num = userId != guid;
					}
					if (num)
					{
						goto IL_0399;
					}
					DesktopLibraryDetailResponse verified = await storefrontApiClient.GetLibraryDetailAsync(detail.LibraryId, value, cancellationToken);
					cancellationToken.ThrowIfCancellationRequested();
					if (verified.LibraryId != detail.LibraryId || verified.SteamAppId != detail.SteamAppId)
					{
						goto IL_04ea;
					}
					AuthSession? currentSession2 = DesktopAppContext.AuthSessionManager.CurrentSession;
					bool num2;
					if (currentSession2 == null)
					{
						num2 = sessionUser.HasValue;
					}
					else
					{
						Guid userId = currentSession2.UserId;
						Guid? guid = sessionUser;
						num2 = userId != guid;
					}
					if (num2)
					{
						goto IL_04ea;
					}
					Progress<Fix54Progress> progress = new Progress<Fix54Progress>(delegate(Fix54Progress update)
					{
						if (operationActive && !cancellationToken.IsCancellationRequested)
						{
							SetFix54StateAsync(detail.LibraryId, documentId, isRunning: true, update.Message, "loading", update.Percent);
						}
					});
					Fix54Result fix54Result = await fix54Service.RunAsync(verified.SteamAppId.Value, progress, cancellationToken);
					operationActive = false;
					fix54WorkActive = false;
					message = fix54Result.Message;
					state = StoreFix54Feedback.StateFor(fix54Result);
					Fix54Outcome outcome = fix54Result.Outcome;
					bool flag = (uint)(outcome - 2) <= 1u;
					if (flag && !fix54Result.RollbackCompleted)
					{
						string backupDirectory = fix54Result.BackupDirectory;
						if (backupDirectory != null)
						{
							message = message + " Bản sao lưu EXE: " + backupDirectory;
						}
					}
					DesktopDiagnosticLog.Write("Fix54", "Completed", $"LibraryId={detail.LibraryId}; SteamAppId={verified.SteamAppId}; Outcome={fix54Result.Outcome}; Changed={fix54Result.ChangedFiles}; RollbackCompleted={fix54Result.RollbackCompleted}");
					goto end_IL_02cd;
					IL_0399:
					throw new InvalidOperationException("Phiên NestG đã thay đổi hoặc hết hạn. Hãy đăng nhập lại trước khi dùng FIX 54.");
					IL_04ea:
					throw new InvalidOperationException("Thông tin game hoặc phiên đăng nhập đã đổi. Hãy mở lại game trong thư viện.");
					end_IL_02cd:;
				}
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				message = "Đã hủy FIX 54.";
				state = "warning";
			}
			catch (Exception ex2)
			{
				message = UserError(ex2, "Không thể chạy FIX 54. Hãy đóng game và thử lại.");
				DesktopDiagnosticLog.Write("Fix54", "Failed", $"LibraryId={detail.LibraryId}; ErrorType={ex2.GetType().Name}");
			}
		}
		finally
		{
			operationActive = false;
			fix54WorkActive = false;
			fix54TerminalFeedback = new StoreFix54Feedback(detail.LibraryId, documentId, state, message);
			if (!base.IsDisposed && !base.Disposing)
			{
				string text = fix54DocumentId;
				if (text != null)
				{
					DesktopLibraryDetailResponse desktopLibraryDetailResponse = currentLibraryDetail;
					if (desktopLibraryDetailResponse != null && currentSurface == StoreSurface.LibraryDetail)
					{
						StoreFix54Feedback storeFix54Feedback = fix54TerminalFeedback.ForDocument(desktopLibraryDetailResponse.LibraryId, text, ShouldSuppressFix54Feedback);
						await SetFix54StateAsync(storeFix54Feedback.LibraryId, storeFix54Feedback.DocumentId, isRunning: false, storeFix54Feedback.Message, storeFix54Feedback.State, null);
					}
				}
			}
		}
	}

	private Task SetFix54StateAsync(Guid libraryId, string documentId, bool isRunning, string message, string state = "loading", int? progressPercentage = null, bool isCancelling = false)
	{
		if (!base.IsDisposed && !base.Disposing && currentSurface == StoreSurface.LibraryDetail)
		{
			DesktopLibraryDetailResponse? desktopLibraryDetailResponse = currentLibraryDetail;
			if (desktopLibraryDetailResponse != null && !(desktopLibraryDetailResponse.LibraryId != libraryId) && !(fix54DocumentId != documentId))
			{
				string text = JsonSerializer.Serialize(new
				{
					documentId = documentId,
					libraryId = libraryId,
					isRunning = isRunning,
					message = message,
					state = (isRunning ? "loading" : state),
					progressPercentage = ((isRunning && !isCancelling) ? progressPercentage : ((int?)null)),
					isCancelling = (isRunning && isCancelling),
					revision = ++fix54UiRevision
				}, JsonOptions);
				return ExecuteScriptAsync("window.setFix54State?.(" + text + ");");
			}
		}
		return Task.CompletedTask;
	}

	private async Task<bool> HandleSteamConnectionUiAsync(WebHomeMessage message, string source)
	{
		if (!SteamConnectionUiService.IsCommand(message.Type))
		{
			return false;
		}
		if (base.IsDisposed || disposalCancellation.IsCancellationRequested || homeWebView.CoreWebView2 == null || source != "about:blank" || homeWebView.CoreWebView2.Source != "about:blank" || currentSurface == StoreSurface.Unknown)
		{
			return true;
		}
		bool acquired = false;
		try
		{
			acquired = await steamConnectionUiGate.WaitAsync(0, disposalCancellation.Token);
			if (!acquired)
			{
				return true;
			}
			string documentId = JsonSerializer.Deserialize<string>(await homeWebView.CoreWebView2.ExecuteScriptAsync("window.__nestgSteamConnection?.documentId ?? null"));
			if (!SteamConnectionUiService.IsTrustedAction(source, currentSurface != StoreSurface.Unknown, message.DocumentId, documentId))
			{
				return true;
			}
			if (!steamConnectionSubscribed)
			{
				steamConnectionSubscribed = true;
				steamConnectionCoordinator.StateChanged += HandleSteamConnectionStateChanged;
			}
			SteamConnectionUiState steamConnectionUiState;
			switch (message.Type)
			{
			case "setSteamConnectionAutoEnable":
			{
				bool? enabled = message.Enabled;
				if (enabled.HasValue)
				{
					bool valueOrDefault2 = enabled == true;
					steamConnectionUiState = steamConnectionCoordinator.SetAutoEnable(valueOrDefault2);
					break;
				}
				goto default;
			}
			case "openSteamConnectionClient":
			{
				string text = await Task.Run(() => steamConnectionCoordinator.Preferences.OpenSteam(disposalCancellation.Token), disposalCancellation.Token);
				if (text.StartsWith("Đã gửi yêu cầu mở Steam.", StringComparison.Ordinal))
				{
					text = "Đã gửi yêu cầu mở Steam.";
				}
				steamConnectionUiState = steamConnectionCoordinator.GetState(text);
				break;
			}
			case "refreshSteamConnection":
				steamConnectionCoordinator.RefreshAsync();
				steamConnectionUiState = steamConnectionCoordinator.GetState();
				break;
			case "setSteamConnectionEnabled":
			{
				bool? enabled = message.Enabled;
				if (enabled.HasValue)
				{
					bool valueOrDefault = enabled == true;
					steamConnectionCoordinator.SetEnabledAsync(valueOrDefault);
					steamConnectionUiState = steamConnectionCoordinator.GetState();
					break;
				}
				goto default;
			}
			default:
				steamConnectionCoordinator.InitializeShell();
				steamConnectionUiState = steamConnectionCoordinator.GetState();
				break;
			}
			string text2 = JsonSerializer.Serialize(new
			{
				DocumentId = documentId,
				Kind = steamConnectionUiState.Kind,
				Enabled = steamConnectionUiState.Enabled,
				CanToggle = steamConnectionUiState.CanToggle,
				AutoEnable = steamConnectionUiState.AutoEnable,
				Label = steamConnectionUiState.Label,
				Detail = steamConnectionUiState.Detail,
				Feedback = steamConnectionUiState.Feedback,
				Busy = steamConnectionUiState.Busy,
				Checks = steamConnectionUiState.Checks,
				Reasons = steamConnectionUiState.Reasons,
				CheckedAt = steamConnectionUiState.CheckedAt,
				OperationId = steamConnectionUiState.OperationId,
				Revision = steamConnectionUiState.Revision,
				DnsEnabled = steamConnectionUiState.DnsEnabled,
				DpiEnabled = steamConnectionUiState.DpiEnabled,
				WarpConnected = steamConnectionUiState.WarpConnected,
				IndependentVerified = steamConnectionUiState.IndependentVerified,
				CleanupPending = steamConnectionUiState.CleanupPending,
				ErrorCode = steamConnectionUiState.ErrorCode,
				ErrorStage = steamConnectionUiState.ErrorStage
			}, JsonOptions);
			await ExecuteScriptAsync("window.__nestgSteamConnection?.setState(" + text2 + ");");
		}
		catch (OperationCanceledException) when (disposalCancellation.IsCancellationRequested)
		{
		}
		catch
		{
		}
		finally
		{
			if (acquired)
			{
				steamConnectionUiGate.Release();
			}
		}
		return true;
	}

	private void HandleSteamConnectionStateChanged(object? sender, EventArgs e)
	{
		if (base.IsDisposed || base.Disposing || disposalCancellation.IsCancellationRequested || !base.IsHandleCreated)
		{
			return;
		}
		try
		{
			BeginInvoke(async delegate
			{
				if (ShouldStopStartup || !base.Visible || homeWebView.CoreWebView2 == null || homeWebView.CoreWebView2.Source != "about:blank" || currentSurface == StoreSurface.Unknown)
				{
					return;
				}
				try
				{
					string text = JsonSerializer.Deserialize<string>(await homeWebView.CoreWebView2.ExecuteScriptAsync("window.__nestgSteamConnection?.documentId ?? null"));
					if (!string.IsNullOrEmpty(text))
					{
						SteamConnectionUiState state = steamConnectionCoordinator.GetState();
						string text2 = JsonSerializer.Serialize(new
						{
							DocumentId = text,
							Kind = state.Kind,
							Enabled = state.Enabled,
							CanToggle = state.CanToggle,
							AutoEnable = state.AutoEnable,
							Label = state.Label,
							Detail = state.Detail,
							Feedback = state.Feedback,
							Busy = state.Busy,
							Checks = state.Checks,
							Reasons = state.Reasons,
							CheckedAt = state.CheckedAt,
							OperationId = state.OperationId,
							Revision = state.Revision,
							DnsEnabled = state.DnsEnabled,
							DpiEnabled = state.DpiEnabled,
							WarpConnected = state.WarpConnected,
							IndependentVerified = state.IndependentVerified,
							CleanupPending = state.CleanupPending,
							ErrorCode = state.ErrorCode,
							ErrorStage = state.ErrorStage
						}, JsonOptions);
						await ExecuteScriptAsync("window.__nestgSteamConnection?.setState(" + text2 + ");");
					}
				}
				catch
				{
				}
			});
		}
		catch (InvalidOperationException)
		{
		}
	}

	private void DetachSteamConnection()
	{
		if (steamConnectionSubscribed)
		{
			steamConnectionCoordinator.StateChanged -= HandleSteamConnectionStateChanged;
		}
		steamConnectionSubscribed = false;
	}

	private async Task StopSteamConnectionForCloseAsync()
	{
		try
		{
			await steamConnectionCoordinator.ShutdownAsync().WaitAsync(TimeSpan.FromSeconds(25.0));
		}
		catch
		{
		}
	}

	private async Task<bool> HandleSupportContactUiAsync(WebHomeMessage message, string source)
	{
		if (!StoreSupportContact.IsCommand(message.Type))
		{
			return false;
		}
		if (base.IsDisposed || disposalCancellation.IsCancellationRequested || homeWebView.CoreWebView2 == null || source != "about:blank" || homeWebView.CoreWebView2.Source != "about:blank" || currentSurface == StoreSurface.Unknown || string.IsNullOrEmpty(message.RequestId) || message.RequestId.Length > 100)
		{
			return true;
		}
		try
		{
			string text = JsonSerializer.Deserialize<string>(await homeWebView.CoreWebView2.ExecuteScriptAsync("window.__nestgSupportContact?.documentId ?? null"));
			if (!StoreSupportContact.IsTrustedAction(source, message.DocumentId, text) || base.IsDisposed || disposalCancellation.IsCancellationRequested || homeWebView.CoreWebView2.Source != "about:blank")
			{
				return true;
			}
			bool success = false;
			string message2;
			try
			{
				if (message.Type == "copySupportPhone")
				{
					Clipboard.SetText("0947709029");
					message2 = "Đã sao chép số điện thoại hỗ trợ.";
				}
				else
				{
					Process.Start(new ProcessStartInfo("https://zalo.me/0947709029")
					{
						UseShellExecute = true
					});
					message2 = "Đã gửi yêu cầu mở Zalo.";
				}
				success = true;
			}
			catch
			{
				message2 = ((message.Type == "copySupportPhone") ? "Chưa sao chép được. Bạn có thể chọn số điện thoại để sao chép." : "Không thể mở Zalo. Bạn có thể quét QR hoặc liên hệ qua số điện thoại.");
			}
			string text2 = JsonSerializer.Serialize(new
			{
				DocumentId = text,
				RequestId = message.RequestId,
				Success = success,
				Message = message2
			}, JsonOptions);
			await ExecuteScriptAsync("window.__nestgSupportContact?.completeAction(" + text2 + ");");
		}
		catch
		{
		}
		return true;
	}
}
