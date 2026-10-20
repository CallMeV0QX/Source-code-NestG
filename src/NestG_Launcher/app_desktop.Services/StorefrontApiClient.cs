using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using UI_Desktop.Store;

namespace app_desktop.Services;

internal sealed class StorefrontApiClient
{
	private sealed class CartCountResponse
	{
		public int CartItemCount { get; set; }
	}

	internal sealed class StorefrontCartMutationResponse
	{
		public bool Success { get; set; }

		public string Message { get; set; } = string.Empty;

		public int CartItemCount { get; set; }
	}

	internal sealed class StorefrontReviewMutationResponse
	{
		public bool Success { get; set; }

		public string Message { get; set; } = string.Empty;
	}

	private StorefrontHomeResponse? legacyDesktopCatalog;

	private const string GenericDenuvoDownloadError = "Không thể nạp game Denuvo lúc này. Vui lòng thử lại sau hoặc liên hệ admin.";

	private static readonly TimeSpan ApiRequestTimeout = TimeSpan.FromSeconds(30.0);

	private static readonly TimeSpan RequestTimeout = TimeSpan.FromMinutes(10.0);

	private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
	{
		PropertyNameCaseInsensitive = true
	};

	private readonly HttpClient httpClient;

	private readonly TimeSpan runtimeIdleTimeout = TimeSpan.FromSeconds(30.0);

	private readonly TimeSpan runtimeFileTimeout = TimeSpan.FromMinutes(10.0);

	private readonly TimeSpan denuvoIdleTimeout = TimeSpan.FromSeconds(30.0);

	private readonly TimeSpan denuvoFileTimeout = TimeSpan.FromMinutes(10.0);

	public async Task<StorefrontHomeResponse> GetDesktopHomeAsync(string? accessToken = null, CancellationToken cancellationToken = default(CancellationToken))
	{
		legacyDesktopCatalog = null;
		StorefrontHomeResponse payload;
		try
		{
			payload = JsonSerializer.Deserialize<StorefrontHomeResponse>(await GetStringAsync("api/desktop/storefront/home", accessToken, cancellationToken, isRuntimeBundle: false, allowMissingEndpoint: true), JsonOptions) ?? throw new InvalidOperationException("API cửa hàng không trả về dữ liệu hợp lệ.");
		}
		catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
		{
			StorefrontHomeResponse storefrontHomeResponse = (legacyDesktopCatalog = await GetHomeAsync(accessToken, cancellationToken));
			StorefrontHomeCatalogPageResponse storefrontHomeCatalogPageResponse = new StoreCatalogQuery(null, null, null, "default", null).PageLegacy(storefrontHomeResponse.Games, 1, 24);
			string[] array = (from item in storefrontHomeResponse.Categories.Select((StorefrontCategorySectionResponse item) => item.Name).Concat(storefrontHomeResponse.Games.SelectMany((StorefrontProductCardResponse item) => item.CategoryNames))
				where !string.IsNullOrWhiteSpace(item)
				select item).Distinct<string>(StringComparer.OrdinalIgnoreCase).ToArray();
			payload = new StorefrontHomeResponse
			{
				HeroBannerImageUrl = storefrontHomeResponse.HeroBannerImageUrl,
				Games = storefrontHomeCatalogPageResponse.Games,
				SpotlightGames = storefrontHomeResponse.SpotlightGames.Take(6).ToList(),
				SaleGames = storefrontHomeResponse.SaleGames.Take(10).ToList(),
				CatalogCategories = array,
				CatalogGenres = array,
				CatalogMaximumPrice = storefrontHomeResponse.Games.Select((StorefrontProductCardResponse item) => StoreGame.FromApi(item).SalePrice).DefaultIfEmpty().Max(),
				CatalogPage = 1,
				CatalogPageSize = 24,
				CatalogTotalItems = storefrontHomeCatalogPageResponse.TotalCount,
				CatalogHasMore = storefrontHomeCatalogPageResponse.HasMore,
				CatalogNextPage = storefrontHomeCatalogPageResponse.NextPage
			};
			DesktopDiagnosticLog.Write("Storefront", "LegacyCatalogCompatibility", $"Games={storefrontHomeCatalogPageResponse.TotalCount}");
		}
		NormalizeAssetUrls(payload.Games);
		NormalizeAssetUrls(payload.SpotlightGames);
		payload.HeroBannerImageUrl = NormalizeAssetUrl(payload.HeroBannerImageUrl) ?? payload.HeroBannerImageUrl;
		foreach (StorefrontSaleGameResponse saleGame in payload.SaleGames)
		{
			saleGame.PosterImageUrl = NormalizeAssetUrl(saleGame.PosterImageUrl) ?? saleGame.PosterImageUrl;
		}
		return payload;
	}

	public async Task<StorefrontHomeCatalogPageResponse> GetDesktopCatalogPageAsync(int page, StoreCatalogQuery query, CancellationToken cancellationToken = default(CancellationToken))
	{
		cancellationToken.ThrowIfCancellationRequested();
		query = query.Bounded();
		StorefrontHomeResponse storefrontHomeResponse = legacyDesktopCatalog;
		if (storefrontHomeResponse != null)
		{
			return query.PageLegacy(storefrontHomeResponse.Games, page, 24);
		}
		StorefrontHomeCatalogPageResponse storefrontHomeCatalogPageResponse = JsonSerializer.Deserialize<StorefrontHomeCatalogPageResponse>(await GetStringAsync($"api/desktop/storefront/catalog?page={Math.Clamp(page, 1, 1000000)}&pageSize=24&{query.ToQueryString()}", null, cancellationToken), JsonOptions) ?? throw new InvalidOperationException("API danh mục không trả về dữ liệu hợp lệ.");
		NormalizeAssetUrls(storefrontHomeCatalogPageResponse.Games);
		return storefrontHomeCatalogPageResponse;
	}

	internal StorefrontApiClient(HttpClient httpClient, TimeSpan? runtimeIdleTimeout = null, TimeSpan? runtimeFileTimeout = null, TimeSpan? denuvoIdleTimeout = null, TimeSpan? denuvoFileTimeout = null)
	{
		this.httpClient = httpClient;
		this.runtimeIdleTimeout = runtimeIdleTimeout ?? this.runtimeIdleTimeout;
		this.runtimeFileTimeout = runtimeFileTimeout ?? this.runtimeFileTimeout;
		this.denuvoIdleTimeout = denuvoIdleTimeout ?? this.denuvoIdleTimeout;
		this.denuvoFileTimeout = denuvoFileTimeout ?? this.denuvoFileTimeout;
	}

	public StorefrontApiClient(string apiBaseUrl)
	{
		httpClient = new HttpClient
		{
			BaseAddress = new Uri(NormalizeBaseUrl(apiBaseUrl)),
			Timeout = RequestTimeout
		};
		httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("NestGLauncherDesktop/1.0");
	}

	public async Task<StorefrontHomeResponse> GetHomeAsync(string? accessToken = null, CancellationToken cancellationToken = default(CancellationToken))
	{
		StorefrontHomeResponse storefrontHomeResponse = JsonSerializer.Deserialize<StorefrontHomeResponse>(await GetStringAsync("api/storefront/home", accessToken, cancellationToken), JsonOptions);
		if (storefrontHomeResponse == null)
		{
			throw new InvalidOperationException("API cửa hàng không trả về dữ liệu hợp lệ.");
		}
		NormalizeAssetUrls(storefrontHomeResponse.Games);
		NormalizeAssetUrls(storefrontHomeResponse.SpotlightGames);
		storefrontHomeResponse.HeroBannerImageUrl = NormalizeAssetUrl(storefrontHomeResponse.HeroBannerImageUrl) ?? storefrontHomeResponse.HeroBannerImageUrl;
		foreach (StorefrontSaleGameResponse saleGame in storefrontHomeResponse.SaleGames)
		{
			saleGame.PosterImageUrl = NormalizeAssetUrl(saleGame.PosterImageUrl) ?? saleGame.PosterImageUrl;
		}
		foreach (StorefrontCategorySectionResponse category in storefrontHomeResponse.Categories)
		{
			NormalizeAssetUrls(category.Games);
		}
		return storefrontHomeResponse;
	}

	public async Task<StorefrontHomeCatalogPageResponse> GetHomeCatalogPageAsync(int page, int pageSize, string? accessToken = null, CancellationToken cancellationToken = default(CancellationToken))
	{
		int value = Math.Max(1, page);
		int value2 = Math.Clamp((pageSize <= 0) ? 24 : pageSize, 1, 60);
		string endpoint = $"api/storefront/home/catalog?page={value}&pageSize={value2}";
		StorefrontHomeCatalogPageResponse storefrontHomeCatalogPageResponse = JsonSerializer.Deserialize<StorefrontHomeCatalogPageResponse>(await GetStringAsync(endpoint, accessToken, cancellationToken), JsonOptions);
		if (storefrontHomeCatalogPageResponse == null)
		{
			throw new InvalidOperationException("API danh mục không trả về dữ liệu hợp lệ.");
		}
		foreach (StorefrontCategorySectionResponse category in storefrontHomeCatalogPageResponse.Categories)
		{
			NormalizeAssetUrls(category.Games);
		}
		return storefrontHomeCatalogPageResponse;
	}

	public async Task<IReadOnlyDictionary<Guid, string>> GetSalePortraitImagesAsync(CancellationToken cancellationToken = default(CancellationToken))
	{
		DesktopSalePortraitResponse? obj = JsonSerializer.Deserialize<DesktopSalePortraitResponse>(await GetStringAsync("api/desktop/storefront/sale-portraits", null, cancellationToken), JsonOptions) ?? throw new InvalidOperationException("API ảnh sale desktop không trả về dữ liệu hợp lệ.");
		Dictionary<Guid, string> dictionary = new Dictionary<Guid, string>();
		foreach (DesktopSalePortraitItemResponse item in obj.Items)
		{
			if (!(item.GameId == Guid.Empty))
			{
				string value = NormalizeAssetUrl(item.PortraitImageUrl);
				if (!string.IsNullOrWhiteSpace(value))
				{
					dictionary[item.GameId] = value;
				}
			}
		}
		return dictionary;
	}

	public async Task<IReadOnlyList<DesktopHomeBannerSlotResponse>> GetDesktopHomeBannerSlotsAsync(CancellationToken cancellationToken = default(CancellationToken))
	{
		DesktopHomeBannerResponse desktopHomeBannerResponse = JsonSerializer.Deserialize<DesktopHomeBannerResponse>(await GetStringAsync("api/desktop/storefront/home-banners", null, cancellationToken), JsonOptions);
		if (desktopHomeBannerResponse == null)
		{
			throw new InvalidOperationException("API banner desktop không trả về dữ liệu hợp lệ.");
		}
		foreach (DesktopHomeBannerSlotResponse item in desktopHomeBannerResponse.Items)
		{
			item.ImageUrl = NormalizeAssetUrl(item.ImageUrl) ?? item.ImageUrl;
			item.TargetUrl = ResolveAbsoluteUrl(item.TargetUrl);
		}
		return (from item in desktopHomeBannerResponse.Items
			where !string.IsNullOrWhiteSpace(item.ImageUrl)
			orderby item.DisplayOrder
			select item).ToArray();
	}

	public async Task<IReadOnlyList<StoreComboCardResponse>> GetComboCardsAsync(CancellationToken cancellationToken = default(CancellationToken))
	{
		List<StoreComboCardResponse> list = JsonSerializer.Deserialize<List<StoreComboCardResponse>>(await GetStringAsync("api/desktop/storefront/combos?take=100", null, cancellationToken), JsonOptions) ?? throw new InvalidOperationException("API combo không trả về danh sách hợp lệ.");
		foreach (StoreComboCardResponse item in list)
		{
			item.PosterUrl = NormalizeAssetUrl(item.PosterUrl) ?? item.PosterUrl;
		}
		return list;
	}

	public async Task<IReadOnlyList<StorefrontProductCardResponse>> GetSpotlightAsync(string? accessToken = null, CancellationToken cancellationToken = default(CancellationToken))
	{
		List<StorefrontProductCardResponse> list = JsonSerializer.Deserialize<List<StorefrontProductCardResponse>>(await GetStringAsync("api/storefront/spotlight", accessToken, cancellationToken), JsonOptions);
		if (list == null)
		{
			throw new InvalidOperationException("API spotlight không trả về dữ liệu hợp lệ.");
		}
		NormalizeAssetUrls(list);
		return list;
	}

	public async Task<int> GetCartItemCountAsync(string? accessToken, CancellationToken cancellationToken = default(CancellationToken))
	{
		if (string.IsNullOrWhiteSpace(accessToken))
		{
			return 0;
		}
		CartCountResponse cartCountResponse = JsonSerializer.Deserialize<CartCountResponse>(await GetStringAsync("api/storefront/cart/count", accessToken, cancellationToken), JsonOptions);
		if (cartCountResponse == null)
		{
			throw new InvalidOperationException("API giỏ hàng không trả về dữ liệu hợp lệ.");
		}
		return Math.Max(0, cartCountResponse.CartItemCount);
	}

	public async Task<DesktopCartResponse> GetCartAsync(string accessToken, string? promoCode = null, CancellationToken cancellationToken = default(CancellationToken))
	{
		if (string.IsNullOrWhiteSpace(accessToken))
		{
			throw new InvalidOperationException("Bạn cần đăng nhập để xem giỏ hàng.");
		}
		string text = "api/storefront/cart";
		if (!string.IsNullOrWhiteSpace(promoCode))
		{
			text = text + "?promo=" + Uri.EscapeDataString(promoCode.Trim());
		}
		DesktopCartResponse desktopCartResponse = JsonSerializer.Deserialize<DesktopCartResponse>(await GetStringAsync(text, accessToken, cancellationToken), JsonOptions);
		if (desktopCartResponse == null)
		{
			throw new InvalidOperationException("API giỏ hàng không trả về dữ liệu hợp lệ.");
		}
		foreach (DesktopCartLineItemResponse item in desktopCartResponse.Items)
		{
			item.PosterImageUrl = NormalizeAssetUrl(item.PosterImageUrl) ?? item.PosterImageUrl;
		}
		foreach (DesktopComboCartLineItemResponse comboItem in desktopCartResponse.ComboItems)
		{
			comboItem.PosterImageUrl = NormalizeAssetUrl(comboItem.PosterImageUrl) ?? comboItem.PosterImageUrl;
		}
		return desktopCartResponse;
	}

	public async Task<DesktopCheckoutPaymentResponse> CreateCheckoutAsync(string? promoCode, decimal? expectedTotalAmount, string? accessToken, string paymentMethod = "sepay", CancellationToken cancellationToken = default(CancellationToken))
	{
		if (string.IsNullOrWhiteSpace(accessToken))
		{
			throw new InvalidOperationException("Bạn cần đăng nhập để thanh toán.");
		}
		using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, "api/payments/checkout")
		{
			Content = JsonContent.Create(new { promoCode, expectedTotalAmount, paymentMethod }, null, JsonOptions)
		};
		request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
		using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken);
		string text = await response.Content.ReadAsStringAsync(cancellationToken);
		if (!response.IsSuccessStatusCode)
		{
			throw new InvalidOperationException(ParseErrorMessage(text, response.ReasonPhrase));
		}
		DesktopCheckoutPaymentResponse desktopCheckoutPaymentResponse = JsonSerializer.Deserialize<DesktopCheckoutPaymentResponse>(text, JsonOptions);
		if (desktopCheckoutPaymentResponse == null)
		{
			throw new InvalidOperationException("API thanh toán không trả về dữ liệu hợp lệ.");
		}
		return NormalizeCheckoutResponse(desktopCheckoutPaymentResponse);
	}

	public async Task<DesktopCheckoutPaymentResponse> CreateSingleItemCheckoutAsync(Guid gameId, Guid? versionId, decimal? expectedUnitPrice, string? promoCode, string? accessToken, string paymentMethod = "sepay", CancellationToken cancellationToken = default(CancellationToken))
	{
		if (string.IsNullOrWhiteSpace(accessToken))
		{
			throw new InvalidOperationException("Bạn cần đăng nhập để thanh toán.");
		}
		using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, "api/payments/checkout/item")
		{
			Content = JsonContent.Create(new
			{
				gameId = gameId,
				versionId = versionId,
				expectedUnitPrice = expectedUnitPrice,
				quantity = 1,
				promoCode = promoCode,
				paymentMethod = paymentMethod
			}, null, JsonOptions)
		};
		request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
		using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken);
		string text = await response.Content.ReadAsStringAsync(cancellationToken);
		if (!response.IsSuccessStatusCode)
		{
			throw new InvalidOperationException(ParseErrorMessage(text, response.ReasonPhrase));
		}
		DesktopCheckoutPaymentResponse desktopCheckoutPaymentResponse = JsonSerializer.Deserialize<DesktopCheckoutPaymentResponse>(text, JsonOptions);
		if (desktopCheckoutPaymentResponse == null)
		{
			throw new InvalidOperationException("API mua nhanh không trả về dữ liệu hợp lệ.");
		}
		return NormalizeCheckoutResponse(desktopCheckoutPaymentResponse);
	}

	public async Task<DesktopCheckoutPaymentResponse> GetCheckoutAsync(string orderCode, string? accessToken, CancellationToken cancellationToken = default(CancellationToken))
	{
		if (string.IsNullOrWhiteSpace(accessToken))
		{
			throw new InvalidOperationException("Bạn cần đăng nhập để xem thanh toán.");
		}
		DesktopCheckoutPaymentResponse desktopCheckoutPaymentResponse = JsonSerializer.Deserialize<DesktopCheckoutPaymentResponse>(await GetStringAsync("api/payments/" + Uri.EscapeDataString(orderCode), accessToken, cancellationToken), JsonOptions);
		if (desktopCheckoutPaymentResponse == null)
		{
			throw new InvalidOperationException("API thanh toán không trả về dữ liệu hợp lệ.");
		}
		return NormalizeCheckoutResponse(desktopCheckoutPaymentResponse);
	}

	public async Task<DesktopCheckoutPaymentResponse> PayCheckoutWithWalletAsync(string orderCode, string? accessToken, CancellationToken cancellationToken = default(CancellationToken))
	{
		if (string.IsNullOrWhiteSpace(accessToken))
		{
			throw new InvalidOperationException("Bạn cần đăng nhập để thanh toán bằng ví.");
		}
		if (string.IsNullOrWhiteSpace(orderCode))
		{
			throw new InvalidOperationException("Không tìm thấy mã đơn hàng để thanh toán bằng ví.");
		}
		using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, "api/payments/" + Uri.EscapeDataString(orderCode.Trim()) + "/wallet");
		request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
		using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken);
		string text = await response.Content.ReadAsStringAsync(cancellationToken);
		if (!response.IsSuccessStatusCode)
		{
			throw new InvalidOperationException(ParseErrorMessage(text, response.ReasonPhrase));
		}
		DesktopCheckoutPaymentResponse desktopCheckoutPaymentResponse = JsonSerializer.Deserialize<DesktopCheckoutPaymentResponse>(text, JsonOptions);
		if (desktopCheckoutPaymentResponse == null)
		{
			throw new InvalidOperationException("API thanh toán ví không trả về dữ liệu hợp lệ.");
		}
		return NormalizeCheckoutResponse(desktopCheckoutPaymentResponse);
	}

	public async Task<DesktopCheckoutPaymentResponse> CreateWalletTopUpAsync(decimal amount, string? accessToken, CancellationToken cancellationToken = default(CancellationToken))
	{
		if (string.IsNullOrWhiteSpace(accessToken))
		{
			throw new InvalidOperationException("Bạn cần đăng nhập để nạp tiền vào ví.");
		}
		using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, "api/account/wallet/topup")
		{
			Content = JsonContent.Create(new { amount }, null, JsonOptions)
		};
		request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
		using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken);
		string text = await response.Content.ReadAsStringAsync(cancellationToken);
		if (!response.IsSuccessStatusCode)
		{
			throw new InvalidOperationException(ParseErrorMessage(text, response.ReasonPhrase));
		}
		DesktopCheckoutPaymentResponse desktopCheckoutPaymentResponse = JsonSerializer.Deserialize<DesktopCheckoutPaymentResponse>(text, JsonOptions);
		if (desktopCheckoutPaymentResponse == null)
		{
			throw new InvalidOperationException("API nạp tiền ví không trả về dữ liệu hợp lệ.");
		}
		return NormalizeCheckoutResponse(desktopCheckoutPaymentResponse);
	}

	public async Task<DesktopCheckoutStatusResponse> GetCheckoutStatusAsync(string orderCode, string? accessToken, CancellationToken cancellationToken = default(CancellationToken))
	{
		if (string.IsNullOrWhiteSpace(accessToken))
		{
			throw new InvalidOperationException("Bạn cần đăng nhập để xem trạng thái thanh toán.");
		}
		return JsonSerializer.Deserialize<DesktopCheckoutStatusResponse>(await GetStringAsync("api/payments/" + Uri.EscapeDataString(orderCode) + "/status", accessToken, cancellationToken), JsonOptions) ?? throw new InvalidOperationException("API thanh toán không trả về trạng thái hợp lệ.");
	}

	public async Task<DesktopEventPageResponse> GetEventsAsync(string accessToken, CancellationToken cancellationToken = default(CancellationToken))
	{
		if (string.IsNullOrWhiteSpace(accessToken))
		{
			throw new InvalidOperationException("Bạn cần đăng nhập để xem sự kiện.");
		}
		DesktopEventPageResponse desktopEventPageResponse = JsonSerializer.Deserialize<DesktopEventPageResponse>(await GetStringAsync("api/desktop/events", accessToken, cancellationToken), JsonOptions);
		if (desktopEventPageResponse == null)
		{
			throw new InvalidOperationException("API sự kiện không trả về dữ liệu hợp lệ.");
		}
		if (desktopEventPageResponse.RandomSteamGame != null)
		{
			desktopEventPageResponse.RandomSteamGame.HeroArtworkPath = NormalizeEventArtworkUrl(desktopEventPageResponse.RandomSteamGame.HeroArtworkPath) ?? desktopEventPageResponse.RandomSteamGame.HeroArtworkPath;
			foreach (DesktopChestTierResponse tier in desktopEventPageResponse.RandomSteamGame.Tiers)
			{
				tier.ArtworkPath = NormalizeEventArtworkUrl(tier.ArtworkPath) ?? tier.ArtworkPath;
			}
		}
		foreach (DesktopChestHistoryItemResponse item in desktopEventPageResponse.ChestHistory)
		{
			item.Reward.ImageUrl = NormalizeAssetUrl(item.Reward.ImageUrl) ?? item.Reward.ImageUrl;
		}
		if (desktopEventPageResponse.NightMarket != null)
		{
			foreach (DesktopNightMarketSlotResponse slot in desktopEventPageResponse.NightMarket.Slots)
			{
				if (slot.Offer != null)
				{
					slot.Offer.ImageUrl = NormalizeAssetUrl(slot.Offer.ImageUrl) ?? slot.Offer.ImageUrl;
				}
			}
		}
		return desktopEventPageResponse;
	}

	public Task<DesktopEventCheckoutResponse> CreateChestEventCheckoutAsync(string tierCode, string paymentMethod, string idempotencyKey, string accessToken, CancellationToken cancellationToken = default(CancellationToken))
	{
		return PostEventCheckoutAsync("api/desktop/events/chests/" + Uri.EscapeDataString(tierCode) + "/checkout", new { paymentMethod, idempotencyKey }, accessToken, cancellationToken);
	}

	public async Task<NightMarketOpeningCreateResponse> OpenNightMarketSlotAsync(int slotNumber, string idempotencyKey, string accessToken, CancellationToken cancellationToken = default(CancellationToken))
	{
		if (string.IsNullOrWhiteSpace(accessToken))
		{
			throw new InvalidOperationException("Bạn cần đăng nhập để mở thẻ Night Market.");
		}
		using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, $"api/desktop/events/night-market/slots/{slotNumber}/open")
		{
			Content = JsonContent.Create(new { idempotencyKey }, null, JsonOptions)
		};
		request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
		using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken);
		string text = await response.Content.ReadAsStringAsync(cancellationToken);
		if (!response.IsSuccessStatusCode)
		{
			throw new InvalidOperationException(ParseErrorMessage(text, response.ReasonPhrase));
		}
		NightMarketOpeningCreateResponse nightMarketOpeningCreateResponse = JsonSerializer.Deserialize<NightMarketOpeningCreateResponse>(text, JsonOptions);
		if (nightMarketOpeningCreateResponse == null || nightMarketOpeningCreateResponse.OpeningId == Guid.Empty || !nightMarketOpeningCreateResponse.RewardReady)
		{
			throw new InvalidOperationException("API chưa tạo được ưu đãi Night Market.");
		}
		return nightMarketOpeningCreateResponse;
	}

	public Task<DesktopEventCheckoutResponse> CreateNightMarketPurchaseCheckoutAsync(Guid openingId, string paymentMethod, string idempotencyKey, string accessToken, CancellationToken cancellationToken = default(CancellationToken))
	{
		return PostEventCheckoutAsync($"api/desktop/events/night-market/openings/{openingId:D}/purchase-checkout", new { paymentMethod, idempotencyKey }, accessToken, cancellationToken);
	}

	public async Task<DesktopChestOpeningResponse> GetChestEventOpeningAsync(Guid openingId, bool markRevealed, string accessToken, CancellationToken cancellationToken = default(CancellationToken))
	{
		DesktopChestOpeningResponse desktopChestOpeningResponse = JsonSerializer.Deserialize<DesktopChestOpeningResponse>(await GetStringAsync($"api/desktop/events/chest-openings/{openingId:D}?markRevealed={markRevealed.ToString().ToLowerInvariant()}", accessToken, cancellationToken), JsonOptions);
		if (desktopChestOpeningResponse == null)
		{
			throw new InvalidOperationException("API sự kiện không trả về phần thưởng hợp lệ.");
		}
		if (desktopChestOpeningResponse.Reward != null)
		{
			desktopChestOpeningResponse.Reward.ImageUrl = NormalizeAssetUrl(desktopChestOpeningResponse.Reward.ImageUrl) ?? desktopChestOpeningResponse.Reward.ImageUrl;
		}
		return desktopChestOpeningResponse;
	}

	public async Task<DesktopChestOpeningResponse> ConfirmChestEventOpeningAsync(Guid openingId, string accessToken, CancellationToken cancellationToken = default(CancellationToken))
	{
		using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, $"api/desktop/events/chest-openings/{openingId:D}/confirm");
		request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
		using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken);
		string text = await response.Content.ReadAsStringAsync(cancellationToken);
		if (!response.IsSuccessStatusCode)
		{
			throw new InvalidOperationException(ParseErrorMessage(text, response.ReasonPhrase));
		}
		DesktopChestOpeningResponse desktopChestOpeningResponse = JsonSerializer.Deserialize<DesktopChestOpeningResponse>(text, JsonOptions);
		if (desktopChestOpeningResponse == null)
		{
			throw new InvalidOperationException("API sự kiện không xác nhận được phần thưởng.");
		}
		if (desktopChestOpeningResponse.Reward != null)
		{
			desktopChestOpeningResponse.Reward.ImageUrl = NormalizeAssetUrl(desktopChestOpeningResponse.Reward.ImageUrl) ?? desktopChestOpeningResponse.Reward.ImageUrl;
		}
		return desktopChestOpeningResponse;
	}

	public async Task MarkNightMarketOpeningRevealedAsync(Guid openingId, string accessToken, CancellationToken cancellationToken = default(CancellationToken))
	{
		using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, $"api/desktop/events/night-market/openings/{openingId:D}/revealed");
		request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
		using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken);
		string rawContent = await response.Content.ReadAsStringAsync(cancellationToken);
		if (!response.IsSuccessStatusCode)
		{
			throw new InvalidOperationException(ParseErrorMessage(rawContent, response.ReasonPhrase));
		}
	}

	public async Task<DesktopLibraryListResponse> GetLibraryAsync(string accessToken, CancellationToken cancellationToken = default(CancellationToken))
	{
		DesktopLibraryListResponse desktopLibraryListResponse = JsonSerializer.Deserialize<DesktopLibraryListResponse>(await GetStringAsync("api/library", accessToken, cancellationToken), JsonOptions);
		if (desktopLibraryListResponse == null)
		{
			throw new InvalidOperationException("API thư viện không trả về dữ liệu hợp lệ.");
		}
		foreach (DesktopLibraryListItemResponse item in desktopLibraryListResponse.Items)
		{
			item.PosterImageUrl = NormalizeAssetUrl(item.PosterImageUrl);
			item.SpotlightImageUrl = NormalizeAssetUrl(item.SpotlightImageUrl);
		}
		return desktopLibraryListResponse;
	}

	public async Task<DesktopLibraryDetailResponse> GetLibraryDetailAsync(Guid libraryId, string accessToken, CancellationToken cancellationToken = default(CancellationToken))
	{
		DesktopLibraryDetailResponse desktopLibraryDetailResponse = JsonSerializer.Deserialize<DesktopLibraryDetailResponse>(await GetStringAsync($"api/library/{libraryId:D}", accessToken, cancellationToken), JsonOptions);
		if (desktopLibraryDetailResponse == null)
		{
			throw new InvalidOperationException("API chi tiết thư viện không trả về dữ liệu hợp lệ.");
		}
		desktopLibraryDetailResponse.CoverImageUrl = NormalizeAssetUrl(desktopLibraryDetailResponse.CoverImageUrl) ?? string.Empty;
		foreach (DesktopLibraryGalleryItemResponse item in desktopLibraryDetailResponse.Gallery)
		{
			item.ThumbnailUrl = NormalizeAssetUrl(item.ThumbnailUrl) ?? item.ThumbnailUrl;
			item.FullUrl = NormalizeAssetUrl(item.FullUrl) ?? item.FullUrl;
		}
		return desktopLibraryDetailResponse;
	}

	private async Task<DesktopEventCheckoutResponse> PostEventCheckoutAsync(string endpoint, object body, string accessToken, CancellationToken cancellationToken)
	{
		if (string.IsNullOrWhiteSpace(accessToken))
		{
			throw new InvalidOperationException("Bạn cần đăng nhập để thanh toán sự kiện.");
		}
		using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, endpoint)
		{
			Content = JsonContent.Create(body, null, JsonOptions)
		};
		request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
		using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken);
		string text = await response.Content.ReadAsStringAsync(cancellationToken);
		if (!response.IsSuccessStatusCode)
		{
			throw new InvalidOperationException(ParseErrorMessage(text, response.ReasonPhrase));
		}
		DesktopEventCheckoutResponse desktopEventCheckoutResponse = JsonSerializer.Deserialize<DesktopEventCheckoutResponse>(text, JsonOptions);
		if (desktopEventCheckoutResponse == null)
		{
			throw new InvalidOperationException("API sự kiện không trả về giao dịch hợp lệ.");
		}
		desktopEventCheckoutResponse.Checkout = NormalizeCheckoutResponse(desktopEventCheckoutResponse.Checkout);
		return desktopEventCheckoutResponse;
	}

	public async Task<DesktopLibraryDenuvoFixResponse> GetLibraryDenuvoFixAsync(Guid libraryId, string accessToken, CancellationToken cancellationToken = default(CancellationToken))
	{
		DesktopLibraryDenuvoFixResponse desktopLibraryDenuvoFixResponse = JsonSerializer.Deserialize<DesktopLibraryDenuvoFixResponse>(await GetStringAsync($"api/library/{libraryId:D}/denuvo-fix", accessToken, cancellationToken), JsonOptions);
		if (desktopLibraryDenuvoFixResponse == null)
		{
			throw new InvalidOperationException("API Denuvo Fix không trả về dữ liệu hợp lệ.");
		}
		return NormalizeDenuvoFixResponse(desktopLibraryDenuvoFixResponse);
	}

	public async Task<DesktopLibraryDenuvoFixResponse> EnsureLibraryDenuvoFixAsync(Guid libraryId, string accessToken, CancellationToken cancellationToken = default(CancellationToken))
	{
		if (string.IsNullOrWhiteSpace(accessToken))
		{
			throw new InvalidOperationException("Bạn cần đăng nhập để cấp Denuvo Fix.");
		}
		using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, $"api/library/{libraryId:D}/denuvo-fix/ensure");
		request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
		using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken);
		string text = await response.Content.ReadAsStringAsync(cancellationToken);
		if (!response.IsSuccessStatusCode)
		{
			throw new InvalidOperationException(ParseErrorMessage(text, response.ReasonPhrase));
		}
		DesktopLibraryDenuvoFixResponse desktopLibraryDenuvoFixResponse = JsonSerializer.Deserialize<DesktopLibraryDenuvoFixResponse>(text, JsonOptions);
		if (desktopLibraryDenuvoFixResponse == null)
		{
			throw new InvalidOperationException("API cấp Denuvo Fix không trả về dữ liệu hợp lệ.");
		}
		return NormalizeDenuvoFixResponse(desktopLibraryDenuvoFixResponse);
	}

	public async Task<DesktopLibraryPypassFixResponse> GetLibraryPypassFixAsync(Guid libraryId, string accessToken, CancellationToken cancellationToken = default(CancellationToken))
	{
		DesktopLibraryPypassFixResponse desktopLibraryPypassFixResponse = JsonSerializer.Deserialize<DesktopLibraryPypassFixResponse>(await GetStringAsync($"api/library/{libraryId:D}/pypass-fix", accessToken, cancellationToken), JsonOptions);
		if (desktopLibraryPypassFixResponse == null)
		{
			throw new InvalidOperationException("API Pypass Fix không trả về dữ liệu hợp lệ.");
		}
		return NormalizePypassFixResponse(desktopLibraryPypassFixResponse);
	}

	public async Task<DesktopLibraryPypassFixResponse> EnsureLibraryPypassFixAsync(Guid libraryId, string accessToken, CancellationToken cancellationToken = default(CancellationToken))
	{
		if (string.IsNullOrWhiteSpace(accessToken))
		{
			throw new InvalidOperationException("Bạn cần đăng nhập để cấp Pypass Fix.");
		}
		using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, $"api/library/{libraryId:D}/pypass-fix/ensure");
		request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
		using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken);
		string text = await response.Content.ReadAsStringAsync(cancellationToken);
		if (!response.IsSuccessStatusCode)
		{
			throw new InvalidOperationException(ParseErrorMessage(text, response.ReasonPhrase));
		}
		DesktopLibraryPypassFixResponse desktopLibraryPypassFixResponse = JsonSerializer.Deserialize<DesktopLibraryPypassFixResponse>(text, JsonOptions);
		if (desktopLibraryPypassFixResponse == null)
		{
			throw new InvalidOperationException("API cấp Pypass Fix không trả về dữ liệu hợp lệ.");
		}
		return NormalizePypassFixResponse(desktopLibraryPypassFixResponse);
	}

	public async Task<DesktopLibraryPypassFixDeliveryResponse?> TryGetLibraryPypassFixDeliveryAsync(Guid libraryId, string accessToken, CancellationToken cancellationToken = default(CancellationToken))
	{
		if (string.IsNullOrWhiteSpace(accessToken))
		{
			throw new InvalidOperationException("Bạn cần đăng nhập để tải Pypass Fix.");
		}
		using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, $"api/library/{libraryId:D}/pypass-fix/delivery");
		request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
		using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken);
		string text = await response.Content.ReadAsStringAsync(cancellationToken);
		if (response.StatusCode == HttpStatusCode.NotFound)
		{
			return null;
		}
		if (!response.IsSuccessStatusCode)
		{
			throw new InvalidOperationException(ParseErrorMessage(text, response.ReasonPhrase));
		}
		DesktopLibraryPypassFixDeliveryResponse desktopLibraryPypassFixDeliveryResponse = JsonSerializer.Deserialize<DesktopLibraryPypassFixDeliveryResponse>(text, JsonOptions);
		if (desktopLibraryPypassFixDeliveryResponse == null || string.IsNullOrWhiteSpace(desktopLibraryPypassFixDeliveryResponse.DownloadUrl))
		{
			throw new InvalidOperationException("API Pypass Fix không trả về nguồn tải hợp lệ.");
		}
		desktopLibraryPypassFixDeliveryResponse.DownloadUrl = ResolveAbsoluteUrl(desktopLibraryPypassFixDeliveryResponse.DownloadUrl) ?? desktopLibraryPypassFixDeliveryResponse.DownloadUrl;
		return desktopLibraryPypassFixDeliveryResponse;
	}

	public async Task<DesktopLibraryOnlineFixResponse> GetLibraryOnlineFixAsync(Guid libraryId, string accessToken, CancellationToken cancellationToken = default(CancellationToken))
	{
		DesktopLibraryOnlineFixResponse desktopLibraryOnlineFixResponse = JsonSerializer.Deserialize<DesktopLibraryOnlineFixResponse>(await GetStringAsync($"api/library/{libraryId:D}/online-fix", accessToken, cancellationToken), JsonOptions);
		if (desktopLibraryOnlineFixResponse == null)
		{
			throw new InvalidOperationException("API Online Fix không trả về dữ liệu hợp lệ.");
		}
		return NormalizeOnlineFixResponse(desktopLibraryOnlineFixResponse);
	}

	public async Task<DesktopLibraryOnlineFixResponse> EnsureLibraryOnlineFixAsync(Guid libraryId, string accessToken, CancellationToken cancellationToken = default(CancellationToken))
	{
		if (string.IsNullOrWhiteSpace(accessToken))
		{
			throw new InvalidOperationException("Bạn cần đăng nhập để cấp Online Fix.");
		}
		using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, $"api/library/{libraryId:D}/online-fix/ensure");
		request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
		using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken);
		string text = await response.Content.ReadAsStringAsync(cancellationToken);
		if (!response.IsSuccessStatusCode)
		{
			throw new InvalidOperationException(ParseErrorMessage(text, response.ReasonPhrase));
		}
		DesktopLibraryOnlineFixResponse desktopLibraryOnlineFixResponse = JsonSerializer.Deserialize<DesktopLibraryOnlineFixResponse>(text, JsonOptions);
		if (desktopLibraryOnlineFixResponse == null)
		{
			throw new InvalidOperationException("API cấp Online Fix không trả về dữ liệu hợp lệ.");
		}
		return NormalizeOnlineFixResponse(desktopLibraryOnlineFixResponse);
	}

	public async Task<DesktopDenuvoGuideResponse> GetDenuvoGuideAsync(CancellationToken cancellationToken = default(CancellationToken))
	{
		DesktopDenuvoGuideResponse desktopDenuvoGuideResponse = JsonSerializer.Deserialize<DesktopDenuvoGuideResponse>(await GetStringAsync("api/denuvo-guide", null, cancellationToken), JsonOptions);
		if (desktopDenuvoGuideResponse == null)
		{
			throw new InvalidOperationException("API hướng dẫn Denuvo không trả về dữ liệu hợp lệ.");
		}
		foreach (DesktopDenuvoGuideImageResponse image in desktopDenuvoGuideResponse.Images)
		{
			image.Url = ResolveAbsoluteUrl(image.Url) ?? image.Url;
		}
		return desktopDenuvoGuideResponse;
	}

	public async Task<DesktopDllFileListResponse> GetDllFilesAsync(string accessToken, CancellationToken cancellationToken = default(CancellationToken))
	{
		DesktopDllFileListResponse desktopDllFileListResponse = JsonSerializer.Deserialize<DesktopDllFileListResponse>(await GetStringAsync("api/dll-files", accessToken, cancellationToken), JsonOptions);
		if (desktopDllFileListResponse == null)
		{
			throw new InvalidOperationException("API DLL không trả về dữ liệu hợp lệ.");
		}
		foreach (DesktopDllFileItemResponse item in desktopDllFileListResponse.Items)
		{
			item.DownloadUrl = ResolveAbsoluteUrl(item.DownloadUrl) ?? item.DownloadUrl;
		}
		return desktopDllFileListResponse;
	}

	public Task<DesktopDownloadedFileResponse> DownloadLibraryActivationPackageAsync(Guid libraryId, string accessToken, string destinationDirectory, string fallbackFileName, CancellationToken cancellationToken = default(CancellationToken))
	{
		return DownloadFileAsync($"api/library/{libraryId:D}/activation-package", accessToken, destinationDirectory, fallbackFileName, cancellationToken);
	}

	public Task<DesktopDownloadedFileResponse> DownloadLibraryDenuvoFixPackageAsync(Guid libraryId, string accessToken, string destinationDirectory, string fallbackFileName, CancellationToken cancellationToken = default(CancellationToken))
	{
		return DownloadDenuvoFileAsync($"api/library/{libraryId:D}/denuvo-fix/package", accessToken, destinationDirectory, fallbackFileName, cancellationToken);
	}

	public Task<DesktopDownloadedFileResponse> DownloadLibraryDenuvoFixConfigAsync(Guid libraryId, string accessToken, string destinationDirectory, string fallbackFileName, CancellationToken cancellationToken = default(CancellationToken))
	{
		return DownloadDenuvoFileAsync($"api/library/{libraryId:D}/denuvo-fix/config", accessToken, destinationDirectory, fallbackFileName, cancellationToken);
	}

	public Task<DesktopDownloadedFileResponse> DownloadLibraryPypassFixPackageAsync(Guid libraryId, string accessToken, string destinationDirectory, string fallbackFileName, CancellationToken cancellationToken = default(CancellationToken), IProgress<DesktopFileDownloadProgress>? progress = null)
	{
		return DownloadFileAsync($"api/library/{libraryId:D}/pypass-fix/package", accessToken, destinationDirectory, fallbackFileName, cancellationToken, progress);
	}

	public Task<DesktopDownloadedFileResponse> DownloadLibraryOnlineFixPackageAsync(Guid libraryId, string accessToken, string destinationDirectory, string fallbackFileName, CancellationToken cancellationToken = default(CancellationToken))
	{
		return DownloadFileAsync($"api/library/{libraryId:D}/online-fix/package", accessToken, destinationDirectory, fallbackFileName, cancellationToken);
	}

	public async Task PostDenuvoFixEventAsync(Guid libraryId, DesktopDenuvoFixEventRequest eventRequest, string accessToken, CancellationToken cancellationToken = default(CancellationToken))
	{
		if (string.IsNullOrWhiteSpace(accessToken))
		{
			throw new InvalidOperationException("Bạn cần đăng nhập để đồng bộ trạng thái Denuvo Fix.");
		}
		using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, $"api/library/{libraryId:D}/denuvo-fix/events")
		{
			Content = JsonContent.Create(eventRequest, null, JsonOptions)
		};
		request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
		using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken);
		string rawContent = await response.Content.ReadAsStringAsync(cancellationToken);
		if (!response.IsSuccessStatusCode)
		{
			throw new InvalidOperationException(ParseErrorMessage(rawContent, response.ReasonPhrase));
		}
	}

	public async Task PostPypassFixEventAsync(Guid libraryId, DesktopPypassFixEventRequest eventRequest, string accessToken, CancellationToken cancellationToken = default(CancellationToken))
	{
		if (string.IsNullOrWhiteSpace(accessToken))
		{
			throw new InvalidOperationException("Bạn cần đăng nhập để đồng bộ trạng thái Pypass Fix.");
		}
		using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, $"api/library/{libraryId:D}/pypass-fix/events")
		{
			Content = JsonContent.Create(eventRequest, null, JsonOptions)
		};
		request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
		using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken);
		string rawContent = await response.Content.ReadAsStringAsync(cancellationToken);
		if (!response.IsSuccessStatusCode)
		{
			throw new InvalidOperationException(ParseErrorMessage(rawContent, response.ReasonPhrase));
		}
	}

	public async Task PostOnlineFixEventAsync(Guid libraryId, DesktopOnlineFixEventRequest eventRequest, string accessToken, CancellationToken cancellationToken = default(CancellationToken))
	{
		if (string.IsNullOrWhiteSpace(accessToken))
		{
			throw new InvalidOperationException("Bạn cần đăng nhập để đồng bộ trạng thái Online Fix.");
		}
		using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, $"api/library/{libraryId:D}/online-fix/events")
		{
			Content = JsonContent.Create(eventRequest, null, JsonOptions)
		};
		request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
		using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken);
		string rawContent = await response.Content.ReadAsStringAsync(cancellationToken);
		if (!response.IsSuccessStatusCode)
		{
			throw new InvalidOperationException(ParseErrorMessage(rawContent, response.ReasonPhrase));
		}
	}

	public async Task<DesktopDenuvoFixUpdateCheckResponse> CheckDenuvoFixUpdatesAsync(IEnumerable<DesktopDenuvoFixLocalStateRequest> localStates, string accessToken, CancellationToken cancellationToken = default(CancellationToken))
	{
		if (string.IsNullOrWhiteSpace(accessToken))
		{
			throw new InvalidOperationException("Bạn cần đăng nhập để kiểm tra update Denuvo Fix.");
		}
		using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, "api/library/denuvo-fixes/check-updates")
		{
			Content = JsonContent.Create(new DesktopDenuvoFixUpdateCheckRequest
			{
				Items = localStates.ToList()
			}, null, JsonOptions)
		};
		request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
		using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken);
		string text = await response.Content.ReadAsStringAsync(cancellationToken);
		if (!response.IsSuccessStatusCode)
		{
			throw new InvalidOperationException(ParseErrorMessage(text, response.ReasonPhrase));
		}
		return JsonSerializer.Deserialize<DesktopDenuvoFixUpdateCheckResponse>(text, JsonOptions) ?? throw new InvalidOperationException("API Denuvo Fix update không trả về dữ liệu hợp lệ.");
	}

	public Task<DesktopRuntimeBundleResponse> GetOpenSteamToolCloudRuntimeAsync(string accessToken, CancellationToken cancellationToken = default(CancellationToken))
	{
		return GetCloudRuntimeAsync("opensteamtool-cloud", accessToken, cancellationToken);
	}

	internal async Task<DesktopRuntimeBundleResponse> GetCloudRuntimeAsync(string bundleId, string accessToken, CancellationToken cancellationToken = default(CancellationToken), string? version = null, bool coreOnly = false)
	{
		ValidateRuntimeBundleId(bundleId);
		DesktopRuntimeBundleResponse desktopRuntimeBundleResponse = JsonSerializer.Deserialize<DesktopRuntimeBundleResponse>(await GetStringAsync($"api/runtime-bundles/{bundleId}/{((version == null) ? "latest" : Uri.EscapeDataString(version))}{(coreOnly ? "/core" : "")}", accessToken, cancellationToken, isRuntimeBundle: true), JsonOptions);
		if (desktopRuntimeBundleResponse == null)
		{
			throw new InvalidOperationException("API runtime CloudRedirect không trả về dữ liệu hợp lệ.");
		}
		if (desktopRuntimeBundleResponse.BundleId != bundleId)
		{
			throw new InvalidDataException("API trả về sai runtime bundle.");
		}
		foreach (DesktopRuntimeBundleFileResponse file in desktopRuntimeBundleResponse.Files)
		{
			file.DownloadUrl = ResolveRuntimeBundleDownloadUrl(file.DownloadUrl, desktopRuntimeBundleResponse.Version, bundleId, coreOnly);
		}
		return desktopRuntimeBundleResponse;
	}

	public async Task<DesktopDenuvoActivePendingResponse> GetDenuvoActivePendingAsync(string accessToken, CancellationToken cancellationToken = default(CancellationToken))
	{
		if (string.IsNullOrWhiteSpace(accessToken))
		{
			throw new InvalidOperationException("Bạn cần đăng nhập để xem Denuvo Active.");
		}
		return JsonSerializer.Deserialize<DesktopDenuvoActivePendingResponse>(await GetStringAsync("api/library/denuvo-active/pending", accessToken, cancellationToken), JsonOptions) ?? throw new InvalidOperationException("API Denuvo Active không trả về dữ liệu hợp lệ.");
	}

	public async Task<DesktopDenuvoActivePendingItemResponse> GetDenuvoActiveRequestStateAsync(Guid activeRequestId, string accessToken, CancellationToken cancellationToken = default(CancellationToken))
	{
		return JsonSerializer.Deserialize<DesktopDenuvoActivePendingItemResponse>(await GetStringAsync($"api/library/denuvo-active/{activeRequestId:D}", accessToken, cancellationToken), JsonOptions) ?? throw new InvalidOperationException("API Denuvo Active không trả về trạng thái hợp lệ.");
	}

	public Task<DesktopDenuvoActivePendingItemResponse> PostDenuvoActiveGameActivationCompletedForLibraryAsync(Guid libraryId, DesktopDenuvoActiveClientStateRequest state, string accessToken, CancellationToken cancellationToken = default(CancellationToken))
	{
		return PostDenuvoActiveStateForResultAsync($"api/library/denuvo-active/library/{libraryId:D}/game-activation-completed", state, accessToken, cancellationToken);
	}

	public Task<DesktopDenuvoActivePendingItemResponse> RequestDenuvoAdminTokenAsync(Guid libraryId, DesktopDenuvoActiveClientStateRequest state, string accessToken, CancellationToken cancellationToken = default(CancellationToken))
	{
		return PostDenuvoActiveStateForResultAsync($"api/library/denuvo-active/library/{libraryId:D}/request-admin-token", state, accessToken, cancellationToken);
	}

	public Task<DesktopDenuvoActivePendingItemResponse> PostDenuvoActiveSteamGameInstalledAsync(Guid activeRequestId, DesktopDenuvoActiveClientStateRequest state, string accessToken, CancellationToken cancellationToken = default(CancellationToken))
	{
		return PostDenuvoActiveStateForResultAsync($"api/library/denuvo-active/{activeRequestId:D}/steam-game-installed", state, accessToken, cancellationToken);
	}

	public Task PostDenuvoActiveDeliveredAsync(Guid activeRequestId, DesktopDenuvoActiveClientStateRequest state, string accessToken, CancellationToken cancellationToken = default(CancellationToken))
	{
		return PostDenuvoActiveStateAsync($"api/library/denuvo-active/{activeRequestId:D}/delivered", state, accessToken, cancellationToken);
	}

	public Task PostDenuvoActiveAppliedAsync(Guid activeRequestId, DesktopDenuvoActiveClientStateRequest state, string accessToken, CancellationToken cancellationToken = default(CancellationToken))
	{
		return PostDenuvoActiveStateAsync($"api/library/denuvo-active/{activeRequestId:D}/applied", state, accessToken, cancellationToken);
	}

	public Task PostDenuvoActiveErrorAsync(Guid activeRequestId, DesktopDenuvoActiveClientStateRequest state, string accessToken, CancellationToken cancellationToken = default(CancellationToken))
	{
		return PostDenuvoActiveStateAsync($"api/library/denuvo-active/{activeRequestId:D}/error", state, accessToken, cancellationToken);
	}

	public Task<DesktopDownloadedFileResponse> DownloadDllFileAsync(string downloadUrl, string accessToken, string destinationDirectory, string fallbackFileName, CancellationToken cancellationToken = default(CancellationToken))
	{
		return DownloadFileAsync(downloadUrl, accessToken, destinationDirectory, fallbackFileName, cancellationToken);
	}

	public async Task<DesktopDownloadedFileResponse> DownloadRuntimeBundleFileAsync(string downloadUrl, string accessToken, string destinationDirectory, string fallbackFileName, CancellationToken cancellationToken = default(CancellationToken), IProgress<DesktopFileDownloadProgress>? progress = null)
	{
		using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		deadline.CancelAfter(runtimeFileTimeout);
		CancellationTokenSource idle = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token);
		try
		{
			idle.CancelAfter(runtimeIdleTimeout);
			InlineProgress<DesktopFileDownloadProgress> progress2 = new InlineProgress<DesktopFileDownloadProgress>(delegate(DesktopFileDownloadProgress update)
			{
				idle.CancelAfter(runtimeIdleTimeout);
				progress?.Report(update);
			});
			try
			{
				return await DownloadFileAsync(downloadUrl, accessToken, destinationDirectory, fallbackFileName, idle.Token, progress2, isRuntimeBundle: true);
			}
			catch (OperationCanceledException innerException) when (!cancellationToken.IsCancellationRequested)
			{
				throw new TimeoutException("Tải thành phần Cloud Save quá thời gian chờ hoặc máy chủ đã ngừng gửi dữ liệu. Hãy kiểm tra mạng và thử Active lại.", innerException);
			}
		}
		finally
		{
			if (idle != null)
			{
				((IDisposable)idle).Dispose();
			}
		}
	}

	private async Task<DesktopDownloadedFileResponse> DownloadDenuvoFileAsync(string endpoint, string accessToken, string directory, string fallbackFileName, CancellationToken cancellationToken)
	{
		using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		deadline.CancelAfter(denuvoFileTimeout);
		CancellationTokenSource idle = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token);
		try
		{
			idle.CancelAfter(denuvoIdleTimeout);
			try
			{
				DesktopDownloadedFileResponse file = await DownloadFileAsync(endpoint, accessToken, directory, fallbackFileName, idle.Token, new InlineProgress<DesktopFileDownloadProgress>(delegate
				{
					idle.CancelAfter(denuvoIdleTimeout);
				})).ConfigureAwait(continueOnCapturedContext: false);
				using FileStream source = new FileStream(file.FullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
				string text = Convert.ToHexString(await SHA256.HashDataAsync(source, deadline.Token).ConfigureAwait(continueOnCapturedContext: false));
				if (!string.IsNullOrEmpty(file.Sha256) && !text.Equals(file.Sha256, StringComparison.OrdinalIgnoreCase))
				{
					throw new InvalidDataException("File Denuvo tải về sai SHA-256; chưa thay đổi file trong game.");
				}
				file.Sha256 = text;
				return file;
			}
			catch (OperationCanceledException innerException) when (!cancellationToken.IsCancellationRequested)
			{
				throw new TimeoutException("Tải Denuvo quá thời gian chờ hoặc máy chủ đã ngừng gửi dữ liệu. Hãy kiểm tra mạng và thử lại.", innerException);
			}
		}
		finally
		{
			if (idle != null)
			{
				((IDisposable)idle).Dispose();
			}
		}
	}

	public async Task<DesktopAccountResponse> GetAccountAsync(string accessToken, CancellationToken cancellationToken = default(CancellationToken))
	{
		DesktopAccountResponse desktopAccountResponse = JsonSerializer.Deserialize<DesktopAccountResponse>(await GetStringAsync("api/account", accessToken, cancellationToken), JsonOptions);
		if (desktopAccountResponse == null)
		{
			throw new InvalidOperationException("API tài khoản không trả về dữ liệu hợp lệ.");
		}
		foreach (DesktopAccountLibraryItemResponse libraryItem in desktopAccountResponse.LibraryItems)
		{
			libraryItem.PosterImageUrl = NormalizeAssetUrl(libraryItem.PosterImageUrl);
			libraryItem.SpotlightImageUrl = NormalizeAssetUrl(libraryItem.SpotlightImageUrl);
		}
		foreach (DesktopAccountOrderResponse recentOrder in desktopAccountResponse.RecentOrders)
		{
			recentOrder.ActionUrl = ResolveAbsoluteUrl(recentOrder.ActionUrl);
		}
		foreach (DesktopAccountOrderResponse order in desktopAccountResponse.Orders)
		{
			order.ActionUrl = ResolveAbsoluteUrl(order.ActionUrl);
		}
		foreach (DesktopAccountWalletTransactionResponse walletTransaction in desktopAccountResponse.WalletTransactions)
		{
			walletTransaction.ActionUrl = ResolveAbsoluteUrl(walletTransaction.ActionUrl);
		}
		return desktopAccountResponse;
	}

	private async Task<DesktopDenuvoActivePendingItemResponse> PostDenuvoActiveStateForResultAsync(string endpoint, DesktopDenuvoActiveClientStateRequest state, string accessToken, CancellationToken cancellationToken)
	{
		return JsonSerializer.Deserialize<DesktopDenuvoActivePendingItemResponse>(await PostDenuvoActiveStateCoreAsync(endpoint, state, accessToken, cancellationToken), JsonOptions) ?? throw new InvalidOperationException("API Denuvo Active không trả về dữ liệu hợp lệ.");
	}

	private async Task PostDenuvoActiveStateAsync(string endpoint, DesktopDenuvoActiveClientStateRequest state, string accessToken, CancellationToken cancellationToken)
	{
		await PostDenuvoActiveStateCoreAsync(endpoint, state, accessToken, cancellationToken);
	}

	private async Task<string> PostDenuvoActiveStateCoreAsync(string endpoint, DesktopDenuvoActiveClientStateRequest state, string accessToken, CancellationToken cancellationToken)
	{
		if (string.IsNullOrWhiteSpace(accessToken))
		{
			throw new InvalidOperationException("Bạn cần đăng nhập để cập nhật Denuvo Active.");
		}
		using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, endpoint)
		{
			Content = JsonContent.Create(state, null, JsonOptions)
		};
		request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
		using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken);
		string text = await response.Content.ReadAsStringAsync(cancellationToken);
		if (!response.IsSuccessStatusCode)
		{
			throw new InvalidOperationException(ParseErrorMessage(text, response.ReasonPhrase));
		}
		return text;
	}

	public async Task<StorefrontProductDetailResponse> GetProductDetailAsync(Guid gameId, string? accessToken = null, CancellationToken cancellationToken = default(CancellationToken))
	{
		StorefrontProductDetailResponse storefrontProductDetailResponse = JsonSerializer.Deserialize<StorefrontProductDetailResponse>(await GetStringAsync($"api/storefront/products/{gameId}", accessToken, cancellationToken), JsonOptions);
		if (storefrontProductDetailResponse == null)
		{
			throw new InvalidOperationException("API chi tiết game không trả về dữ liệu hợp lệ.");
		}
		NormalizeProductDetailUrls(storefrontProductDetailResponse);
		return storefrontProductDetailResponse;
	}

	public async Task<StoreComboDetailResponse> GetComboDetailAsync(Guid comboId, CancellationToken cancellationToken = default(CancellationToken))
	{
		StoreComboDetailResponse storeComboDetailResponse = JsonSerializer.Deserialize<StoreComboDetailResponse>(await GetStringAsync($"api/desktop/storefront/combos/{comboId:D}", null, cancellationToken), JsonOptions) ?? throw new InvalidOperationException("API chi tiết combo không trả về dữ liệu hợp lệ.");
		storeComboDetailResponse.PosterUrl = NormalizeAssetUrl(storeComboDetailResponse.PosterUrl) ?? storeComboDetailResponse.PosterUrl;
		storeComboDetailResponse.CoverUrl = NormalizeAssetUrl(storeComboDetailResponse.CoverUrl) ?? storeComboDetailResponse.CoverUrl;
		foreach (StoreComboComponentResponse component in storeComboDetailResponse.Components)
		{
			component.PhotoUrl = NormalizeAssetUrl(component.PhotoUrl) ?? component.PhotoUrl;
		}
		foreach (StoreComboMediaResponse medium in storeComboDetailResponse.Media)
		{
			medium.Url = NormalizeAssetUrl(medium.Url) ?? medium.Url;
		}
		return storeComboDetailResponse;
	}

	public async Task<StorefrontReviewMutationResponse> SubmitProductReviewAsync(Guid gameId, decimal rating, string? reviewText, string? accessToken, CancellationToken cancellationToken = default(CancellationToken))
	{
		if (string.IsNullOrWhiteSpace(accessToken))
		{
			throw new InvalidOperationException("Bạn cần đăng nhập để đánh giá sản phẩm.");
		}
		string endpoint = $"api/storefront/products/{gameId:D}/reviews";
		string fallbackEndpoint = $"api/storefront/reviews/{gameId:D}";
		string text = await PostProductReviewAsync(endpoint, rating, reviewText, accessToken, cancellationToken);
		if (text == null)
		{
			text = await PostProductReviewAsync(fallbackEndpoint, rating, reviewText, accessToken, cancellationToken);
		}
		if (text == null)
		{
			throw new InvalidOperationException("Server hiện tại chưa hỗ trợ API đánh giá. Hãy khởi động lại server bằng bản build mới.");
		}
		return JsonSerializer.Deserialize<StorefrontReviewMutationResponse>(text, JsonOptions) ?? throw new InvalidOperationException("API đánh giá không trả về kết quả hợp lệ.");
	}

	private async Task<string?> PostProductReviewAsync(string endpoint, decimal rating, string? reviewText, string accessToken, CancellationToken cancellationToken)
	{
		using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, endpoint)
		{
			Content = JsonContent.Create(new { rating, reviewText }, null, JsonOptions)
		};
		request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
		using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken);
		string text = await response.Content.ReadAsStringAsync(cancellationToken);
		if (response.StatusCode == HttpStatusCode.NotFound)
		{
			return null;
		}
		if (!response.IsSuccessStatusCode)
		{
			throw new InvalidOperationException(ParseErrorMessage(text, response.ReasonPhrase));
		}
		return text;
	}

	public async Task<StorefrontCartMutationResponse> AddToCartAsync(Guid gameId, Guid? versionId, int quantity, string? accessToken, CancellationToken cancellationToken = default(CancellationToken))
	{
		if (string.IsNullOrWhiteSpace(accessToken))
		{
			throw new InvalidOperationException("Bạn cần đăng nhập để thao tác giỏ hàng.");
		}
		using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, "api/storefront/cart/add")
		{
			Content = JsonContent.Create(new { gameId, versionId, quantity }, null, JsonOptions)
		};
		request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
		using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken);
		string text = await response.Content.ReadAsStringAsync(cancellationToken);
		if (!response.IsSuccessStatusCode)
		{
			throw new InvalidOperationException(ParseErrorMessage(text, response.ReasonPhrase));
		}
		return JsonSerializer.Deserialize<StorefrontCartMutationResponse>(text, JsonOptions) ?? throw new InvalidOperationException("API giỏ hàng không trả về kết quả hợp lệ.");
	}

	public async Task<StorefrontCartMutationResponse> AddComboToCartAsync(Guid comboId, string? accessToken, CancellationToken cancellationToken = default(CancellationToken))
	{
		if (string.IsNullOrWhiteSpace(accessToken))
		{
			throw new InvalidOperationException("Bạn cần đăng nhập để thao tác giỏ hàng.");
		}
		using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, "api/storefront/cart/combo/add")
		{
			Content = JsonContent.Create(new { comboId }, null, JsonOptions)
		};
		request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
		using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken);
		string text = await response.Content.ReadAsStringAsync(cancellationToken);
		if (!response.IsSuccessStatusCode)
		{
			throw new InvalidOperationException(ParseErrorMessage(text, response.ReasonPhrase));
		}
		return JsonSerializer.Deserialize<StorefrontCartMutationResponse>(text, JsonOptions) ?? throw new InvalidOperationException("API giỏ hàng không trả về kết quả hợp lệ.");
	}

	public async Task<StorefrontCartMutationResponse> UpdateCartItemAsync(Guid cartItemId, int quantity, string? promoCode, string? accessToken, CancellationToken cancellationToken = default(CancellationToken))
	{
		if (string.IsNullOrWhiteSpace(accessToken))
		{
			throw new InvalidOperationException("Bạn cần đăng nhập để thao tác giỏ hàng.");
		}
		using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, "api/storefront/cart/update")
		{
			Content = JsonContent.Create(new { cartItemId, quantity, promoCode }, null, JsonOptions)
		};
		request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
		using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken);
		string text = await response.Content.ReadAsStringAsync(cancellationToken);
		if (!response.IsSuccessStatusCode)
		{
			throw new InvalidOperationException(ParseErrorMessage(text, response.ReasonPhrase));
		}
		return JsonSerializer.Deserialize<StorefrontCartMutationResponse>(text, JsonOptions) ?? throw new InvalidOperationException("API giỏ hàng không trả về kết quả hợp lệ.");
	}

	public async Task<StorefrontCartMutationResponse> RemoveCartItemAsync(Guid cartItemId, string? promoCode, string? accessToken, CancellationToken cancellationToken = default(CancellationToken))
	{
		if (string.IsNullOrWhiteSpace(accessToken))
		{
			throw new InvalidOperationException("Bạn cần đăng nhập để thao tác giỏ hàng.");
		}
		using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, "api/storefront/cart/remove")
		{
			Content = JsonContent.Create(new { cartItemId, promoCode }, null, JsonOptions)
		};
		request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
		using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken);
		string text = await response.Content.ReadAsStringAsync(cancellationToken);
		if (!response.IsSuccessStatusCode)
		{
			throw new InvalidOperationException(ParseErrorMessage(text, response.ReasonPhrase));
		}
		return JsonSerializer.Deserialize<StorefrontCartMutationResponse>(text, JsonOptions) ?? throw new InvalidOperationException("API giỏ hàng không trả về kết quả hợp lệ.");
	}

	public async Task<StorefrontCartMutationResponse> RemoveComboCartItemAsync(Guid comboCartItemId, string? promoCode, string? accessToken, CancellationToken cancellationToken = default(CancellationToken))
	{
		if (string.IsNullOrWhiteSpace(accessToken))
		{
			throw new InvalidOperationException("Bạn cần đăng nhập để thao tác giỏ hàng.");
		}
		using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, "api/storefront/cart/combo/remove")
		{
			Content = JsonContent.Create(new { comboCartItemId, promoCode }, null, JsonOptions)
		};
		request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
		using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken);
		string text = await response.Content.ReadAsStringAsync(cancellationToken);
		if (!response.IsSuccessStatusCode)
		{
			throw new InvalidOperationException(ParseErrorMessage(text, response.ReasonPhrase));
		}
		return JsonSerializer.Deserialize<StorefrontCartMutationResponse>(text, JsonOptions) ?? throw new InvalidOperationException("API giỏ hàng không trả về kết quả hợp lệ.");
	}

	private static string ParseErrorMessage(string rawContent, string? fallbackReason)
	{
		if (string.IsNullOrWhiteSpace(rawContent))
		{
			return fallbackReason ?? "Không thể tải dữ liệu cửa hàng.";
		}
		try
		{
			using JsonDocument jsonDocument = JsonDocument.Parse(rawContent);
			if (jsonDocument.RootElement.TryGetProperty("message", out var value) && value.ValueKind == JsonValueKind.String)
			{
				return NormalizeServerMessage(value.GetString()) ?? fallbackReason ?? "Không thể tải dữ liệu cửa hàng.";
			}
			if (jsonDocument.RootElement.TryGetProperty("detail", out var value2) && value2.ValueKind == JsonValueKind.String)
			{
				return NormalizeServerMessage(value2.GetString()) ?? fallbackReason ?? "Không thể tải dữ liệu cửa hàng.";
			}
		}
		catch (JsonException)
		{
		}
		return fallbackReason ?? "Không thể tải dữ liệu cửa hàng.";
	}

	private static string? NormalizeServerMessage(string? message)
	{
		if (string.IsNullOrWhiteSpace(message))
		{
			return message;
		}
		string text = message.Trim();
		if (!(text == "Ban da so huu phien ban nay trong thu vien."))
		{
			if (text == "Dang tao thanh toan cho game da chon...")
			{
				return "Đang tạo thanh toán cho game đã chọn...";
			}
			return message;
		}
		return "Bạn đã sở hữu phiên bản này trong thư viện.";
	}

	private static string NormalizeBaseUrl(string apiBaseUrl)
	{
		if (string.IsNullOrWhiteSpace(apiBaseUrl))
		{
			return "https://localhost:7023/";
		}
		if (!apiBaseUrl.EndsWith("/", StringComparison.Ordinal))
		{
			return apiBaseUrl + "/";
		}
		return apiBaseUrl;
	}

	private static string ResolveDownloadFileName(HttpResponseMessage response, string fallbackFileName)
	{
		string fileName = Path.GetFileName(((response.Content.Headers.ContentDisposition?.FileNameStar ?? response.Content.Headers.ContentDisposition?.FileName ?? fallbackFileName) ?? fallbackFileName).Trim('"').Trim());
		if (!string.IsNullOrWhiteSpace(fileName))
		{
			return fileName;
		}
		return fallbackFileName;
	}

	private async Task<string> GetStringAsync(string endpoint, string? accessToken, CancellationToken cancellationToken, bool isRuntimeBundle = false, bool allowMissingEndpoint = false)
	{
		using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, endpoint);
		if (!string.IsNullOrWhiteSpace(accessToken))
		{
			request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
		}
		using CancellationTokenSource timeoutCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		timeoutCancellation.CancelAfter(ApiRequestTimeout);
		HttpResponseMessage response;
		try
		{
			response = await httpClient.SendAsync(request, timeoutCancellation.Token);
		}
		catch (OperationCanceledException innerException) when (!cancellationToken.IsCancellationRequested)
		{
			throw new TimeoutException("Máy chủ phản hồi quá chậm. Vui lòng thử lại.", innerException);
		}
		using (response)
		{
			if (allowMissingEndpoint && response.StatusCode == HttpStatusCode.NotFound)
			{
				throw new HttpRequestException("Desktop catalog endpoint is unavailable.", null, HttpStatusCode.NotFound);
			}
			if (isRuntimeBundle && response.StatusCode == HttpStatusCode.NotFound)
			{
				throw new RuntimeBundleUnavailableException("metadata");
			}
			if (isRuntimeBundle && response.StatusCode == HttpStatusCode.ServiceUnavailable)
			{
				throw new RuntimeBundleUnavailableException("metadata", 503);
			}
			string text = await response.Content.ReadAsStringAsync(timeoutCancellation.Token);
			HttpStatusCode statusCode = response.StatusCode;
			if ((statusCode == HttpStatusCode.Unauthorized || statusCode == HttpStatusCode.Forbidden) ? true : false)
			{
				throw new UnauthorizedAccessException("Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.");
			}
			if (!response.IsSuccessStatusCode)
			{
				throw new InvalidOperationException(ParseErrorMessage(text, response.ReasonPhrase));
			}
			return text;
		}
	}

	private async Task<DesktopDownloadedFileResponse> DownloadFileAsync(string endpointOrUrl, string accessToken, string destinationDirectory, string fallbackFileName, CancellationToken cancellationToken, IProgress<DesktopFileDownloadProgress>? progress = null, bool isRuntimeBundle = false)
	{
		if (string.IsNullOrWhiteSpace(accessToken))
		{
			throw new InvalidOperationException("Bạn cần đăng nhập để tải file.");
		}
		Directory.CreateDirectory(destinationDirectory);
		using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, endpointOrUrl);
		request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
		HttpResponseMessage response;
		try
		{
			response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
		}
		catch (TaskCanceledException innerException) when (!cancellationToken.IsCancellationRequested)
		{
			throw new TimeoutException($"Server quá thời gian chờ khi tải file sau {RequestTimeout.TotalMinutes:0} phút. Hãy kiểm tra timeout của hosting/proxy và tốc độ đọc package trên server.", innerException);
		}
		using (response)
		{
			if (isRuntimeBundle && response.StatusCode == HttpStatusCode.NotFound)
			{
				throw new RuntimeBundleUnavailableException("file");
			}
			if (!response.IsSuccessStatusCode)
			{
				string rawContent = await response.Content.ReadAsStringAsync(cancellationToken);
				bool flag = isRuntimeBundle;
				if (flag)
				{
					HttpStatusCode statusCode = response.StatusCode;
					bool flag2 = ((statusCode == HttpStatusCode.Unauthorized || statusCode == HttpStatusCode.Forbidden) ? true : false);
					flag = !flag2;
				}
				if (flag)
				{
					throw new HttpRequestException($"Runtime download HTTP {response.StatusCode}.", null, response.StatusCode);
				}
				string message = ParseErrorMessage(rawContent, response.ReasonPhrase);
				if (ShouldMaskDenuvoDownloadError(endpointOrUrl, response.StatusCode))
				{
					message = "Không thể nạp game Denuvo lúc này. Vui lòng thử lại sau hoặc liên hệ admin.";
				}
				throw new InvalidOperationException(message);
			}
			string fileName = ResolveDownloadFileName(response, fallbackFileName);
			string destinationPath = Path.Combine(destinationDirectory, fileName);
			long? totalBytes = response.Content.Headers.ContentLength;
			progress?.Report(new DesktopFileDownloadProgress(0L, totalBytes, (totalBytes.HasValue && totalBytes.GetValueOrDefault() > 0) ? new int?(0) : ((int?)null), 0L, null));
			Stopwatch stopwatch = Stopwatch.StartNew();
			long lastProgressAt = Environment.TickCount64;
			long bytesReceived = 0L;
			await using (Stream input = await response.Content.ReadAsStreamAsync(cancellationToken))
			{
				await using FileStream output = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 1048576, useAsync: true);
				byte[] buffer = new byte[1048576];
				while (true)
				{
					int bytesRead = await input.ReadAsync(buffer, cancellationToken);
					if (bytesRead == 0)
					{
						break;
					}
					await output.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
					bytesReceived += bytesRead;
					long tickCount = Environment.TickCount64;
					if (tickCount - lastProgressAt >= 200)
					{
						progress?.Report(BuildFileDownloadProgress(bytesReceived, totalBytes, stopwatch.Elapsed));
						lastProgressAt = tickCount;
					}
				}
				await output.FlushAsync(cancellationToken);
			}
			if (totalBytes.HasValue && totalBytes.GetValueOrDefault() > 0 && bytesReceived != totalBytes.Value)
			{
				throw new IOException("File tải từ server chưa đầy đủ dung lượng.");
			}
			progress?.Report(BuildFileDownloadProgress(bytesReceived, totalBytes, stopwatch.Elapsed, completed: true));
			int result;
			return new DesktopDownloadedFileResponse
			{
				FileName = fileName,
				FullPath = destinationPath,
				ContentType = (response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream"),
				Sha256 = (ReadHeader(response, "X-NestG-Denuvo-Sha256") ?? ReadHeader(response, "X-NestG-Pypass-Sha256") ?? ReadHeader(response, "X-NestG-Online-Sha256")),
				ConfigRevisionNumber = (int.TryParse(ReadHeader(response, "X-NestG-Denuvo-Config-Revision"), out result) ? new int?(result) : ((int?)null)),
				IsZipPasswordProtected = IsTrueHeader(ReadHeader(response, "X-NestG-Denuvo-Zip-Password-Protected") ?? ReadHeader(response, "X-NestG-Pypass-Zip-Password-Protected") ?? ReadHeader(response, "X-NestG-Online-Zip-Password-Protected")),
				ZipPassword = (ReadHeader(response, "X-NestG-Denuvo-Zip-Password") ?? ReadHeader(response, "X-NestG-Pypass-Zip-Password") ?? ReadHeader(response, "X-NestG-Online-Zip-Password"))
			};
		}
	}

	private static DesktopFileDownloadProgress BuildFileDownloadProgress(long bytesReceived, long? totalBytes, TimeSpan elapsed, bool completed = false)
	{
		long num = ((elapsed.TotalSeconds > 0.2) ? ((long)((double)bytesReceived / elapsed.TotalSeconds)) : 0);
		int? percentage = ((totalBytes.HasValue && totalBytes.GetValueOrDefault() > 0) ? new int?((int)Math.Clamp(bytesReceived * 100 / totalBytes.Value, 0L, 100L)) : ((int?)null));
		if (completed && totalBytes.HasValue && totalBytes.GetValueOrDefault() > 0)
		{
			percentage = 100;
		}
		TimeSpan? estimatedRemaining = ((num > 0 && totalBytes.HasValue && totalBytes.GetValueOrDefault() > 0) ? new TimeSpan?(TimeSpan.FromSeconds((double)Math.Max(0L, totalBytes.Value - bytesReceived) / (double)num)) : ((TimeSpan?)null));
		return new DesktopFileDownloadProgress(bytesReceived, totalBytes, percentage, num, estimatedRemaining);
	}

	private static bool ShouldMaskDenuvoDownloadError(string endpointOrUrl, HttpStatusCode statusCode)
	{
		if ((statusCode == HttpStatusCode.Unauthorized || statusCode == HttpStatusCode.Forbidden) ? true : false)
		{
			return false;
		}
		string text = endpointOrUrl.Replace('\\', '/');
		if (!text.Contains("/denuvo-fix/package", StringComparison.OrdinalIgnoreCase))
		{
			return text.Contains("/denuvo-fix/config", StringComparison.OrdinalIgnoreCase);
		}
		return true;
	}

	private static string? ReadHeader(HttpResponseMessage response, string headerName)
	{
		if (!response.Headers.TryGetValues(headerName, out IEnumerable<string> values))
		{
			return null;
		}
		return values.FirstOrDefault();
	}

	private static bool IsTrueHeader(string? value)
	{
		return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
	}

	private void NormalizeAssetUrls(IEnumerable<StorefrontProductCardResponse> items)
	{
		foreach (StorefrontProductCardResponse item in items)
		{
			item.PosterImageUrl = NormalizeAssetUrl(item.PosterImageUrl);
			item.SpotlightImageUrl = NormalizeAssetUrl(item.SpotlightImageUrl);
		}
	}

	private void NormalizeProductDetailUrls(StorefrontProductDetailResponse item)
	{
		item.CoverImageUrl = NormalizeAssetUrl(item.CoverImageUrl) ?? string.Empty;
		foreach (StorefrontProductGalleryItemResponse item2 in item.Gallery)
		{
			item2.ThumbnailUrl = NormalizeAssetUrl(item2.ThumbnailUrl) ?? string.Empty;
			item2.FullUrl = NormalizeAssetUrl(item2.FullUrl) ?? string.Empty;
		}
		NormalizeAssetUrls(item.RelatedProducts);
	}

	private string? NormalizeAssetUrl(string? rawUrl)
	{
		if (string.IsNullOrWhiteSpace(rawUrl))
		{
			return null;
		}
		string text = rawUrl.Trim();
		if (text.StartsWith("//", StringComparison.Ordinal))
		{
			return httpClient.BaseAddress.Scheme + ":" + text;
		}
		if (text.StartsWith("~/", StringComparison.Ordinal))
		{
			text = text.Substring(2);
		}
		if (!Uri.TryCreate(text, UriKind.Absolute, out Uri result))
		{
			return new Uri(httpClient.BaseAddress, text).ToString();
		}
		return result.ToString();
	}

	private string? NormalizeEventArtworkUrl(string? rawUrl)
	{
		if (string.IsNullOrWhiteSpace(rawUrl))
		{
			return null;
		}
		string text = rawUrl.Trim().Replace('\\', '/');
		if (text.StartsWith("~/", StringComparison.Ordinal))
		{
			text = text.Substring(2);
		}
		text = text.TrimStart('/');
		if (text.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
		{
			text = text.Substring("Assets/".Length);
		}
		if (text.StartsWith("Events/", StringComparison.OrdinalIgnoreCase))
		{
			return "https://nestg-assets.local/" + text;
		}
		return NormalizeAssetUrl(rawUrl);
	}

	internal string? ResolveAbsoluteUrl(string? rawUrl)
	{
		return NormalizeAssetUrl(rawUrl);
	}

	private static void ValidateRuntimeBundleId(string bundleId)
	{
		if (!(bundleId == "opensteamtool-cloud") && !(bundleId == "bettersteamtools-cloud"))
		{
			throw new InvalidDataException("Runtime bundle chưa được hỗ trợ.");
		}
	}

	public Task<DesktopDownloadedFileResponse> DownloadDenuvoActiveTokenConfigAsync(Guid activeRequestId, int revisionNumber, string connectionId, string accessToken, string destinationDirectory, string fallbackFileName, CancellationToken cancellationToken = default(CancellationToken))
	{
		return DownloadDenuvoFileAsync($"api/library/denuvo-active/{activeRequestId:D}/token-config?revisionNumber={revisionNumber}&connectionId={Uri.EscapeDataString(connectionId)}", accessToken, destinationDirectory, fallbackFileName, cancellationToken);
	}

	internal string ResolveRuntimeBundleDownloadUrl(string? rawUrl, string version, string bundleId = "opensteamtool-cloud", bool coreOnly = false)
	{
		ValidateRuntimeBundleId(bundleId);
		if (!Uri.TryCreate(ResolveAbsoluteUrl(rawUrl), UriKind.Absolute, out Uri result))
		{
			throw new InvalidDataException("Runtime bundle download URL không hợp lệ.");
		}
		Uri baseAddress = httpClient.BaseAddress;
		bool num = string.Equals(result.Scheme, baseAddress.Scheme, StringComparison.OrdinalIgnoreCase) && string.Equals(result.Host, baseAddress.Host, StringComparison.OrdinalIgnoreCase) && result.Port == baseAddress.Port;
		string value = $"/api/runtime-bundles/{bundleId}/{Uri.EscapeDataString(version)}/{(coreOnly ? "core/" : "")}files/";
		if (!num || !result.AbsolutePath.StartsWith(value, StringComparison.Ordinal))
		{
			throw new InvalidDataException("Runtime bundle download URL phải thuộc đúng API origin và runtime version.");
		}
		return result.ToString();
	}

	private DesktopCheckoutPaymentResponse NormalizeCheckoutResponse(DesktopCheckoutPaymentResponse payload)
	{
		payload.QrImageUrl = ResolveAbsoluteUrl(payload.QrImageUrl) ?? payload.QrImageUrl;
		payload.PollStatusUrl = ResolveAbsoluteUrl(payload.PollStatusUrl) ?? payload.PollStatusUrl;
		return payload;
	}

	private DesktopLibraryDenuvoFixResponse NormalizeDenuvoFixResponse(DesktopLibraryDenuvoFixResponse payload)
	{
		payload.PackageDownloadUrl = ResolveAbsoluteUrl(payload.PackageDownloadUrl) ?? payload.PackageDownloadUrl;
		payload.ConfigDownloadUrl = ResolveAbsoluteUrl(payload.ConfigDownloadUrl) ?? payload.ConfigDownloadUrl;
		return payload;
	}

	private DesktopLibraryPypassFixResponse NormalizePypassFixResponse(DesktopLibraryPypassFixResponse payload)
	{
		payload.PackageDownloadUrl = ResolveAbsoluteUrl(payload.PackageDownloadUrl) ?? payload.PackageDownloadUrl;
		return payload;
	}

	private DesktopLibraryOnlineFixResponse NormalizeOnlineFixResponse(DesktopLibraryOnlineFixResponse payload)
	{
		payload.PackageDownloadUrl = ResolveAbsoluteUrl(payload.PackageDownloadUrl) ?? payload.PackageDownloadUrl;
		return payload;
	}
}
