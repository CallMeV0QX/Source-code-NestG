using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace UI_Desktop.Store;

internal static class StoreLibraryDetailHtmlBuilder
{
	private static readonly Regex HtmlTagPattern = new Regex("<.*?>", RegexOptions.Compiled | RegexOptions.Singleline);

	private static readonly Regex WhitespacePattern = new Regex("\\s{2,}", RegexOptions.Compiled | RegexOptions.Singleline);

	private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);

	private const string DenuvoAdminButtonLabel = "Yêu cầu admin nạp game";

	private const string DenuvoCooldownButtonLabel = "Đã nạp game - còn";

	private const string OnlineFixButtonLabel = "ONLINE FIX";

	internal static string BuildDocument(DesktopLibraryDetailResponse detail, bool isActivating = false, bool isApplyingDenuvo = false, bool isApplyingPypass = false, bool isApplyingOnline = false, DateTime? denuvoCooldownUntilUtc = null, bool showPypassGuide = false, bool showDenuvoGuide = false, string? minimizedDenuvoGuidePayloadJson = null, bool isCloudSaving = false, string? fix54DocumentId = null, bool isFixing54 = false)
	{
		return NormalizeVietnameseMojibake(BuildDocumentCore(detail, null, isActivating, isApplyingDenuvo, isApplyingPypass, isApplyingOnline, denuvoCooldownUntilUtc, showPypassGuide, showDenuvoGuide, minimizedDenuvoGuidePayloadJson, isCloudSaving, fix54DocumentId, isFixing54));
	}

	internal static string BuildDocument(DesktopLibraryDetailResponse detail, StoreWebHomeContext context, bool isActivating = false, bool isApplyingDenuvo = false, bool isApplyingPypass = false, bool isApplyingOnline = false, DateTime? denuvoCooldownUntilUtc = null, bool showPypassGuide = false, bool showDenuvoGuide = false, string? minimizedDenuvoGuidePayloadJson = null, bool isCloudSaving = false, string? fix54DocumentId = null, bool isFixing54 = false)
	{
		return NormalizeVietnameseMojibake(BuildDocumentCore(detail, context, isActivating, isApplyingDenuvo, isApplyingPypass, isApplyingOnline, denuvoCooldownUntilUtc, showPypassGuide, showDenuvoGuide, minimizedDenuvoGuidePayloadJson, isCloudSaving, fix54DocumentId, isFixing54));
	}

	private static string BuildDocumentCore(DesktopLibraryDetailResponse detail, StoreWebHomeContext? context, bool isActivating, bool isApplyingDenuvo, bool isApplyingPypass, bool isApplyingOnline, DateTime? denuvoCooldownUntilUtc, bool showPypassGuide, bool showDenuvoGuide, string? minimizedDenuvoGuidePayloadJson, bool isCloudSaving, string? fix54DocumentId, bool isFixing54)
	{
		string text = PlainText(detail.ShortDescription);
		if (string.IsNullOrWhiteSpace(text))
		{
			text = "Sản phẩm đã được cấp vào thư viện launcher của bạn.";
		}
		if ((object)context != null)
		{
			return BuildSteamLibraryDocument(detail, context, text, isActivating, isApplyingDenuvo, isApplyingPypass, isApplyingOnline, denuvoCooldownUntilUtc, showPypassGuide, showDenuvoGuide, minimizedDenuvoGuidePayloadJson, isCloudSaving, fix54DocumentId, isFixing54);
		}
		string value = (((object)context == null) ? string.Empty : StoreWebHomeHtmlBuilder.BuildStyles());
		string value2 = (((object)context == null) ? "<div class=\"library-page\">" : ("<div class=\"library-detail-shell\">\r\n    " + StoreWebHomeHtmlBuilder.RenderSharedHeader(context, "library") + "\r\n    <main class=\"library-page\">"));
		string value3 = (((object)context == null) ? "</div>" : "    </main>\r\n</div>");
		return $"<!DOCTYPE html>\r\n<html lang=\"vi\">\r\n<head>\r\n<meta charset=\"utf-8\" />\r\n<meta http-equiv=\"X-UA-Compatible\" content=\"IE=edge\" />\r\n<meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\" />\r\n<title>{EncodeText(detail.GameName)}</title>\r\n<style>\r\n{value}\r\n{BuildStyles()}\r\n{((fix54DocumentId == null) ? string.Empty : StoreFix54Html.Styles)}\r\n</style>\r\n</head>\r\n<body>\r\n{value2}\r\n    <section class=\"library-hero\">\r\n        <div class=\"library-hero-media\">\r\n            {BuildHeroMedia(detail)}\r\n        </div>\r\n        <div class=\"library-hero-panel\">\r\n            <span class=\"library-kicker\">ĐÃ SỞ HỮU</span>\r\n            <h1 class=\"library-title\">{EncodeText(detail.GameName)}</h1>\r\n            <p class=\"library-summary\">{EncodeText(text)}</p>\r\n\r\n            <div class=\"library-chip-row\">\r\n                {BuildChip(detail.VersionName)}\r\n                {BuildChip("Order " + detail.OrderCode)}\r\n                {BuildChip(detail.AcquiredAtText)}\r\n            </div>\r\n\r\n            <div class=\"library-metric-grid\">\r\n                {BuildMetricCard("Giá hiện tại", detail.CurrentPriceText, "is-price")}\r\n                {BuildMetricCard("Khuyến mãi", string.IsNullOrWhiteSpace(detail.DiscountText) ? "Đã mua" : detail.DiscountText, "is-positive")}\r\n                {BuildMetricCard("Đánh giá", BuildReviewSummary(detail), string.Empty)}\r\n                {BuildMetricCard("Trạng thái", detail.StatusText, "is-positive")}\r\n            </div>\r\n\r\n            <div class=\"library-action-row\">\r\n                {BuildPlayButton(isActivating)}\r\n                {BuildCloudSaveButton(isCloudSaving, steamStyle: false)}\r\n                {BuildDenuvoButton(detail, isApplyingDenuvo, denuvoCooldownUntilUtc)}\r\n                <button type=\"button\" class=\"library-button library-button--guide\" data-denuvo-guide-button hidden>Hướng dẫn</button>\r\n                {BuildPypassButton(detail, isApplyingPypass)}\r\n                {BuildOnlineButton(detail, isApplyingOnline)}\r\n                {((fix54DocumentId == null) ? BuildWebsiteButton(detail.Website) : StoreFix54Html.BuildButton(detail, fix54DocumentId, isFixing54, steamStyle: false))}\r\n                {BuildDenuvoRealtimeStatus(detail)}\r\n            </div>\r\n        </div>\r\n    </section>\r\n\r\n    <section class=\"library-section\">\r\n        <div class=\"library-section-heading\">\r\n            <h2>Tổng quan sở hữu</h2>\r\n            <p>Thông tin nhanh, package và tag được đồng bộ từ storefront.</p>\r\n        </div>\r\n        <div class=\"library-dual-grid\">\r\n            <article class=\"library-card\">\r\n                <h3>Thông tin game</h3>\r\n                <div class=\"library-fact-list\">\r\n                    {BuildFacts(detail)}\r\n                </div>\r\n            </article>\r\n            <article class=\"library-card\">\r\n                <h3>Gói sản phẩm</h3>\r\n                <ul class=\"library-bullet-list\">\r\n                    {BuildPackageItems(detail)}\r\n                </ul>\r\n                <h3 class=\"library-subheading\">Tag nổi bật</h3>\r\n                <div class=\"library-tag-row\">\r\n                    {BuildTags(detail)}\r\n                </div>\r\n            </article>\r\n        </div>\r\n    </section>\r\n\r\n{value3}\r\n<script>\r\n{BuildScript(detail, showPypassGuide, showDenuvoGuide, minimizedDenuvoGuidePayloadJson)}\r\n{((fix54DocumentId == null) ? string.Empty : StoreFix54Html.Script)}\r\n</script>\r\n</body>\r\n</html>";
	}

	private static string BuildSteamLibraryDocument(DesktopLibraryDetailResponse detail, StoreWebHomeContext context, string summaryText, bool isActivating, bool isApplyingDenuvo, bool isApplyingPypass, bool isApplyingOnline, DateTime? denuvoCooldownUntilUtc, bool showPypassGuide, bool showDenuvoGuide, string? minimizedDenuvoGuidePayloadJson, bool isCloudSaving, string? fix54DocumentId, bool isFixing54)
	{
		string value = ResolveSteamThumbnail(detail);
		string value2 = (string.IsNullOrWhiteSpace(value) ? ("<div class=\"steam-news-thumb steam-news-thumb--empty\">" + EncodeText(Initial(detail.GameName)) + "</div>") : $"<img class=\"steam-news-thumb\" src=\"{EncodeAttribute(value)}\" alt=\"{EncodeAttribute(detail.GameName)}\" />");
		string value3 = (string.IsNullOrWhiteSpace(detail.DiscountText) ? "Đã sở hữu" : detail.DiscountText);
		string value4 = BuildReviewSummary(detail);
		return $"<!DOCTYPE html>\r\n<html lang=\"vi\">\r\n<head>\r\n<meta charset=\"utf-8\" />\r\n<meta http-equiv=\"X-UA-Compatible\" content=\"IE=edge\" />\r\n<meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\" />\r\n<title>{EncodeText(detail.GameName)}</title>\r\n<style>\r\n{StoreWebHomeHtmlBuilder.BuildStyles()}\r\n{BuildStyles()}\r\n{((fix54DocumentId == null) ? string.Empty : StoreFix54Html.Styles)}\r\n{((fix54DocumentId == null) ? string.Empty : ".steam-install-dock .fix54-button { min-height:34px; }")}\r\n</style>\r\n</head>\r\n<body>\r\n<div class=\"steam-library-shell\">\r\n    {StoreWebHomeHtmlBuilder.RenderSharedHeader(context, "library")}\r\n    <main class=\"steam-library-page\">\r\n        <section class=\"steam-hero\"{BuildSteamHeroStyle(detail)} data-drag-region>\r\n            <div class=\"steam-hero-fade\"></div>\r\n            <div class=\"steam-game-title\">\r\n                <span>TH&#431; VI&#7878;N</span>\r\n                <h1>{EncodeText(detail.GameName)}</h1>\r\n                <p>{EncodeText(detail.VersionName)} &bull; {EncodeText(detail.StatusText)}</p>\r\n            </div>\r\n            <div class=\"steam-install-dock\">\r\n                {BuildSteamInstallButton(isActivating)}\r\n                {BuildCloudSaveButton(isCloudSaving, steamStyle: true)}\r\n                {BuildSteamDenuvoButton(detail, isApplyingDenuvo, denuvoCooldownUntilUtc)}\r\n                <button type=\"button\" class=\"steam-cloud-button steam-guide-button\" data-denuvo-guide-button hidden>HƯỚNG DẪN</button>\r\n                {BuildSteamPypassButton(detail, isApplyingPypass)}\r\n                {BuildSteamOnlineButton(detail, isApplyingOnline)}\r\n                {((fix54DocumentId == null) ? BuildSteamWebsiteButton(detail.Website) : StoreFix54Html.BuildButton(detail, fix54DocumentId, isFixing54, steamStyle: true))}\r\n                {BuildDenuvoRealtimeStatus(detail)}\r\n            </div>\r\n        </section>\r\n\r\n        <nav class=\"steam-tabs\" aria-label=\"Library sections\">\r\n            <button type=\"button\" class=\"is-active\" disabled aria-current=\"page\">Trang cửa hàng</button>\r\n            <button type=\"button\" disabled aria-disabled=\"true\" title=\"Mục này chưa khả dụng\">DLC</button>\r\n            <button type=\"button\" disabled aria-disabled=\"true\" title=\"Mục này chưa khả dụng\">Cộng đồng</button>\r\n            <button type=\"button\" disabled aria-disabled=\"true\" title=\"Mục này chưa khả dụng\">Thảo luận</button>\r\n            <button type=\"button\" disabled aria-disabled=\"true\" title=\"Mục này chưa khả dụng\">Hướng dẫn</button>\r\n            <button type=\"button\" disabled aria-disabled=\"true\" title=\"Mục này chưa khả dụng\">Hỗ trợ</button>\r\n        </nav>\r\n\r\n        <section class=\"steam-detail-layout\">\r\n            <div class=\"steam-main-column\">\r\n                <section class=\"steam-panel steam-featured-panel\">\r\n                    <div class=\"steam-panel-heading\">\r\n                        <span>NỔI BẬT</span>\r\n                        <button type=\"button\" disabled aria-disabled=\"true\" title=\"Danh sách đầy đủ chưa khả dụng\">XEM TẤT CẢ</button>\r\n                    </div>\r\n                    <article class=\"steam-news-card\">\r\n                        {value2}\r\n                        <div>\r\n                            <small>COMMUNITY ANNOUNCEMENTS</small>\r\n                            <strong>{EncodeText(detail.GameName)} - {EncodeText(detail.VersionName)}</strong>\r\n                            <p>{EncodeText(summaryText)}</p>\r\n                        </div>\r\n                    </article>\r\n                </section>\r\n\r\n                <section class=\"steam-panel steam-activity-panel\">\r\n                    <div class=\"steam-panel-heading\">\r\n                        <span>HOẠT ĐỘNG</span>\r\n                    </div>\r\n                    <div class=\"steam-activity-input\">Nói gì đó về game này với bạn bè của bạn...</div>\r\n                </section>\r\n\r\n                <section class=\"steam-panel steam-description-panel\">\r\n                    <div class=\"steam-panel-heading\">\r\n                        <span>THÔNG TIN GAME</span>\r\n                    </div>\r\n                    <p>{EncodeText(summaryText)}</p>\r\n                    {BuildSteamTagRow(detail)}\r\n                </section>\r\n            </div>\r\n\r\n            <aside class=\"steam-side-column\">\r\n                <section class=\"steam-panel steam-owned-panel\">\r\n                    <div class=\"steam-panel-heading\">\r\n                        <span>SỞ HỮU</span>\r\n                    </div>\r\n                    <div class=\"steam-owned-grid\">\r\n                        {BuildSteamInfoItem("Giá hiện tại", detail.CurrentPriceText)}\r\n                        {BuildSteamInfoItem("Khuyến mãi", value3)}\r\n                        {BuildSteamInfoItem("Đánh giá", value4)}\r\n                        {BuildSteamInfoItem("Ngày mua", detail.AcquiredAtText)}\r\n                        {BuildSteamInfoItem("Phiên bản", detail.VersionName)}\r\n                        {BuildSteamInfoItem("Mã đơn", detail.OrderCode)}\r\n                    </div>\r\n                </section>\r\n\r\n                <section class=\"steam-panel steam-facts-panel\">\r\n                    <div class=\"steam-panel-heading\">\r\n                        <span>CHI TIẾT</span>\r\n                    </div>\r\n                    <div class=\"steam-fact-list\">\r\n                        {BuildSteamFactRows(detail)}\r\n                    </div>\r\n                </section>\r\n            </aside>\r\n        </section>\r\n    </main>\r\n</div>\r\n<script>\r\n{BuildScript(detail, showPypassGuide, showDenuvoGuide, minimizedDenuvoGuidePayloadJson)}\r\n{((fix54DocumentId == null) ? string.Empty : StoreFix54Html.Script)}\r\n</script>\r\n</body>\r\n</html>";
	}

	private static string BuildDenuvoRealtimeStatus(DesktopLibraryDetailResponse detail)
	{
		DesktopLibraryDenuvoFixResponse denuvoFix = detail.DenuvoFix;
		if (denuvoFix == null || !denuvoFix.HasDenuvoFix)
		{
			return string.Empty;
		}
		return "<p data-denuvo-realtime-status role=\"status\" aria-live=\"polite\" aria-atomic=\"true\" style=\"flex-basis:100%;margin:0;color:#c9d3e3;font-size:13px;line-height:1.5;text-align:center\">Đang kiểm tra kết nối nạp game...</p>";
	}

	private static string BuildSteamHeroStyle(DesktopLibraryDetailResponse detail)
	{
		string value = ResolveSteamHeroImage(detail);
		if (!string.IsNullOrWhiteSpace(value))
		{
			return " style=\"--steam-hero-image: url('" + EncodeAttribute(value) + "');\"";
		}
		return string.Empty;
	}

	private static string ResolveSteamHeroImage(DesktopLibraryDetailResponse detail)
	{
		if (!string.IsNullOrWhiteSpace(detail.CoverImageUrl))
		{
			return detail.CoverImageUrl;
		}
		foreach (DesktopLibraryGalleryItemResponse item in detail.Gallery)
		{
			if (!string.IsNullOrWhiteSpace(item.FullUrl))
			{
				return item.FullUrl;
			}
			if (!string.IsNullOrWhiteSpace(item.ThumbnailUrl))
			{
				return item.ThumbnailUrl;
			}
		}
		return string.Empty;
	}

	private static string ResolveSteamThumbnail(DesktopLibraryDetailResponse detail)
	{
		foreach (DesktopLibraryGalleryItemResponse item in detail.Gallery)
		{
			if (!string.IsNullOrWhiteSpace(item.ThumbnailUrl))
			{
				return item.ThumbnailUrl;
			}
			if (!string.IsNullOrWhiteSpace(item.FullUrl))
			{
				return item.FullUrl;
			}
		}
		return detail.CoverImageUrl;
	}

	private static string BuildSteamInstallButton(bool isActivating)
	{
		string value = (isActivating ? " is-loading" : string.Empty);
		string value2 = (isActivating ? " disabled aria-disabled=\"true\" " : " aria-disabled=\"false\" ");
		string value3 = (isActivating ? "LOADING..." : "ACTIVE");
		return $"<button type=\"button\" class=\"steam-install-button{value}\" data-action=\"play\" data-default-label=\"ACTIVE\"{value2}>{value3}</button>";
	}

	private static string BuildCloudSaveButton(bool isCloudSaving, bool steamStyle)
	{
		string value = (steamStyle ? "steam-cloud-button steam-cloud-save-button" : "library-button library-button--cloud-save");
		string value2 = (isCloudSaving ? " is-loading" : string.Empty);
		string value3 = (isCloudSaving ? " disabled aria-disabled=\"true\" aria-busy=\"true\" " : " aria-disabled=\"false\" aria-busy=\"false\" ");
		string value4 = (isCloudSaving ? "ĐANG LƯU..." : "CLOUD SAVE");
		return $"<button type=\"button\" class=\"{value}{value2}\" data-action=\"cloudsave:open\" data-default-label=\"CLOUD SAVE\" title=\"Quản lý bản lưu game\" aria-label=\"Mở tùy chọn bản lưu game\"{value3} hidden>\r\n    <svg class=\"cloud-save-icon\" viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.9\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\" focusable=\"false\">\r\n        <path d=\"M7.2 18.5H6a4 4 0 0 1-.55-7.96A6.3 6.3 0 0 1 17.6 9.2a4.7 4.7 0 0 1 .4 9.3h-1.2\" />\r\n        <path d=\"M12 10.5v9M8.8 13.8 12 10.5l3.2 3.3\" />\r\n    </svg>\r\n    <span>{value4}</span>\r\n</button>";
	}

	private static string BuildSteamDenuvoButton(DesktopLibraryDetailResponse detail, bool isApplyingDenuvo, DateTime? cooldownUntilUtc)
	{
		DesktopLibraryDenuvoFixResponse denuvoFix = detail.DenuvoFix;
		if (denuvoFix == null || !denuvoFix.HasDenuvoFix)
		{
			return string.Empty;
		}
		DateTime? cooldownUntilUtc2 = ResolveActiveDenuvoCooldownUntilUtc(cooldownUntilUtc);
		bool hasValue = cooldownUntilUtc2.HasValue;
		bool flag = string.Equals(denuvoFix.Status, "revoked", StringComparison.OrdinalIgnoreCase);
		string value = (IsDenuvoUpdatePending(denuvoFix) ? "denuvo:update" : "denuvo:apply");
		string text = "Yêu cầu admin nạp game".ToUpperInvariant();
		string text2 = "Đã nạp game - còn".ToUpperInvariant();
		string value2 = (isApplyingDenuvo ? "LOADING..." : (flag ? "GAME REVOKED" : (hasValue ? (text2 + " " + FormatDenuvoCooldownRemaining(cooldownUntilUtc2.Value)) : text)));
		string value3 = (isApplyingDenuvo ? " is-loading" : string.Empty) + (hasValue ? " is-cooldown" : string.Empty);
		string value4 = ((flag || hasValue) ? " data-denuvo-locked=\"true\" " : string.Empty);
		string value5 = BuildDenuvoCooldownAttributes(cooldownUntilUtc2, text2);
		string value6 = ((isApplyingDenuvo || flag || hasValue) ? " disabled aria-disabled=\"true\" " : " aria-disabled=\"false\" ");
		return $"<button type=\"button\" class=\"steam-cloud-button{value3}\" data-action=\"{value}\" data-default-label=\"{text}\"{value4}{value5}{value6}>{EncodeText(value2)}</button>";
	}

	private static string BuildSteamPypassButton(DesktopLibraryDetailResponse detail, bool isApplyingPypass)
	{
		DesktopLibraryPypassFixResponse pypassFix = detail.PypassFix;
		if (pypassFix == null || !pypassFix.HasPypassFix)
		{
			return string.Empty;
		}
		bool flag = string.Equals(pypassFix.Status, "revoked", StringComparison.OrdinalIgnoreCase);
		string text = ResolvePypassButtonLabel(pypassFix, uppercase: true);
		string value = (isApplyingPypass ? "LOADING..." : (flag ? "PYPASS REVOKED" : text));
		string value2 = (isApplyingPypass ? " is-loading" : string.Empty);
		string value3 = (flag ? " data-pypass-locked=\"true\" " : string.Empty);
		string value4 = ((isApplyingPypass || flag) ? " disabled aria-disabled=\"true\" " : " aria-disabled=\"false\" ");
		return $"<button type=\"button\" class=\"steam-cloud-button{value2}\" data-action=\"pypass:apply\" data-default-label=\"{text}\"{value3}{value4}>{value}</button>";
	}

	private static string BuildSteamOnlineButton(DesktopLibraryDetailResponse detail, bool isApplyingOnline)
	{
		DesktopLibraryOnlineFixResponse onlineFix = detail.OnlineFix;
		if (onlineFix == null || !onlineFix.HasOnlineFix)
		{
			return string.Empty;
		}
		bool flag = string.Equals(onlineFix.Status, "revoked", StringComparison.OrdinalIgnoreCase);
		string text = ResolveOnlineButtonLabel(onlineFix, uppercase: true);
		string value = (isApplyingOnline ? "LOADING..." : (flag ? "GAME REVOKED" : text));
		string value2 = (isApplyingOnline ? " is-loading" : string.Empty);
		string value3 = (flag ? " data-online-locked=\"true\" " : string.Empty);
		string value4 = ((isApplyingOnline || flag) ? " disabled aria-disabled=\"true\" " : " aria-disabled=\"false\" ");
		return $"<button type=\"button\" class=\"steam-cloud-button{value2}\" data-action=\"online:apply\" data-default-label=\"{text}\"{value3}{value4}>{value}</button>";
	}

	private static string BuildSteamWebsiteButton(string? website)
	{
		if (string.IsNullOrWhiteSpace(website))
		{
			return "<button type=\"button\" class=\"steam-cloud-button\" disabled aria-disabled=\"true\">STORE</button>";
		}
		return "<button type=\"button\" class=\"steam-cloud-button\" data-action=\"open:" + Uri.EscapeDataString(website) + "\">STORE</button>";
	}

	private static string BuildSteamInfoItem(string label, string? value)
	{
		string value2 = (string.IsNullOrWhiteSpace(value) ? "-" : value.Trim());
		return $"<div class=\"steam-info-item\">\r\n    <span>{EncodeText(label)}</span>\r\n    <strong>{EncodeText(value2)}</strong>\r\n</div>";
	}

	private static string BuildSteamTagRow(DesktopLibraryDetailResponse detail)
	{
		List<string> list;
		if (detail.SummaryTags.Count <= 0)
		{
			int num = 1;
			list = new List<string>(num);
			CollectionsMarshal.SetCount(list, num);
			CollectionsMarshal.AsSpan(list)[0] = "Library";
		}
		else
		{
			list = detail.SummaryTags;
		}
		StringBuilder stringBuilder = new StringBuilder("<div class=\"steam-tag-row\">");
		foreach (string item in list.Take(8))
		{
			StringBuilder stringBuilder2 = stringBuilder;
			StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(13, 1, stringBuilder2);
			handler.AppendLiteral("<span>");
			handler.AppendFormatted(EncodeText(item));
			handler.AppendLiteral("</span>");
			stringBuilder2.Append(ref handler);
		}
		stringBuilder.Append("</div>");
		return stringBuilder.ToString();
	}

	private static string BuildSteamFactRows(DesktopLibraryDetailResponse detail)
	{
		IEnumerable<DesktopLibraryFactResponse> enumerable2;
		if (detail.Facts.Count <= 0)
		{
			IEnumerable<DesktopLibraryFactResponse> enumerable = new _003C_003Ez__ReadOnlySingleElementList<DesktopLibraryFactResponse>(new DesktopLibraryFactResponse
			{
				Label = "Trạng thái",
				Value = detail.StatusText
			});
			enumerable2 = enumerable;
		}
		else
		{
			enumerable2 = detail.Facts.Take(8);
		}
		StringBuilder stringBuilder = new StringBuilder();
		foreach (DesktopLibraryFactResponse item in enumerable2)
		{
			StringBuilder stringBuilder2 = stringBuilder;
			StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(78, 2, stringBuilder2);
			handler.AppendLiteral("<div class=\"steam-fact-row\">\r\n    <span>");
			handler.AppendFormatted(EncodeText(item.Label));
			handler.AppendLiteral("</span>\r\n    <strong>");
			handler.AppendFormatted(EncodeText(item.Value));
			handler.AppendLiteral("</strong>\r\n</div>");
			stringBuilder2.AppendLine(ref handler);
		}
		return stringBuilder.ToString();
	}

	private static string Initial(string? value)
	{
		return (string.IsNullOrWhiteSpace(value) ? "N" : value.Trim()).Substring(0, 1).ToUpperInvariant();
	}

	private static string BuildStyles()
	{
		return ":root {\r\n    color-scheme: dark;\r\n    --bg: #0f141c;\r\n    --panel: #151c26;\r\n    --panel-soft: #1b2531;\r\n    --panel-elevated: #202b39;\r\n    --border: #344252;\r\n    --border-strong: #44566d;\r\n    --text: #f4f6f8;\r\n    --muted: #98a7bb;\r\n    --accent: #3eba7d;\r\n    --accent-soft: rgba(62, 186, 125, 0.18);\r\n    --success: #48d29d;\r\n    --warning: #f0d686;\r\n    --link: #8eb8ff;\r\n    --shadow: 0 22px 50px rgba(0, 0, 0, 0.26);\r\n}\r\n\r\n* {\r\n    box-sizing: border-box;\r\n}\r\n\r\nhtml, body {\r\n    margin: 0;\r\n    padding: 0;\r\n    background: var(--bg);\r\n    color: var(--text);\r\n    font-family: \"Segoe UI Variable Text\", \"Segoe UI\", Arial, sans-serif;\r\n}\r\n\r\nbody {\r\n    min-height: 100vh;\r\n}\r\n\r\n.library-detail-shell {\r\n    min-height: 100vh;\r\n}\r\n\r\n.steam-library-shell {\r\n    min-height: 100vh;\r\n    background: #080f1a;\r\n}\r\n\r\n.steam-library-page {\r\n    min-height: calc(100vh - 112px);\r\n    background: #080f1a;\r\n    color: #c7d5e0;\r\n}\r\n\r\n.steam-hero {\r\n    position: relative;\r\n    min-height: clamp(360px, 42vw, 540px);\r\n    overflow: hidden;\r\n    background:\r\n        linear-gradient(180deg, rgba(0,0,0,.08), rgba(8,15,26,.1) 48%, rgba(8,15,26,.96) 100%),\r\n        var(--steam-hero-image, linear-gradient(135deg, #1a3144, #09111d));\r\n    background-size: cover;\r\n    background-position: center;\r\n    border-bottom: 1px solid rgba(102,192,244,.16);\r\n}\r\n\r\n.steam-hero-fade {\r\n    position: absolute;\r\n    inset: 0;\r\n    background:\r\n        linear-gradient(90deg, rgba(8,15,26,.52), transparent 28%, transparent 70%, rgba(8,15,26,.58)),\r\n        linear-gradient(180deg, transparent 58%, rgba(8,15,26,.96));\r\n    pointer-events: none;\r\n}\r\n\r\n.steam-game-title {\r\n    position: absolute;\r\n    left: clamp(24px, 4vw, 72px);\r\n    bottom: 84px;\r\n    z-index: 2;\r\n    max-width: min(720px, 52vw);\r\n    text-shadow: 0 2px 22px rgba(0,0,0,.8);\r\n}\r\n\r\n.steam-game-title span {\r\n    display: block;\r\n    color: #66c0f4;\r\n    font-size: 12px;\r\n    font-weight: 900;\r\n    letter-spacing: 0;\r\n}\r\n\r\n.steam-game-title h1 {\r\n    margin: 8px 0 6px;\r\n    color: #f2f7ff;\r\n    font-size: 58px;\r\n    line-height: 1.02;\r\n}\r\n\r\n.steam-game-title p {\r\n    margin: 0;\r\n    color: #b8c8d6;\r\n    font-size: 15px;\r\n    font-weight: 700;\r\n}\r\n\r\n.steam-install-dock {\r\n    position: absolute;\r\n    left: 50%;\r\n    bottom: 28px;\r\n    z-index: 3;\r\n    display: flex;\r\n    flex-wrap: wrap;\r\n    align-items: center;\r\n    justify-content: center;\r\n    gap: 8px;\r\n    width: max-content;\r\n    max-width: calc(100% - 36px);\r\n    transform: translateX(-50%);\r\n}\r\n\r\n.steam-install-button,\r\n.steam-cloud-button {\r\n    height: 34px;\r\n    border: 1px solid rgba(102,192,244,.26);\r\n    border-radius: 2px;\r\n    color: #eaf7ff;\r\n    font: inherit;\r\n    font-size: 12px;\r\n    font-weight: 900;\r\n    letter-spacing: 0;\r\n    cursor: pointer;\r\n    box-shadow: 0 8px 24px rgba(0,0,0,.34);\r\n}\r\n\r\n.steam-install-button {\r\n    min-width: 112px;\r\n    padding: 0 22px;\r\n    position: relative;\r\n    background: linear-gradient(180deg, #49a9e8, #1b75a8);\r\n}\r\n\r\n.steam-install-button.is-loading {\r\n    cursor: wait;\r\n    opacity: .84;\r\n}\r\n\r\n.steam-install-button:disabled,\r\n.steam-cloud-button:disabled {\r\n    cursor: default;\r\n    opacity: .62;\r\n}\r\n\r\n.steam-cloud-button.is-cooldown,\r\n.library-button.is-cooldown {\r\n    cursor: default;\r\n    white-space: nowrap;\r\n}\r\n\r\n.steam-cloud-button.is-cooldown {\r\n    min-width: 244px;\r\n    display: inline-flex;\r\n    align-items: center;\r\n    justify-content: center;\r\n    gap: 7px;\r\n}\r\n\r\n.library-button.is-cooldown {\r\n    min-width: 260px;\r\n}\r\n\r\n.steam-cloud-button.is-cooldown::before,\r\n.library-button.is-cooldown::before {\r\n    content: \"\\1F512\";\r\n    font-size: .95em;\r\n    line-height: 1;\r\n}\r\n\r\n.steam-cloud-button {\r\n    min-width: 38px;\r\n    padding: 0 11px;\r\n    background: rgba(18, 34, 48, .88);\r\n}\r\n\r\n.steam-cloud-save-button {\r\n    min-width: 148px;\r\n    display: inline-flex;\r\n    align-items: center;\r\n    justify-content: center;\r\n    gap: 8px;\r\n    border-color: rgba(89, 196, 235, .52);\r\n    background: linear-gradient(180deg, rgba(28, 91, 126, .96), rgba(17, 57, 82, .96));\r\n    color: #e9faff;\r\n}\r\n\r\n.cloud-save-icon {\r\n    width: 18px;\r\n    height: 18px;\r\n    flex: 0 0 18px;\r\n}\r\n\r\n.steam-cloud-save-button.is-loading,\r\n.library-button--cloud-save.is-loading {\r\n    cursor: wait;\r\n}\r\n\r\n.steam-cloud-save-button.is-loading .cloud-save-icon,\r\n.library-button--cloud-save.is-loading .cloud-save-icon {\r\n    display: none;\r\n}\r\n\r\n.steam-cloud-save-button.is-loading::before,\r\n.library-button--cloud-save.is-loading::before {\r\n    content: \"\";\r\n    width: 15px;\r\n    height: 15px;\r\n    flex: 0 0 15px;\r\n    border: 2px solid rgba(255,255,255,.28);\r\n    border-top-color: #ffffff;\r\n    border-radius: 50%;\r\n    animation: library-spin .82s linear infinite;\r\n}\r\n\r\n.steam-install-button:focus-visible,\r\n.steam-cloud-button:focus-visible,\r\n.library-button:focus-visible {\r\n    outline: 3px solid rgba(142, 219, 255, .92);\r\n    outline-offset: 3px;\r\n}\r\n\r\n[data-denuvo-guide-button][hidden] {\r\n    display: none;\r\n}\r\n\r\n.steam-guide-button {\r\n    border-color: rgba(82, 224, 166, .52);\r\n    background: linear-gradient(180deg, rgba(37, 107, 83, .96), rgba(20, 70, 56, .96));\r\n    color: #d9ffef;\r\n    animation: denuvoGuideReveal .2s ease-out;\r\n}\r\n\r\n.steam-guide-button::before {\r\n    content: \"?\";\r\n    display: inline-grid;\r\n    width: 17px;\r\n    height: 17px;\r\n    margin-right: 6px;\r\n    place-items: center;\r\n    border: 1px solid rgba(217, 255, 239, .72);\r\n    border-radius: 50%;\r\n    font-size: 11px;\r\n    line-height: 1;\r\n}\r\n\r\n.steam-tabs {\r\n    height: 36px;\r\n    display: flex;\r\n    align-items: stretch;\r\n    gap: 0;\r\n    padding: 0 clamp(22px, 3vw, 60px);\r\n    background: #102033;\r\n    border-top: 1px solid rgba(102,192,244,.08);\r\n    border-bottom: 1px solid rgba(102,192,244,.12);\r\n}\r\n\r\n.steam-tabs button {\r\n    min-width: 74px;\r\n    padding: 0 17px;\r\n    border: 0;\r\n    border-bottom: 2px solid transparent;\r\n    background: transparent;\r\n    color: #b8c8d6;\r\n    font: inherit;\r\n    font-size: 12px;\r\n    font-weight: 800;\r\n    cursor: pointer;\r\n}\r\n\r\n.steam-tabs button.is-active {\r\n    color: #e8f7ff;\r\n    border-bottom-color: #66c0f4;\r\n    background: rgba(102,192,244,.08);\r\n}\r\n.steam-tabs button:disabled { cursor: not-allowed; }\r\n.steam-tabs button:disabled:not(.is-active), .steam-panel-heading button:disabled { opacity: .52; cursor: not-allowed; }\r\n\r\n.steam-detail-layout {\r\n    width: min(1120px, calc(100vw - 42px));\r\n    margin: 22px auto 96px;\r\n    display: grid;\r\n    grid-template-columns: minmax(0, 760px) minmax(260px, 320px);\r\n    gap: 18px;\r\n    align-items: start;\r\n}\r\n\r\n.steam-main-column,\r\n.steam-side-column {\r\n    display: grid;\r\n    gap: 16px;\r\n    min-width: 0;\r\n}\r\n\r\n.steam-panel {\r\n    border: 1px solid rgba(102,192,244,.13);\r\n    border-radius: 3px;\r\n    background: linear-gradient(180deg, rgba(20,37,54,.94), rgba(13,28,42,.96));\r\n    box-shadow: 0 18px 34px rgba(0,0,0,.22);\r\n}\r\n\r\n.steam-panel-heading {\r\n    min-height: 42px;\r\n    display: flex;\r\n    align-items: center;\r\n    justify-content: space-between;\r\n    padding: 0 14px;\r\n    border-bottom: 1px solid rgba(102,192,244,.1);\r\n}\r\n\r\n.steam-panel-heading span {\r\n    color: #7aa7c7;\r\n    font-size: 12px;\r\n    font-weight: 950;\r\n    letter-spacing: 0;\r\n}\r\n\r\n.steam-panel-heading button {\r\n    height: 24px;\r\n    border: 1px solid rgba(102,192,244,.26);\r\n    border-radius: 2px;\r\n    background: rgba(17,37,55,.82);\r\n    color: #9ccce8;\r\n    font: inherit;\r\n    font-size: 11px;\r\n    font-weight: 900;\r\n}\r\n\r\n.steam-news-card {\r\n    display: grid;\r\n    grid-template-columns: 142px minmax(0, 1fr);\r\n    gap: 14px;\r\n    margin: 12px 14px 14px;\r\n    padding: 10px;\r\n    border: 1px solid rgba(102,192,244,.13);\r\n    background: rgba(25,47,68,.76);\r\n}\r\n\r\n.steam-news-thumb,\r\n.steam-news-thumb--empty {\r\n    width: 100%;\r\n    aspect-ratio: 16 / 9;\r\n    object-fit: cover;\r\n    display: grid;\r\n    place-items: center;\r\n    background: #0b1521;\r\n    color: #8edbff;\r\n    font-size: 28px;\r\n    font-weight: 950;\r\n}\r\n\r\n.steam-news-card small {\r\n    display: block;\r\n    color: #7797ad;\r\n    font-size: 11px;\r\n    font-weight: 950;\r\n    letter-spacing: 0;\r\n    margin-bottom: 8px;\r\n}\r\n\r\n.steam-news-card strong {\r\n    display: block;\r\n    overflow: hidden;\r\n    color: #f2f7ff;\r\n    font-size: 15px;\r\n    line-height: 1.25;\r\n    text-overflow: ellipsis;\r\n    white-space: nowrap;\r\n}\r\n\r\n.steam-news-card p,\r\n.steam-description-panel p {\r\n    margin: 7px 0 0;\r\n    color: #a9bac8;\r\n    font-size: 13px;\r\n    line-height: 1.55;\r\n}\r\n\r\n.steam-activity-input {\r\n    margin: 14px;\r\n    padding: 12px 14px;\r\n    border: 1px dashed rgba(102,192,244,.18);\r\n    background: rgba(9,19,31,.5);\r\n    color: #627b90;\r\n    font-size: 13px;\r\n}\r\n\r\n.steam-description-panel {\r\n    padding-bottom: 14px;\r\n}\r\n\r\n.steam-description-panel p {\r\n    margin: 14px;\r\n}\r\n\r\n.steam-tag-row {\r\n    display: flex;\r\n    flex-wrap: wrap;\r\n    gap: 8px;\r\n    margin: 0 14px;\r\n}\r\n\r\n.steam-tag-row span {\r\n    padding: 6px 9px;\r\n    border: 1px solid rgba(102,192,244,.14);\r\n    border-radius: 2px;\r\n    background: rgba(102,192,244,.08);\r\n    color: #9ccce8;\r\n    font-size: 12px;\r\n    font-weight: 800;\r\n}\r\n\r\n.steam-owned-grid {\r\n    display: grid;\r\n    gap: 1px;\r\n    padding: 12px;\r\n}\r\n\r\n.steam-info-item,\r\n.steam-fact-row {\r\n    display: grid;\r\n    grid-template-columns: minmax(94px, .8fr) minmax(0, 1fr);\r\n    gap: 10px;\r\n    padding: 9px 0;\r\n    border-bottom: 1px solid rgba(102,192,244,.08);\r\n}\r\n\r\n.steam-info-item:last-child,\r\n.steam-fact-row:last-child {\r\n    border-bottom: 0;\r\n}\r\n\r\n.steam-info-item span,\r\n.steam-fact-row span {\r\n    color: #748ea4;\r\n    font-size: 12px;\r\n}\r\n\r\n.steam-info-item strong,\r\n.steam-fact-row strong {\r\n    min-width: 0;\r\n    overflow: hidden;\r\n    color: #dcecf7;\r\n    font-size: 12px;\r\n    text-align: right;\r\n    text-overflow: ellipsis;\r\n    white-space: nowrap;\r\n}\r\n\r\n.steam-fact-list {\r\n    padding: 12px;\r\n}\r\n\r\n[data-action=\"cloudsave:open\"][hidden] {\r\n    display: none;\r\n}\r\n\r\n.activation-overlay[hidden] {\r\n    display: none;\r\n}\r\n\r\n.cloudsave-choice-overlay[hidden], .cloudsave-restore-overlay[hidden] { display: none; }\r\n.cloudsave-choice-overlay, .cloudsave-restore-overlay {\r\n    position: fixed;\r\n    inset: 0;\r\n    z-index: 9999;\r\n    display: grid;\r\n    place-items: center;\r\n    padding: 16px;\r\n    background: rgba(2, 7, 16, .78);\r\n    backdrop-filter: blur(12px);\r\n}\r\nbody.cloudsave-choice-open, body.cloudsave-restore-open { overflow: hidden; }\r\n.cloudsave-choice-dialog {\r\n    width: min(680px, calc(100vw - 32px));\r\n    max-height: calc(100vh - 32px);\r\n    overflow: auto;\r\n    padding: 24px;\r\n    border: 1px solid rgba(102, 192, 244, .34);\r\n    border-radius: 16px;\r\n    background: linear-gradient(160deg, #10233a, #071321 72%);\r\n    box-shadow: 0 22px 55px rgba(0, 0, 0, .55);\r\n    color: #eaf5fc;\r\n}\r\n.cloudsave-choice-header {\r\n    display: flex;\r\n    align-items: flex-start;\r\n    justify-content: space-between;\r\n    gap: 16px;\r\n    margin-bottom: 24px;\r\n}\r\n.cloudsave-choice-header h2 {\r\n    margin: 0 0 7px;\r\n    font-size: clamp(21px, 3vw, 27px);\r\n    line-height: 1.2;\r\n}\r\n.cloudsave-choice-header p {\r\n    margin: 0;\r\n    color: #b9d0e1;\r\n    font-size: 14px;\r\n    line-height: 1.55;\r\n}\r\n.cloudsave-choice-close {\r\n    display: grid;\r\n    place-items: center;\r\n    flex: none;\r\n    width: 44px;\r\n    height: 44px;\r\n    border: 1px solid rgba(170, 204, 227, .35);\r\n    border-radius: 10px;\r\n    background: rgba(255, 255, 255, .06);\r\n    color: #d8edf9;\r\n    cursor: pointer;\r\n}\r\n.cloudsave-choice-close:hover { background: rgba(255, 255, 255, .12); }\r\n.cloudsave-choice-grid {\r\n    display: grid;\r\n    grid-template-columns: repeat(2, minmax(0, 1fr));\r\n    gap: 14px;\r\n}\r\n.cloudsave-choice-card {\r\n    display: flex;\r\n    flex-direction: column;\r\n    min-height: 255px;\r\n    padding: 20px;\r\n    border: 1px solid rgba(102, 192, 244, .39);\r\n    border-radius: 14px;\r\n    background: rgba(26, 57, 82, .55);\r\n}\r\n.cloudsave-choice-card.is-unavailable {\r\n    border-color: rgba(142, 169, 190, .25);\r\n    background: rgba(18, 31, 45, .68);\r\n}\r\n.cloudsave-choice-card-icon {\r\n    display: grid;\r\n    place-items: center;\r\n    width: 42px;\r\n    height: 42px;\r\n    margin-bottom: 18px;\r\n    border-radius: 11px;\r\n    background: rgba(102, 192, 244, .16);\r\n    color: #8fd6ff;\r\n}\r\n.cloudsave-choice-card.is-unavailable .cloudsave-choice-card-icon {\r\n    background: rgba(166, 184, 201, .12);\r\n    color: #a8bdcc;\r\n}\r\n.cloudsave-choice-card h3 {\r\n    margin: 0 0 9px;\r\n    font-size: 17px;\r\n    line-height: 1.35;\r\n}\r\n.cloudsave-choice-card p {\r\n    margin: 0 0 18px;\r\n    color: #bed1df;\r\n    font-size: 13px;\r\n    line-height: 1.55;\r\n}\r\n.cloudsave-choice-card.is-unavailable p { color: #a9bdcc; }\r\n.cloudsave-choice-badge {\r\n    display: inline-block;\r\n    margin: 0 0 9px;\r\n    color: #b8cbd7;\r\n    font-size: 11px;\r\n    font-weight: 700;\r\n    letter-spacing: .05em;\r\n    text-transform: uppercase;\r\n}\r\n.cloudsave-choice-action {\r\n    min-height: 44px;\r\n    width: 100%;\r\n    margin-top: auto;\r\n    padding: 10px 14px;\r\n    border: 1px solid #91d7fb;\r\n    border-radius: 9px;\r\n    background: #77c9f5;\r\n    color: #092035;\r\n    font-size: 13px;\r\n    font-weight: 700;\r\n    cursor: pointer;\r\n}\r\n.cloudsave-choice-action:hover:not(:disabled) { background: #a9e0fc; }\r\n.cloudsave-choice-action:disabled {\r\n    border-color: rgba(176, 196, 209, .23);\r\n    background: rgba(162, 184, 199, .12);\r\n    color: #b8cbd8;\r\n    cursor: not-allowed;\r\n}\r\n.cloudsave-choice-dialog button:focus-visible {\r\n    outline: 3px solid #ffe0a4;\r\n    outline-offset: 3px;\r\n}\r\n.cloudsave-restore-dialog {\r\n    width: min(720px, calc(100vw - 32px));\r\n    max-height: calc(100vh - 32px);\r\n    display: flex;\r\n    flex-direction: column;\r\n    overflow: hidden;\r\n    border: 1px solid rgba(102, 192, 244, .42);\r\n    border-radius: 16px;\r\n    background: linear-gradient(160deg, #10233a, #071321 72%);\r\n    box-shadow: 0 22px 55px rgba(0, 0, 0, .55);\r\n    color: #eaf5fc;\r\n}\r\n.cloudsave-restore-header, .cloudsave-restore-footer { padding: 20px 24px; }\r\n.cloudsave-restore-header { border-bottom: 1px solid rgba(170, 204, 227, .2); }\r\n.cloudsave-restore-header h2 { margin: 0 0 6px; font-size: 23px; line-height: 1.2; }\r\n.cloudsave-restore-header p { margin: 0; color: #bed1df; line-height: 1.55; }\r\n.cloudsave-restore-content { min-height: 0; overflow: auto; padding: 18px 24px; }\r\n.cloudsave-restore-list { display: grid; gap: 10px; }\r\n.cloudsave-restore-list[hidden], .cloudsave-restore-warning[hidden] { display: none; }\r\n.cloudsave-restore-version {\r\n    width: 100%; min-height: 76px; display: flex; align-items: center;\r\n    justify-content: space-between; gap: 16px; text-align: left; padding: 14px 16px;\r\n    border: 1px solid rgba(102, 192, 244, .38); border-radius: 12px;\r\n    background: rgba(26, 57, 82, .55); color: #eaf5fc; cursor: pointer;\r\n}\r\n.cloudsave-restore-version:hover { border-color: #91d7fb; background: rgba(39, 77, 106, .72); }\r\n.cloudsave-restore-version strong, .cloudsave-restore-version small { display: block; }\r\n.cloudsave-restore-version strong { font-size: 15px; margin-bottom: 5px; }\r\n.cloudsave-restore-version small { color: #bed1df; font-size: 12px; line-height: 1.4; }\r\n.cloudsave-restore-version svg { flex: none; color: #8fd6ff; }\r\n.cloudsave-restore-empty { padding: 22px; border: 1px dashed rgba(170, 204, 227, .35);\r\n    border-radius: 12px; color: #bed1df; line-height: 1.55; }\r\n.cloudsave-restore-warning { padding: 16px; border: 1px solid rgba(255, 197, 107, .45);\r\n    border-radius: 12px; background: rgba(106, 69, 22, .25); color: #fff0d7; line-height: 1.55; }\r\n.cloudsave-restore-warning strong { display: block; margin-bottom: 6px; }\r\n.cloudsave-restore-footer { display: flex; justify-content: flex-end; gap: 10px;\r\n    border-top: 1px solid rgba(170, 204, 227, .2); }\r\n.cloudsave-restore-footer button { min-height: 44px; padding: 10px 16px;\r\n    border-radius: 9px; font-size: 13px; font-weight: 700; cursor: pointer; }\r\n.cloudsave-restore-cancel { border: 1px solid rgba(170, 204, 227, .45);\r\n    background: rgba(255,255,255,.07); color: #eaf5fc; }\r\n.cloudsave-restore-confirm { border: 1px solid #ffce8b; background: #ffbf70; color: #2a1705; }\r\n.cloudsave-restore-confirm[hidden] { display: none; }\r\n.cloudsave-restore-dialog button:focus-visible { outline: 3px solid #ffe0a4; outline-offset: 3px; }\r\n@media (max-width: 560px) {\r\n    .cloudsave-choice-dialog { padding: 16px; }\r\n    .cloudsave-choice-header { margin-bottom: 14px; }\r\n    .cloudsave-choice-grid { grid-template-columns: 1fr; gap: 10px; }\r\n    .cloudsave-choice-card { min-height: 0; padding: 14px; }\r\n    .cloudsave-choice-card-icon { display: none; }\r\n    .cloudsave-choice-card h3 { margin-bottom: 6px; }\r\n    .cloudsave-choice-card p { margin-bottom: 12px; line-height: 1.45; }\r\n    .cloudsave-restore-header, .cloudsave-restore-content, .cloudsave-restore-footer { padding: 16px; }\r\n    .cloudsave-restore-version { align-items: flex-start; }\r\n}\r\n\r\n.activation-overlay {\r\n    position: fixed;\r\n    inset: 0;\r\n    z-index: 10000;\r\n    display: grid;\r\n    place-items: center;\r\n    padding: 14px;\r\n    background: rgba(2, 7, 16, .78);\r\n    backdrop-filter: blur(12px);\r\n}\r\n\r\nbody.activation-modal-open {\r\n    overflow: hidden;\r\n}\r\n\r\n.activation-dialog {\r\n    --activation-accent: #ff5d68;\r\n    --activation-accent-strong: #ff9aa0;\r\n    --activation-accent-soft: rgba(255, 93, 104, .12);\r\n    --activation-border: rgba(255, 93, 104, .38);\r\n    --activation-card-border: rgba(255, 93, 104, .30);\r\n    width: min(620px, calc(100vw - 32px));\r\n    max-height: calc(100vh - 32px);\r\n    display: grid;\r\n    grid-template-rows: auto minmax(0, 1fr) auto;\r\n    position: relative;\r\n    overflow: hidden;\r\n    border: 1px solid var(--activation-border);\r\n    border-radius: 14px;\r\n    background:\r\n        linear-gradient(135deg, var(--activation-accent-soft), transparent 30%),\r\n        linear-gradient(180deg, rgba(8, 17, 31, .98), rgba(5, 11, 22, .99));\r\n    box-shadow:\r\n        0 16px 42px rgba(0,0,0,.54),\r\n        0 0 0 1px rgba(132, 164, 214, .08);\r\n}\r\n\r\n.activation-dialog.is-success {\r\n    --activation-accent: #52e0a6;\r\n    --activation-accent-strong: #9ff5cf;\r\n    --activation-accent-soft: rgba(82, 224, 166, .12);\r\n    --activation-border: rgba(82, 224, 166, .34);\r\n    --activation-card-border: rgba(82, 224, 166, .28);\r\n}\r\n\r\n.activation-dialog.has-guide {\r\n    width: min(960px, calc(100vw - 32px));\r\n}\r\n\r\n.activation-dialog.has-zalo-qr {\r\n    width: min(780px, calc(100vw - 32px));\r\n}\r\n\r\n.activation-dialog.has-guide.has-zalo-qr {\r\n    width: min(980px, calc(100vw - 32px));\r\n}\r\n.activation-dialog.is-warning {\r\n    --activation-accent: #ffd36f;\r\n    --activation-accent-strong: #ffe2a0;\r\n    --activation-accent-soft: rgba(255, 211, 111, .11);\r\n    --activation-border: rgba(255, 211, 111, .34);\r\n    --activation-card-border: rgba(255, 211, 111, .28);\r\n}\r\n\r\n.activation-dialog.is-loading {\r\n    --activation-accent: #66c0f4;\r\n    --activation-accent-strong: #b8e8ff;\r\n    --activation-accent-soft: rgba(102, 192, 244, .11);\r\n    --activation-border: rgba(102, 192, 244, .34);\r\n    --activation-card-border: rgba(102, 192, 244, .28);\r\n}\r\n\r\n.activation-dialog.is-error {\r\n    --activation-accent: #ff5d68;\r\n    --activation-accent-strong: #ff9aa0;\r\n    --activation-accent-soft: rgba(255, 93, 104, .12);\r\n    --activation-border: rgba(255, 93, 104, .38);\r\n    --activation-card-border: rgba(255, 93, 104, .30);\r\n}\r\n\r\n.activation-dialog.is-locked .activation-dismiss-button,\r\n.activation-dialog.is-locked .activation-actions {\r\n    display: none;\r\n}\r\n\r\n.activation-hero {\r\n    position: relative;\r\n    display: grid;\r\n    grid-template-columns: 42px minmax(0, 1fr);\r\n    gap: 14px;\r\n    align-items: center;\r\n    min-height: 84px;\r\n    padding: 18px 42px 14px 22px;\r\n    border-bottom: 1px solid rgba(133, 164, 214, .12);\r\n    background:\r\n        linear-gradient(90deg, var(--activation-accent-soft), transparent 45%),\r\n        linear-gradient(180deg, rgba(8, 18, 34, .98), rgba(5, 12, 24, .98));\r\n}\r\n\r\n.activation-dismiss-button {\r\n    position: absolute;\r\n    top: 8px;\r\n    right: 9px;\r\n    width: 30px;\r\n    height: 30px;\r\n    display: grid;\r\n    place-items: center;\r\n    border: 0;\r\n    border-radius: 7px;\r\n    background: transparent;\r\n    color: #aab7c8;\r\n    font-size: 26px;\r\n    line-height: 1;\r\n    cursor: pointer;\r\n    transition: color .16s ease, background .16s ease;\r\n}\r\n\r\n.activation-dismiss-button:hover {\r\n    background: rgba(255,255,255,.06);\r\n    color: #ffffff;\r\n}\r\n\r\n.activation-mark {\r\n    width: 42px;\r\n    height: 42px;\r\n    display: grid;\r\n    place-items: center;\r\n    justify-self: center;\r\n    color: var(--activation-accent-strong);\r\n    filter: none;\r\n}\r\n\r\n.activation-mark::before,\r\n.activation-mark::after {\r\n    display: none;\r\n}\r\n\r\n.activation-status-symbol {\r\n    display: grid;\r\n    place-items: center;\r\n    width: 42px;\r\n    height: 42px;\r\n    color: var(--activation-accent-strong);\r\n    font-size: 40px;\r\n    font-weight: 950;\r\n    line-height: 1;\r\n    text-shadow: none;\r\n}\r\n\r\n.activation-dialog.is-loading .activation-status-symbol {\r\n    animation: activationPulse 1.2s ease-in-out infinite;\r\n}\r\n\r\n.activation-title-block {\r\n    min-width: 0;\r\n}\r\n\r\n.activation-game,\r\n.activation-meta,\r\n.activation-badge {\r\n    display: none;\r\n}\r\n\r\n.activation-title {\r\n    margin: 0;\r\n    color: #f4fbff;\r\n    font-size: clamp(22px, 2.8vw, 28px);\r\n    font-weight: 950;\r\n    line-height: 1.1;\r\n    letter-spacing: 0;\r\n}\r\n\r\n.activation-summary {\r\n    margin: 7px 0 0;\r\n    max-width: 500px;\r\n    color: #c3ccda;\r\n    font-size: 12.5px;\r\n    line-height: 1.42;\r\n}\r\n\r\n.activation-content {\r\n    min-height: 0;\r\n    overflow-y: auto;\r\n    overflow-x: hidden;\r\n    padding: 12px 24px 8px;\r\n    scrollbar-width: none;\r\n}\r\n\r\n.activation-dialog.has-zalo-qr .activation-content {\r\n    display: grid;\r\n    grid-template-columns: minmax(0, 1fr) 154px;\r\n    gap: 14px;\r\n    align-items: start;\r\n}\r\n\r\n.activation-main-column {\r\n    min-width: 0;\r\n}\r\n\r\n.activation-content::-webkit-scrollbar {\r\n    display: none;\r\n}\r\n\r\n.activation-phone,\r\n.activation-phone[hidden],\r\n.activation-zalo-qr[hidden] {\r\n    display: none;\r\n}\r\n\r\n.activation-phone::before {\r\n    content: \"\";\r\n}\r\n\r\n.activation-section-title {\r\n    display: none;\r\n}\r\n\r\n.activation-loader[hidden] {\r\n    display: none;\r\n}\r\n\r\n.activation-loader {\r\n    display: grid;\r\n    grid-template-columns: 28px minmax(0, 1fr);\r\n    gap: 8px;\r\n    align-items: center;\r\n    margin: 0 0 8px;\r\n    padding: 8px 10px;\r\n    border: 1px solid var(--activation-border);\r\n    border-radius: 8px;\r\n    background: rgba(11, 24, 41, .54);\r\n}\r\n\r\n.activation-loader-spinner {\r\n    width: 22px;\r\n    height: 22px;\r\n    border-radius: 50%;\r\n    border: 2px solid rgba(255, 255, 255, .14);\r\n    border-top-color: var(--activation-accent);\r\n    animation: activationSpin .82s linear infinite;\r\n}\r\n\r\n.activation-loader-body {\r\n    min-width: 0;\r\n    display: grid;\r\n    gap: 8px;\r\n}\r\n\r\n.activation-loader-heading {\r\n    min-width: 0;\r\n    display: flex;\r\n    align-items: center;\r\n    justify-content: space-between;\r\n    gap: 12px;\r\n}\r\n\r\n.activation-loader strong {\r\n    min-width: 0;\r\n    color: #eaf7ff;\r\n    font-size: 12px;\r\n    line-height: 1.35;\r\n}\r\n\r\n.activation-loader-percent {\r\n    display: none;\r\n    flex: 0 0 auto;\r\n    color: #70d6ff;\r\n    font-size: 15px;\r\n    font-weight: 950;\r\n    line-height: 1;\r\n    font-variant-numeric: tabular-nums;\r\n}\r\n\r\n.activation-progress-track {\r\n    display: none;\r\n    position: relative;\r\n    height: 9px;\r\n    overflow: hidden;\r\n    border: 1px solid rgba(94, 189, 255, .2);\r\n    border-radius: 999px;\r\n    background: rgba(4, 12, 24, .82);\r\n    box-shadow: inset 0 1px 2px rgba(0, 0, 0, .42);\r\n}\r\n\r\n.activation-progress-fill {\r\n    display: block;\r\n    width: 0;\r\n    height: 100%;\r\n    border-radius: inherit;\r\n    background: linear-gradient(90deg, #1d8cff 0%, #43c7ff 58%, #70f0d0 100%);\r\n    box-shadow: 0 0 14px rgba(43, 180, 255, .42);\r\n    transition: width .22s ease-out;\r\n}\r\n\r\n.activation-loader.has-progress {\r\n    grid-template-columns: minmax(0, 1fr);\r\n    padding: 11px 12px 12px;\r\n    border-color: rgba(68, 174, 255, .38);\r\n    background: linear-gradient(135deg, rgba(11, 28, 49, .94), rgba(7, 19, 35, .88));\r\n}\r\n\r\n.activation-loader.has-progress .activation-loader-spinner {\r\n    display: none;\r\n}\r\n\r\n.activation-loader.has-progress .activation-loader-percent {\r\n    display: block;\r\n}\r\n\r\n.activation-loader.has-progress .activation-progress-track {\r\n    display: block;\r\n}\r\n\r\n.activation-loader.is-indeterminate .activation-loader-percent {\r\n    color: #a9c7de;\r\n    font-size: 11px;\r\n    font-weight: 850;\r\n}\r\n\r\n.activation-loader.is-indeterminate .activation-progress-fill {\r\n    width: 32% !important;\r\n    animation: activationProgressIndeterminate 1.15s ease-in-out infinite;\r\n}\r\n\r\n.activation-zalo-qr {\r\n    display: grid;\r\n    gap: 8px;\r\n    align-self: start;\r\n    justify-self: end;\r\n    width: 154px;\r\n    padding: 9px;\r\n    border: 1px solid rgba(255, 255, 255, .16);\r\n    border-radius: 8px;\r\n    background: #ffffff;\r\n    box-shadow: 0 14px 30px rgba(0, 0, 0, .28);\r\n}\r\n\r\n.activation-zalo-qr img {\r\n    display: block;\r\n    width: 100%;\r\n    aspect-ratio: 1;\r\n    border-radius: 5px;\r\n    object-fit: contain;\r\n}\r\n\r\n.activation-zalo-qr span {\r\n    color: #111827;\r\n    font-size: 11px;\r\n    font-weight: 900;\r\n    line-height: 1.2;\r\n    text-align: center;\r\n}\r\n\r\n@keyframes activationSpin {\r\n    to {\r\n        transform: rotate(360deg);\r\n    }\r\n}\r\n\r\n@keyframes activationLoadingBar {\r\n    0% {\r\n        transform: translateX(-120%);\r\n    }\r\n\r\n    100% {\r\n        transform: translateX(260%);\r\n    }\r\n}\r\n\r\n@keyframes activationPulse {\r\n    0%, 100% {\r\n        transform: scale(1);\r\n        opacity: 1;\r\n    }\r\n\r\n    50% {\r\n        transform: scale(.92);\r\n        opacity: .72;\r\n    }\r\n}\r\n\r\n@keyframes activationProgressIndeterminate {\r\n    0% {\r\n        transform: translateX(-125%);\r\n    }\r\n\r\n    100% {\r\n        transform: translateX(330%);\r\n    }\r\n}\r\n\r\n@media (prefers-reduced-motion: reduce) {\r\n    .activation-loader-spinner,\r\n    .activation-dialog.is-loading .activation-status-symbol,\r\n    .steam-cloud-save-button.is-loading::before,\r\n    .library-button--cloud-save.is-loading::before {\r\n        animation: none;\r\n    }\r\n\r\n    .activation-progress-fill {\r\n        transition: none;\r\n    }\r\n\r\n    .activation-loader.is-indeterminate .activation-progress-fill {\r\n        width: 100% !important;\r\n        opacity: .52;\r\n        animation: none;\r\n    }\r\n}\r\n\r\n.activation-metric-grid {\r\n    display: grid;\r\n    grid-template-columns: repeat(2, minmax(0, 1fr));\r\n    gap: 10px;\r\n    margin-bottom: 10px;\r\n}\r\n\r\n.activation-metric-grid[hidden],\r\n.activation-path-list[hidden],\r\n.activation-guide[hidden] {\r\n    display: none;\r\n}\r\n\r\n.activation-metric {\r\n    min-width: 0;\r\n    min-height: 54px;\r\n    padding: 9px 11px;\r\n    border: 1px solid rgba(83, 115, 160, .28);\r\n    border-radius: 8px;\r\n    background: rgba(9, 19, 34, .80);\r\n}\r\n\r\n.activation-metric::before {\r\n    display: none;\r\n}\r\n\r\n.activation-metric:first-child {\r\n    border-color: var(--activation-card-border);\r\n    background: linear-gradient(135deg, var(--activation-accent-soft), rgba(8, 18, 33, .68));\r\n}\r\n\r\n.activation-metric span,\r\n.activation-path-row span {\r\n    display: block;\r\n    color: #8f9caf;\r\n    font-size: 10px;\r\n    font-weight: 900;\r\n    text-transform: uppercase;\r\n}\r\n\r\n.activation-metric strong {\r\n    display: block;\r\n    margin-top: 5px;\r\n    overflow-wrap: anywhere;\r\n    color: #eef8ff;\r\n    font-size: 15px;\r\n    line-height: 1.16;\r\n}\r\n\r\n.activation-metric.is-success strong {\r\n    color: #9ef4cf;\r\n}\r\n\r\n.activation-metric.is-warning strong {\r\n    color: #ffd774;\r\n}\r\n\r\n.activation-metric.is-error strong {\r\n    color: #ffb1b8;\r\n}\r\n\r\n.activation-path-list {\r\n    display: grid;\r\n    gap: 0;\r\n    margin: 0 0 8px;\r\n    border: 1px solid rgba(102,192,244,.16);\r\n    border-radius: 8px;\r\n    overflow: hidden;\r\n    background: rgba(8, 17, 29, .82);\r\n}\r\n\r\n.activation-path-row {\r\n    display: grid;\r\n    grid-template-columns: 120px minmax(0, 1fr) auto;\r\n    gap: 8px;\r\n    align-items: center;\r\n    padding: 8px 10px;\r\n    background: rgba(20, 38, 57, .42);\r\n}\r\n\r\n.activation-path-row strong {\r\n    min-width: 0;\r\n    color: #dbeaf7;\r\n    font-size: 12px;\r\n    line-height: 1.32;\r\n    overflow-wrap: anywhere;\r\n}\r\n\r\n.activation-path-copy {\r\n    min-height: 28px;\r\n    padding: 0 9px;\r\n    border: 1px solid rgba(72, 210, 157, .26);\r\n    border-radius: 7px;\r\n    background: rgba(72, 210, 157, .10);\r\n    color: #c8ffe9;\r\n    font-size: 11px;\r\n    font-weight: 900;\r\n    cursor: pointer;\r\n}\r\n\r\n.activation-guide {\r\n    display: grid;\r\n    gap: 10px;\r\n    margin: 0 0 8px;\r\n    padding: 10px;\r\n    border: 1px solid rgba(102,192,244,.13);\r\n    border-radius: 5px;\r\n    background: rgba(8, 17, 29, .7);\r\n}\r\n\r\n.activation-guide h3 {\r\n    margin: 0;\r\n    color: #f2fbff;\r\n    font-size: 14px;\r\n    line-height: 1.3;\r\n}\r\n\r\n.activation-guide p {\r\n    margin: 0;\r\n    color: #a9bbcf;\r\n    font-size: 12px;\r\n    line-height: 1.45;\r\n}\r\n\r\n.activation-guide ol {\r\n    display: grid;\r\n    gap: 8px;\r\n    margin: 0;\r\n    padding-left: 18px;\r\n}\r\n\r\n.activation-guide li {\r\n    color: #cbd9e8;\r\n    font-size: 12px;\r\n    line-height: 1.45;\r\n}\r\n\r\n.activation-guide li strong {\r\n    display: block;\r\n    margin-bottom: 3px;\r\n    color: #ffffff;\r\n}\r\n\r\n.activation-guide-images {\r\n    display: grid;\r\n    grid-template-columns: repeat(auto-fit, minmax(250px, 1fr));\r\n    gap: 12px;\r\n}\r\n\r\n.activation-guide figure {\r\n    margin: 0;\r\n    display: grid;\r\n    gap: 6px;\r\n    min-width: 0;\r\n}\r\n\r\n.activation-guide img {\r\n    width: 100%;\r\n    aspect-ratio: 16 / 9;\r\n    border-radius: 5px;\r\n    border: 1px solid rgba(102,192,244,.18);\r\n    background: rgba(3, 9, 18, .74);\r\n    object-fit: contain;\r\n    image-rendering: auto;\r\n}\r\n\r\n.activation-guide figcaption {\r\n    color: #91a7bc;\r\n    font-size: 11px;\r\n    line-height: 1.35;\r\n}\r\n\r\n.activation-message-list {\r\n    display: grid;\r\n    gap: 5px;\r\n    margin: 0;\r\n    padding: 0;\r\n    list-style: none;\r\n}\r\n\r\n.activation-message-list li {\r\n    position: relative;\r\n    padding-left: 16px;\r\n    color: #c4cfdd;\r\n    font-size: 12.5px;\r\n    line-height: 1.34;\r\n}\r\n\r\n.activation-message-list li::before {\r\n    content: \"\";\r\n    position: absolute;\r\n    left: 1px;\r\n    top: 7px;\r\n    width: 6px;\r\n    height: 6px;\r\n    border-radius: 50%;\r\n    background: var(--activation-accent);\r\n}\r\n\r\n.activation-details-toggle {\r\n    width: fit-content;\r\n    margin-top: 10px;\r\n    padding: 0;\r\n    border: 0;\r\n    background: transparent;\r\n    color: #66c0f4;\r\n    font-size: 12px;\r\n    font-weight: 800;\r\n    cursor: pointer;\r\n}\r\n\r\n.activation-details-toggle:hover {\r\n    color: #9ddcff;\r\n    text-decoration: underline;\r\n}\r\n\r\n.activation-technical-details {\r\n    max-height: 180px;\r\n    margin: 9px 0 0;\r\n    padding: 10px;\r\n    overflow: auto;\r\n    border: 1px solid rgba(102,192,244,.16);\r\n    border-radius: 6px;\r\n    background: rgba(3, 9, 18, .82);\r\n    color: #9fb1c5;\r\n    font: 11px/1.45 Consolas, \"Courier New\", monospace;\r\n    white-space: pre-wrap;\r\n    overflow-wrap: anywhere;\r\n}\r\n\r\n.activation-actions {\r\n    display: flex;\r\n    flex-wrap: wrap;\r\n    justify-content: flex-end;\r\n    gap: 10px;\r\n    padding: 10px 24px 14px;\r\n    border-top: 1px solid rgba(133, 164, 214, .10);\r\n    background: rgba(6, 13, 25, .94);\r\n}\r\n\r\n.activation-primary-button,\r\n.activation-close-button,\r\n.activation-log-button {\r\n    min-width: 118px;\r\n    height: 38px;\r\n    border-radius: 8px;\r\n    color: #f6fbff;\r\n    font: inherit;\r\n    font-size: 14px;\r\n    font-weight: 950;\r\n    letter-spacing: 0;\r\n    cursor: pointer;\r\n    transition: transform .16s ease, background .16s ease, border-color .16s ease;\r\n}\r\n\r\n.activation-primary-button {\r\n    border: 1px solid var(--activation-card-border);\r\n    background: rgba(255,255,255,.055);\r\n    color: var(--activation-accent-strong);\r\n    box-shadow: inset 0 1px 0 rgba(255,255,255,.06);\r\n}\r\n\r\n.activation-close-button {\r\n    border: 1px solid rgba(135, 159, 198, .42);\r\n    background: rgba(13, 24, 43, .86);\r\n    box-shadow: inset 0 1px 0 rgba(255,255,255,.05);\r\n}\r\n\r\n.activation-primary-button:hover,\r\n.activation-close-button:hover {\r\n    transform: translateY(-1px);\r\n}\r\n\r\n.activation-primary-button:hover {\r\n    background: rgba(255,255,255,.09);\r\n}\r\n\r\n.activation-close-button:hover {\r\n    border-color: rgba(176, 198, 230, .66);\r\n    background: rgba(20, 35, 60, .92);\r\n}\r\n.activation-log-button {\r\n    border: 1px solid rgba(135, 159, 198, .42);\r\n    background: rgba(13, 24, 43, .86);\r\n    padding: 0 14px;\r\n}\r\n.activation-log-button:hover {\r\n    border-color: rgba(176, 198, 230, .66);\r\n    background: rgba(20, 35, 60, .92);\r\n}\r\n.activation-log-button:focus-visible {\r\n    outline: 3px solid var(--activation-accent-strong);\r\n    outline-offset: 3px;\r\n}\r\n.activation-log-button:disabled {\r\n    opacity: .65;\r\n    cursor: wait;\r\n}\r\n.activation-log-status {\r\n    margin: 8px 0 0;\r\n    font-size: 13px;\r\n    color: #d9e5f5;\r\n    overflow-wrap: anywhere;\r\n}\r\n.activation-log-button[hidden],\r\n.activation-log-status[hidden] {\r\n    display: none;\r\n}\r\n.library-page {\r\n    max-width: 1480px;\r\n    margin: 0 auto;\r\n    padding: 24px;\r\n}\r\n\r\n.library-hero,\r\n.library-card,\r\n.library-section-heading,\r\n.library-gallery-strip,\r\n.library-metric-item {\r\n    border: 1px solid var(--border);\r\n}\r\n\r\n.library-hero {\r\n    display: grid;\r\n    grid-template-columns: minmax(320px, 1.1fr) minmax(320px, 0.9fr);\r\n    gap: 18px;\r\n    padding: 18px;\r\n    background: linear-gradient(180deg, rgba(24, 34, 46, 0.92), rgba(17, 24, 34, 0.98));\r\n    box-shadow: var(--shadow);\r\n}\r\n\r\n.library-hero-media,\r\n.library-hero-panel {\r\n    min-width: 0;\r\n}\r\n\r\n.library-cover-shell {\r\n    background: #0b1017;\r\n    min-height: 340px;\r\n    overflow: hidden;\r\n    position: relative;\r\n}\r\n\r\n.library-gallery {\r\n    width: 100%;\r\n}\r\n\r\n.library-cover-shell img {\r\n    width: 100%;\r\n    height: 100%;\r\n    object-fit: cover;\r\n    display: block;\r\n}\r\n\r\n.library-cover-empty {\r\n    display: grid;\r\n    place-items: center;\r\n    min-height: 340px;\r\n    color: var(--muted);\r\n    letter-spacing: 0.14em;\r\n    font-size: 0.78rem;\r\n}\r\n\r\n.library-gallery-strip {\r\n    display: grid;\r\n    grid-template-columns: repeat(auto-fit, minmax(min(100%, 108px), 1fr));\r\n    gap: 12px;\r\n    margin-top: 14px;\r\n    padding: 12px;\r\n    background: rgba(27, 37, 49, 0.9);\r\n}\r\n\r\n.library-gallery-button {\r\n    position: absolute;\r\n    top: 50%;\r\n    transform: translateY(-50%);\r\n    z-index: 2;\r\n    width: 44px;\r\n    height: 44px;\r\n    border: 1px solid rgba(255, 255, 255, 0.12);\r\n    background: rgba(10, 15, 22, 0.72);\r\n    color: var(--text);\r\n    font-size: 1.25rem;\r\n    font-weight: 700;\r\n    cursor: pointer;\r\n}\r\n\r\n.library-gallery-button[data-gallery-step=\"-1\"] {\r\n    left: 14px;\r\n}\r\n\r\n.library-gallery-button[data-gallery-step=\"1\"] {\r\n    right: 14px;\r\n}\r\n\r\n.library-gallery-thumb {\r\n    padding: 0;\r\n    border: 1px solid rgba(255, 255, 255, 0.08);\r\n    background: transparent;\r\n    cursor: pointer;\r\n}\r\n\r\n.library-gallery-thumb.is-active {\r\n    border-color: rgba(62, 186, 125, 0.94);\r\n    box-shadow: inset 0 0 0 1px rgba(62, 186, 125, 0.42);\r\n}\r\n\r\n.library-gallery-thumb img {\r\n    width: 100%;\r\n    aspect-ratio: 16 / 9;\r\n    object-fit: cover;\r\n    display: block;\r\n}\r\n\r\n.library-hero-panel {\r\n    display: flex;\r\n    flex-direction: column;\r\n    gap: 14px;\r\n    padding: 6px 4px;\r\n}\r\n\r\n.library-kicker {\r\n    align-self: flex-start;\r\n    padding: 8px 12px;\r\n    background: var(--accent-soft);\r\n    color: #ffeef0;\r\n    font-size: 0.78rem;\r\n    font-weight: 700;\r\n    letter-spacing: 0.08em;\r\n}\r\n\r\n.library-title {\r\n    margin: 0;\r\n    font-size: clamp(2rem, 3vw, 2.7rem);\r\n    line-height: 1.12;\r\n}\r\n\r\n.library-summary {\r\n    margin: 0;\r\n    color: var(--muted);\r\n    line-height: 1.7;\r\n    font-size: 1rem;\r\n}\r\n\r\n.library-chip-row,\r\n.library-tag-row,\r\n.library-action-row {\r\n    display: flex;\r\n    flex-wrap: wrap;\r\n    gap: 10px;\r\n}\r\n\r\n.library-chip {\r\n    display: inline-flex;\r\n    align-items: center;\r\n    padding: 8px 12px;\r\n    background: rgba(64, 81, 103, 0.45);\r\n    border: 1px solid rgba(255, 255, 255, 0.08);\r\n    color: var(--text);\r\n    font-size: 0.82rem;\r\n    font-weight: 600;\r\n}\r\n\r\n.library-metric-grid {\r\n    display: grid;\r\n    grid-template-columns: repeat(2, minmax(0, 1fr));\r\n    gap: 12px;\r\n}\r\n\r\n.library-metric-item {\r\n    padding: 14px;\r\n    background: rgba(23, 31, 42, 0.92);\r\n}\r\n\r\n.library-metric-label {\r\n    display: block;\r\n    color: var(--muted);\r\n    font-size: 0.78rem;\r\n    margin-bottom: 8px;\r\n    text-transform: uppercase;\r\n    letter-spacing: 0.08em;\r\n}\r\n\r\n.library-metric-value {\r\n    display: block;\r\n    font-size: 1.08rem;\r\n    font-weight: 700;\r\n    line-height: 1.35;\r\n}\r\n\r\n.library-metric-item.is-price .library-metric-value {\r\n    color: var(--link);\r\n}\r\n\r\n.library-metric-item.is-positive .library-metric-value {\r\n    color: var(--success);\r\n}\r\n\r\n.library-metric-item.is-warning .library-metric-value {\r\n    color: var(--warning);\r\n}\r\n\r\n.library-button {\r\n    min-width: 164px;\r\n    padding: 14px 18px;\r\n    border: 1px solid var(--border-strong);\r\n    background: #202a37;\r\n    color: var(--text);\r\n    font: inherit;\r\n    font-weight: 700;\r\n    cursor: pointer;\r\n    display: inline-flex;\r\n    align-items: center;\r\n    justify-content: center;\r\n    gap: 10px;\r\n    transition: opacity 0.18s ease, filter 0.18s ease;\r\n}\r\n\r\n.library-button--primary {\r\n    border-color: #2f935f;\r\n    background: linear-gradient(135deg, #3eba7d, #2f935f);\r\n}\r\n\r\n.library-button--cloud-save {\r\n    min-width: 176px;\r\n    border-color: rgba(89, 196, 235, .58);\r\n    background: linear-gradient(135deg, #246f98, #184b6a);\r\n    color: #eefaff;\r\n}\r\n\r\n.library-button--guide {\r\n    min-width: 132px;\r\n    border-color: rgba(82, 224, 166, .52);\r\n    background: linear-gradient(135deg, rgba(37, 107, 83, .96), rgba(20, 70, 56, .96));\r\n    color: #d9ffef;\r\n    animation: denuvoGuideReveal .2s ease-out;\r\n}\r\n\r\n.library-button--guide::before {\r\n    content: \"?\";\r\n    display: inline-grid;\r\n    width: 19px;\r\n    height: 19px;\r\n    place-items: center;\r\n    border: 1px solid rgba(217, 255, 239, .72);\r\n    border-radius: 50%;\r\n    font-size: 12px;\r\n    line-height: 1;\r\n}\r\n\r\n@keyframes denuvoGuideReveal {\r\n    from {\r\n        opacity: 0;\r\n        transform: translateY(-4px) scale(.96);\r\n    }\r\n    to {\r\n        opacity: 1;\r\n        transform: translateY(0) scale(1);\r\n    }\r\n}\r\n\r\n.library-button:disabled {\r\n    cursor: wait;\r\n    opacity: 0.78;\r\n    filter: saturate(0.88);\r\n}\r\n\r\n.library-button.is-cooldown:disabled {\r\n    cursor: default;\r\n}\r\n\r\n.library-button.is-cooldown::before {\r\n    content: \"\\1F512\";\r\n    font-size: .95em;\r\n    line-height: 1;\r\n}\r\n\r\n.library-button.is-loading::before {\r\n    content: \"\";\r\n    width: 15px;\r\n    height: 15px;\r\n    border: 2px solid rgba(255, 255, 255, 0.32);\r\n    border-top-color: rgba(255, 255, 255, 0.96);\r\n    border-radius: 999px;\r\n    animation: library-spin 0.82s linear infinite;\r\n}\r\n\r\n@keyframes library-spin {\r\n    to {\r\n        transform: rotate(360deg);\r\n    }\r\n}\r\n\r\n.library-section {\r\n    margin-top: 22px;\r\n}\r\n\r\n.library-section-heading {\r\n    display: flex;\r\n    flex-direction: column;\r\n    gap: 6px;\r\n    margin-bottom: 14px;\r\n    padding: 16px 18px;\r\n    background: rgba(21, 28, 38, 0.84);\r\n}\r\n\r\n.library-section-heading h2,\r\n.library-card h3 {\r\n    margin: 0;\r\n}\r\n\r\n.library-section-heading p,\r\n.library-card p {\r\n    margin: 0;\r\n    color: var(--muted);\r\n    line-height: 1.6;\r\n}\r\n\r\n.library-dual-grid,\r\n.library-tile-grid,\r\n.library-review-list {\r\n    display: grid;\r\n    gap: 14px;\r\n}\r\n\r\n.library-dual-grid {\r\n    grid-template-columns: repeat(2, minmax(0, 1fr));\r\n}\r\n\r\n.library-tile-grid {\r\n    grid-template-columns: repeat(3, minmax(0, 1fr));\r\n}\r\n\r\n.library-card {\r\n    display: flex;\r\n    flex-direction: column;\r\n    gap: 12px;\r\n    padding: 18px;\r\n    background: var(--panel-soft);\r\n    min-width: 0;\r\n}\r\n\r\n.library-subheading {\r\n    margin: 6px 0 0;\r\n    font-size: 0.98rem;\r\n}\r\n\r\n.library-fact-list {\r\n    display: grid;\r\n    gap: 10px;\r\n}\r\n\r\n.library-fact-row {\r\n    display: grid;\r\n    grid-template-columns: minmax(110px, 136px) 1fr;\r\n    gap: 12px;\r\n    align-items: start;\r\n}\r\n\r\n.library-fact-row dt {\r\n    color: var(--muted);\r\n}\r\n\r\n.library-fact-row dd {\r\n    margin: 0;\r\n    font-weight: 600;\r\n    line-height: 1.55;\r\n}\r\n\r\n.library-bullet-list {\r\n    display: grid;\r\n    gap: 10px;\r\n    margin: 0;\r\n    padding-left: 18px;\r\n    color: var(--muted);\r\n}\r\n\r\n.library-copy {\r\n    white-space: pre-wrap;\r\n    line-height: 1.72;\r\n}\r\n\r\n.library-edition-status {\r\n    display: flex;\r\n    flex-wrap: wrap;\r\n    gap: 8px;\r\n}\r\n\r\n.library-chip.is-owned {\r\n    background: rgba(62, 186, 125, 0.18);\r\n    color: #e6fff2;\r\n}\r\n\r\n.library-chip.is-selected {\r\n    background: rgba(84, 126, 214, 0.18);\r\n    color: #eef4ff;\r\n}\r\n\r\n.library-price-stack {\r\n    display: grid;\r\n    gap: 6px;\r\n}\r\n\r\n.library-price-current {\r\n    color: var(--link);\r\n    font-size: 1.18rem;\r\n    font-weight: 700;\r\n}\r\n\r\n.library-price-reference {\r\n    color: var(--muted);\r\n    font-size: 0.85rem;\r\n}\r\n\r\n.library-review-list {\r\n    grid-template-columns: 1fr;\r\n}\r\n\r\n.library-review-card {\r\n    display: grid;\r\n    grid-template-columns: 52px minmax(0, 1fr);\r\n    gap: 14px;\r\n}\r\n\r\n.library-avatar {\r\n    width: 42px;\r\n    height: 42px;\r\n    display: grid;\r\n    place-items: center;\r\n    border: 1px solid var(--border-strong);\r\n    background: #2c394e;\r\n    font-weight: 700;\r\n}\r\n\r\n.library-review-meta {\r\n    color: var(--muted);\r\n    font-size: 0.85rem;\r\n    margin-top: 4px;\r\n}\r\n\r\n@media (max-width: 1180px) {\r\n    .steam-hero {\r\n        min-height: 340px;\r\n    }\r\n\r\n    .steam-game-title {\r\n        max-width: min(720px, 70vw);\r\n        bottom: 78px;\r\n    }\r\n\r\n    .steam-game-title h1 {\r\n        font-size: 46px;\r\n    }\r\n\r\n    .steam-detail-layout {\r\n        grid-template-columns: 1fr;\r\n        width: min(920px, calc(100vw - 34px));\r\n    }\r\n\r\n    .steam-side-column {\r\n        grid-template-columns: repeat(2, minmax(0, 1fr));\r\n    }\r\n\r\n    .library-page {\r\n        padding: 20px;\r\n    }\r\n\r\n    .library-hero,\r\n    .library-dual-grid {\r\n        grid-template-columns: 1fr;\r\n    }\r\n\r\n    .library-tile-grid {\r\n        grid-template-columns: repeat(2, minmax(0, 1fr));\r\n    }\r\n}\r\n\r\n@media (max-width: 760px) {\r\n    .steam-hero {\r\n        min-height: 300px;\r\n    }\r\n\r\n    .steam-game-title {\r\n        left: 18px;\r\n        right: 18px;\r\n        bottom: 82px;\r\n        max-width: none;\r\n    }\r\n\r\n    .steam-game-title h1 {\r\n        font-size: 30px;\r\n    }\r\n\r\n    .steam-install-dock {\r\n        left: 18px;\r\n        right: 18px;\r\n        width: auto;\r\n        max-width: none;\r\n        justify-content: center;\r\n        transform: none;\r\n    }\r\n\r\n    .steam-tabs {\r\n        overflow-x: auto;\r\n        padding: 0 12px;\r\n        scrollbar-width: none;\r\n    }\r\n\r\n    .steam-tabs::-webkit-scrollbar {\r\n        display: none;\r\n    }\r\n\r\n    .steam-detail-layout {\r\n        width: calc(100vw - 24px);\r\n        margin-top: 14px;\r\n    }\r\n\r\n    .steam-side-column {\r\n        grid-template-columns: 1fr;\r\n    }\r\n\r\n    .steam-news-card {\r\n        grid-template-columns: 1fr;\r\n    }\r\n\r\n    .steam-info-item,\r\n    .steam-fact-row {\r\n        grid-template-columns: 1fr;\r\n    }\r\n\r\n    .steam-info-item strong,\r\n    .steam-fact-row strong {\r\n        text-align: left;\r\n        white-space: normal;\r\n    }\r\n\r\n    .activation-overlay {\r\n        padding: 8px;\r\n        align-items: center;\r\n    }\r\n\r\n    .activation-dialog {\r\n        width: 100%;\r\n        max-height: calc(100vh - 16px);\r\n        border-radius: 12px;\r\n    }\r\n\r\n    .activation-dialog.is-locked .activation-dismiss-button,\r\n.activation-dialog.is-locked .activation-actions {\r\n    display: none;\r\n}\r\n\r\n.activation-hero {\r\n        grid-template-columns: 34px minmax(0, 1fr);\r\n        min-height: auto;\r\n        gap: 10px;\r\n        padding: 28px 12px 10px;\r\n        text-align: left;\r\n    }\r\n\r\n    .activation-dismiss-button {\r\n        top: 6px;\r\n        right: 6px;\r\n        width: 28px;\r\n        height: 28px;\r\n        font-size: 24px;\r\n    }\r\n\r\n    .activation-mark,\r\n    .activation-status-symbol {\r\n        width: 34px;\r\n        height: 34px;\r\n    }\r\n\r\n    .activation-status-symbol {\r\n        font-size: 32px;\r\n    }\r\n\r\n    .activation-title {\r\n        font-size: 19px;\r\n    }\r\n\r\n    .activation-summary {\r\n        max-width: none;\r\n        margin-top: 5px;\r\n        font-size: 11.5px;\r\n    }\r\n\r\n    .activation-content {\r\n        padding: 9px 12px 7px;\r\n        overflow-y: auto;\r\n        overflow-x: hidden;\r\n    }\r\n\r\n    .activation-dialog.has-zalo-qr .activation-content {\r\n        grid-template-columns: 1fr;\r\n        gap: 9px;\r\n    }\r\n\r\n    .activation-zalo-qr {\r\n        justify-self: start;\r\n        width: 132px;\r\n        padding: 7px;\r\n    }\r\n\r\n    .activation-phone,\r\n    .activation-section-title {\r\n        display: none;\r\n    }\r\n\r\n    .activation-metric-grid {\r\n        grid-template-columns: 1fr;\r\n        gap: 7px;\r\n        margin-bottom: 8px;\r\n    }\r\n\r\n    .activation-metric {\r\n        min-height: 48px;\r\n        padding: 8px 10px;\r\n    }\r\n\r\n    .activation-metric span {\r\n        font-size: 9.5px;\r\n    }\r\n\r\n    .activation-metric strong {\r\n        margin-top: 4px;\r\n        font-size: 14px;\r\n    }\r\n\r\n    .activation-path-row {\r\n        grid-template-columns: 1fr;\r\n        gap: 5px;\r\n    }\r\n\r\n    .activation-message-list li {\r\n        padding-left: 15px;\r\n        font-size: 12px;\r\n    }\r\n\r\n    .activation-message-list li::before {\r\n        top: 6px;\r\n        width: 5px;\r\n        height: 5px;\r\n    }\r\n\r\n    .activation-actions {\r\n        flex-direction: row;\r\n        gap: 8px;\r\n        padding: 9px 12px 11px;\r\n    }\r\n\r\n    .activation-primary-button,\r\n    .activation-close-button {\r\n        width: 100%;\r\n        min-width: 0;\r\n        height: 34px;\r\n        font-size: 13px;\r\n    }\r\n\r\n    .library-page {\r\n        padding: 16px;\r\n    }\r\n\r\n    .library-hero,\r\n    .library-section-heading,\r\n    .library-card {\r\n        padding: 14px;\r\n    }\r\n\r\n    .library-metric-grid,\r\n    .library-tile-grid {\r\n        grid-template-columns: 1fr;\r\n    }\r\n\r\n    .library-fact-row,\r\n    .library-review-card {\r\n        grid-template-columns: 1fr;\r\n    }\r\n\r\n    .library-avatar {\r\n        margin-bottom: 6px;\r\n    }\r\n}";
	}

	private static string BuildScript(DesktopLibraryDetailResponse detail, bool showPypassGuide, bool showDenuvoGuide, string? minimizedDenuvoGuidePayloadJson)
	{
		string value = BuildInitialGuidePayload(detail, showPypassGuide, showDenuvoGuide);
		string value2 = (string.IsNullOrWhiteSpace(minimizedDenuvoGuidePayloadJson) ? "null" : minimizedDenuvoGuidePayloadJson);
		string value3 = JsonSerializer.Serialize(StoreSupportContact.QrDataUri, JsonOptions);
		return $"const bridge = window.chrome?.webview;\r\nconst playButton = document.querySelector('[data-action=\"play\"]');\r\nconst cloudSaveButton = document.querySelector('[data-action=\"cloudsave:open\"]');\r\nconst denuvoButton = document.querySelector('[data-action^=\"denuvo:\"]');\r\nconst denuvoGuideButton = document.querySelector('[data-denuvo-guide-button]');\r\nconst pypassButton = document.querySelector('[data-action^=\"pypass:\"]');\r\nconst onlineButton = document.querySelector('[data-action^=\"online:\"]');\r\nconst accountToggle = document.querySelector('[data-account-toggle]');\r\nconst accountMenu = document.querySelector('[data-account-menu]');\r\nconst initialActivationGuide = {value};\r\nconst initialMinimizedDenuvoGuide = {value2};\r\nconst zaloQrDataUri = {value3};\r\nlet activationOverlay = null;\r\nlet cloudSaveChoiceOverlay = null;\r\nlet cloudSavePreviousFocus = null;\r\nlet cloudSaveRestoreOverlay = null;\r\nlet cloudSaveRestoreVersions = [];\r\nlet cloudSaveSelectedRestore = null;\r\nlet activationAutoCloseTimer = null;\r\nlet activationCloseLocked = false;\r\nlet activeActivationPayload = null;\r\nlet minimizedDenuvoGuidePayload = null;\r\nlet denuvoCooldownTimer = null;\r\n\r\nconst denuvoRealtimeLibraryId = {JsonSerializer.Serialize(detail.LibraryId)};\r\nlet denuvoRealtimeRevision = -1;\r\nwindow.__nestgDenuvoRealtime = {{\r\n    update(payload) {{\r\n        const status = document.querySelector('[data-denuvo-realtime-status]');\r\n        if (!status || !payload || !Number.isFinite(payload.revision) || payload.revision < denuvoRealtimeRevision) return;\r\n        if (payload.libraryId && String(payload.libraryId).toLowerCase() !== denuvoRealtimeLibraryId.toLowerCase()) return;\r\n        const copy = {{\r\n            stopped: 'Kết nối nạp game đã dừng.',\r\n            connecting: 'Đang kết nối để nạp game...',\r\n            retrying: 'Kết nối bị gián đoạn. Launcher đang tự kết nối lại...',\r\n            connected: 'Đã kết nối. Đang đồng bộ trạng thái game...',\r\n            presence_pending: 'Đã kết nối. Đang đồng bộ trạng thái game...',\r\n            tracked: 'Đã kết nối, sẵn sàng gửi yêu cầu nạp game.',\r\n            presence_rejected: 'Phiên hiện tại chưa thể theo dõi game này. Hãy kiểm tra tài khoản và quyền sở hữu game.',\r\n            auth_required: 'Phiên đăng nhập không còn hợp lệ. Hãy đăng nhập lại để nạp game.'\r\n        }};\r\n        const text = copy[payload.stage];\r\n        if (!text) return;\r\n        denuvoRealtimeRevision = payload.revision;\r\n        if (status.textContent !== text) status.textContent = text;\r\n        status.dataset.state = payload.stage;\r\n    }}\r\n}};\r\n\r\nwindow.__nestgHome = {{\r\n    setCartCount() {{}},\r\n    setWalletBalance() {{}},\r\n    toast() {{}}\r\n}};\r\n\r\nconst ensureActivationOverlay = () => {{\r\n    if (activationOverlay) {{\r\n        return activationOverlay;\r\n    }}\r\n\r\n    activationOverlay = document.createElement('div');\r\n    activationOverlay.className = 'activation-overlay';\r\n    activationOverlay.hidden = true;\r\n    activationOverlay.setAttribute('data-activation-overlay', '');\r\n    activationOverlay.innerHTML = `\r\n        <section class=\"activation-dialog\" role=\"dialog\" aria-modal=\"true\" aria-labelledby=\"activation-title\" tabindex=\"-1\">\r\n            <button type=\"button\" class=\"activation-dismiss-button\" data-activation-close aria-label=\"Đóng\">&times;</button>\r\n            <header class=\"activation-hero\">\r\n                <div class=\"activation-mark\" aria-hidden=\"true\"><span class=\"activation-status-symbol\" data-activation-symbol>!</span></div>\r\n                <div class=\"activation-title-block\">\r\n                    <div class=\"activation-game\" data-activation-game></div>\r\n                    <h2 class=\"activation-title\" id=\"activation-title\" data-activation-title></h2>\r\n                    <p class=\"activation-meta\" data-activation-meta></p>\r\n                    <p class=\"activation-summary\" data-activation-summary></p>\r\n                </div>\r\n                <div class=\"activation-badge\" data-activation-badge></div>\r\n            </header>\r\n            <div class=\"activation-content\">\r\n                <div class=\"activation-main-column\">\r\n                    <h3 class=\"activation-section-title\">Th\\u00f4ng tin k\\u00edch ho\\u1ea1t</h3>\r\n                    <div class=\"activation-loader\" data-activation-loader aria-live=\"polite\" hidden>\r\n                        <span class=\"activation-loader-spinner\" aria-hidden=\"true\"></span>\r\n                        <div class=\"activation-loader-body\">\r\n                            <div class=\"activation-loader-heading\">\r\n                                <strong data-activation-loader-text></strong>\r\n                                <b class=\"activation-loader-percent\" data-activation-loader-percent>0%</b>\r\n                            </div>\r\n                            <div class=\"activation-progress-track\"\r\n                                 data-activation-progress\r\n                                 role=\"progressbar\"\r\n                                 aria-label=\"Ti\\u1ebfn \\u0111\\u1ed9 t\\u1ea3i package\"\r\n                                 aria-valuemin=\"0\"\r\n                                 aria-valuemax=\"100\"\r\n                                 aria-valuenow=\"0\"\r\n                                 hidden>\r\n                                <i class=\"activation-progress-fill\" data-activation-progress-fill></i>\r\n                            </div>\r\n                        </div>\r\n                    </div>\r\n                    <div class=\"activation-phone\" data-activation-phone hidden></div>\r\n                    <div class=\"activation-metric-grid\" data-activation-metrics></div>\r\n                    <div class=\"activation-path-list\" data-activation-paths></div>\r\n                    <article class=\"activation-guide\" data-activation-guide hidden></article>\r\n                    <ul class=\"activation-message-list\" data-activation-messages></ul>\r\n                    <button type=\"button\" class=\"activation-details-toggle\" data-activation-details-toggle hidden>Xem chi tiết</button>\r\n                    <pre class=\"activation-technical-details\" data-activation-technical-details hidden></pre>\r\n                    <p class=\"activation-log-status\" data-activation-log-status role=\"status\" aria-live=\"polite\" hidden></p>\r\n                </div>\r\n                <aside class=\"activation-zalo-qr\" data-activation-zalo-qr hidden>\r\n                    <img data-activation-zalo-qr-image alt=\"QR Zalo hỗ trợ\" />\r\n                    <span>Qu\\u00e9t QR Zalo h\\u1ed7 tr\\u1ee3</span>\r\n                </aside>\r\n            </div>\r\n            <footer class=\"activation-actions\">\r\n                <button type=\"button\" class=\"activation-log-button\" data-activation-log-download hidden>Tải file log</button>\r\n                <button type=\"button\" class=\"activation-primary-button\" data-activation-primary>Ki\\u1ec3m tra l\\u1ea1i</button>\r\n                <button type=\"button\" class=\"activation-close-button\" data-activation-close>\\u0110\\u00f3ng</button>\r\n            </footer>\r\n        </section>`;\r\n    document.body.append(activationOverlay);\r\n    return activationOverlay;\r\n}};\r\n\r\nconst normalizeVietnameseText = value => {{\r\n    const text = String(value || '');\r\n    if (!text || !/[\\u00c2\\u00c3\\u00c4\\u00c5\\u00c6]|\\u00e1[\\u00ba\\u00bb]/.test(text)) {{\r\n        return text;\r\n    }}\r\n\r\n    try {{\r\n        const cp1252 = new Map([\r\n            [0x20ac, 0x80], [0x201a, 0x82], [0x0192, 0x83], [0x201e, 0x84],\r\n            [0x2026, 0x85], [0x2020, 0x86], [0x2021, 0x87], [0x02c6, 0x88],\r\n            [0x2030, 0x89], [0x0160, 0x8a], [0x2039, 0x8b], [0x0152, 0x8c],\r\n            [0x017d, 0x8e], [0x2018, 0x91], [0x2019, 0x92], [0x201c, 0x93],\r\n            [0x201d, 0x94], [0x2022, 0x95], [0x2013, 0x96], [0x2014, 0x97],\r\n            [0x02dc, 0x98], [0x2122, 0x99], [0x0161, 0x9a], [0x203a, 0x9b],\r\n            [0x0153, 0x9c], [0x017e, 0x9e], [0x0178, 0x9f]\r\n        ]);\r\n        const bytes = [];\r\n        for (const char of text) {{\r\n            const code = char.codePointAt(0);\r\n            if (code <= 0xff) {{\r\n                bytes.push(code);\r\n                continue;\r\n            }}\r\n            const mapped = cp1252.get(code);\r\n            if (mapped === undefined) {{\r\n                return text;\r\n            }}\r\n            bytes.push(mapped);\r\n        }}\r\n        return new TextDecoder('utf-8', {{ fatal: true }}).decode(new Uint8Array(bytes));\r\n    }} catch {{\r\n        return text;\r\n    }}\r\n}};\r\n\r\nconst setActivationText = (selector, value) => {{\r\n    const element = activationOverlay?.querySelector(selector);\r\n    if (element) {{\r\n        element.textContent = normalizeVietnameseText(value);\r\n    }}\r\n}};\r\n\r\nconst findActivationPhone = payload => {{\r\n    const values = [\r\n        payload?.phoneText,\r\n        payload?.summary,\r\n        payload?.loaderText,\r\n        ...(Array.isArray(payload?.messages) ? payload.messages : [])\r\n    ];\r\n    for (const value of values) {{\r\n        const match = String(value || '').match(/\\b0\\d{{9,10}}\\b/);\r\n        if (match) {{\r\n            return match[0];\r\n        }}\r\n    }}\r\n    return '';\r\n}};\r\n\r\nconst trimActivationText = (value, fallback = '', limit = 170) => {{\r\n    const text = normalizeVietnameseText(value || '').replace(/\\s+/g, ' ').trim();\r\n    const safeText = text || fallback;\r\n    if (!safeText || safeText.length <= limit) {{\r\n        return safeText;\r\n    }}\r\n    return `${{safeText.slice(0, Math.max(0, limit - 3)).trim()}}...`;\r\n}};\r\n\r\nconst getActivationStateCopy = state => {{\r\n    if (state === 'success') {{\r\n        return {{\r\n            title: 'K\\u00edch ho\\u1ea1t th\\u00e0nh c\\u00f4ng',\r\n            summary: 'Game \\u0111\\u00e3 s\\u1eb5n s\\u00e0ng. B\\u1ea1n c\\u00f3 th\\u1ec3 ti\\u1ebfp t\\u1ee5c t\\u1eeb launcher.',\r\n            symbol: '\\u2713',\r\n            primary: 'Ho\\u00e0n t\\u1ea5t',\r\n            status: 'Ho\\u00e0n t\\u1ea5t'\r\n        }};\r\n    }}\r\n\r\n    if (state === 'warning') {{\r\n        return {{\r\n            title: 'C\\u1ea7n ki\\u1ec3m tra',\r\n            summary: 'Launcher c\\u1ea7n th\\u00eam m\\u1ed9t b\\u01b0\\u1edbc x\\u00e1c nh\\u1eadn tr\\u01b0\\u1edbc khi ho\\u00e0n t\\u1ea5t.',\r\n            symbol: '!',\r\n            primary: '\\u0110\\u00e3 hi\\u1ec3u',\r\n            status: 'C\\u1ea7n ki\\u1ec3m tra'\r\n        }};\r\n    }}\r\n\r\n    if (state === 'loading') {{\r\n        return {{\r\n            title: '\\u0110ang x\\u1eed l\\u00fd',\r\n            summary: 'Vui l\\u00f2ng gi\\u1eef launcher m\\u1edf trong l\\u00fac thao t\\u00e1c \\u0111ang ch\\u1ea1y.',\r\n            symbol: '...',\r\n            primary: '\\u1ea8n th\\u00f4ng b\\u00e1o',\r\n            status: '\\u0110ang x\\u1eed l\\u00fd'\r\n        }};\r\n    }}\r\n\r\n    return {{\r\n        title: 'Kh\\u00f4ng th\\u1ec3 k\\u00edch ho\\u1ea1t',\r\n        summary: 'Kh\\u00f4ng th\\u1ec3 ho\\u00e0n t\\u1ea5t thao t\\u00e1c. Vui l\\u00f2ng ki\\u1ec3m tra l\\u1ea1i sau.',\r\n        symbol: '!',\r\n        primary: 'Ki\\u1ec3m tra l\\u1ea1i',\r\n        status: 'C\\u1ea7n th\\u1eed l\\u1ea1i'\r\n    }};\r\n}};\r\n\r\nconst pickActivationMetrics = (payload, state, copy) => {{\r\n    const metrics = Array.isArray(payload?.metrics) ? payload.metrics : [];\r\n    const findMetric = pattern => metrics.find(metric => pattern.test(normalizeVietnameseText(metric?.label || '').toLowerCase()));\r\n    const statusMetric = findMetric(/trang|status/) || metrics[0] || {{\r\n        label: 'Tr\\u1ea1ng th\\u00e1i',\r\n        value: copy.status,\r\n        tone: state\r\n    }};\r\n    const versionMetric = findMetric(/phi|version/) || metrics.find(metric => metric !== statusMetric) || {{\r\n        label: 'Phi\\u00ean b\\u1ea3n',\r\n        value: 'Ch\\u01b0a x\\u00e1c \\u0111\\u1ecbnh',\r\n        tone: 'default'\r\n    }};\r\n\r\n    return [statusMetric, versionMetric]\r\n        .filter(Boolean)\r\n        .slice(0, 2)\r\n        .map(metric => ({{\r\n            label: trimActivationText(metric.label, '-', 42),\r\n            value: trimActivationText(metric.value, '-', 30),\r\n            tone: metric.tone || (metric === statusMetric ? state : 'default')\r\n        }}));\r\n}};\r\n\r\nconst pickActivationMessages = (payload, state, summary) => {{\r\n    const sourceMessages = Array.isArray(payload?.messages) ? payload.messages : [];\r\n    const messages = [];\r\n    for (const rawMessage of sourceMessages) {{\r\n        const message = trimActivationText(rawMessage, '', payload?.fullSummary ? 240 : 96);\r\n        if (!message || message.toLowerCase() === summary.toLowerCase()) {{\r\n            continue;\r\n        }}\r\n        if (!messages.some(existing => existing.toLowerCase() === message.toLowerCase())) {{\r\n            messages.push(message);\r\n        }}\r\n        const maxMessages = payload?.adminStatus ? 3 : 2;\r\n        if (messages.length >= maxMessages) {{\r\n            break;\r\n        }}\r\n    }}\r\n\r\n    if (messages.length === 0 && state === 'error') {{\r\n        messages.push('H\\u00e3y ch\\u1edd game t\\u1ea3i xong, ki\\u1ec3m tra k\\u1ebft n\\u1ed1i v\\u00e0 th\\u1eed l\\u1ea1i.');\r\n    }}\r\n\r\n    if (messages.length === 0 && state === 'success') {{\r\n        messages.push('Thao t\\u00e1c \\u0111\\u00e3 ho\\u00e0n t\\u1ea5t.');\r\n    }}\r\n\r\n    return messages.slice(0, payload?.adminStatus ? 3 : 2);\r\n}};\r\n\r\nconst renderActivationGuide = (guideRoot, payload, state) => {{\r\n    guideRoot.textContent = '';\r\n    const guide = payload?.guide;\r\n    if (state !== 'success' || !guide || typeof guide !== 'object') {{\r\n        guideRoot.hidden = true;\r\n        return false;\r\n    }}\r\n\r\n    const title = trimActivationText(guide.title, '', 90);\r\n    const intro = trimActivationText(guide.intro, '', 220);\r\n    const steps = Array.isArray(guide.steps)\r\n        ? guide.steps\r\n            .map(step => ({{\r\n                title: trimActivationText(step?.title, '', 80),\r\n                body: trimActivationText(step?.body, '', 150)\r\n            }}))\r\n            .filter(step => step.title || step.body)\r\n            .slice(0, 5)\r\n        : [];\r\n    const images = Array.isArray(guide.images)\r\n        ? guide.images\r\n            .filter(image => String(image?.url || '').trim().length > 0)\r\n            .slice(0, 3)\r\n        : [];\r\n\r\n    if (!title && !intro && steps.length === 0 && images.length === 0) {{\r\n        guideRoot.hidden = true;\r\n        return false;\r\n    }}\r\n\r\n    if (title) {{\r\n        const heading = document.createElement('h3');\r\n        heading.textContent = title;\r\n        guideRoot.append(heading);\r\n    }}\r\n\r\n    if (intro) {{\r\n        const summary = document.createElement('p');\r\n        summary.textContent = intro;\r\n        guideRoot.append(summary);\r\n    }}\r\n\r\n    if (steps.length > 0) {{\r\n        const list = document.createElement('ol');\r\n        steps.forEach(step => {{\r\n            const item = document.createElement('li');\r\n            if (step.title) {{\r\n                const strong = document.createElement('strong');\r\n                strong.textContent = step.title;\r\n                item.append(strong);\r\n            }}\r\n            if (step.body) {{\r\n                const body = document.createElement('p');\r\n                body.textContent = step.body;\r\n                item.append(body);\r\n            }}\r\n            list.append(item);\r\n        }});\r\n        guideRoot.append(list);\r\n    }}\r\n\r\n    if (images.length > 0) {{\r\n        const imageGrid = document.createElement('div');\r\n        imageGrid.className = 'activation-guide-images';\r\n        images.forEach(image => {{\r\n            const figure = document.createElement('figure');\r\n            const img = document.createElement('img');\r\n            img.src = String(image.url || '');\r\n            img.alt = trimActivationText(image.alt || image.caption, 'Huong dan Denuvo', 80);\r\n            figure.append(img);\r\n            const caption = trimActivationText(image.caption, '', 90);\r\n            if (caption) {{\r\n                const figcaption = document.createElement('figcaption');\r\n                figcaption.textContent = caption;\r\n                figure.append(figcaption);\r\n            }}\r\n            imageGrid.append(figure);\r\n        }});\r\n        guideRoot.append(imageGrid);\r\n    }}\r\n\r\n    guideRoot.hidden = false;\r\n    return true;\r\n}};\r\nconst renderActivationResult = payload => {{\r\n    const overlay = ensureActivationOverlay();\r\n    const dialog = overlay.querySelector('.activation-dialog');\r\n    const requestedState = String(payload?.state || '').toLowerCase();\r\n    const state = ['success', 'warning', 'loading', 'error'].includes(requestedState)\r\n        ? requestedState\r\n        : 'success';\r\n    const copy = getActivationStateCopy(state);\r\n    const paths = Array.isArray(payload?.paths) ? payload.paths : [];\r\n    const phoneText = findActivationPhone(payload);\r\n    const displayTitle = trimActivationText(payload?.title, copy.title, 72);\r\n    const displaySummary = trimActivationText(payload?.summary, copy.summary, payload?.fullSummary ? 240 : state === 'error' ? 110 : 140);\r\n\r\n    activeActivationPayload = payload || null;\r\n    if (Boolean(payload?.minimizeToGuide) && denuvoGuideButton) {{\r\n        denuvoGuideButton.hidden = true;\r\n    }}\r\n\r\n    activationCloseLocked = Boolean(payload?.lockClose);\r\n    dialog.classList.remove('is-success', 'is-warning', 'is-error', 'is-loading', 'is-minimal-loading', 'is-compact-success', 'is-admin-status', 'is-locked', 'has-guide', 'has-zalo-qr');\r\n    dialog.classList.add(`is-${{state}}`);\r\n    dialog.classList.toggle('is-locked', activationCloseLocked);\r\n    setActivationText('[data-activation-symbol]', copy.symbol);\r\n    setActivationText('[data-activation-game]', payload?.gameName || 'NestG');\r\n    setActivationText('[data-activation-title]', displayTitle);\r\n    setActivationText('[data-activation-meta]', payload?.metaText || '');\r\n    setActivationText('[data-activation-badge]', payload?.badgeText || '');\r\n    setActivationText('[data-activation-summary]', displaySummary);\r\n    setActivationText('[data-activation-primary]', payload?.primaryActionText || copy.primary);\r\n    setActivationText('.activation-close-button', '\\u0110\\u00f3ng');\r\n    const secondaryCloseButton = overlay.querySelector('.activation-close-button');\r\n    if (secondaryCloseButton) secondaryCloseButton.hidden = Boolean(payload?.singleCloseAction);\r\n    const primaryButton = overlay.querySelector('[data-activation-primary]');\r\n    if (primaryButton) {{\r\n        primaryButton.disabled = false;\r\n    }}\r\n    const logButton = overlay.querySelector('[data-activation-log-download]');\r\n    logButton.hidden = state !== 'error' || !/^[a-f0-9]{{32}}$/.test(String(payload?.downloadLogId || ''));\r\n    logButton.disabled = false;\r\n    logButton.textContent = 'Tải file log';\r\n    const logStatus = overlay.querySelector('[data-activation-log-status]');\r\n    logStatus.textContent = '';\r\n    logStatus.hidden = true;\r\n\r\n    const loader = overlay.querySelector('[data-activation-loader]');\r\n    const loaderText = overlay.querySelector('[data-activation-loader-text]');\r\n    const loaderPercent = overlay.querySelector('[data-activation-loader-percent]');\r\n    const progressTrack = overlay.querySelector('[data-activation-progress]');\r\n    const progressFill = overlay.querySelector('[data-activation-progress-fill]');\r\n    const loaderMessage = trimActivationText(payload?.loaderText, state === 'loading' ? copy.summary : '', 150);\r\n    const showLoader = state === 'loading' && loaderMessage.length > 0;\r\n    const rawProgress = payload?.progressPercentage;\r\n    const hasNumericProgress = state === 'loading'\r\n        && rawProgress !== null\r\n        && rawProgress !== undefined\r\n        && rawProgress !== ''\r\n        && Number.isFinite(Number(rawProgress));\r\n    const isIndeterminateProgress = state === 'loading' && Boolean(payload?.progressIndeterminate);\r\n    const hasProgress = hasNumericProgress || isIndeterminateProgress;\r\n    const progressPercentage = hasNumericProgress\r\n        ? Math.max(0, Math.min(100, Math.round(Number(rawProgress))))\r\n        : 0;\r\n    if (loader) {{\r\n        loader.hidden = !showLoader;\r\n        loader.classList.toggle('has-progress', hasProgress);\r\n        loader.classList.toggle('is-indeterminate', isIndeterminateProgress);\r\n    }}\r\n    if (loaderText) {{\r\n        loaderText.textContent = loaderMessage;\r\n    }}\r\n    if (loaderPercent) {{\r\n        loaderPercent.textContent = isIndeterminateProgress\r\n            ? '\\u0110ang chu\\u1ea9n b\\u1ecb'\r\n            : `${{progressPercentage}}%`;\r\n    }}\r\n    if (progressTrack) {{\r\n        progressTrack.hidden = !hasProgress;\r\n        if (isIndeterminateProgress) {{\r\n            progressTrack.removeAttribute('aria-valuenow');\r\n            progressTrack.setAttribute('aria-valuetext', 'Server \\u0111ang chu\\u1ea9n b\\u1ecb package');\r\n        }} else {{\r\n            progressTrack.setAttribute('aria-valuenow', String(progressPercentage));\r\n            progressTrack.setAttribute('aria-valuetext', `${{progressPercentage}}%`);\r\n        }}\r\n    }}\r\n    if (progressFill) {{\r\n        progressFill.style.width = `${{progressPercentage}}%`;\r\n    }}\r\n\r\n    const phoneRoot = overlay.querySelector('[data-activation-phone]');\r\n    if (phoneRoot) {{\r\n        phoneRoot.hidden = phoneText.length === 0;\r\n        phoneRoot.textContent = phoneText;\r\n    }}\r\n\r\n    const metricsRoot = overlay.querySelector('[data-activation-metrics]');\r\n    const displayMetrics = pickActivationMetrics(payload, state, copy);\r\n    metricsRoot.textContent = '';\r\n    metricsRoot.hidden = displayMetrics.length === 0;\r\n    displayMetrics.forEach(metric => {{\r\n        const item = document.createElement('div');\r\n        item.className = `activation-metric is-${{metric.tone || 'default'}}`;\r\n        const body = document.createElement('div');\r\n        const label = document.createElement('span');\r\n        label.textContent = metric.label;\r\n        const value = document.createElement('strong');\r\n        value.textContent = metric.value || '-';\r\n        body.append(label, value);\r\n        item.append(body);\r\n        metricsRoot.append(item);\r\n    }});\r\n\r\n    const pathsRoot = overlay.querySelector('[data-activation-paths]');\r\n    const visiblePaths = state === 'success'\r\n        ? paths.filter(path => String(path?.value || '').trim().length > 0 && String(path?.value || '').trim() !== '-').slice(0, 1)\r\n        : [];\r\n    pathsRoot.textContent = '';\r\n    pathsRoot.hidden = visiblePaths.length === 0;\r\n    visiblePaths.forEach(path => {{\r\n        const row = document.createElement('div');\r\n        row.className = 'activation-path-row';\r\n        const label = document.createElement('span');\r\n        label.textContent = trimActivationText(path.label, '', 42);\r\n        const value = document.createElement('strong');\r\n        value.textContent = trimActivationText(path.value, '-', 120);\r\n        const copyValue = String(path.copyValue || path.actionValue || path.value || '');\r\n        row.append(label, value);\r\n        if (copyValue) {{\r\n            const button = document.createElement('button');\r\n            button.type = 'button';\r\n            button.className = 'activation-path-copy';\r\n            button.textContent = trimActivationText(path.actionText, 'Copy', 24);\r\n            const defaultCopyText = button.textContent;\r\n            const markCopied = () => {{\r\n                button.textContent = '\\u0110\\u00e3 copy';\r\n                window.setTimeout(() => {{\r\n                    button.textContent = defaultCopyText;\r\n                }}, 1400);\r\n            }};\r\n            button.addEventListener('click', async () => {{\r\n                try {{\r\n                    if (bridge?.postMessage) {{\r\n                        bridge.postMessage({{ type: 'copyText', text: copyValue }});\r\n                        markCopied();\r\n                        return;\r\n                    }}\r\n\r\n                    await navigator.clipboard.writeText(copyValue);\r\n                    markCopied();\r\n                }} catch {{\r\n                    button.textContent = 'Không copy được';\r\n                }}\r\n            }});\r\n            row.append(button);\r\n        }}\r\n        pathsRoot.append(row);\r\n    }});\r\n\r\n    const guideRoot = overlay.querySelector('[data-activation-guide]');\r\n    const guideVisible = renderActivationGuide(guideRoot, payload, state);\r\n    dialog.classList.toggle('has-guide', guideVisible);\r\n\r\n    const messagesRoot = overlay.querySelector('[data-activation-messages]');\r\n    messagesRoot.textContent = '';\r\n    const messages = guideVisible ? [] : pickActivationMessages(payload, state, displaySummary);\r\n    messagesRoot.classList.toggle('has-items', messages.length > 0);\r\n    messages.forEach(message => {{\r\n        const item = document.createElement('li');\r\n        item.textContent = message;\r\n        messagesRoot.append(item);\r\n    }});\r\n\r\n    const technicalDetails = Array.isArray(payload?.technicalDetails)\r\n        ? payload.technicalDetails\r\n            .map(value => normalizeVietnameseText(value || '').trim())\r\n            .filter(value => value.length > 0)\r\n        : [];\r\n    const detailsToggle = overlay.querySelector('[data-activation-details-toggle]');\r\n    const detailsRoot = overlay.querySelector('[data-activation-technical-details]');\r\n    if (detailsToggle && detailsRoot) {{\r\n        detailsToggle.hidden = technicalDetails.length === 0;\r\n        detailsToggle.textContent = 'Xem chi tiết';\r\n        detailsRoot.hidden = true;\r\n        detailsRoot.textContent = technicalDetails.join('\\n');\r\n        detailsToggle.onclick = () => {{\r\n            const willShow = detailsRoot.hidden;\r\n            detailsRoot.hidden = !willShow;\r\n            detailsToggle.textContent = willShow ? 'Ẩn chi tiết' : 'Xem chi tiết';\r\n        }};\r\n    }}\r\n\r\n    const showZaloQr = Boolean(payload?.zaloQr) && typeof zaloQrDataUri === 'string' && zaloQrDataUri.length > 0;\r\n    dialog.classList.toggle('has-zalo-qr', showZaloQr);\r\n    const zaloQrRoot = overlay.querySelector('[data-activation-zalo-qr]');\r\n    const zaloQrImage = overlay.querySelector('[data-activation-zalo-qr-image]');\r\n    if (zaloQrRoot) {{\r\n        zaloQrRoot.hidden = !showZaloQr;\r\n    }}\r\n    if (zaloQrImage) {{\r\n        if (showZaloQr) {{\r\n            zaloQrImage.src = zaloQrDataUri;\r\n        }} else {{\r\n            zaloQrImage.removeAttribute('src');\r\n        }}\r\n    }}\r\n\r\n    overlay.hidden = false;\r\n    document.body.classList.add('activation-modal-open');\r\n    const focusTarget = activationCloseLocked ? dialog : overlay.querySelector('[data-activation-primary]');\r\n    focusTarget?.focus?.();\r\n\r\n    if (activationAutoCloseTimer) {{\r\n        window.clearTimeout(activationAutoCloseTimer);\r\n        activationAutoCloseTimer = null;\r\n    }}\r\n\r\n    const autoCloseMilliseconds = Number(payload?.autoCloseMilliseconds || 0);\r\n    if (Number.isFinite(autoCloseMilliseconds) && autoCloseMilliseconds > 0) {{\r\n        activationAutoCloseTimer = window.setTimeout(() => {{\r\n            closeActivationResult();\r\n            activationAutoCloseTimer = null;\r\n        }}, autoCloseMilliseconds);\r\n    }}\r\n}};\r\n\r\nconst closeActivationResult = (force = false) => {{\r\n    if (!activationOverlay) {{\r\n        return;\r\n    }}\r\n\r\n    if (activationCloseLocked && !force) {{\r\n        return;\r\n    }}\r\n\r\n    activationOverlay.hidden = true;\r\n    activationCloseLocked = false;\r\n    document.body.classList.remove('activation-modal-open');\r\n    if (Boolean(activeActivationPayload?.minimizeToGuide)) {{\r\n        minimizeDenuvoGuide(activeActivationPayload, true);\r\n    }}\r\n    activeActivationPayload = null;\r\n    if (activationAutoCloseTimer) {{\r\n        window.clearTimeout(activationAutoCloseTimer);\r\n        activationAutoCloseTimer = null;\r\n    }}\r\n}};\r\n\r\nconst minimizeDenuvoGuide = (payload, focusButton = false) => {{\r\n    if (!Boolean(payload?.minimizeToGuide) || !denuvoGuideButton) {{\r\n        return;\r\n    }}\r\n\r\n    minimizedDenuvoGuidePayload = payload;\r\n    denuvoGuideButton.hidden = false;\r\n    if (focusButton) {{\r\n        denuvoGuideButton.focus?.();\r\n    }}\r\n}};\r\n\r\nconst clearMinimizedDenuvoGuide = () => {{\r\n    minimizedDenuvoGuidePayload = null;\r\n    if (Boolean(activeActivationPayload?.minimizeToGuide)) {{\r\n        activeActivationPayload = null;\r\n    }}\r\n    if (denuvoGuideButton) {{\r\n        denuvoGuideButton.hidden = true;\r\n    }}\r\n}};\r\n\r\nwindow.__nestgActivation = {{\r\n    show: renderActivationResult,\r\n    logDownloadFinished: (id, message) => {{\r\n        if (!activationOverlay || activationOverlay.hidden || activeActivationPayload?.state !== 'error'\r\n            || activeActivationPayload?.downloadLogId !== id) return;\r\n        const button = activationOverlay.querySelector('[data-activation-log-download]');\r\n        button.disabled = false;\r\n        button.textContent = 'Tải file log';\r\n        const status = activationOverlay.querySelector('[data-activation-log-status]');\r\n        status.textContent = String(message || '');\r\n        status.hidden = !status.textContent;\r\n    }},\r\n    close: closeActivationResult,\r\n    minimize: minimizeDenuvoGuide,\r\n    clearMinimizedGuide: clearMinimizedDenuvoGuide\r\n}};\r\n\r\nif (initialActivationGuide) {{\r\n    window.setTimeout(() => renderActivationResult(initialActivationGuide), 220);\r\n}}\r\n\r\nif (initialMinimizedDenuvoGuide) {{\r\n    minimizeDenuvoGuide(initialMinimizedDenuvoGuide);\r\n}}\r\n\r\nconst setAccountMenuOpen = isOpen => {{\r\n    if (!accountToggle || !accountMenu) {{\r\n        return;\r\n    }}\r\n\r\n    accountMenu.hidden = !isOpen;\r\n    accountToggle.classList.toggle('is-open', isOpen);\r\n    accountToggle.setAttribute('aria-expanded', String(isOpen));\r\n}};\r\n\r\nconst applyActivationState = isLoading => {{\r\n    if (!playButton) {{\r\n        return;\r\n    }}\r\n\r\n    const defaultLabel = playButton.getAttribute('data-default-label') ?? 'Active';\r\n    playButton.classList.toggle('is-loading', isLoading);\r\n    playButton.disabled = isLoading;\r\n    playButton.setAttribute('aria-disabled', isLoading ? 'true' : 'false');\r\n    playButton.textContent = isLoading ? 'Loading...' : defaultLabel;\r\n}};\r\n\r\nconst applyCloudSaveState = isLoading => {{\r\n    if (!cloudSaveButton) {{\r\n        return;\r\n    }}\r\n\r\n    const defaultLabel = cloudSaveButton.getAttribute('data-default-label') ?? 'CLOUD SAVE';\r\n    const label = cloudSaveButton.querySelector('span');\r\n    cloudSaveButton.classList.toggle('is-loading', isLoading);\r\n    cloudSaveButton.disabled = isLoading;\r\n    cloudSaveButton.setAttribute('aria-disabled', isLoading ? 'true' : 'false');\r\n    cloudSaveButton.setAttribute('aria-busy', isLoading ? 'true' : 'false');\r\n    if (label) {{\r\n        label.textContent = isLoading ? 'ĐANG XỬ LÝ...' : defaultLabel;\r\n    }}\r\n}};\r\n\r\nwindow.__nestgCloudSave = {{\r\n    setBusy: applyCloudSaveState,\r\n    showVersions: versions => openCloudSaveRestoreVersions(versions)\r\n}};\r\n\r\nconst ensureCloudSaveChoiceOverlay = () => {{\r\n    if (cloudSaveChoiceOverlay) return cloudSaveChoiceOverlay;\r\n    cloudSaveChoiceOverlay = document.createElement('div');\r\n    cloudSaveChoiceOverlay.className = 'cloudsave-choice-overlay';\r\n    cloudSaveChoiceOverlay.hidden = true;\r\n    cloudSaveChoiceOverlay.setAttribute('data-cloudsave-choice-overlay', '');\r\n    cloudSaveChoiceOverlay.innerHTML = `\r\n        <section class=\"cloudsave-choice-dialog\" role=\"dialog\" aria-modal=\"true\" aria-labelledby=\"cloudsave-choice-title\" aria-describedby=\"cloudsave-choice-description\" tabindex=\"-1\">\r\n            <header class=\"cloudsave-choice-header\">\r\n                <div>\r\n                    <h2 id=\"cloudsave-choice-title\">Quản lý bản lưu</h2>\r\n                    <p id=\"cloudsave-choice-description\">Chọn cách quản lý save của game này.</p>\r\n                </div>\r\n                <button type=\"button\" class=\"cloudsave-choice-close\" data-cloudsave-choice-close aria-label=\"Đóng tùy chọn bản lưu\">\r\n                    <svg viewBox=\"0 0 24 24\" width=\"20\" height=\"20\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.9\" stroke-linecap=\"round\" aria-hidden=\"true\"><path d=\"M5 5l14 14M19 5L5 19\"/></svg>\r\n                </button>\r\n            </header>\r\n            <div class=\"cloudsave-choice-grid\">\r\n                <article class=\"cloudsave-choice-card\">\r\n                    <div class=\"cloudsave-choice-card-icon\" aria-hidden=\"true\"><svg viewBox=\"0 0 24 24\" width=\"25\" height=\"25\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.9\" stroke-linecap=\"round\" stroke-linejoin=\"round\"><path d=\"M7.2 18.5H6a4 4 0 0 1-.55-7.96A6.3 6.3 0 0 1 17.6 9.2a4.7 4.7 0 0 1 .4 9.3h-1.2\"/><path d=\"M12 10.5v9M8.8 13.8 12 10.5l3.2 3.3\"/></svg></div>\r\n                    <h3>Sao lưu lên Google Drive</h3>\r\n                    <p>Launcher sẽ thoát game, chờ save đồng bộ và đóng Steam trước khi backup. Hãy lưu tiến độ trong game trước khi tiếp tục.</p>\r\n                    <button type=\"button\" class=\"cloudsave-choice-action\" data-cloudsave-drive>Sao lưu lên Google Drive</button>\r\n                </article>\r\n                <article class=\"cloudsave-choice-card\">\r\n                    <div class=\"cloudsave-choice-card-icon\" aria-hidden=\"true\"><svg viewBox=\"0 0 24 24\" width=\"25\" height=\"25\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.9\" stroke-linecap=\"round\" stroke-linejoin=\"round\"><path d=\"M7.2 18.5H6a4 4 0 0 1-.55-7.96A6.3 6.3 0 0 1 17.6 9.2a4.7 4.7 0 0 1 .4 9.3h-1.2\"/><path d=\"M12 19.5v-9m-3.2 5.7 3.2 3.3 3.2-3.3\"/></svg></div>\r\n                    <h3>Khôi phục bản lưu</h3>\r\n                    <p>Chọn một phiên bản đã lưu trên Drive. Launcher sẽ kiểm tra bản lưu và bảo vệ save hiện tại trước khi thay.</p>\r\n                    <button type=\"button\" class=\"cloudsave-choice-action\" data-cloudsave-restore-open>Chọn bản để khôi phục</button>\r\n                </article>\r\n            </div>\r\n        </section>`;\r\n    document.body.appendChild(cloudSaveChoiceOverlay);\r\n    return cloudSaveChoiceOverlay;\r\n}};\r\n\r\nconst openCloudSaveChoice = () => {{\r\n    if (cloudSaveButton?.disabled || (activationOverlay && !activationOverlay.hidden)) return;\r\n    const overlay = ensureCloudSaveChoiceOverlay();\r\n    cloudSavePreviousFocus = cloudSaveButton;\r\n    overlay.hidden = false;\r\n    document.body.classList.add('cloudsave-choice-open');\r\n    overlay.querySelector('[data-cloudsave-choice-close]')?.focus();\r\n}};\r\n\r\nconst closeCloudSaveChoice = () => {{\r\n    if (!cloudSaveChoiceOverlay || cloudSaveChoiceOverlay.hidden) return;\r\n    cloudSaveChoiceOverlay.hidden = true;\r\n    document.body.classList.remove('cloudsave-choice-open');\r\n    cloudSavePreviousFocus?.focus?.();\r\n    cloudSavePreviousFocus = null;\r\n}};\r\n\r\nconst ensureCloudSaveRestoreOverlay = () => {{\r\n    if (cloudSaveRestoreOverlay) return cloudSaveRestoreOverlay;\r\n    cloudSaveRestoreOverlay = document.createElement('div');\r\n    cloudSaveRestoreOverlay.className = 'cloudsave-restore-overlay';\r\n    cloudSaveRestoreOverlay.hidden = true;\r\n    cloudSaveRestoreOverlay.setAttribute('data-cloudsave-restore-overlay', '');\r\n    cloudSaveRestoreOverlay.innerHTML = `\r\n        <section class=\"cloudsave-restore-dialog\" role=\"dialog\" aria-modal=\"true\" aria-labelledby=\"cloudsave-restore-title\" aria-describedby=\"cloudsave-restore-description\" tabindex=\"-1\">\r\n            <header class=\"cloudsave-restore-header\">\r\n                <h2 id=\"cloudsave-restore-title\">Chọn bản lưu để khôi phục</h2>\r\n                <p id=\"cloudsave-restore-description\">Các phiên bản hoàn tất trong Google Drive của tài khoản đang kết nối.</p>\r\n            </header>\r\n            <div class=\"cloudsave-restore-content\">\r\n                <div class=\"cloudsave-restore-list\" data-cloudsave-restore-list></div>\r\n                <div class=\"cloudsave-restore-warning\" data-cloudsave-restore-warning hidden>\r\n                    <strong>Thay save đang dùng bằng bản đã chọn?</strong>\r\n                    <span data-cloudsave-restore-selected></span>\r\n                    <p>Hãy lưu tiến độ trong game trước khi tiếp tục. Launcher sẽ đóng game và Steam bình thường, giữ bản dự phòng save hiện tại, rồi áp dụng bản đã chọn.</p>\r\n                </div>\r\n            </div>\r\n            <footer class=\"cloudsave-restore-footer\">\r\n                <button type=\"button\" class=\"cloudsave-restore-cancel\" data-cloudsave-restore-cancel>Đóng</button>\r\n                <button type=\"button\" class=\"cloudsave-restore-confirm\" data-cloudsave-restore-confirm hidden>Xác nhận khôi phục</button>\r\n            </footer>\r\n        </section>`;\r\n    document.body.appendChild(cloudSaveRestoreOverlay);\r\n    return cloudSaveRestoreOverlay;\r\n}};\r\n\r\nconst showCloudSaveRestoreList = () => {{\r\n    const overlay = ensureCloudSaveRestoreOverlay();\r\n    const list = overlay.querySelector('[data-cloudsave-restore-list]');\r\n    const warning = overlay.querySelector('[data-cloudsave-restore-warning]');\r\n    const cancel = overlay.querySelector('[data-cloudsave-restore-cancel]');\r\n    const confirm = overlay.querySelector('[data-cloudsave-restore-confirm]');\r\n    cloudSaveSelectedRestore = null;\r\n    list.hidden = false;\r\n    warning.hidden = true;\r\n    confirm.hidden = true;\r\n    cancel.textContent = 'Đóng';\r\n    list.replaceChildren();\r\n    if (!cloudSaveRestoreVersions.length) {{\r\n        const empty = document.createElement('div');\r\n        empty.className = 'cloudsave-restore-empty';\r\n        empty.textContent = 'Chưa có bản lưu hoàn tất cho game này trong tài khoản Google Drive đang kết nối.';\r\n        list.appendChild(empty);\r\n    }} else {{\r\n        for (const version of cloudSaveRestoreVersions) {{\r\n            const button = document.createElement('button');\r\n            button.type = 'button';\r\n            button.className = 'cloudsave-restore-version';\r\n            button.setAttribute('data-cloudsave-restore-version', version.snapshotId);\r\n            const body = document.createElement('span');\r\n            const title = document.createElement('strong');\r\n            title.textContent = version.date;\r\n            const detail = document.createElement('small');\r\n            detail.textContent = `${{version.fileCount}} file · ${{version.size}} · Steam account ${{version.steamAccountId}}`;\r\n            body.append(title, detail);\r\n            const arrow = document.createElementNS('http://www.w3.org/2000/svg', 'svg');\r\n            arrow.setAttribute('aria-hidden', 'true');\r\n            arrow.setAttribute('viewBox', '0 0 20 20');\r\n            arrow.setAttribute('width', '20');\r\n            arrow.setAttribute('height', '20');\r\n            const arrowPath = document.createElementNS('http://www.w3.org/2000/svg', 'path');\r\n            arrowPath.setAttribute('d', 'm7 4 6 6-6 6');\r\n            arrowPath.setAttribute('fill', 'none');\r\n            arrowPath.setAttribute('stroke', 'currentColor');\r\n            arrowPath.setAttribute('stroke-width', '2');\r\n            arrowPath.setAttribute('stroke-linecap', 'round');\r\n            arrowPath.setAttribute('stroke-linejoin', 'round');\r\n            arrow.appendChild(arrowPath);\r\n            button.append(body, arrow);\r\n            list.appendChild(button);\r\n        }}\r\n    }}\r\n    cancel.focus();\r\n}};\r\n\r\nconst openCloudSaveRestoreVersions = versions => {{\r\n    cloudSaveRestoreVersions = Array.isArray(versions) ? versions.filter(item =>\r\n        item && /^[0-9]{{8}}T[0-9]{{9}}Z-[0-9a-f]{{8}}$/.test(item.snapshotId)) : [];\r\n    const overlay = ensureCloudSaveRestoreOverlay();\r\n    overlay.hidden = false;\r\n    document.body.classList.add('cloudsave-restore-open');\r\n    cloudSavePreviousFocus = cloudSaveButton;\r\n    showCloudSaveRestoreList();\r\n}};\r\n\r\nconst closeCloudSaveRestoreOverlay = () => {{\r\n    if (!cloudSaveRestoreOverlay || cloudSaveRestoreOverlay.hidden) return;\r\n    cloudSaveRestoreOverlay.hidden = true;\r\n    document.body.classList.remove('cloudsave-restore-open');\r\n    cloudSaveSelectedRestore = null;\r\n    cloudSavePreviousFocus?.focus?.();\r\n    cloudSavePreviousFocus = null;\r\n}};\r\n\r\nconst selectCloudSaveRestoreVersion = snapshotId => {{\r\n    const chosen = cloudSaveRestoreVersions.find(item => item.snapshotId === snapshotId);\r\n    if (!chosen) return;\r\n    cloudSaveSelectedRestore = chosen;\r\n    const overlay = ensureCloudSaveRestoreOverlay();\r\n    overlay.querySelector('[data-cloudsave-restore-list]').hidden = true;\r\n    overlay.querySelector('[data-cloudsave-restore-warning]').hidden = false;\r\n    overlay.querySelector('[data-cloudsave-restore-selected]').textContent =\r\n        `${{chosen.date}} · ${{chosen.fileCount}} file · ${{chosen.size}} · Steam account ${{chosen.steamAccountId}}`;\r\n    overlay.querySelector('[data-cloudsave-restore-cancel]').textContent = 'Quay lại';\r\n    overlay.querySelector('[data-cloudsave-restore-confirm]').hidden = false;\r\n    overlay.querySelector('[data-cloudsave-restore-cancel]').focus();\r\n}};\r\n\r\nconst formatDenuvoCooldown = remainingMs => {{\r\n    const totalSeconds = Math.max(0, Math.ceil(remainingMs / 1000));\r\n    const hours = Math.floor(totalSeconds / 3600);\r\n    const minutes = Math.floor((totalSeconds % 3600) / 60);\r\n    const seconds = totalSeconds % 60;\r\n    return `${{String(hours).padStart(2, '0')}}:${{String(minutes).padStart(2, '0')}}:${{String(seconds).padStart(2, '0')}}`;\r\n}};\r\n\r\nconst unlockDenuvoCooldown = () => {{\r\n    clearMinimizedDenuvoGuide();\r\n    if (!denuvoButton) {{\r\n        return;\r\n    }}\r\n\r\n    if (denuvoCooldownTimer) {{\r\n        clearInterval(denuvoCooldownTimer);\r\n        denuvoCooldownTimer = null;\r\n    }}\r\n\r\n    const defaultLabel = denuvoButton.getAttribute('data-default-label') ?? 'Y\\u00eau c\\u1ea7u admin n\\u1ea1p game';\r\n    denuvoButton.removeAttribute('data-denuvo-cooldown-until');\r\n    denuvoButton.removeAttribute('data-denuvo-locked');\r\n    denuvoButton.classList.remove('is-cooldown', 'is-loading');\r\n    denuvoButton.disabled = false;\r\n    denuvoButton.setAttribute('aria-disabled', 'false');\r\n    denuvoButton.textContent = normalizeVietnameseText(defaultLabel);\r\n}};\r\n\r\nconst updateDenuvoCooldown = () => {{\r\n    if (!denuvoButton) {{\r\n        return;\r\n    }}\r\n\r\n    const rawUntil = denuvoButton.getAttribute('data-denuvo-cooldown-until');\r\n    if (!rawUntil) {{\r\n        return;\r\n    }}\r\n\r\n    const untilMs = Date.parse(rawUntil);\r\n    if (!Number.isFinite(untilMs)) {{\r\n        unlockDenuvoCooldown();\r\n        return;\r\n    }}\r\n\r\n    const remainingMs = untilMs - Date.now();\r\n    if (remainingMs <= 0) {{\r\n        unlockDenuvoCooldown();\r\n        return;\r\n    }}\r\n\r\n    const prefix = denuvoButton.getAttribute('data-denuvo-cooldown-prefix') ?? '\\u0110\\u00e3 n\\u1ea1p game - c\\u00f2n';\r\n    denuvoButton.setAttribute('data-denuvo-locked', 'true');\r\n    denuvoButton.classList.add('is-cooldown');\r\n    denuvoButton.classList.remove('is-loading');\r\n    denuvoButton.disabled = true;\r\n    denuvoButton.setAttribute('aria-disabled', 'true');\r\n    denuvoButton.textContent = normalizeVietnameseText(`${{prefix}} ${{formatDenuvoCooldown(remainingMs)}}`);\r\n}};\r\n\r\nconst startDenuvoCooldown = cooldownUntil => {{\r\n    if (!denuvoButton) {{\r\n        return;\r\n    }}\r\n\r\n    if (cooldownUntil) {{\r\n        denuvoButton.setAttribute('data-denuvo-cooldown-until', cooldownUntil);\r\n    }}\r\n\r\n    updateDenuvoCooldown();\r\n    if (!denuvoButton.hasAttribute('data-denuvo-cooldown-until')) {{\r\n        return;\r\n    }}\r\n\r\n    if (denuvoCooldownTimer) {{\r\n        clearInterval(denuvoCooldownTimer);\r\n    }}\r\n\r\n    denuvoCooldownTimer = setInterval(updateDenuvoCooldown, 1000);\r\n}};\r\n\r\nconst applyDenuvoState = isLoading => {{\r\n    if (!denuvoButton) {{\r\n        return;\r\n    }}\r\n\r\n    if (denuvoButton.getAttribute('data-denuvo-locked') === 'true') {{\r\n        return;\r\n    }}\r\n\r\n    const defaultLabel = denuvoButton.getAttribute('data-default-label') ?? 'Y\\u00eau c\\u1ea7u admin n\\u1ea1p game';\r\n    denuvoButton.classList.toggle('is-loading', isLoading);\r\n    denuvoButton.disabled = isLoading;\r\n    denuvoButton.setAttribute('aria-disabled', isLoading ? 'true' : 'false');\r\n    denuvoButton.textContent = isLoading ? 'Loading...' : defaultLabel;\r\n}};\r\n\r\nconst applyPypassState = isLoading => {{\r\n    if (!pypassButton) {{\r\n        return;\r\n    }}\r\n\r\n    if (pypassButton.getAttribute('data-pypass-locked') === 'true') {{\r\n        return;\r\n    }}\r\n\r\n    const defaultLabel = pypassButton.getAttribute('data-default-label') ?? 'FIX';\r\n    pypassButton.classList.toggle('is-loading', isLoading);\r\n    pypassButton.disabled = isLoading;\r\n    pypassButton.setAttribute('aria-disabled', isLoading ? 'true' : 'false');\r\n    pypassButton.textContent = isLoading ? 'Loading...' : defaultLabel;\r\n}};\r\n\r\nconst applyOnlineState = isLoading => {{\r\n    if (!onlineButton) {{\r\n        return;\r\n    }}\r\n\r\n    if (onlineButton.getAttribute('data-online-locked') === 'true') {{\r\n        return;\r\n    }}\r\n\r\n    const defaultLabel = onlineButton.getAttribute('data-default-label') ?? 'ONLINE FIX';\r\n    onlineButton.classList.toggle('is-loading', isLoading);\r\n    onlineButton.disabled = isLoading;\r\n    onlineButton.setAttribute('aria-disabled', isLoading ? 'true' : 'false');\r\n    onlineButton.textContent = isLoading ? 'Loading...' : defaultLabel;\r\n}};\r\n\r\napplyActivationState(playButton?.classList.contains('is-loading') ?? false);\r\napplyCloudSaveState(cloudSaveButton?.classList.contains('is-loading') ?? false);\r\napplyDenuvoState(denuvoButton?.classList.contains('is-loading') ?? false);\r\nstartDenuvoCooldown();\r\napplyPypassState(pypassButton?.classList.contains('is-loading') ?? false);\r\napplyOnlineState(onlineButton?.classList.contains('is-loading') ?? false);\r\n\r\nbridge?.addEventListener('message', event => {{\r\n    if (event.data === 'activation:start') {{\r\n        applyActivationState(true);\r\n        return;\r\n    }}\r\n\r\n    if (event.data === 'activation:finish') {{\r\n        applyActivationState(false);\r\n        return;\r\n    }}\r\n\r\n    if (event.data === 'cloudsave:start') {{\r\n        applyCloudSaveState(true);\r\n        return;\r\n    }}\r\n\r\n    if (event.data === 'cloudsave:finish') {{\r\n        applyCloudSaveState(false);\r\n        return;\r\n    }}\r\n\r\n    if (event.data === 'denuvo:start') {{\r\n        applyDenuvoState(true);\r\n        return;\r\n    }}\r\n\r\n    if (event.data === 'denuvo:finish') {{\r\n        applyDenuvoState(false);\r\n        return;\r\n    }}\r\n\r\n    if (typeof event.data === 'string' && event.data.startsWith('denuvo:cooldown:')) {{\r\n        startDenuvoCooldown(event.data.slice('denuvo:cooldown:'.length));\r\n        return;\r\n    }}\r\n\r\n    if (event.data === 'denuvo:unlocked') {{\r\n        unlockDenuvoCooldown();\r\n        return;\r\n    }}\r\n\r\n    if (event.data === 'denuvo:applied') {{\r\n        if (denuvoButton && denuvoButton.getAttribute('data-denuvo-locked') !== 'true') {{\r\n            denuvoButton.setAttribute('data-default-label', 'Y\\u00eau c\\u1ea7u admin n\\u1ea1p game');\r\n            denuvoButton.textContent = 'Y\\u00eau c\\u1ea7u admin n\\u1ea1p game';\r\n            denuvoButton.classList.remove('is-loading');\r\n            denuvoButton.disabled = false;\r\n            denuvoButton.setAttribute('aria-disabled', 'false');\r\n        }}\r\n    }}\r\n\r\n    if (event.data === 'pypass:start') {{\r\n        applyPypassState(true);\r\n        return;\r\n    }}\r\n\r\n    if (event.data === 'pypass:finish') {{\r\n        applyPypassState(false);\r\n        return;\r\n    }}\r\n\r\n    if (event.data === 'pypass:applied') {{\r\n        if (pypassButton && pypassButton.getAttribute('data-pypass-locked') !== 'true') {{\r\n            pypassButton.setAttribute('data-default-label', 'FIX');\r\n            pypassButton.textContent = 'FIX';\r\n            pypassButton.classList.remove('is-loading');\r\n            pypassButton.disabled = false;\r\n            pypassButton.setAttribute('aria-disabled', 'false');\r\n        }}\r\n    }}\r\n\r\n    if (event.data === 'online:start') {{\r\n        applyOnlineState(true);\r\n        return;\r\n    }}\r\n\r\n    if (event.data === 'online:finish') {{\r\n        applyOnlineState(false);\r\n        return;\r\n    }}\r\n\r\n    if (event.data === 'online:applied') {{\r\n        if (onlineButton && onlineButton.getAttribute('data-online-locked') !== 'true') {{\r\n            onlineButton.setAttribute('data-default-label', 'ONLINE FIX');\r\n            onlineButton.textContent = 'ONLINE FIX';\r\n            onlineButton.classList.remove('is-loading');\r\n            onlineButton.disabled = false;\r\n            onlineButton.setAttribute('aria-disabled', 'false');\r\n        }}\r\n    }}\r\n}});\r\n\r\ndocument.querySelectorAll('[data-gallery-root]').forEach(root => {{\r\n    const stage = root.querySelector('[data-gallery-stage]');\r\n    const thumbs = Array.from(root.querySelectorAll('[data-gallery-target]'));\r\n    const prevButton = root.querySelector('[data-gallery-step=\"-1\"]');\r\n    const nextButton = root.querySelector('[data-gallery-step=\"1\"]');\r\n    let activeIndex = Number.parseInt(root.getAttribute('data-gallery-index') ?? '0', 10);\r\n    if (!Number.isFinite(activeIndex)) {{\r\n        activeIndex = 0;\r\n    }}\r\n\r\n    const syncGallery = index => {{\r\n        if (!stage || thumbs.length === 0) {{\r\n            return;\r\n        }}\r\n\r\n        activeIndex = (index + thumbs.length) % thumbs.length;\r\n        root.setAttribute('data-gallery-index', String(activeIndex));\r\n\r\n        thumbs.forEach((thumb, thumbIndex) => {{\r\n            const isActive = thumbIndex === activeIndex;\r\n            thumb.classList.toggle('is-active', isActive);\r\n            if (isActive) {{\r\n                const nextUrl = thumb.getAttribute('data-image-url');\r\n                const nextAlt = thumb.getAttribute('data-image-alt');\r\n                if (nextUrl) {{\r\n                    stage.setAttribute('src', nextUrl);\r\n                }}\r\n                stage.setAttribute('alt', nextAlt ?? '');\r\n                thumb.scrollIntoView({{ block: 'nearest', inline: 'nearest' }});\r\n            }}\r\n        }});\r\n    }};\r\n\r\n    thumbs.forEach((thumb, thumbIndex) => {{\r\n        thumb.addEventListener('click', event => {{\r\n            event.preventDefault();\r\n            syncGallery(thumbIndex);\r\n        }});\r\n    }});\r\n\r\n    prevButton?.addEventListener('click', event => {{\r\n        event.preventDefault();\r\n        syncGallery(activeIndex - 1);\r\n    }});\r\n    nextButton?.addEventListener('click', event => {{\r\n        event.preventDefault();\r\n        syncGallery(activeIndex + 1);\r\n    }});\r\n    syncGallery(activeIndex);\r\n}});\r\n\r\ndocument.addEventListener('pointerdown', event => {{\r\n    const target = event.target instanceof Element ? event.target : null;\r\n    if (!target || target.closest('button, input, select, [data-host-action], [data-action]')) {{\r\n        return;\r\n    }}\r\n\r\n    if (target.closest('[data-drag-region]')) {{\r\n        bridge?.postMessage({{ type: 'dragWindow' }});\r\n    }}\r\n}});\r\n\r\ndocument.addEventListener('click', event => {{\r\n    const target = event.target instanceof Element ? event.target : null;\r\n    if (!target) {{\r\n        return;\r\n    }}\r\n\r\n    if (cloudSaveRestoreOverlay && !cloudSaveRestoreOverlay.hidden) {{\r\n        const version = target.closest('[data-cloudsave-restore-version]');\r\n        if (version) {{\r\n            event.preventDefault();\r\n            selectCloudSaveRestoreVersion(version.getAttribute('data-cloudsave-restore-version'));\r\n            return;\r\n        }}\r\n        if (target.closest('[data-cloudsave-restore-cancel]')) {{\r\n            event.preventDefault();\r\n            if (cloudSaveSelectedRestore) showCloudSaveRestoreList();\r\n            else closeCloudSaveRestoreOverlay();\r\n            return;\r\n        }}\r\n        if (target.closest('[data-cloudsave-restore-confirm]') && cloudSaveSelectedRestore) {{\r\n            event.preventDefault();\r\n            const snapshotId = cloudSaveSelectedRestore.snapshotId;\r\n            closeCloudSaveRestoreOverlay();\r\n            applyCloudSaveState(true);\r\n            bridge?.postMessage(`cloudsave:restore:${{snapshotId}}`);\r\n            return;\r\n        }}\r\n        if (target.matches('[data-cloudsave-restore-overlay]')) {{\r\n            event.preventDefault();\r\n            closeCloudSaveRestoreOverlay();\r\n            return;\r\n        }}\r\n    }}\r\n\r\n    if (cloudSaveChoiceOverlay && !cloudSaveChoiceOverlay.hidden) {{\r\n        if (target.closest('[data-cloudsave-drive]')) {{\r\n            event.preventDefault();\r\n            closeCloudSaveChoice();\r\n            applyCloudSaveState(true);\r\n            bridge?.postMessage('cloudsave:backup');\r\n            return;\r\n        }}\r\n        if (target.closest('[data-cloudsave-restore-open]')) {{\r\n            event.preventDefault();\r\n            closeCloudSaveChoice();\r\n            applyCloudSaveState(true);\r\n            bridge?.postMessage('cloudsave:versions');\r\n            return;\r\n        }}\r\n        if (target.closest('[data-cloudsave-choice-close]') ||\r\n            target.matches('[data-cloudsave-choice-overlay]')) {{\r\n            event.preventDefault();\r\n            closeCloudSaveChoice();\r\n            return;\r\n        }}\r\n    }}\r\n\r\n    if (target.closest('[data-denuvo-guide-button]')) {{\r\n        event.preventDefault();\r\n        if (minimizedDenuvoGuidePayload) {{\r\n            renderActivationResult(minimizedDenuvoGuidePayload);\r\n        }}\r\n        return;\r\n    }}\r\n\r\n    if (target.closest('[data-activation-log-download]')) {{\r\n        event.preventDefault();\r\n        const button = target.closest('[data-activation-log-download]');\r\n        const id = String(activeActivationPayload?.downloadLogId || '');\r\n        if (button.disabled || button.hidden || activeActivationPayload?.state !== 'error'\r\n            || !/^[a-f0-9]{{32}}$/.test(id) || !bridge) return;\r\n        button.disabled = true;\r\n        button.textContent = 'Đang tải log…';\r\n        const status = activationOverlay.querySelector('[data-activation-log-status]');\r\n        status.textContent = '';\r\n        status.hidden = true;\r\n        bridge.postMessage('activation-log:download:' + id);\r\n        return;\r\n    }}\r\n\r\n    if (target.closest('[data-activation-primary]')) {{\r\n        event.preventDefault();\r\n        const cancelAction = String(activeActivationPayload?.cancelAction || '').trim();\r\n        if (cancelAction) {{\r\n            bridge?.postMessage(cancelAction);\r\n            activeActivationPayload = {{ ...activeActivationPayload, cancelAction: null }};\r\n            const primary = target.closest('[data-activation-primary]');\r\n            if (primary) {{\r\n                primary.textContent = '\\u0110ang h\\u1ee7y...';\r\n                primary.disabled = true;\r\n            }}\r\n            return;\r\n        }}\r\n\r\n        if (!activationCloseLocked) {{\r\n            closeActivationResult();\r\n        }}\r\n        return;\r\n    }}\r\n\r\n    if (target.closest('[data-activation-close]')) {{\r\n        event.preventDefault();\r\n        if (!activationCloseLocked) {{\r\n            closeActivationResult();\r\n        }}\r\n        return;\r\n    }}\r\n\r\n    if (target.matches('[data-activation-overlay]')) {{\r\n        if (!activationCloseLocked) {{\r\n            closeActivationResult();\r\n        }}\r\n        return;\r\n    }}\r\n\r\n    if (target.closest('[data-account-toggle]')) {{\r\n        event.preventDefault();\r\n        event.stopPropagation();\r\n        setAccountMenuOpen(Boolean(accountMenu?.hidden));\r\n        return;\r\n    }}\r\n\r\n    if (!target.closest('[data-account-menu]')) {{\r\n        setAccountMenuOpen(false);\r\n    }}\r\n\r\n    const hostActionElement = target.closest('[data-host-action]');\r\n    if (hostActionElement && bridge) {{\r\n        event.preventDefault();\r\n        bridge.postMessage({{\r\n            type: hostActionElement.getAttribute('data-host-action')\r\n        }});\r\n        return;\r\n    }}\r\n\r\n    const actionElement = target.closest('[data-action]');\r\n    if (!actionElement || !bridge) {{\r\n        return;\r\n    }}\r\n\r\n    const action = actionElement.getAttribute('data-action');\r\n    if (!action) {{\r\n        return;\r\n    }}\r\n\r\n    if (action.startsWith('fix54:run:')) {{\r\n        if (actionElement.disabled) return;\r\n        window.setFix54State?.({{documentId:actionElement.dataset.fix54DocumentId,\r\n            libraryId:actionElement.dataset.fix54LibraryId, isRunning:true, message:'Đang chuẩn bị FIX 54...'}});\r\n    }}\r\n\r\n    if (action.startsWith('fix54:cancel:') && actionElement.disabled) return;\r\n\r\n    if (action === 'play') {{\r\n        if (actionElement.hasAttribute('disabled')) {{\r\n            return;\r\n        }}\r\n\r\n        applyActivationState(true);\r\n    }}\r\n\r\n    if (action === 'cloudsave:open') {{\r\n        if (actionElement.hasAttribute('disabled')) {{\r\n            return;\r\n        }}\r\n        event.preventDefault();\r\n        openCloudSaveChoice();\r\n        return;\r\n    }}\r\n\r\n    if (action.startsWith('denuvo:')) {{\r\n        if (actionElement.hasAttribute('disabled')) {{\r\n            return;\r\n        }}\r\n\r\n        applyDenuvoState(true);\r\n    }}\r\n\r\n    if (action.startsWith('pypass:')) {{\r\n        if (actionElement.hasAttribute('disabled')) {{\r\n            return;\r\n        }}\r\n\r\n        applyPypassState(true);\r\n    }}\r\n\r\n    if (action.startsWith('online:')) {{\r\n        if (actionElement.hasAttribute('disabled')) {{\r\n            return;\r\n        }}\r\n\r\n        applyOnlineState(true);\r\n    }}\r\n\r\n    bridge.postMessage(action);\r\n}});\r\n\r\ndocument.addEventListener('keydown', event => {{\r\n    if (cloudSaveRestoreOverlay && !cloudSaveRestoreOverlay.hidden) {{\r\n        if (event.key === 'Escape') {{\r\n            event.preventDefault();\r\n            closeCloudSaveRestoreOverlay();\r\n            return;\r\n        }}\r\n        if (event.key === 'Tab') {{\r\n            const focusable = [...cloudSaveRestoreOverlay.querySelectorAll('button:not(:disabled):not([hidden])')]\r\n                .filter(button => button.getClientRects().length > 0);\r\n            const first = focusable[0];\r\n            const last = focusable[focusable.length - 1];\r\n            if (event.shiftKey && document.activeElement === first) {{\r\n                event.preventDefault();\r\n                last.focus();\r\n            }} else if (!event.shiftKey && document.activeElement === last) {{\r\n                event.preventDefault();\r\n                first.focus();\r\n            }}\r\n            return;\r\n        }}\r\n    }}\r\n    if (cloudSaveChoiceOverlay && !cloudSaveChoiceOverlay.hidden) {{\r\n        if (event.key === 'Escape') {{\r\n            event.preventDefault();\r\n            closeCloudSaveChoice();\r\n            return;\r\n        }}\r\n        if (event.key === 'Tab') {{\r\n            const focusable = [...cloudSaveChoiceOverlay.querySelectorAll('button:not(:disabled)')];\r\n            const first = focusable[0];\r\n            const last = focusable[focusable.length - 1];\r\n            if (event.shiftKey && document.activeElement === first) {{\r\n                event.preventDefault();\r\n                last.focus();\r\n            }} else if (!event.shiftKey && document.activeElement === last) {{\r\n                event.preventDefault();\r\n                first.focus();\r\n            }}\r\n            return;\r\n        }}\r\n    }}\r\n    if (event.key === 'Escape' && activationOverlay && !activationOverlay.hidden && !activationCloseLocked) {{\r\n        closeActivationResult();\r\n    }}\r\n}});";
	}

	private static string BuildPypassGuidePayload(DesktopLibraryDetailResponse detail, bool showPypassGuide)
	{
		DesktopLibraryPypassFixResponse pypassFix = detail.PypassFix;
		if (!showPypassGuide || pypassFix == null || !pypassFix.HasPypassFix || string.Equals(pypassFix.Status, "revoked", StringComparison.OrdinalIgnoreCase))
		{
			return "null";
		}
		return JsonSerializer.Serialize(new
		{
			state = "warning",
			gameName = detail.GameName,
			title = "Hướng dẫn Active-2",
			badgeText = "Pypass",
			metaText = BuildPypassGuideMeta(detail),
			summary = "Game này cần hoàn tất đúng thứ tự. Bấm Active trước để thêm game vào Steam, tải/cài game đầy đủ, sau đó quay lại launcher và bấm Active-2.",
			metrics = new[]
			{
				new
				{
					label = "Bước 1",
					value = "Bấm Active",
					tone = "default"
				},
				new
				{
					label = "Bước 2",
					value = "Tải game xong",
					tone = "warning"
				},
				new
				{
					label = "Bước 3",
					value = "Bấm Active-2",
					tone = "success"
				}
			},
			paths = Array.Empty<object>(),
			messages = new string[3] { "Đừng bấm Active-2 khi game chưa tải/cài xong trong Steam.", "Sau khi tải game hoàn tất, quay lại màn hình thư viện của launcher và bấm Active-2.", "Giữ launcher mở trong lúc Active-2 đang xử lý để launcher kiểm tra và cập nhật trạng thái." },
			primaryActionText = "Đã hiểu"
		}, JsonOptions);
	}

	private static string BuildInitialGuidePayload(DesktopLibraryDetailResponse detail, bool showPypassGuide, bool showDenuvoGuide)
	{
		string text = BuildDenuvoGuidePayload(detail, showDenuvoGuide);
		if (!string.Equals(text, "null", StringComparison.Ordinal))
		{
			return text;
		}
		return BuildPypassGuidePayload(detail, showPypassGuide);
	}

	private static string BuildDenuvoGuidePayload(DesktopLibraryDetailResponse detail, bool showDenuvoGuide)
	{
		DesktopLibraryDenuvoFixResponse denuvoFix = detail.DenuvoFix;
		if (!showDenuvoGuide || denuvoFix == null || !denuvoFix.HasDenuvoFix || string.Equals(denuvoFix.Status, "revoked", StringComparison.OrdinalIgnoreCase))
		{
			return "null";
		}
		return JsonSerializer.Serialize(new
		{
			state = "warning",
			gameName = detail.GameName,
			title = "Hướng dẫn nạp game",
			badgeText = "Denuvo",
			metaText = string.Empty,
			summary = "Game này cần làm đúng thứ tự trước khi admin nạp game. Hoàn tất các bước bên dưới rồi gọi admin theo số hỗ trợ.",
			metrics = Array.Empty<object>(),
			paths = Array.Empty<object>(),
			messages = new string[3] { "Bước 1: bấm Active để thêm game vào Steam.", "Bước 2: tải/cài game đầy đủ trong Steam.", "Bước 3: gọi admin 0947709029 để được hướng dẫn nạp game." },
			primaryActionText = "Đã hiểu",
			phoneText = "0947709029",
			adminStatus = true,
			zaloQr = true
		}, JsonOptions);
	}

	private static string BuildPypassGuideMeta(DesktopLibraryDetailResponse detail)
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
		return "Thư viện launcher";
	}

	private static string BuildPlayButton(bool isActivating)
	{
		string value = (isActivating ? " is-loading" : string.Empty);
		string value2 = (isActivating ? " disabled aria-disabled=\"true\" " : " aria-disabled=\"false\" ");
		string value3 = (isActivating ? "Loading..." : "Active");
		return $"<button type=\"button\" class=\"library-button library-button--primary{value}\" data-action=\"play\" data-default-label=\"Active\"{value2}>{value3}</button>";
	}

	private static string BuildDenuvoButton(DesktopLibraryDetailResponse detail, bool isApplyingDenuvo, DateTime? cooldownUntilUtc)
	{
		DesktopLibraryDenuvoFixResponse denuvoFix = detail.DenuvoFix;
		if (denuvoFix == null || !denuvoFix.HasDenuvoFix)
		{
			return string.Empty;
		}
		DateTime? cooldownUntilUtc2 = ResolveActiveDenuvoCooldownUntilUtc(cooldownUntilUtc);
		bool hasValue = cooldownUntilUtc2.HasValue;
		bool flag = string.Equals(denuvoFix.Status, "revoked", StringComparison.OrdinalIgnoreCase);
		string value = (IsDenuvoUpdatePending(denuvoFix) ? "denuvo:update" : "denuvo:apply");
		string text = "Yêu cầu admin nạp game";
		string value2 = (isApplyingDenuvo ? "Loading..." : (flag ? "Game revoked" : (hasValue ? ("Đã nạp game - còn " + FormatDenuvoCooldownRemaining(cooldownUntilUtc2.Value)) : text)));
		string value3 = (isApplyingDenuvo ? " is-loading" : string.Empty) + (hasValue ? " is-cooldown" : string.Empty);
		string value4 = ((flag || hasValue) ? " data-denuvo-locked=\"true\" " : string.Empty);
		string value5 = BuildDenuvoCooldownAttributes(cooldownUntilUtc2, "Đã nạp game - còn");
		string value6 = ((isApplyingDenuvo || flag || hasValue) ? " disabled aria-disabled=\"true\" " : " aria-disabled=\"false\" ");
		return $"<button type=\"button\" class=\"library-button{value3}\" data-action=\"{value}\" data-default-label=\"{text}\"{value4}{value5}{value6}>{EncodeText(value2)}</button>";
	}

	private static string BuildPypassButton(DesktopLibraryDetailResponse detail, bool isApplyingPypass)
	{
		DesktopLibraryPypassFixResponse pypassFix = detail.PypassFix;
		if (pypassFix == null || !pypassFix.HasPypassFix)
		{
			return string.Empty;
		}
		bool flag = string.Equals(pypassFix.Status, "revoked", StringComparison.OrdinalIgnoreCase);
		string text = ResolvePypassButtonLabel(pypassFix, uppercase: false);
		string value = (isApplyingPypass ? "Loading..." : (flag ? "Pypass revoked" : text));
		string value2 = (isApplyingPypass ? " is-loading" : string.Empty);
		string value3 = (flag ? " data-pypass-locked=\"true\" " : string.Empty);
		string value4 = ((isApplyingPypass || flag) ? " disabled aria-disabled=\"true\" " : " aria-disabled=\"false\" ");
		return $"<button type=\"button\" class=\"library-button{value2}\" data-action=\"pypass:apply\" data-default-label=\"{text}\"{value3}{value4}>{value}</button>";
	}

	private static string BuildOnlineButton(DesktopLibraryDetailResponse detail, bool isApplyingOnline)
	{
		DesktopLibraryOnlineFixResponse onlineFix = detail.OnlineFix;
		if (onlineFix == null || !onlineFix.HasOnlineFix)
		{
			return string.Empty;
		}
		bool flag = string.Equals(onlineFix.Status, "revoked", StringComparison.OrdinalIgnoreCase);
		string text = ResolveOnlineButtonLabel(onlineFix, uppercase: false);
		string value = (isApplyingOnline ? "Loading..." : (flag ? "Game revoked" : text));
		string value2 = (isApplyingOnline ? " is-loading" : string.Empty);
		string value3 = (flag ? " data-online-locked=\"true\" " : string.Empty);
		string value4 = ((isApplyingOnline || flag) ? " disabled aria-disabled=\"true\" " : " aria-disabled=\"false\" ");
		return $"<button type=\"button\" class=\"library-button{value2}\" data-action=\"online:apply\" data-default-label=\"{text}\"{value3}{value4}>{value}</button>";
	}

	private static DateTime? ResolveActiveDenuvoCooldownUntilUtc(DateTime? cooldownUntilUtc)
	{
		if (!cooldownUntilUtc.HasValue)
		{
			return null;
		}
		DateTime dateTime = cooldownUntilUtc.Value.ToUniversalTime();
		if (!(dateTime > DateTime.UtcNow))
		{
			return null;
		}
		return dateTime;
	}

	private static string BuildDenuvoCooldownAttributes(DateTime? cooldownUntilUtc, string labelPrefix)
	{
		if (!cooldownUntilUtc.HasValue)
		{
			return string.Empty;
		}
		string value = cooldownUntilUtc.Value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
		return $" data-denuvo-cooldown-until=\"{EncodeAttribute(value)}\" data-denuvo-cooldown-prefix=\"{EncodeAttribute(labelPrefix)}\"";
	}

	private static string FormatDenuvoCooldownRemaining(DateTime cooldownUntilUtc)
	{
		TimeSpan timeSpan = cooldownUntilUtc.ToUniversalTime() - DateTime.UtcNow;
		if (timeSpan <= TimeSpan.Zero)
		{
			return "00:00:00";
		}
		int value = (int)Math.Floor(timeSpan.TotalHours);
		return $"{value:00}:{timeSpan.Minutes:00}:{timeSpan.Seconds:00}";
	}

	private static string ResolveDenuvoButtonLabel(DesktopLibraryDenuvoFixResponse denuvo, string action, bool uppercase)
	{
		string text = ((!(action == "denuvo:update")) ? (denuvo.Status.Trim().ToLowerInvariant() switch
		{
			"installed" => "Yêu cầu admin nạp game", 
			"updated" => "Yêu cầu admin nạp game", 
			"error" => "Yêu cầu admin nạp game", 
			_ => "Yêu cầu admin nạp game", 
		}) : "Yêu cầu admin nạp game");
		string text2 = text;
		if (!uppercase)
		{
			return text2;
		}
		return text2.ToUpperInvariant();
	}

	private static bool IsDenuvoUpdatePending(DesktopLibraryDenuvoFixResponse denuvo)
	{
		if (string.Equals(denuvo.Status, "update_pending", StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}
		int? activeConfigRevisionNumber = denuvo.ActiveConfigRevisionNumber;
		if (activeConfigRevisionNumber.HasValue)
		{
			int valueOrDefault = activeConfigRevisionNumber.GetValueOrDefault();
			activeConfigRevisionNumber = denuvo.LastAppliedRevisionNumber;
			if (activeConfigRevisionNumber.HasValue)
			{
				int valueOrDefault2 = activeConfigRevisionNumber.GetValueOrDefault();
				return valueOrDefault > valueOrDefault2;
			}
		}
		return false;
	}

	private static string ResolvePypassButtonLabel(DesktopLibraryPypassFixResponse pypass, bool uppercase)
	{
		string text = ((!(pypass.Status.Trim().ToLowerInvariant() == "installing")) ? "FIX" : "Loading...");
		string text2 = text;
		if (!uppercase)
		{
			return text2;
		}
		return text2.ToUpperInvariant();
	}

	private static string BuildHeroMedia(DesktopLibraryDetailResponse detail)
	{
		List<(string, string)> galleryItems = GetGalleryItems(detail);
		StringBuilder stringBuilder = new StringBuilder();
		if (galleryItems.Count > 0)
		{
			(string, string) tuple = galleryItems[0];
			StringBuilder stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder3 = stringBuilder2;
			StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(165, 3, stringBuilder2);
			handler.AppendLiteral("<div class=\"library-gallery\" data-gallery-root data-gallery-index=\"0\">\r\n<div class=\"library-cover-shell\">\r\n    <img src=\"");
			handler.AppendFormatted(EncodeAttribute(tuple.Item1));
			handler.AppendLiteral("\" alt=\"");
			handler.AppendFormatted(EncodeAttribute(tuple.Item2));
			handler.AppendLiteral("\" data-gallery-stage />\r\n    ");
			handler.AppendFormatted(BuildGalleryNavigationButtons(galleryItems.Count));
			handler.AppendLiteral("\r\n</div>");
			stringBuilder3.AppendLine(ref handler);
		}
		else
		{
			stringBuilder.AppendLine("<div class=\"library-cover-shell library-cover-empty\">\r\n    <span>NO COVER</span>\r\n</div>");
		}
		if (galleryItems.Count > 1)
		{
			stringBuilder.AppendLine("<div class=\"library-gallery-strip\">");
			for (int i = 0; i < galleryItems.Count; i++)
			{
				(string, string) tuple2 = galleryItems[i];
				StringBuilder stringBuilder2 = stringBuilder;
				StringBuilder stringBuilder4 = stringBuilder2;
				StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(149, 6, stringBuilder2);
				handler.AppendLiteral("<button type=\"button\" class=\"library-gallery-thumb");
				handler.AppendFormatted((i == 0) ? " is-active" : string.Empty);
				handler.AppendLiteral("\" data-gallery-target=\"");
				handler.AppendFormatted(i);
				handler.AppendLiteral("\" data-image-url=\"");
				handler.AppendFormatted(EncodeAttribute(tuple2.Item1));
				handler.AppendLiteral("\" data-image-alt=\"");
				handler.AppendFormatted(EncodeAttribute(tuple2.Item2));
				handler.AppendLiteral("\">\r\n    <img src=\"");
				handler.AppendFormatted(EncodeAttribute(tuple2.Item1));
				handler.AppendLiteral("\" alt=\"");
				handler.AppendFormatted(EncodeAttribute(tuple2.Item2));
				handler.AppendLiteral("\" />\r\n</button>");
				stringBuilder4.AppendLine(ref handler);
			}
			stringBuilder.AppendLine("</div>");
		}
		if (galleryItems.Count > 0)
		{
			stringBuilder.AppendLine("</div>");
		}
		return stringBuilder.ToString();
	}

	private static string BuildGalleryNavigationButtons(int galleryCount)
	{
		if (galleryCount <= 1)
		{
			return string.Empty;
		}
		return "<button type=\"button\" class=\"library-gallery-button\" data-gallery-step=\"-1\" aria-label=\"Ảnh trước\">‹</button>\r\n<button type=\"button\" class=\"library-gallery-button\" data-gallery-step=\"1\" aria-label=\"Ảnh sau\">›</button>";
	}

	private static List<(string Url, string Alt)> GetGalleryItems(DesktopLibraryDetailResponse detail)
	{
		List<(string Url, string Alt)> results = new List<(string, string)>();
		HashSet<string> seenUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		AddItem(detail.CoverImageUrl, detail.GameName);
		foreach (DesktopLibraryGalleryItemResponse item in detail.Gallery)
		{
			AddItem(string.IsNullOrWhiteSpace(item.FullUrl) ? item.ThumbnailUrl : item.FullUrl, item.Alt);
		}
		return results.Take(6).ToList();
		void AddItem(string? url, string? alt)
		{
			if (!string.IsNullOrWhiteSpace(url) && seenUrls.Add(url))
			{
				results.Add((url, string.IsNullOrWhiteSpace(alt) ? detail.GameName : alt));
			}
		}
	}

	private static string BuildFacts(DesktopLibraryDetailResponse detail)
	{
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
		StringBuilder stringBuilder = new StringBuilder();
		foreach (DesktopLibraryFactResponse item in list)
		{
			StringBuilder stringBuilder2 = stringBuilder;
			StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(66, 2, stringBuilder2);
			handler.AppendLiteral("<dl class=\"library-fact-row\">\r\n    <dt>");
			handler.AppendFormatted(EncodeText(item.Label));
			handler.AppendLiteral("</dt>\r\n    <dd>");
			handler.AppendFormatted(EncodeText(item.Value));
			handler.AppendLiteral("</dd>\r\n</dl>");
			stringBuilder2.AppendLine(ref handler);
		}
		return stringBuilder.ToString();
	}

	private static string BuildPackageItems(DesktopLibraryDetailResponse detail)
	{
		List<string> list;
		if (detail.PackageItems.Count <= 0)
		{
			int num = 1;
			list = new List<string>(num);
			CollectionsMarshal.SetCount(list, num);
			CollectionsMarshal.AsSpan(list)[0] = "Không có thông tin gói sản phẩm bổ sung.";
		}
		else
		{
			list = detail.PackageItems;
		}
		StringBuilder stringBuilder = new StringBuilder();
		foreach (string item in list)
		{
			StringBuilder stringBuilder2 = stringBuilder;
			StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(9, 1, stringBuilder2);
			handler.AppendLiteral("<li>");
			handler.AppendFormatted(EncodeText(item));
			handler.AppendLiteral("</li>");
			stringBuilder2.AppendLine(ref handler);
		}
		return stringBuilder.ToString();
	}

	private static string BuildTags(DesktopLibraryDetailResponse detail)
	{
		List<string> list;
		if (detail.SummaryTags.Count <= 0)
		{
			int num = 1;
			list = new List<string>(num);
			CollectionsMarshal.SetCount(list, num);
			CollectionsMarshal.AsSpan(list)[0] = "Library";
		}
		else
		{
			list = detail.SummaryTags;
		}
		StringBuilder stringBuilder = new StringBuilder();
		foreach (string item in list.Take(12))
		{
			stringBuilder.Append(BuildChip(item));
		}
		return stringBuilder.ToString();
	}

	private static string BuildDescriptionBlocks(DesktopLibraryDetailResponse detail)
	{
		List<(string, string)> list = new List<(string, string)>();
		string text = PlainText(detail.ShortDescription);
		if (!string.IsNullOrWhiteSpace(text))
		{
			list.Add(("Tổng quan", text));
		}
		string text2 = PlainText(detail.AboutTheGameHtml);
		if (!string.IsNullOrWhiteSpace(text2))
		{
			list.Add(("Nội dung game", text2));
		}
		string text3 = PlainText(detail.DetailedDescriptionHtml);
		if (!string.IsNullOrWhiteSpace(text3))
		{
			list.Add(("Chi tiết nội dung", text3));
		}
		if (list.Count == 0)
		{
			list.Add(("Mô tả", "Launcher hiện chưa có thêm mô tả chi tiết cho sản phẩm này."));
		}
		StringBuilder stringBuilder = new StringBuilder();
		foreach (var item in list.Take(3))
		{
			StringBuilder stringBuilder2 = stringBuilder;
			StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(91, 2, stringBuilder2);
			handler.AppendLiteral("<article class=\"library-card\">\r\n    <h3>");
			handler.AppendFormatted(EncodeText(item.Item1));
			handler.AppendLiteral("</h3>\r\n    <p class=\"library-copy\">");
			handler.AppendFormatted(EncodeText(item.Item2));
			handler.AppendLiteral("</p>\r\n</article>");
			stringBuilder2.AppendLine(ref handler);
		}
		return stringBuilder.ToString();
	}

	private static string BuildEditionsSection(DesktopLibraryDetailResponse detail)
	{
		if (detail.Editions.Count == 0)
		{
			return string.Empty;
		}
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.AppendLine("<section class=\"library-section\">\r\n    <div class=\"library-section-heading\">\r\n        <h2>Phiên bản và quyền sở hữu</h2>\r\n        <p>Phiên bản đã mua được highlight để đối chiếu nhanh với storefront.</p>\r\n    </div>\r\n    <div class=\"library-tile-grid\">");
		foreach (DesktopLibraryEditionResponse edition in detail.Editions)
		{
			stringBuilder.AppendLine(BuildEditionCard(edition));
		}
		stringBuilder.AppendLine("    </div>\r\n</section>");
		return stringBuilder.ToString();
	}

	private static string BuildEditionCard(DesktopLibraryEditionResponse edition)
	{
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.AppendLine("<div class=\"library-edition-status\">");
		StringBuilder stringBuilder2 = stringBuilder;
		StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(35, 2, stringBuilder2);
		handler.AppendLiteral("<span class=\"library-chip ");
		handler.AppendFormatted(edition.IsOwned ? "is-owned" : string.Empty);
		handler.AppendLiteral("\">");
		handler.AppendFormatted(EncodeText(edition.IsOwned ? "Bạn đã sở hữu" : "Tham chiếu"));
		handler.AppendLiteral("</span>");
		stringBuilder2.AppendLine(ref handler);
		if (edition.IsSelected)
		{
			stringBuilder.AppendLine("<span class=\"library-chip is-selected\">Đang chọn</span>");
		}
		stringBuilder.AppendLine("</div>");
		string value = PlainText(edition.Note);
		if (string.IsNullOrWhiteSpace(value))
		{
			value = "Thông tin phiên bản sẽ đồng bộ thêm từ storefront.";
		}
		string value2 = ((string.IsNullOrWhiteSpace(edition.ReferencePriceText) || string.Equals(edition.ReferencePriceText, edition.CurrentPriceText, StringComparison.OrdinalIgnoreCase)) ? string.Empty : ("<span class=\"library-price-reference\">Giá gốc: " + EncodeText(edition.ReferencePriceText) + "</span>"));
		string value3 = (string.IsNullOrWhiteSpace(edition.DiscountText) ? string.Empty : ("<span class=\"library-chip\">" + EncodeText(edition.DiscountText) + "</span>"));
		return $"<article class=\"library-card\">\r\n    {stringBuilder}\r\n    <h3>{EncodeText(edition.Name)}</h3>\r\n    <p>{EncodeText(value)}</p>\r\n    <div class=\"library-price-stack\">\r\n        <span class=\"library-price-current\">{EncodeText(edition.CurrentPriceText)}</span>\r\n        {value2}\r\n        {value3}\r\n    </div>\r\n</article>";
	}

	private static string BuildRequirementsSection(DesktopLibraryDetailResponse detail)
	{
		if (detail.Requirements.Count == 0)
		{
			return string.Empty;
		}
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.AppendLine("<section class=\"library-section\">\r\n    <div class=\"library-section-heading\">\r\n        <h2>Yêu cầu hệ thống</h2>\r\n        <p>Dữ liệu requirement được render lại từ thông tin Steam và storefront.</p>\r\n    </div>\r\n    <div class=\"library-tile-grid\">");
		foreach (DesktopLibraryRequirementResponse requirement in detail.Requirements)
		{
			StringBuilder stringBuilder2 = stringBuilder;
			StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(91, 2, stringBuilder2);
			handler.AppendLiteral("<article class=\"library-card\">\r\n    <h3>");
			handler.AppendFormatted(EncodeText(requirement.Platform));
			handler.AppendLiteral("</h3>\r\n    <p class=\"library-copy\">");
			handler.AppendFormatted(EncodeText(PlainText(requirement.Html)));
			handler.AppendLiteral("</p>\r\n</article>");
			stringBuilder2.AppendLine(ref handler);
		}
		stringBuilder.AppendLine("    </div>\r\n</section>");
		return stringBuilder.ToString();
	}

	private static string BuildReviewsSection(DesktopLibraryDetailResponse detail)
	{
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
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.AppendLine("<section class=\"library-section\">\r\n    <div class=\"library-section-heading\">\r\n        <h2>Đánh giá người chơi</h2>\r\n        <p>Review được trích từ dữ liệu hiện có để launcher có trải nghiệm gần với web.</p>\r\n    </div>\r\n    <div class=\"library-review-list\">");
		foreach (DesktopLibraryReviewResponse item in list.Take(4))
		{
			stringBuilder.AppendLine(BuildReviewCard(item));
		}
		stringBuilder.AppendLine("    </div>\r\n</section>");
		return stringBuilder.ToString();
	}

	private static string BuildReviewCard(DesktopLibraryReviewResponse review)
	{
		string value = (string.IsNullOrWhiteSpace(review.ReviewText) ? "Người dùng chưa để lại nội dung chi tiết." : review.ReviewText);
		string metaText = BuildReviewMeta(review);
		return $"<article class=\"library-card\">\r\n    <div class=\"library-review-card\">\r\n        <div class=\"library-avatar\">{EncodeText(string.IsNullOrWhiteSpace(review.AvatarText) ? "N" : review.AvatarText)}</div>\r\n        <div>\r\n            <h3>{EncodeText(review.DisplayName)}</h3>\r\n            {BuildReviewMetaMarkup(metaText)}\r\n            <p class=\"library-copy\">{EncodeText(value)}</p>\r\n        </div>\r\n    </div>\r\n</article>";
	}

	private static string BuildReviewMetaMarkup(string metaText)
	{
		if (string.IsNullOrWhiteSpace(metaText))
		{
			return string.Empty;
		}
		return "<div class=\"library-review-meta\">" + EncodeText(metaText) + "</div>";
	}

	private static string ResolveOnlineButtonLabel(DesktopLibraryOnlineFixResponse online, bool uppercase)
	{
		string text = ((!(online.Status.Trim().ToLowerInvariant() == "installing")) ? "ONLINE FIX" : "Loading...");
		string text2 = text;
		if (!uppercase)
		{
			return text2;
		}
		return text2.ToUpperInvariant();
	}

	private static string BuildWebsiteButton(string? website)
	{
		if (string.IsNullOrWhiteSpace(website))
		{
			return string.Empty;
		}
		string text = Uri.EscapeDataString(website);
		return "<button type=\"button\" class=\"library-button\" data-action=\"open:" + text + "\">Trang s&#7843;n ph&#7849;m</button>";
	}

	private static string BuildMetricCard(string label, string value, string modifierClass)
	{
		string value2 = (string.IsNullOrWhiteSpace(modifierClass) ? string.Empty : (" " + modifierClass));
		return $"<article class=\"library-metric-item{value2}\">\r\n    <span class=\"library-metric-label\">{EncodeText(label)}</span>\r\n    <span class=\"library-metric-value\">{EncodeText(value)}</span>\r\n</article>";
	}

	private static string BuildChip(string text)
	{
		return "<span class=\"library-chip\">" + EncodeText(text) + "</span>";
	}

	private static string BuildReviewSummary(DesktopLibraryDetailResponse detail)
	{
		List<string> list = new List<string>();
		if (!string.IsNullOrWhiteSpace(detail.RatingScoreText))
		{
			list.Add(detail.RatingScoreText);
		}
		if (!string.IsNullOrWhiteSpace(detail.RatingCountText))
		{
			list.Add(detail.RatingCountText);
		}
		if (list.Count != 0)
		{
			return string.Join(" | ", list);
		}
		return "Chưa có dữ liệu";
	}

	private static string BuildReviewMeta(DesktopLibraryReviewResponse review)
	{
		List<string> list = new List<string>();
		if (!string.IsNullOrWhiteSpace(review.RatingText) && review.RatingText != "-")
		{
			list.Add(review.RatingText);
		}
		if (!string.IsNullOrWhiteSpace(review.CreatedAtText))
		{
			list.Add(review.CreatedAtText);
		}
		return string.Join(" | ", list);
	}

	private static string PlainText(string? html)
	{
		if (string.IsNullOrWhiteSpace(html))
		{
			return string.Empty;
		}
		string input = Regex.Replace(html, "<br\\s*/?>", "\n", RegexOptions.IgnoreCase);
		input = Regex.Replace(input, "<hr.*?>", "\n", RegexOptions.IgnoreCase);
		input = HtmlTagPattern.Replace(input, " ");
		input = WebUtility.HtmlDecode(input);
		input = input.Replace("\r", string.Empty);
		input = Regex.Replace(input, "\\s+\n", "\n");
		input = Regex.Replace(input, "\n\\s+", "\n");
		input = WhitespacePattern.Replace(input, " ");
		input = Regex.Replace(input, "\n{3,}", "\n\n");
		return input.Trim();
	}

	private static string NormalizeVietnameseMojibake(string value)
	{
		if (string.IsNullOrEmpty(value) || !LooksLikeVietnameseMojibake(value))
		{
			return value;
		}
		StringBuilder output = new StringBuilder(value.Length);
		List<byte> bytes = new List<byte>();
		StringBuilder original = new StringBuilder();
		foreach (char c in value)
		{
			byte? b = MapWindows1252Byte(c);
			if (b.HasValue && IsMojibakeRunCharacter(c))
			{
				bytes.Add(b.Value);
				original.Append(c);
			}
			else
			{
				FlushBuffer();
				output.Append(c);
			}
		}
		FlushBuffer();
		return output.ToString();
		void FlushBuffer()
		{
			if (bytes.Count != 0)
			{
				if (!LooksLikeVietnameseMojibake(original.ToString()))
				{
					output.Append(original);
					bytes.Clear();
					original.Clear();
				}
				else
				{
					try
					{
						output.Append(new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetString(bytes.ToArray()));
					}
					catch (DecoderFallbackException)
					{
						output.Append(original);
					}
					bytes.Clear();
					original.Clear();
				}
			}
		}
	}

	private static bool IsMojibakeRunCharacter(char character)
	{
		if (character >= '\u0080')
		{
			return MapWindows1252Byte(character).HasValue;
		}
		return false;
	}

	private static bool LooksLikeVietnameseMojibake(string value)
	{
		if (!value.Contains('Â') && !value.Contains('Ã') && !value.Contains('Ä') && !value.Contains('Å') && !value.Contains('Æ') && !value.Contains("\ufffd", StringComparison.Ordinal))
		{
			return value.Contains("\ufffd", StringComparison.Ordinal);
		}
		return true;
	}

	private static byte? MapWindows1252Byte(char character)
	{
		if (character <= 'ÿ')
		{
			return (byte)character;
		}
		return character switch
		{
			'€' => (byte)128, 
			'‚' => (byte)130, 
			'ƒ' => (byte)131, 
			'„' => (byte)132, 
			'…' => (byte)133, 
			'†' => (byte)134, 
			'‡' => (byte)135, 
			'ˆ' => (byte)136, 
			'‰' => (byte)137, 
			'Š' => (byte)138, 
			'‹' => (byte)139, 
			'Œ' => (byte)140, 
			'Ž' => (byte)142, 
			'‘' => (byte)145, 
			'’' => (byte)146, 
			'“' => (byte)147, 
			'”' => (byte)148, 
			'•' => (byte)149, 
			'–' => (byte)150, 
			'—' => (byte)151, 
			'\u02dc' => (byte)152, 
			'™' => (byte)153, 
			'š' => (byte)154, 
			'›' => (byte)155, 
			'œ' => (byte)156, 
			'ž' => (byte)158, 
			'Ÿ' => (byte)159, 
			_ => null, 
		};
	}

	private static string EncodeText(string? value)
	{
		return WebUtility.HtmlEncode(value ?? string.Empty);
	}

	private static string EncodeAttribute(string? value)
	{
		return WebUtility.HtmlEncode(value ?? string.Empty);
	}
}
