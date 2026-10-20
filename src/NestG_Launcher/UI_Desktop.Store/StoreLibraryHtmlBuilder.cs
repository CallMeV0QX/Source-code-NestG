using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using app_desktop.Services.SteamExclusions;

namespace UI_Desktop.Store;

internal static class StoreLibraryHtmlBuilder
{
	private sealed record LibraryNewsItem(Guid GameId, string Title, string? ImageUrl, string? DiscountText, string BadgeText, string? CurrentPriceText, string? ReferencePriceText);

	public static string BuildDocument(DesktopLibraryListResponse library, StorePageData? pageData, StoreWebHomeContext context, SteamExclusionToggleState? exclusionState = null)
	{
		DesktopLibraryListItemResponse[] array = library.Items.OrderByDescending((DesktopLibraryListItemResponse item) => item.AcquiredAt).ThenBy<DesktopLibraryListItemResponse, string>((DesktopLibraryListItemResponse item) => item.GameName, StringComparer.OrdinalIgnoreCase).ToArray();
		DesktopLibraryListItemResponse[] items = array;
		IReadOnlyList<LibraryNewsItem> items2 = ResolveWhatsNewItems(pageData, array);
		return NormalizeVietnameseMojibake($"<!DOCTYPE html>\r\n<html lang=\"vi\">\r\n<head>\r\n<meta charset=\"utf-8\" />\r\n<meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\" />\r\n<title>NestG Library</title>\r\n<style>{StoreWebHomeHtmlBuilder.BuildStyles()}{BuildStyles()}</style>\r\n</head>\r\n<body>\r\n<div class=\"library-shell\">\r\n    {StoreWebHomeHtmlBuilder.RenderSharedHeader(context, "library", showSearch: false, RenderExclusionToggle(exclusionState ?? new SteamExclusionToggleState()))}\n\r\n    <aside class=\"sidebar\" data-drag-region>\r\n        <div class=\"sidebar-heading\">\r\n            <strong>Thư viện</strong>\n            <span>Trò chơi và phần mềm</span>\r\n        </div>\r\n        <form class=\"library-search-form\" data-library-search-form role=\"search\">\n            <label for=\"library-search-input\">Tìm kiếm trong thư viện</label>\n            <div class=\"library-search\">\n                <input id=\"library-search-input\" type=\"search\" placeholder=\"Tên game, phiên bản, mã đơn...\" data-library-search />\n                <button type=\"submit\" aria-label=\"Tìm kiếm trong thư viện\">\n                    <svg viewBox=\"0 0 24 24\" aria-hidden=\"true\" focusable=\"false\"><path d=\"M10.8 18.1a7.3 7.3 0 1 1 0-14.6 7.3 7.3 0 0 1 0 14.6Zm5.1-2.2 4.6 4.6\" /></svg>\n                </button>\n            </div>\n        </form>\n        <button type=\"button\" class=\"library-list-toggle\" data-library-list-toggle aria-expanded=\"false\" aria-controls=\"library-navigation\" hidden>Danh sách game</button>\n        <div class=\"library-navigation\" id=\"library-navigation\" data-library-navigation>\n            <div class=\"sidebar-list\" data-sidebar-list>\n                {RenderSidebarItems(array)}\n                <div class=\"sidebar-empty\" data-sidebar-empty hidden>Không tìm thấy game.</div>\n            </div>\n        </div>\n    </aside>\r\n\r\n    <main class=\"content\">\n        <section class=\"whats-new\">\n            <div class=\"section-title\">\r\n                <h1>What's New</h1>\r\n                <span>Thịnh hành trên Steam</span>\r\n            </div>\r\n            <div class=\"news-rail-wrap\">\r\n                <div class=\"news-rail\" data-news-rail>\r\n                    {RenderWhatsNewItems(items2)}\r\n                </div>\r\n            </div>\r\n        </section>\r\n\r\n        <section class=\"library-toolbar\">\r\n            <h2 class=\"library-count\" role=\"status\" aria-live=\"polite\" aria-atomic=\"true\">TẤT CẢ GAME (<span data-total-count>{array.Length}</span>)</h2>\n            <label>Sắp xếp:\r\n                <select data-sort-select>\r\n                    <option value=\"default\">Mặc định</option>\r\n                    <option value=\"title\">Tên A-Z</option>\r\n                    <option value=\"recent\">Mới mua</option>\r\n                </select>\r\n            </label>\r\n            <button type=\"button\" class=\"installed-filter\" disabled aria-disabled=\"true\" title=\"Launcher chưa có dữ liệu cài đặt local đáng tin cậy\">Đã cài đặt</button>\r\n        </section>\r\n\r\n        <section class=\"owned-grid\" data-owned-grid>\r\n            {RenderOwnedItems(items)}\r\n        </section>\r\n        <section class=\"empty-state\" data-empty-state hidden>\r\n            <strong>{((array.Length == 0) ? "Thư viện chưa có game" : "Không tìm thấy game")}</strong>\n            <span>{((array.Length == 0) ? "Game đã mua hoặc được cấp sẽ xuất hiện tại đây." : "Thử đổi từ khóa tìm kiếm trong thư viện.")}</span>\n        </section>\r\n    </main>\r\n\r\n    <div class=\"toast-host\" data-toast-host aria-live=\"polite\" aria-atomic=\"true\"></div>\r\n</div>\n{StoreWebHomeHtmlBuilder.BuildLoadingOverlay()}\n<script>{BuildScript()}</script>\n</body>\r\n</html>");
	}

	private static string RenderExclusionToggle(SteamExclusionToggleState state)
	{
		return $"<div class=\"library-defender\" aria-label=\"Ngoại lệ game\" aria-busy=\"{state.Busy.ToString().ToLowerInvariant()}\" data-defender-control>\n        <span id=\"defender-toggle-label\">Ngoại lệ game</span>\n        <button type=\"button\" role=\"switch\" class=\"library-defender-switch\" data-defender-toggle\n            aria-labelledby=\"defender-toggle-label\" aria-describedby=\"defender-toggle-status\"\n            title=\"{Attr(state.Busy ? "Đang xử lý…" : state.Status)}\"\n            aria-checked=\"{state.Enabled.ToString().ToLowerInvariant()}\" {(state.Busy ? "disabled" : "")}>\n            <span aria-hidden=\"true\"></span>\n        </button>\n    <span class=\"library-defender-status\" id=\"defender-toggle-status\" role=\"status\" aria-live=\"polite\" aria-atomic=\"true\" data-defender-status>{Html(state.Busy ? "Đang xử lý…" : state.Status)}</span>\n</div>";
	}

	private static IReadOnlyList<LibraryNewsItem> ResolveWhatsNewItems(StorePageData? pageData, IReadOnlyList<DesktopLibraryListItemResponse> libraryItems)
	{
		if (pageData != null)
		{
			if (pageData.SaleGames.Count > 0)
			{
				return (from item in pageData.SaleGames.Take(10)
					select new LibraryNewsItem(item.GameId, item.Title, FirstImage(item.PortraitImageUrl, item.PosterImageUrl), item.DiscountText, item.EditionLabel, item.CurrentPriceText, item.ReferencePriceText)).ToArray();
			}
			IReadOnlyList<StoreGame> readOnlyList = ((pageData.SpotlightGames.Count > 0) ? pageData.SpotlightGames : pageData.Games);
			if (readOnlyList.Count > 0)
			{
				return (from item in readOnlyList.Take(10)
					select new LibraryNewsItem(item.GameId, item.Title, FirstImage(item.SpotlightImageUrl, item.PosterImageUrl), item.DiscountText, item.BadgeText, item.DisplaySalePriceText, SamePrice(item.DisplayOriginalPriceText, item.DisplaySalePriceText) ? null : item.DisplayOriginalPriceText)).ToArray();
			}
		}
		return (from item in libraryItems.Take(10)
			select new LibraryNewsItem(item.GameId, item.GameName, FirstImage(item.SpotlightImageUrl, item.PosterImageUrl), null, item.Badge, item.CurrentPriceText, null)).ToArray();
	}

	private static string RenderSidebarItems(IReadOnlyList<DesktopLibraryListItemResponse> items)
	{
		if (items.Count == 0)
		{
			return "<div class=\"sidebar-empty\">Thư viện chưa có game.</div>";
		}
		StringBuilder stringBuilder = new StringBuilder();
		foreach (DesktopLibraryListItemResponse item in items)
		{
			StringBuilder stringBuilder2 = stringBuilder;
			StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(219, 7, stringBuilder2);
			handler.AppendLiteral("<button type=\"button\"\r\n        class=\"sidebar-game\"\r\n        data-library-id=\"");
			handler.AppendFormatted(item.LibraryId, "D");
			handler.AppendLiteral("\"\r\n        data-library-title=\"");
			handler.AppendFormatted(Attr(item.GameName));
			handler.AppendLiteral("\"\n        title=\"");
			handler.AppendFormatted(Attr(item.GameName));
			handler.AppendLiteral("\"\n        aria-label=\"Mở ");
			handler.AppendFormatted(Attr(item.GameName));
			handler.AppendLiteral("\"\n        data-library-search=\"");
			handler.AppendFormatted(Attr($"{item.GameName} {item.VersionName} {item.Category} {item.OrderCode}"));
			handler.AppendLiteral("\">\n    ");
			handler.AppendFormatted(RenderTinyImage(item.PosterImageUrl, item.GameName));
			handler.AppendLiteral("\r\n    <span>");
			handler.AppendFormatted(Html(item.GameName));
			handler.AppendLiteral("</span>\r\n</button>");
			stringBuilder2.Append(ref handler);
		}
		return stringBuilder.ToString();
	}

	private static string RenderWhatsNewItems(IReadOnlyList<LibraryNewsItem> items)
	{
		if (items.Count == 0)
		{
			return "<div class=\"news-empty\">Chưa có nội dung mới.</div>";
		}
		StringBuilder stringBuilder = new StringBuilder();
		foreach (LibraryNewsItem item in items)
		{
			string discount = (string.IsNullOrWhiteSpace(item.DiscountText) ? item.BadgeText : item.DiscountText);
			StringBuilder stringBuilder2 = stringBuilder;
			StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(184, 4, stringBuilder2);
			handler.AppendLiteral("<article class=\"news-card\" role=\"button\" tabindex=\"0\" aria-label=\"Mở ");
			handler.AppendFormatted(Attr(item.Title));
			handler.AppendLiteral("\" data-host-action=\"openGame\" data-game-id=\"");
			handler.AppendFormatted(item.GameId, "D");
			handler.AppendLiteral("\">\r\n    ");
			handler.AppendFormatted(RenderNewsImage(item.ImageUrl, item.Title));
			handler.AppendLiteral("\r\n    <div class=\"news-meta\">\r\n        ");
			handler.AppendFormatted(RenderNewsPrice(item, discount));
			handler.AppendLiteral("\r\n    </div>\r\n</article>");
			stringBuilder2.Append(ref handler);
		}
		return stringBuilder.ToString();
	}

	private static string RenderOwnedItems(IReadOnlyList<DesktopLibraryListItemResponse> items)
	{
		if (items.Count == 0)
		{
			return string.Empty;
		}
		StringBuilder stringBuilder = new StringBuilder();
		foreach (DesktopLibraryListItemResponse item in items)
		{
			StringBuilder stringBuilder2 = stringBuilder;
			StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(433, 8, stringBuilder2);
			handler.AppendLiteral("<article class=\"owned-card\"\r\n         role=\"button\"\r\n         tabindex=\"0\"\r\n         aria-label=\"Mở ");
			handler.AppendFormatted(Attr(item.GameName));
			handler.AppendLiteral("\"\r\n         data-library-card\r\n         data-library-id=\"");
			handler.AppendFormatted(item.LibraryId, "D");
			handler.AppendLiteral("\"\r\n         data-title=\"");
			handler.AppendFormatted(Attr(item.GameName));
			handler.AppendLiteral("\"\r\n         data-acquired=\"");
			handler.AppendFormatted(item.AcquiredAt.Ticks);
			handler.AppendLiteral("\"\r\n         data-search=\"");
			handler.AppendFormatted(Attr($"{item.GameName} {item.VersionName} {item.Category} {item.OrderCode}"));
			handler.AppendLiteral("\">\r\n    ");
			handler.AppendFormatted(RenderOwnedImage(FirstImage(item.SpotlightImageUrl, item.PosterImageUrl), item.GameName));
			handler.AppendLiteral("\r\n    <div class=\"owned-body\">\r\n        <strong>");
			handler.AppendFormatted(Html(item.GameName));
			handler.AppendLiteral("</strong>\r\n        <div class=\"owned-meta\">\r\n            <span>MUA</span>\r\n            <small>• ");
			handler.AppendFormatted(Html(item.AcquiredAtText));
			handler.AppendLiteral("</small>\r\n        </div>\r\n    </div>\r\n</article>");
			stringBuilder2.Append(ref handler);
		}
		return stringBuilder.ToString();
	}

	private static string RenderTinyImage(string? imageUrl, string alt)
	{
		if (string.IsNullOrWhiteSpace(imageUrl))
		{
			return "<span class=\"tiny-fallback\">" + Html(Initial(alt)) + "</span>";
		}
		return $"<img src=\"{Attr(imageUrl)}\" alt=\"{Attr(alt)}\" loading=\"lazy\" />";
	}

	private static string RenderNewsImage(string? imageUrl, string alt)
	{
		if (string.IsNullOrWhiteSpace(imageUrl))
		{
			return "<div class=\"news-fallback\">" + Html(Initial(alt)) + "</div>";
		}
		return $"<img src=\"{Attr(imageUrl)}\" alt=\"{Attr(alt)}\" loading=\"lazy\" />";
	}

	private static string RenderOwnedImage(string? imageUrl, string alt)
	{
		if (string.IsNullOrWhiteSpace(imageUrl))
		{
			return "<div class=\"owned-fallback\">" + Html(Initial(alt)) + "</div>";
		}
		return $"<img src=\"{Attr(imageUrl)}\" alt=\"{Attr(alt)}\" loading=\"lazy\" />";
	}

	private static string RenderNewsPrice(LibraryNewsItem item, string? discount)
	{
		bool flag = !string.IsNullOrWhiteSpace(item.CurrentPriceText);
		bool flag2 = !string.IsNullOrWhiteSpace(item.ReferencePriceText) && !SamePrice(item.ReferencePriceText, item.CurrentPriceText);
		bool flag3 = !string.IsNullOrWhiteSpace(discount);
		if (flag || flag2 || flag3)
		{
			return $"<div class=\"news-price-stack\">\r\n    {(flag2 ? ("<s>" + Html(item.ReferencePriceText) + "</s>") : string.Empty)}\r\n    {(flag ? ("<strong>" + Html(item.CurrentPriceText) + "</strong>") : string.Empty)}\r\n    {(flag3 ? ("<span class=\"news-discount\">" + Html(discount) + "</span>") : string.Empty)}\r\n</div>";
		}
		return string.Empty;
	}

	private static string FirstImage(string? primary, string? fallback)
	{
		string? text;
		if (string.IsNullOrWhiteSpace(primary))
		{
			text = fallback;
			if (text == null)
			{
				return string.Empty;
			}
		}
		else
		{
			text = primary;
		}
		return text;
	}

	private static bool SamePrice(string? left, string? right)
	{
		if (!string.IsNullOrWhiteSpace(left) && !string.IsNullOrWhiteSpace(right))
		{
			return string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
		}
		return false;
	}

	private static string BuildStyles()
	{
		return ":root {\r\n    color-scheme: dark;\r\n    --bg: #0b1118;\r\n    --chrome: #101821;\r\n    --sidebar: #101923;\r\n    --sidebar-2: #0d151e;\r\n    --panel: #162435;\r\n    --panel-2: #1c3147;\r\n    --line: rgba(102, 192, 244, .16);\r\n    --line-strong: rgba(102, 192, 244, .36);\r\n    --text: #dbe8f4;\r\n    --muted: #7e91a5;\r\n    --blue: #66c0f4;\r\n    --green: #79bd22;\r\n}\r\n* { box-sizing: border-box; }\r\nhtml, body { min-height: 100%; margin: 0; }\r\n[hidden] { display: none !important; }\n/* Reuse the storefront loading visual without changing the library's scroll position. */\nbody.app-loading-active .library-shell { pointer-events: none; }\n.app-loading-overlay { cursor: wait; overflow: auto; }\n.app-loading-dialog { max-height: 100%; overflow: auto; }\n.app-loading-dialog:focus-visible { outline: 2px solid var(--blue); outline-offset: 4px; }\n.app-loading-copy { overflow-wrap: anywhere; }\n@media (max-width: 520px) {\n    .app-loading-overlay { padding: 16px; }\n    .app-loading-dialog { grid-template-columns: 1fr; justify-items: center; padding: 20px; text-align: center; }\n}\nbody {\n    min-width: 0;\r\n    overflow-x: hidden;\r\n    background: linear-gradient(180deg, #101923 0, #0b1118 26%, #0b1118 100%);\r\n    color: var(--text);\r\n    font-family: \"Segoe UI Variable Text\", \"Segoe UI\", Arial, sans-serif;\r\n}\r\nbutton, input, select { font: inherit; color: inherit; }\r\nbutton { cursor: pointer; }\n.library-shell .main-row--navigation-only { grid-template-columns: minmax(0, 1fr) auto; column-gap: 16px; height: auto; min-height: 46px; }\n.library-defender { display: flex; align-items: center; justify-self: end; gap: 10px; height: 38px; white-space: nowrap; }\n.library-defender > span:first-child { font-size: 13px; color: #b5c6d8; }\n.library-defender-switch { flex: 0 0 48px; width: 48px; height: 44px; padding: 10px 2px; border: 0; border-radius: 8px; background: transparent; }\n.library-defender-switch > span { display: block; width: 44px; height: 24px; border-radius: 20px; background: #657487; position: relative; }\n.library-defender-switch > span::after { content: ''; position: absolute; top: 3px; left: 3px; width: 18px; height: 18px; border-radius: 50%; background: #fff; }\n.library-defender-switch[aria-checked=\"true\"] > span { background: #247fa6; }\n.library-defender-switch[aria-checked=\"true\"] > span::after { left: 23px; }\n.library-defender-status { position: absolute; width: 1px; height: 1px; margin: -1px; padding: 0; overflow: hidden; clip-path: inset(50%); white-space: nowrap; }\n.library-defender button:disabled { opacity: .6; cursor: wait; }\n.library-defender[aria-busy=\"true\"] > span:first-child { color: #66c0f4; }\n@media (max-width: 560px) {\n    .library-shell .main-row--navigation-only { column-gap: 8px; }\n    .library-shell .primary-nav { gap: 10px; }\n    .library-shell .nav-item { font-size: 11px; letter-spacing: 0; }\n    .library-shell .nav-item--event { gap: 4px; }\n    .library-shell .nav-new-badge { min-width: 22px; padding-inline: 3px; font-size: 9px; }\n    .library-defender { gap: 6px; }\n    .library-defender > span:first-child { font-size: 12px; }\n    .library-defender-switch { width: 42px; flex-basis: 42px; }\n    .library-defender-switch > span { width: 38px; }\n    .library-defender-switch[aria-checked=\"true\"] > span::after { left: 17px; }\n}\n.library-shell {\n    height: 100dvh;\n    min-height: 0;\n    display: grid;\r\n    grid-template-columns: 330px minmax(0, 1fr);\r\n    grid-template-rows: auto minmax(0, 1fr);\r\n}\r\n.library-shell > .top-chrome {\n    grid-column: 1 / -1;\n    position: static;\n}\n.sidebar {\n    grid-column: 1;\r\n    grid-row: 2;\n    min-width: 0;\n    min-height: 0;\n    display: flex;\n    flex-direction: column;\n    overflow: hidden;\r\n    padding: 14px 14px 22px 16px;\r\n    border-right: 1px solid rgba(102,192,244,.06);\r\n    background: linear-gradient(90deg, #101923, #0d151e);\r\n}\r\n.sidebar-heading { display: grid; gap: 4px; margin-bottom: 16px; }\r\n.sidebar-heading strong { color: #d8e8f6; font-size: 19px; }\r\n.sidebar-heading span { color: #76879a; font-size: 15px; }\n.library-search-form { margin: 0 0 14px; min-width: 0; }\n.library-search-form > label { display: block; margin-bottom: 6px; font-size: 13px; color: #a7b9c9; }\n.library-search {\n    height: 40px;\r\n    display: grid;\r\n    grid-template-columns: minmax(0, 1fr) 42px;\r\n    border: 1px solid rgba(102,192,244,.08);\r\n    background: #0b1420;\r\n}\r\n.library-search input {\r\n    min-width: 0;\r\n    padding: 0 14px;\r\n    border: 0;\r\n    outline: 0;\r\n    background: transparent;\r\n    color: #a7b9c9;\r\n    font-size: 15px;\r\n}\r\n.library-search button {\n    display: grid;\n    place-items: center;\n    border: 1px solid rgba(102,192,244,.12);\r\n    background: #111d2b;\r\n    color: #9fb6ca;\n}\n.library-search svg { width: 20px; height: 20px; fill: none; stroke: currentColor; stroke-width: 2; stroke-linecap: round; }\n.library-search:focus-within { border-color: #66c0f4; }\n.library-search input::placeholder { color: #8fa0b6; }\n.library-list-toggle {\n    display: block;\n    min-height: 38px;\n    padding: 8px 12px;\n    border: 1px solid rgba(102,192,244,.36);\n    border-radius: 4px;\n    background: #162435;\n    font-size: 13px;\n}\n.library-list-toggle[aria-expanded=\"true\"] { background: #234460; }\n.library-navigation { display: flex; flex-direction: column; min-height: 0; flex: 1; }\n.library-shell :is(button, select):focus-visible { outline: 2px solid #66c0f4; outline-offset: -2px; }\n.library-count {\n    margin: 0;\n    color: #aab8c8;\r\n    font-size: 14px;\r\n    font-weight: 900;\r\n    letter-spacing: .12em;\r\n}\r\n.sidebar-list {\n    min-height: 0;\n    flex: 1;\n    overflow: auto;\n    scrollbar-width: thin;\n    scrollbar-color: #527490 #0d151e;\n    scrollbar-gutter: stable;\n    padding: 2px;\n}\r\n.sidebar-game {\r\n    width: 100%;\r\n    min-height: 34px;\r\n    display: grid;\r\n    grid-template-columns: 30px minmax(0, 1fr);\r\n    align-items: center;\r\n    gap: 10px;\r\n    padding: 4px 6px;\r\n    border: 0;\r\n    background: transparent;\r\n    color: #cbd8e6;\r\n    text-align: left;\r\n    font-size: 15px;\r\n}\r\n.sidebar-game:hover,\r\n.sidebar-game.is-selected { background: rgba(102,192,244,.11); color: #fff; }\r\n.sidebar-game img,\r\n.tiny-fallback {\r\n    width: 26px;\r\n    height: 26px;\r\n    object-fit: cover;\r\n    display: grid;\r\n    place-items: center;\r\n    background: #263548;\r\n    color: #bfeaff;\r\n    font-size: 12px;\r\n    font-weight: 900;\r\n}\r\n.sidebar-game span:last-child {\n    min-width: 0;\r\n    overflow: hidden;\r\n    text-overflow: ellipsis;\r\n    white-space: nowrap;\r\n}\n.sidebar-game:is(:hover, :focus-visible) span:last-child {\n    white-space: normal;\n    overflow-wrap: anywhere;\n}\n.sidebar-empty,\r\n.news-empty {\r\n    padding: 16px;\r\n    color: var(--muted);\r\n    border: 1px solid var(--line);\r\n    background: rgba(7,17,28,.62);\r\n}\r\n.content {\n    grid-column: 2;\r\n    grid-row: 2;\r\n    min-width: 0;\n    min-height: 0;\n    overflow-y: auto;\n    scrollbar-width: thin;\n    scrollbar-color: #527490 #0d1722;\n    padding: 18px 16px 80px 28px;\r\n    background: #0d1722;\r\n}\r\n.section-title {\n    display: flex;\n    flex-wrap: wrap;\n    align-items: baseline;\r\n    gap: 10px;\r\n    margin: 0 0 10px;\r\n}\r\n.section-title h1 {\r\n    margin: 0;\r\n    color: #e8f4ff;\r\n    font-size: 20px;\r\n    line-height: 1;\r\n}\r\n.section-title span { color: #9aadc0; font-size: 13px; font-weight: 700; }\n.news-rail-wrap { position: relative; }\r\n.news-rail {\r\n    display: grid;\r\n    grid-auto-flow: column;\r\n    grid-auto-columns: clamp(190px, 24vw, 286px);\r\n    gap: 12px;\r\n    overflow-x: auto;\r\n    scroll-behavior: smooth;\r\n    scrollbar-width: none;\r\n    -ms-overflow-style: none;\r\n    padding: 4px 0 18px;\r\n}\r\n.news-rail::-webkit-scrollbar {\r\n    display: none;\r\n    width: 0;\r\n    height: 0;\r\n}\r\n.news-card {\r\n    position: relative;\r\n    overflow: hidden;\r\n    border: 1px solid rgba(102,192,244,.15);\r\n    background: #1a2b3d;\r\n    box-shadow: 0 12px 26px rgba(0,0,0,.22);\r\n}\r\n.news-card img,\r\n.news-fallback {\r\n    width: 100%;\r\n    height: auto;\r\n    aspect-ratio: 16 / 8.4;\r\n    display: grid;\r\n    place-items: center;\r\n    object-fit: cover;\r\n    background: linear-gradient(135deg, #173d5b, #0b1621);\r\n    color: #c8ecff;\r\n    font-size: 34px;\r\n    font-weight: 950;\r\n}\r\n.news-meta {\r\n    position: absolute;\r\n    left: 0;\r\n    right: 0;\r\n    bottom: 0;\r\n    min-height: 42px;\r\n    display: flex;\r\n    align-items: center;\r\n    justify-content: flex-end;\r\n    padding: 0 10px;\r\n    background: linear-gradient(180deg, rgba(13, 28, 43, .88), rgba(17, 35, 52, .98));\r\n    border-top: 1px solid rgba(102,192,244,.16);\r\n}\r\n.news-price-stack {\r\n    min-width: 0;\r\n    display: flex;\r\n    align-items: center;\r\n    justify-content: flex-end;\r\n    gap: 6px;\r\n}\r\n.news-price-stack s {\r\n    color: #7890a4;\r\n    font-size: 12px;\r\n    white-space: nowrap;\r\n}\r\n.news-price-stack strong {\r\n    color: #d8f8ff;\r\n    font-size: 13px;\r\n    font-weight: 900;\r\n    white-space: nowrap;\r\n}\r\n.news-discount {\r\n    padding: 4px 8px;\r\n    background: #4c8b1f;\r\n    color: #c9f86a;\r\n    font-size: 13px;\r\n    font-weight: 900;\r\n}\r\n.library-toolbar {\n    min-height: 48px;\n    display: grid;\r\n    grid-template-columns: minmax(0, 1fr) auto auto;\r\n    align-items: center;\n    gap: 10px;\r\n    margin: 12px 0 16px;\n    padding-top: 12px;\n    border-top: 1px solid rgba(102,192,244,.11);\r\n}\r\n.library-toolbar label {\r\n    display: flex;\r\n    align-items: center;\r\n    gap: 9px;\r\n    color: #9fb0c1;\n    font-size: 14px;\r\n}\r\n.library-toolbar select,\r\n.installed-filter {\r\n    height: 35px;\r\n    border: 1px solid rgba(102,192,244,.16);\r\n    background: #121f2d;\r\n    color: #c8d8e8;\r\n}\r\n.library-toolbar select { padding: 0 34px 0 12px; }\r\n.installed-filter { padding: 0 13px; }\r\n.owned-grid {\r\n    display: grid;\r\n    grid-template-columns: repeat(auto-fit, minmax(min(100%, 252px), 1fr));\r\n    gap: 22px;\r\n    align-items: start;\r\n}\r\n.owned-card {\r\n    overflow: hidden;\r\n    border: 1px solid rgba(102,192,244,.13);\r\n    background: #17283b;\r\n    box-shadow: 0 12px 24px rgba(0,0,0,.22);\r\n}\r\n.owned-card:hover {\r\n    border-color: rgba(102,192,244,.38);\r\n    filter: brightness(1.08);\r\n}\r\n.owned-card:focus-visible, .news-card:focus-visible { outline: 2px solid #66c0f4; outline-offset: 3px; }\r\n.installed-filter:disabled, .library-search button:disabled { cursor: not-allowed; opacity: .56; }\r\n.owned-card img,\r\n.owned-fallback {\r\n    width: 100%;\r\n    aspect-ratio: 16 / 6.8;\r\n    display: grid;\r\n    place-items: center;\r\n    object-fit: cover;\r\n    background: linear-gradient(135deg, #253f50, #101923);\r\n    color: #c8ecff;\r\n    font-size: 32px;\r\n    font-weight: 950;\r\n}\r\n.owned-body { padding: 12px 13px 10px; }\r\n.owned-body strong {\n    display: block;\r\n    min-height: 40px;\r\n    overflow: hidden;\r\n    color: #dfefff;\r\n    font-size: 16px;\r\n    line-height: 1.22;\n    overflow-wrap: anywhere;\n}\r\n.owned-meta {\r\n    display: flex;\r\n    align-items: center;\r\n    gap: 8px;\r\n    margin-top: 5px;\r\n}\r\n.owned-meta span {\r\n    color: #66c0f4;\r\n    font-size: 13px;\r\n    font-weight: 900;\r\n}\r\n.owned-meta small {\r\n    color: #9fb0c1;\r\n    font-size: 13px;\r\n}\r\n.empty-state {\r\n    margin-top: 28px;\r\n    padding: 24px;\r\n    border: 1px solid var(--line);\r\n    background: rgba(10,23,35,.75);\r\n}\r\n.empty-state strong { display: block; color: #edf7ff; }\r\n.empty-state span { display: block; margin-top: 6px; color: var(--muted); }\r\n.toast-host {\r\n    position: fixed;\r\n    right: 22px;\r\n    bottom: 22px;\r\n    z-index: 30;\r\n    display: grid;\r\n    gap: 10px;\r\n}\r\n.toast {\r\n    max-width: 360px;\r\n    padding: 13px 15px;\r\n    border: 1px solid rgba(102,192,244,.25);\r\n    border-radius: 5px;\r\n    background: rgba(9,23,34,.96);\r\n    color: #dff2ff;\r\n    box-shadow: 0 18px 36px rgba(0,0,0,.32);\r\n    font-size: 13px;\r\n}\r\n@media (max-width: 1180px) {\r\n    .library-shell { grid-template-columns: 260px minmax(0, 1fr); }\r\n    .content { padding: 16px 14px 72px 18px; }\r\n    .news-rail { grid-auto-columns: clamp(180px, 28vw, 240px); }\r\n    .owned-grid {\r\n        grid-template-columns: repeat(auto-fit, minmax(min(100%, 230px), 1fr));\r\n        gap: 18px;\r\n    }\r\n    .library-toolbar {\r\n        grid-template-columns: minmax(0, 1fr) minmax(150px, auto) auto;\r\n        align-items: center;\r\n    }\r\n}\r\n@media (max-width: 960px) {\n    .library-shell {\n        height: auto;\n        min-height: 100dvh;\n        grid-template-columns: minmax(0, 1fr);\n        grid-template-rows: auto auto 1fr;\n    }\r\n    .library-shell > .top-chrome {\r\n        grid-row: 1;\r\n    }\r\n    .library-shell > .sidebar {\r\n        grid-column: 1;\r\n        grid-row: 2;\r\n        display: grid;\r\n        grid-template-columns: minmax(0, 1fr) auto;\n        align-items: end;\n        gap: 12px;\r\n        padding: 12px 14px;\r\n        border-right: 0;\r\n        border-bottom: 1px solid rgba(102,192,244,.08);\r\n    }\r\n    .sidebar-heading { display: none; }\n    .library-search-form { margin: 0; }\n    .library-navigation { grid-column: 1 / -1; }\n    .sidebar-list {\n        max-height: min(240px, 45dvh);\n        overflow-x: hidden;\n        overflow-y: auto;\n    }\r\n    .library-shell > .content {\r\n        grid-column: 1;\r\n        grid-row: 3;\r\n    }\r\n    .content {\n        overflow: visible;\n        padding: 16px 14px 72px;\n    }\r\n    .news-rail {\r\n        grid-auto-columns: clamp(165px, 30vw, 220px);\r\n        gap: 10px;\r\n    }\r\n    .news-meta {\r\n        min-height: 38px;\r\n        align-items: center;\r\n        padding: 7px 8px;\r\n    }\r\n    .news-price-stack {\r\n        justify-content: flex-end;\r\n        flex-wrap: wrap;\r\n        gap: 4px;\r\n    }\r\n    .news-price-stack s {\r\n        font-size: 11px;\r\n    }\r\n    .news-price-stack strong,\r\n    .news-discount {\r\n        font-size: 12px;\r\n    }\r\n    .news-discount {\r\n        padding: 3px 6px;\r\n    }\r\n    .library-toolbar {\n        grid-template-columns: minmax(0, 1fr) auto;\n        min-height: 0;\r\n        align-items: stretch;\r\n    }\r\n    .library-count { grid-column: 1 / -1; }\n    .library-toolbar label {\r\n        justify-content: space-between;\r\n    }\r\n}\r\n@media (max-width: 720px) {\r\n    .section-title {\r\n        display: grid;\r\n        align-items: start;\r\n    }\r\n    .news-rail {\r\n        grid-auto-columns: clamp(150px, 42vw, 190px);\r\n    }\r\n    .news-price-stack s {\r\n        display: none;\r\n    }\r\n    .news-price-stack strong,\r\n    .news-discount {\r\n        font-size: 11px;\r\n    }\r\n    .owned-grid {\r\n        grid-template-columns: 1fr;\r\n    }\r\n}";
	}

	private static string BuildScript()
	{
		return "(() => {\r\n    const post = message => window.chrome?.webview?.postMessage(message);\n    const defenderControl = document.querySelector('[data-defender-control]');\n    const defenderToggle = document.querySelector('[data-defender-toggle]');\n    const defenderStatus = document.querySelector('[data-defender-status]');\n    let defenderBusy = defenderControl.getAttribute('aria-busy') === 'true';\n    window.__nestgDefender = {\n        update(state) {\n            defenderBusy = Boolean(state.busy);\n            defenderControl.setAttribute('aria-busy', String(defenderBusy));\n            defenderToggle.setAttribute('aria-checked', String(Boolean(state.enabled)));\n            defenderStatus.textContent = defenderBusy ? 'Đang xử lý…' : (state.status || 'Chưa kiểm tra');\n            defenderToggle.title = defenderStatus.textContent;\n            defenderControl.querySelectorAll('button').forEach(button => button.disabled = defenderBusy);\n        }\n    };\n    function requestDefender(enabled) {\n        if (defenderBusy) return;\n        defenderBusy = true;\n        defenderControl.setAttribute('aria-busy', 'true');\n        defenderControl.querySelectorAll('button').forEach(button => button.disabled = true);\n        defenderStatus.textContent = 'Đang xử lý…';\n        defenderToggle.title = defenderStatus.textContent;\n        try {\n            if (!window.chrome?.webview?.postMessage) throw new Error('Host unavailable');\n            post({type: 'setSteamExclusions', enabled});\n        } catch {\n            defenderBusy = false;\n            defenderControl.setAttribute('aria-busy', 'false');\n            defenderControl.querySelectorAll('button').forEach(button => button.disabled = false);\n            defenderStatus.textContent = 'Không gửi được yêu cầu. Vui lòng thử lại.';\n            defenderToggle.title = defenderStatus.textContent;\n            window.__nestgHome?.toast(defenderStatus.textContent);\n        }\n    }\n    defenderToggle.addEventListener('click', () => requestDefender(defenderToggle.getAttribute('aria-checked') !== 'true'));\n    const searchInput = document.querySelector('[data-library-search]');\n    const searchForm = document.querySelector('[data-library-search-form]');\n    const listToggle = document.querySelector('[data-library-list-toggle]');\n    const libraryNavigation = document.querySelector('[data-library-navigation]');\n    const compactLayout = window.matchMedia('(max-width: 960px)');\n    let compactListExpanded = false;\n    const sidebarItems = Array.from(document.querySelectorAll('.sidebar-game[data-library-id]'));\r\n    const cards = Array.from(document.querySelectorAll('[data-library-card]'));\r\n    const grid = document.querySelector('[data-owned-grid]');\r\n    const emptyState = document.querySelector('[data-empty-state]');\n    const sidebarEmpty = document.querySelector('[data-sidebar-empty]');\n    const totalCount = document.querySelector('[data-total-count]');\r\n    const sortSelect = document.querySelector('[data-sort-select]');\r\n    const newsRail = document.querySelector('[data-news-rail]');\r\n    const toastHost = document.querySelector('[data-toast-host]');\r\n    const accountToggle = document.querySelector('[data-account-toggle]');\r\n    const accountMenu = document.querySelector('[data-account-menu]');\n    const libraryShell = document.querySelector('.library-shell');\n    const loadingOverlay = document.querySelector('[data-app-loading]');\n    const loadingDialog = loadingOverlay.querySelector('.app-loading-dialog');\n    let isOpeningItem = false;\n    let focusBeforeLoading = null;\n\n    window.__nestgLoading = {\n        show(title = 'Đang mở game', message = 'NestG đang tải chi tiết game trong thư viện.') {\n            // The host can repeat show() after receiving the click; preserve the original focus.\n            if (loadingOverlay.hidden) focusBeforeLoading = document.activeElement;\n            loadingOverlay.querySelector('[data-app-loading-title]').textContent = title;\n            loadingOverlay.querySelector('[data-app-loading-message]').textContent = message;\n            setAccountMenuOpen(false);\n            loadingOverlay.hidden = false;\n            loadingDialog.focus({ preventScroll: true });\n            libraryShell.inert = true;\n            libraryShell.setAttribute('aria-busy', 'true');\n            document.body.classList.add('app-loading-active');\n        },\n        hide() {\n            const wasVisible = !loadingOverlay.hidden;\n            libraryShell.inert = false;\n            libraryShell.removeAttribute('aria-busy');\n            document.body.classList.remove('app-loading-active');\n            loadingOverlay.hidden = true;\n            isOpeningItem = false;\n            if (wasVisible && focusBeforeLoading?.isConnected) {\n                // A resize during loading may have collapsed the sidebar.\n                const destination = libraryNavigation.hidden && libraryNavigation.contains(focusBeforeLoading)\n                    ? listToggle : focusBeforeLoading;\n                destination.focus?.({ preventScroll: true });\n            }\n            focusBeforeLoading = null;\n        }\n    };\n\n    // inert blocks real input; capture guards also reject queued/synthetic activations.\n    for (const eventName of ['click', 'dblclick', 'pointerdown', 'submit']) {\n        document.addEventListener(eventName, event => {\n            if (loadingOverlay.hidden) return;\n            event.preventDefault();\n            event.stopImmediatePropagation();\n        }, true);\n    }\n    document.addEventListener('keydown', event => {\n        if (loadingOverlay.hidden || !['Tab', 'Enter', ' ', 'Escape'].includes(event.key)) return;\n        event.preventDefault();\n        event.stopImmediatePropagation();\n        loadingDialog.focus({ preventScroll: true });\n    }, true);\n\n    function openItem(message, trigger) {\n        if (isOpeningItem || !loadingOverlay.hidden) return;\n        isOpeningItem = true;\n        // Mouse activation of an article does not focus it automatically.\n        trigger?.focus({ preventScroll: true });\n        window.__nestgLoading.show();\n        try {\n            if (!window.chrome?.webview?.postMessage) throw new Error('Host unavailable');\n            post(message);\n        } catch {\n            window.__nestgLoading.hide();\n            window.__nestgHome.toast('Không thể mở game. Vui lòng thử lại hoặc mở lại ứng dụng.');\n        }\n    }\n\n    function updateLibraryNavigation() {\n        if (!listToggle || !libraryNavigation) return;\n        const expanded = !compactLayout.matches || compactListExpanded;\n        // Show the destination before transferring focus; hide controls only afterwards.\n        if (compactLayout.matches) listToggle.hidden = false;\n        if (!expanded && libraryNavigation.contains(document.activeElement)) listToggle.focus();\n        if (!compactLayout.matches && document.activeElement === listToggle) searchInput?.focus();\n        listToggle.hidden = !compactLayout.matches;\n        libraryNavigation.hidden = !expanded;\n        listToggle.setAttribute('aria-expanded', String(expanded));\n        listToggle.textContent = expanded ? 'Thu gọn danh sách' : 'Danh sách game';\n    }\n    listToggle?.addEventListener('click', () => {\n        compactListExpanded = !compactListExpanded;\n        updateLibraryNavigation();\n    });\n    compactLayout.addEventListener('change', updateLibraryNavigation);\n    updateLibraryNavigation();\n\r\n    function setAccountMenuOpen(isOpen) {\r\n        if (!accountToggle || !accountMenu) return;\r\n        accountMenu.hidden = !isOpen;\r\n        accountToggle.classList.toggle('is-open', isOpen);\r\n        accountToggle.setAttribute('aria-expanded', String(isOpen));\r\n    }\r\n\r\n    window.__nestgHome = {\r\n        setCartCount() {},\r\n        setWalletBalance() {},\r\n        toast(message) {\r\n            if (!toastHost || !message) return;\r\n            const toast = document.createElement('div');\r\n            toast.className = 'toast';\r\n            toast.textContent = message;\r\n            toastHost.append(toast);\r\n            window.setTimeout(() => toast.remove(), 3400);\r\n        }\r\n    };\r\n\r\n    function normalize(value) {\r\n        return String(value || '').trim().toLowerCase();\r\n    }\r\n\r\n    function getQuery() {\r\n        return normalize(searchInput?.value);\n    }\r\n\r\n    function applyFilter() {\r\n        const query = getQuery();\r\n        const visibleIds = new Set();\n\r\n        cards.forEach(card => {\r\n            const visible = !query || normalize(card.getAttribute('data-search')).includes(query);\r\n            card.hidden = !visible;\r\n            if (visible) visibleIds.add(card.getAttribute('data-library-id'));\n        });\n        sidebarItems.forEach(item => {\n            item.hidden = !visibleIds.has(item.getAttribute('data-library-id'));\n        });\n        if (totalCount) totalCount.textContent = String(visibleIds.size);\n        if (emptyState) emptyState.hidden = visibleIds.size !== 0;\n        if (sidebarEmpty) sidebarEmpty.hidden = visibleIds.size !== 0 || cards.length === 0;\n        const whatsNew = document.querySelector('.whats-new');\n        if (whatsNew) whatsNew.hidden = Boolean(query);\n    }\r\n\r\n    function sortCards() {\r\n        if (!grid) return;\r\n        const mode = sortSelect?.value || 'default';\r\n        const sorted = [...cards].sort((left, right) => {\r\n            if (mode === 'title') {\r\n                return String(left.getAttribute('data-title') || '').localeCompare(\r\n                    String(right.getAttribute('data-title') || ''),\r\n                    'vi',\r\n                    { sensitivity: 'base' });\r\n            }\r\n            if (mode === 'recent') {\r\n                return Number(right.getAttribute('data-acquired') || 0) -\r\n                    Number(left.getAttribute('data-acquired') || 0);\r\n            }\r\n            return 0;\r\n        });\r\n        sorted.forEach(card => grid.append(card));\r\n        applyFilter();\r\n    }\r\n\r\n    function openLibraryItem(libraryId, trigger) {\n        if (!libraryId || isOpeningItem) return;\n        sidebarItems.forEach(item => {\r\n            item.classList.toggle('is-selected', item.getAttribute('data-library-id') === libraryId);\r\n        });\r\n        openItem({ type: 'openLibraryItem', libraryId }, trigger);\n    }\r\n\r\n    function startNewsRailAutoScroll() {\r\n        if (!newsRail || window.matchMedia('(prefers-reduced-motion: reduce)').matches) return;\r\n\r\n        const originals = Array.from(newsRail.querySelectorAll('.news-card:not([data-loop-clone])'));\r\n        if (originals.length < 2) return;\r\n\r\n        originals.forEach((card, index) => {\r\n            card.setAttribute('data-loop-source', String(index));\r\n        });\r\n\r\n        originals.forEach((card, index) => {\r\n            const clone = card.cloneNode(true);\r\n            clone.setAttribute('data-loop-clone', 'true');\r\n            clone.setAttribute('data-loop-source', String(index));\r\n            clone.setAttribute('aria-hidden', 'true');\r\n            clone.setAttribute('tabindex', '-1');\r\n            newsRail.append(clone);\r\n        });\r\n\r\n        let loopWidth = 0;\r\n        let lastFrameTime = 0;\r\n        let autoPosition = 0;\r\n        const pixelsPerSecond = 48;\r\n\r\n        function normalizeLoopPosition() {\r\n            if (loopWidth <= 0) return;\r\n\r\n            autoPosition = newsRail.scrollLeft;\r\n            while (autoPosition >= loopWidth) {\r\n                autoPosition -= loopWidth;\r\n            }\r\n\r\n            while (autoPosition < 0) {\r\n                autoPosition += loopWidth;\r\n            }\r\n\r\n            newsRail.scrollLeft = autoPosition;\r\n        }\r\n\r\n        function refreshLoopMetrics() {\r\n            const firstOriginal = originals[0];\r\n            const firstClone = newsRail.querySelector('.news-card[data-loop-clone=\"true\"]');\r\n            loopWidth = firstOriginal && firstClone\r\n                ? Math.max(0, firstClone.getBoundingClientRect().left - firstOriginal.getBoundingClientRect().left)\r\n                : 0;\r\n            normalizeLoopPosition();\r\n        }\r\n\r\n        function canAutoScroll() {\r\n            return loopWidth > newsRail.clientWidth + 4 &&\r\n                newsRail.querySelectorAll('.news-card:not([data-loop-clone]):not([hidden])').length > 1;\r\n        }\r\n\r\n        function animate(now) {\r\n            if (!lastFrameTime) {\r\n                lastFrameTime = now;\r\n            }\r\n\r\n            const elapsed = Math.min(80, now - lastFrameTime);\r\n            lastFrameTime = now;\r\n\r\n            if (!document.hidden && loadingOverlay.hidden && canAutoScroll()) {\n                autoPosition += pixelsPerSecond * elapsed / 1000;\r\n                while (autoPosition >= loopWidth) {\r\n                    autoPosition -= loopWidth;\r\n                }\r\n\r\n                newsRail.scrollLeft = autoPosition;\r\n            } else {\r\n                autoPosition = newsRail.scrollLeft;\r\n            }\r\n\r\n            window.requestAnimationFrame(animate);\r\n        }\r\n\r\n        newsRail.style.scrollBehavior = 'auto';\r\n        window.addEventListener('resize', () => window.setTimeout(refreshLoopMetrics, 80));\r\n        document.addEventListener('visibilitychange', () => {\r\n            lastFrameTime = 0;\r\n            if (!document.hidden) {\r\n                normalizeLoopPosition();\r\n            }\r\n        });\r\n\r\n        window.requestAnimationFrame(refreshLoopMetrics);\r\n        window.setTimeout(refreshLoopMetrics, 160);\r\n        window.setTimeout(refreshLoopMetrics, 700);\r\n        window.requestAnimationFrame(animate);\r\n    }\r\n\r\n    searchInput?.addEventListener('input', applyFilter);\n    searchInput?.addEventListener('search', applyFilter);\n    searchForm?.addEventListener('submit', event => {\n        event.preventDefault();\n        applyFilter();\n    });\n    sortSelect?.addEventListener('change', sortCards);\r\n    startNewsRailAutoScroll();\r\n\r\n    document.addEventListener('pointerdown', event => {\r\n        const target = event.target instanceof Element ? event.target : null;\r\n        if (!target || target.closest('button, input, select, label, [data-library-card], [data-host-action]')) return;\n        if (target.closest('[data-drag-region]')) post({ type: 'dragWindow' });\r\n    });\r\n\r\n    document.addEventListener('click', event => {\r\n        const target = event.target instanceof Element ? event.target : null;\r\n        if (!target) return;\r\n\r\n        if (target.closest('[data-account-toggle]')) {\r\n            event.preventDefault();\r\n            event.stopPropagation();\r\n            setAccountMenuOpen(Boolean(accountMenu?.hidden));\r\n            return;\r\n        }\r\n\r\n        if (!target.closest('[data-account-menu]')) {\r\n            setAccountMenuOpen(false);\r\n        }\r\n\r\n        const libraryTarget = target.closest('[data-library-id]');\r\n        if (libraryTarget) {\r\n            event.preventDefault();\r\n            openLibraryItem(libraryTarget.getAttribute('data-library-id'), libraryTarget);\n            return;\r\n        }\r\n\r\n        const actionTarget = target.closest('[data-host-action]');\r\n        if (!actionTarget) return;\r\n        event.preventDefault();\r\n        const message = {\n            type: actionTarget.getAttribute('data-host-action'),\n            gameId: actionTarget.getAttribute('data-game-id')\n        };\n        if (message.type === 'openGame') openItem(message, actionTarget);\n        else post(message);\n    });\r\n\r\n    document.addEventListener('keydown', event => {\n        if (event.key === 'Escape') {\n            if (accountMenu && !accountMenu.hidden) {\n                setAccountMenuOpen(false);\n                accountToggle?.focus();\n                return;\n            }\n            if (compactLayout.matches && compactListExpanded &&\n                (libraryNavigation?.contains(document.activeElement) || document.activeElement === listToggle)) {\n                compactListExpanded = false;\n                listToggle?.focus();\n                updateLibraryNavigation();\n                return;\n            }\n        }\n        const target = event.target instanceof Element\r\n            ? event.target.closest('[role=\"button\"][data-library-id], [role=\"button\"][data-host-action]')\r\n            : null;\r\n        if (!target || (event.key !== 'Enter' && event.key !== ' ')) return;\r\n        event.preventDefault();\r\n        target.click();\r\n    });\r\n\r\n    document.addEventListener('error', event => {\r\n        const image = event.target instanceof HTMLImageElement ? event.target : null;\r\n        if (!image) return;\r\n        const fallback = document.createElement('div');\r\n        fallback.className = image.closest('.owned-card') ? 'owned-fallback' :\r\n            image.closest('.news-card') ? 'news-fallback' : 'tiny-fallback';\r\n        fallback.textContent = (image.alt || 'N').trim().charAt(0).toUpperCase();\r\n        image.replaceWith(fallback);\r\n    }, true);\r\n\r\n    applyFilter();\r\n})();";
	}

	private static string Initial(string? value)
	{
		return (string.IsNullOrWhiteSpace(value) ? "N" : value.Trim()).Substring(0, 1).ToUpperInvariant();
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
		if (!value.Contains('Â') && !value.Contains('Ã') && !value.Contains('Ä') && !value.Contains('Å') && !value.Contains('Æ') && !value.Contains("áº", StringComparison.Ordinal))
		{
			return value.Contains("á»", StringComparison.Ordinal);
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

	private static string Html(string? value)
	{
		return WebUtility.HtmlEncode(value ?? string.Empty);
	}

	private static string Attr(string? value)
	{
		return WebUtility.HtmlEncode(value ?? string.Empty);
	}
}
