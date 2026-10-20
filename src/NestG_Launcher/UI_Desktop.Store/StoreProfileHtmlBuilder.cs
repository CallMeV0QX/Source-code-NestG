using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;

namespace UI_Desktop.Store;

internal static class StoreProfileHtmlBuilder
{
	public static string BuildDocument(DesktopAccountResponse account, StoreWebHomeContext context)
	{
		return $"<!DOCTYPE html>\r\n<html lang=\"vi\">\r\n<head>\r\n<meta charset=\"utf-8\" />\r\n<meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\" />\r\n<title>NestG Profile</title>\r\n<style>{BuildStyles()}{StoreWarpHeaderHtml.Styles}{StoreWindowsUpdateHeaderHtml.Styles}{StoreSteamConnectionHeaderHtml.Styles}{StoreSupportContactHeaderHtml.Styles}</style>\r\n</head>\r\n<body>\r\n<div class=\"profile-shell\">\r\n    <header class=\"profile-chrome\" data-drag-region>\r\n        <div class=\"nestg-brand-warp\">\r\n            <div class=\"brand-cluster\">\r\n                <span class=\"brand-mark\">N</span>\r\n                <span class=\"brand-copy\">\r\n                    <strong>NestG</strong>\r\n                    <small>Launcher Profile</small>\r\n                </span>\r\n            </div>\r\n            {StoreWarpHeaderHtml.Render()}\r\n            {StoreWindowsUpdateHeaderHtml.Render()}\r\n            {StoreSteamConnectionHeaderHtml.Render()}\r\n        </div>\r\n        <nav class=\"profile-nav\">\r\n            <button type=\"button\" data-host-action=\"backHome\">Cửa hàng</button>\r\n            <button type=\"button\" data-host-action=\"openWalletTopup\">Nạp ví</button>\r\n            <button type=\"button\" data-host-action=\"openOrderHistory\">Đơn hàng</button>\r\n        </nav>\r\n        <div class=\"nestg-header-account\">\r\n            {StoreSupportContactHeaderHtml.Render()}\r\n            {StoreAccountMenuHtml.Build(account, "profile", context.NotificationUnreadCount)}\r\n        </div>\r\n        <div class=\"window-controls\">\r\n            <button type=\"button\" class=\"window-button\" data-host-action=\"minimize\" aria-label=\"Thu nhỏ cửa sổ\">-</button>\r\n            <button type=\"button\" class=\"window-button\" data-host-action=\"toggleMaximize\" aria-label=\"Phóng to hoặc khôi phục cửa sổ\">&#9633;</button>\r\n            <button type=\"button\" class=\"window-button is-close\" data-host-action=\"close\" aria-label=\"Đóng cửa sổ\">&#215;</button>\r\n        </div>\r\n    </header>\r\n\r\n    <main class=\"profile-content\">\r\n        <section class=\"hero-card\">\r\n            <div class=\"hero-identity\">\r\n                <div class=\"avatar-ring\">\r\n                    <span>{Initial(account.DisplayName)}</span>\r\n                </div>\r\n                <div>\r\n                    <span class=\"eyebrow\">TÀI KHOẢN CÁ NHÂN</span>\r\n                    <h1>{Html(account.DisplayName)}</h1>\r\n                    <p>{Html(account.UserEmail)}</p>\r\n                    <div class=\"status-row\">\r\n                        {RenderVerifiedBadge(account.EmailVerified)}\r\n                        <span>{Html(account.WalletStatusText)}</span>\r\n                        <span>Phiên bản {Html(context.VersionText)}</span>\r\n                    </div>\r\n                </div>\r\n            </div>\r\n            <div class=\"wallet-card\">\r\n                <span>Số dư NestG</span>\r\n                <strong>{Html(account.WalletBalanceText)}</strong>\r\n                <small>Trạng thái ví: {Html(account.WalletStatusText)}</small>\r\n                <button type=\"button\" data-host-action=\"openWalletTopup\">Nạp thêm số dư</button>\r\n            </div>\r\n        </section>\r\n\r\n        <section class=\"metric-grid\">\r\n            {RenderMetric("Đơn hàng", account.TotalOrders.ToString("N0"), "Tổng đơn đã tạo")}\r\n            {RenderMetric("Thư viện", account.TotalLibraryItems.ToString("N0"), "Game đang sở hữu")}\r\n            {RenderMetric("Đã thanh toán", account.PaidOrders.ToString("N0"), "Đơn hoàn tất")}\r\n            {RenderMetric("Đang chờ", account.PendingOrders.ToString("N0"), "Đơn cần xử lý")}\r\n        </section>\r\n\r\n        <section class=\"profile-layout\">\r\n            <div class=\"panel personal-panel\">\r\n                <div class=\"panel-heading\">\r\n                    <span>Thông tin tài khoản</span>\r\n                    <button type=\"button\" data-host-action=\"refreshProfile\">Làm mới</button>\r\n                </div>\r\n                <div class=\"info-list\">\r\n                    {RenderInfo("Tên hiển thị", account.DisplayName)}\r\n                    {RenderInfo("Email", account.UserEmail)}\r\n                    {RenderInfo("Xác thực email", account.EmailVerified ? "Đã xác thực" : "Chưa xác thực")}\r\n                    {RenderInfo("Ngày tạo", account.CreatedAtText)}\r\n                    {RenderInfo("Đăng nhập gần nhất", account.LastLoginAtText ?? "Chưa có dữ liệu")}\r\n                    {RenderInfo("Mã người dùng", account.UserId.ToString("D"))}\r\n                </div>\r\n            </div>\r\n\r\n            <div class=\"panel timeline-panel\">\r\n                <div class=\"panel-heading\">\r\n                    <span>Hoạt động gần đây</span>\r\n                    <small>{account.RecentOrders.Count} đơn gần nhất</small>\r\n                </div>\r\n                <div class=\"activity-list\">\r\n                    {RenderRecentOrders(account.RecentOrders)}\r\n                </div>\r\n            </div>\r\n        </section>\r\n\r\n        <section class=\"profile-layout profile-layout--bottom\">\r\n            <div class=\"panel library-panel\">\r\n                <div class=\"panel-heading\">\r\n                    <span>Game mới trong thư viện</span>\r\n                    <small>{account.LibraryItems.Count} game</small>\r\n                </div>\r\n                <div class=\"library-strip\">\r\n                    {RenderLibraryItems(account.LibraryItems.Take(6).ToArray())}\r\n                </div>\r\n            </div>\r\n\r\n            <div class=\"panel wallet-panel\">\r\n                <div class=\"panel-heading\">\r\n                    <span>Giao dịch ví</span>\r\n                    <small>{account.WalletTransactions.Count} giao dịch</small>\r\n                </div>\r\n                <div class=\"transaction-list\">\r\n                    {RenderWalletTransactions(account.WalletTransactions.Take(5).ToArray())}\r\n                </div>\r\n            </div>\r\n        </section>\r\n    </main>\r\n\r\n    <div class=\"toast-host\" data-toast-host></div>\r\n</div>\r\n{BuildLoadingOverlay()}\r\n<script>{BuildScript()}</script>\r\n</body>\r\n</html>";
	}

	private static string RenderMetric(string label, string value, string hint)
	{
		return $"<article class=\"metric-card\">\r\n    <span>{Html(label)}</span>\r\n    <strong>{Html(value)}</strong>\r\n    <small>{Html(hint)}</small>\r\n</article>";
	}

	private static string RenderInfo(string label, string value)
	{
		return $"<div class=\"info-item\">\r\n    <span>{Html(label)}</span>\r\n    <strong>{Html(value)}</strong>\r\n</div>";
	}

	private static string RenderVerifiedBadge(bool isVerified)
	{
		if (!isVerified)
		{
			return "<span class=\"status-badge status-badge--warn\">Email chưa xác thực</span>";
		}
		return "<span class=\"status-badge status-badge--good\">Email đã xác thực</span>";
	}

	private static string RenderRecentOrders(IReadOnlyList<DesktopAccountOrderResponse> orders)
	{
		if (orders.Count == 0)
		{
			return "<div class=\"empty-state\">Chưa có đơn hàng nào.</div>";
		}
		StringBuilder stringBuilder = new StringBuilder();
		foreach (DesktopAccountOrderResponse order in orders)
		{
			StringBuilder stringBuilder2 = stringBuilder;
			StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(194, 5, stringBuilder2);
			handler.AppendLiteral("<article class=\"activity-card\">\r\n    <div>\r\n        <strong>");
			handler.AppendFormatted(Html(order.OrderCode));
			handler.AppendLiteral("</strong>\r\n        <span>");
			handler.AppendFormatted(Html(order.LineSummary));
			handler.AppendLiteral("</span>\r\n    </div>\r\n    <div>\r\n        <strong>");
			handler.AppendFormatted(Html(order.TotalAmountText));
			handler.AppendLiteral("</strong>\r\n        <small>");
			handler.AppendFormatted(Html(order.StatusText));
			handler.AppendLiteral(" · ");
			handler.AppendFormatted(Html(order.CreatedAtText));
			handler.AppendLiteral("</small>\r\n    </div>\r\n</article>");
			stringBuilder2.Append(ref handler);
		}
		return stringBuilder.ToString();
	}

	private static string RenderLibraryItems(IReadOnlyList<DesktopAccountLibraryItemResponse> items)
	{
		if (items.Count == 0)
		{
			return "<div class=\"empty-state\">Thư viện chưa có game.</div>";
		}
		StringBuilder stringBuilder = new StringBuilder();
		foreach (DesktopAccountLibraryItemResponse item in items)
		{
			StringBuilder stringBuilder2 = stringBuilder;
			StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(146, 4, stringBuilder2);
			handler.AppendLiteral("<article class=\"library-card\">\r\n    ");
			handler.AppendFormatted(RenderImage(item.PosterImageUrl, item.GameName));
			handler.AppendLiteral("\r\n    <div>\r\n        <strong>");
			handler.AppendFormatted(Html(item.GameName));
			handler.AppendLiteral("</strong>\r\n        <span>");
			handler.AppendFormatted(Html(item.VersionName));
			handler.AppendLiteral("</span>\r\n        <small>");
			handler.AppendFormatted(Html(item.AcquiredAtText));
			handler.AppendLiteral("</small>\r\n    </div>\r\n</article>");
			stringBuilder2.Append(ref handler);
		}
		return stringBuilder.ToString();
	}

	private static string RenderWalletTransactions(IReadOnlyList<DesktopAccountWalletTransactionResponse> transactions)
	{
		if (transactions.Count == 0)
		{
			return "<div class=\"empty-state\">Chưa có giao dịch ví.</div>";
		}
		StringBuilder stringBuilder = new StringBuilder();
		foreach (DesktopAccountWalletTransactionResponse transaction in transactions)
		{
			string value = (string.Equals(transaction.DirectionTone, "positive", StringComparison.OrdinalIgnoreCase) ? "is-positive" : (string.Equals(transaction.DirectionTone, "negative", StringComparison.OrdinalIgnoreCase) ? "is-negative" : string.Empty));
			StringBuilder stringBuilder2 = stringBuilder;
			StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(198, 6, stringBuilder2);
			handler.AppendLiteral("<article class=\"transaction-card ");
			handler.AppendFormatted(value);
			handler.AppendLiteral("\">\r\n    <div>\r\n        <strong>");
			handler.AppendFormatted(Html(transaction.Title));
			handler.AppendLiteral("</strong>\r\n        <span>");
			handler.AppendFormatted(Html(transaction.Subtitle));
			handler.AppendLiteral("</span>\r\n    </div>\r\n    <div>\r\n        <strong>");
			handler.AppendFormatted(Html(transaction.AmountText));
			handler.AppendLiteral("</strong>\r\n        <small>");
			handler.AppendFormatted(Html(transaction.StatusText));
			handler.AppendLiteral(" · ");
			handler.AppendFormatted(Html(transaction.CreatedAtText));
			handler.AppendLiteral("</small>\r\n    </div>\r\n</article>");
			stringBuilder2.Append(ref handler);
		}
		return stringBuilder.ToString();
	}

	private static string RenderImage(string? imageUrl, string alt)
	{
		if (string.IsNullOrWhiteSpace(imageUrl))
		{
			return "<div class=\"image-fallback\">" + Html(Initial(alt)) + "</div>";
		}
		return $"<img src=\"{Attr(imageUrl)}\" alt=\"{Attr(alt)}\" loading=\"lazy\" />";
	}

	private static string BuildStyles()
	{
		return ":root {\r\n    color-scheme: dark;\r\n    --bg: #070d14;\r\n    --panel: #0d1a26;\r\n    --panel-2: #102234;\r\n    --panel-3: #132b42;\r\n    --line: rgba(102, 192, 244, .16);\r\n    --line-strong: rgba(102, 192, 244, .34);\r\n    --text: #edf6ff;\r\n    --muted: #91a7bb;\r\n    --soft: #c8d9ea;\r\n    --cyan: #66c0f4;\r\n    --blue: #1c7fbd;\r\n    --green: #9bd33d;\r\n    --red: #ff8d8d;\r\n}\r\n* { box-sizing: border-box; }\r\nhtml, body { min-height: 100%; margin: 0; }\r\n[hidden] { display: none !important; }\r\nbody {\r\n    min-width: 1000px;\r\n    overflow-x: hidden;\r\n    background:\r\n        radial-gradient(circle at 18% 18%, rgba(102, 192, 244, .12), transparent 28%),\r\n        radial-gradient(circle at 82% 8%, rgba(155, 211, 61, .08), transparent 26%),\r\n        linear-gradient(180deg, #08121d 0%, #050b12 100%);\r\n    color: var(--text);\r\n    font-family: \"Segoe UI Variable Text\", \"Segoe UI\", Arial, sans-serif;\r\n}\r\nbutton { font: inherit; color: inherit; cursor: pointer; }\r\n.profile-shell { min-height: 100vh; padding-bottom: 30px; }\r\n.profile-chrome {\r\n    position: sticky;\r\n    top: 0;\r\n    z-index: 20;\r\n    min-height: 70px;\r\n    display: grid;\r\n    grid-template-columns: auto minmax(0, 1fr) auto auto;\r\n    align-items: center;\r\n    gap: 18px;\r\n    padding: 12px 22px;\r\n    border-bottom: 1px solid rgba(102, 192, 244, .16);\r\n    background: rgba(8, 16, 25, .94);\r\n    backdrop-filter: blur(16px);\r\n    box-shadow: 0 18px 36px rgba(0, 0, 0, .28);\r\n}\r\n.brand-cluster,\r\n.account-chip,\r\n.profile-nav { display: flex; align-items: center; }\r\n.brand-cluster { gap: 11px; }\r\n.brand-mark {\r\n    display: grid;\r\n    place-items: center;\r\n    border: 1px solid rgba(102, 192, 244, .48);\r\n    border-radius: 4px;\r\n    background: linear-gradient(180deg, rgba(28, 93, 132, .84), rgba(14, 47, 72, .92));\r\n    color: #bfeaff;\r\n    font-weight: 900;\r\n}\r\n.brand-mark { width: 36px; height: 36px; font-size: 18px; }\r\n.brand-copy { display: grid; gap: 1px; }\r\n.brand-copy strong { font-size: 16px; letter-spacing: .02em; }\r\n.brand-copy small { color: var(--muted); font-size: 11px; text-transform: uppercase; letter-spacing: .12em; }\r\n.profile-nav {\r\n    justify-self: center;\r\n    gap: 8px;\r\n    padding: 4px;\r\n    border: 1px solid rgba(102, 192, 244, .12);\r\n    border-radius: 999px;\r\n    background: rgba(8, 22, 34, .82);\r\n}\r\n.profile-nav button {\r\n    min-height: 34px;\r\n    padding: 0 15px;\r\n    border: 0;\r\n    border-radius: 999px;\r\n    background: transparent;\r\n    color: #b8d3e5;\r\n    font-size: 12px;\r\n    font-weight: 800;\r\n}\r\n.profile-nav button:hover,\r\n.profile-nav button:focus-visible {\r\n    outline: 0;\r\n    background: rgba(102, 192, 244, .13);\r\n    color: #f2fbff;\r\n}\r\n" + StoreAccountMenuHtml.Styles + "\r\n.window-controls { display: flex; gap: 4px; }\r\n.window-button {\r\n    width: 32px;\r\n    height: 28px;\r\n    border: 0;\r\n    border-radius: 4px;\r\n    background: transparent;\r\n    color: #cfd9e6;\r\n}\r\n.window-button:hover { background: rgba(255, 255, 255, .08); }\r\n.window-button.is-close:hover { background: #c83232; color: #fff; }\r\n.profile-content {\r\n    width: min(1420px, calc(100vw - 54px));\r\n    margin: 26px auto 0;\r\n}\r\n.hero-card {\r\n    display: grid;\r\n    grid-template-columns: minmax(0, 1fr) 360px;\r\n    gap: 20px;\r\n    min-height: 260px;\r\n    padding: 26px;\r\n    border: 1px solid rgba(102, 192, 244, .22);\r\n    border-radius: 8px;\r\n    background:\r\n        radial-gradient(circle at 20% 30%, rgba(102, 192, 244, .15), transparent 34%),\r\n        linear-gradient(135deg, rgba(20, 51, 78, .98), rgba(8, 20, 31, .96));\r\n    box-shadow: 0 22px 58px rgba(0, 0, 0, .32);\r\n}\r\n.hero-identity { display: flex; align-items: center; gap: 26px; min-width: 0; }\r\n.hero-identity > div:last-child { min-width: 0; }\r\n.avatar-ring {\r\n    width: 144px;\r\n    height: 144px;\r\n    display: grid;\r\n    place-items: center;\r\n    border: 1px solid rgba(102, 192, 244, .54);\r\n    border-radius: 12px;\r\n    background:\r\n        linear-gradient(180deg, rgba(26, 88, 125, .92), rgba(10, 36, 56, .95));\r\n    box-shadow:\r\n        inset 0 1px 0 rgba(255, 255, 255, .08),\r\n        0 16px 36px rgba(0, 0, 0, .28);\r\n}\r\n.avatar-ring span {\r\n    color: #d8f5ff;\r\n    font-size: 58px;\r\n    font-weight: 950;\r\n}\r\n.eyebrow {\r\n    color: var(--cyan);\r\n    font-size: 11px;\r\n    font-weight: 950;\r\n    letter-spacing: .16em;\r\n}\r\n.hero-identity h1 {\r\n    max-width: 620px;\r\n    margin: 8px 0 3px;\r\n    color: #f4fbff;\r\n    font-size: clamp(28px, 4vw, 52px);\r\n    line-height: 1.04;\r\n    letter-spacing: -.045em;\r\n    overflow-wrap: anywhere;\r\n    word-break: break-word;\r\n}\r\n.hero-identity p {\r\n    margin: 0;\r\n    overflow-wrap: anywhere;\r\n    color: #9fb7cc;\r\n    font-size: 15px;\r\n}\r\n.status-row {\r\n    display: flex;\r\n    flex-wrap: wrap;\r\n    gap: 9px;\r\n    margin-top: 20px;\r\n}\r\n.status-row span {\r\n    min-height: 30px;\r\n    display: inline-flex;\r\n    align-items: center;\r\n    padding: 0 11px;\r\n    border: 1px solid rgba(102, 192, 244, .16);\r\n    border-radius: 999px;\r\n    background: rgba(8, 21, 32, .68);\r\n    color: #bed7e8;\r\n    font-size: 12px;\r\n    font-weight: 750;\r\n}\r\n.status-row .status-badge--good {\r\n    border-color: rgba(155, 211, 61, .36);\r\n    color: #c9f48b;\r\n}\r\n.status-row .status-badge--warn {\r\n    border-color: rgba(255, 190, 90, .36);\r\n    color: #ffd089;\r\n}\r\n.wallet-card {\r\n    align-self: stretch;\r\n    display: flex;\r\n    flex-direction: column;\r\n    justify-content: center;\r\n    padding: 26px;\r\n    border: 1px solid rgba(155, 211, 61, .25);\r\n    border-radius: 8px;\r\n    background:\r\n        radial-gradient(circle at 78% 20%, rgba(155, 211, 61, .18), transparent 34%),\r\n        linear-gradient(180deg, rgba(13, 32, 28, .95), rgba(8, 21, 23, .98));\r\n}\r\n.wallet-card span {\r\n    color: #a2b9c9;\r\n    font-size: 12px;\r\n    font-weight: 850;\r\n    letter-spacing: .08em;\r\n    text-transform: uppercase;\r\n}\r\n.wallet-card strong {\r\n    margin-top: 8px;\r\n    color: #dfffb5;\r\n    font-size: 38px;\r\n    line-height: 1;\r\n}\r\n.wallet-card small {\r\n    margin-top: 10px;\r\n    color: #8fa99d;\r\n    font-size: 12px;\r\n}\r\n.wallet-card button {\r\n    width: fit-content;\r\n    margin-top: 24px;\r\n    min-height: 40px;\r\n    padding: 0 16px;\r\n    border: 1px solid rgba(155, 211, 61, .28);\r\n    border-radius: 4px;\r\n    background: linear-gradient(180deg, #9bd33d, #75a920);\r\n    color: #fff;\r\n    font-size: 12px;\r\n    font-weight: 900;\r\n}\r\n.metric-grid {\r\n    display: grid;\r\n    grid-template-columns: repeat(4, minmax(0, 1fr));\r\n    gap: 16px;\r\n    margin-top: 18px;\r\n}\r\n.metric-card,\r\n.panel {\r\n    border: 1px solid rgba(102, 192, 244, .14);\r\n    border-radius: 7px;\r\n    background: linear-gradient(180deg, rgba(15, 35, 52, .95), rgba(7, 18, 28, .98));\r\n    box-shadow: 0 16px 34px rgba(0, 0, 0, .22);\r\n}\r\n.metric-card {\r\n    min-height: 128px;\r\n    padding: 20px;\r\n}\r\n.metric-card span,\r\n.panel-heading span {\r\n    color: #92aabe;\r\n    font-size: 12px;\r\n    font-weight: 900;\r\n    letter-spacing: .08em;\r\n    text-transform: uppercase;\r\n}\r\n.metric-card strong {\r\n    display: block;\r\n    margin-top: 12px;\r\n    color: #eef9ff;\r\n    font-size: 34px;\r\n    line-height: 1;\r\n}\r\n.metric-card small {\r\n    display: block;\r\n    margin-top: 10px;\r\n    color: #7f95a8;\r\n    font-size: 12px;\r\n}\r\n.profile-layout {\r\n    display: grid;\r\n    grid-template-columns: minmax(0, .9fr) minmax(0, 1.1fr);\r\n    gap: 18px;\r\n    margin-top: 18px;\r\n}\r\n.profile-layout--bottom { grid-template-columns: minmax(0, 1.05fr) minmax(0, .95fr); }\r\n.panel { overflow: hidden; }\r\n.panel-heading {\r\n    min-height: 58px;\r\n    display: flex;\r\n    align-items: center;\r\n    justify-content: space-between;\r\n    gap: 12px;\r\n    padding: 0 18px;\r\n    border-bottom: 1px solid rgba(102, 192, 244, .12);\r\n}\r\n.panel-heading button {\r\n    min-height: 32px;\r\n    padding: 0 12px;\r\n    border: 1px solid rgba(102, 192, 244, .2);\r\n    border-radius: 4px;\r\n    background: rgba(102, 192, 244, .08);\r\n    color: #a8e2ff;\r\n    font-size: 12px;\r\n    font-weight: 850;\r\n}\r\n.panel-heading small { color: #778da1; font-size: 12px; }\r\n.info-list { padding: 8px 18px 18px; }\r\n.info-item {\r\n    display: grid;\r\n    grid-template-columns: 150px minmax(0, 1fr);\r\n    gap: 18px;\r\n    padding: 16px 0;\r\n    border-bottom: 1px solid rgba(102, 192, 244, .08);\r\n}\r\n.info-item:last-child { border-bottom: 0; }\r\n.info-item span {\r\n    color: #859bad;\r\n    font-size: 12px;\r\n    font-weight: 800;\r\n}\r\n.info-item strong {\r\n    min-width: 0;\r\n    overflow-wrap: anywhere;\r\n    color: #dcecf8;\r\n    font-size: 13px;\r\n}\r\n.activity-list,\r\n.transaction-list { padding: 12px; }\r\n.activity-card,\r\n.transaction-card {\r\n    display: grid;\r\n    grid-template-columns: minmax(0, 1fr) auto;\r\n    gap: 18px;\r\n    align-items: center;\r\n    padding: 14px;\r\n    border: 1px solid rgba(102, 192, 244, .09);\r\n    border-radius: 5px;\r\n    background: rgba(5, 16, 25, .58);\r\n}\r\n.activity-card + .activity-card,\r\n.transaction-card + .transaction-card { margin-top: 10px; }\r\n.activity-card strong,\r\n.transaction-card strong {\r\n    display: block;\r\n    color: #ecf7ff;\r\n    font-size: 13px;\r\n}\r\n.activity-card span,\r\n.transaction-card span {\r\n    display: block;\r\n    margin-top: 4px;\r\n    color: #8fa6b8;\r\n    font-size: 12px;\r\n}\r\n.activity-card small,\r\n.transaction-card small {\r\n    display: block;\r\n    margin-top: 5px;\r\n    color: #758da0;\r\n    font-size: 11px;\r\n}\r\n.activity-card > div:last-child,\r\n.transaction-card > div:last-child { text-align: right; }\r\n.transaction-card.is-positive > div:last-child strong { color: #bdf18a; }\r\n.transaction-card.is-negative > div:last-child strong { color: #ffabab; }\r\n.library-strip {\r\n    display: grid;\r\n    grid-template-columns: repeat(3, minmax(0, 1fr));\r\n    gap: 12px;\r\n    padding: 14px;\r\n}\r\n.library-card {\r\n    min-width: 0;\r\n    overflow: hidden;\r\n    border: 1px solid rgba(102, 192, 244, .1);\r\n    border-radius: 6px;\r\n    background: rgba(5, 16, 25, .58);\r\n}\r\n.library-card img,\r\n.image-fallback {\r\n    width: 100%;\r\n    aspect-ratio: 16 / 9;\r\n    display: grid;\r\n    place-items: center;\r\n    object-fit: cover;\r\n    background: linear-gradient(135deg, #12344f, #081622);\r\n    color: #b7e8ff;\r\n    font-size: 28px;\r\n    font-weight: 950;\r\n}\r\n.library-card div:last-child { padding: 12px; }\r\n.library-card strong {\r\n    display: block;\r\n    overflow: hidden;\r\n    color: #ecf7ff;\r\n    font-size: 13px;\r\n    text-overflow: ellipsis;\r\n    white-space: nowrap;\r\n}\r\n.library-card span,\r\n.library-card small {\r\n    display: block;\r\n    margin-top: 4px;\r\n    overflow: hidden;\r\n    color: #8fa6b8;\r\n    font-size: 12px;\r\n    text-overflow: ellipsis;\r\n    white-space: nowrap;\r\n}\r\n.library-card small { color: #758da0; font-size: 11px; }\r\n.empty-state {\r\n    padding: 18px;\r\n    color: #7f96aa;\r\n    font-size: 13px;\r\n}\r\n.toast-host {\r\n    position: fixed;\r\n    right: 22px;\r\n    bottom: 22px;\r\n    z-index: 50;\r\n    display: grid;\r\n    gap: 10px;\r\n}\r\n.toast {\r\n    max-width: 360px;\r\n    padding: 13px 15px;\r\n    border: 1px solid rgba(102, 192, 244, .25);\r\n    border-radius: 5px;\r\n    background: rgba(9, 23, 34, .96);\r\n    color: #dff2ff;\r\n    box-shadow: 0 18px 36px rgba(0, 0, 0, .32);\r\n    font-size: 13px;\r\n}\r\n.app-loading-overlay {\r\n    position: fixed;\r\n    inset: 0;\r\n    z-index: 70;\r\n    display: grid;\r\n    place-items: center;\r\n    background: rgba(3, 8, 13, .62);\r\n    backdrop-filter: blur(8px);\r\n}\r\n.app-loading-dialog {\r\n    width: min(420px, calc(100vw - 48px));\r\n    padding: 24px;\r\n    border: 1px solid rgba(102, 192, 244, .24);\r\n    border-radius: 7px;\r\n    background: linear-gradient(180deg, rgba(16, 35, 52, .98), rgba(7, 17, 26, .98));\r\n    box-shadow: 0 28px 70px rgba(0, 0, 0, .38);\r\n}\r\n.app-loading-kicker {\r\n    color: var(--cyan);\r\n    font-size: 11px;\r\n    font-weight: 950;\r\n    letter-spacing: .16em;\r\n}\r\n.app-loading-dialog h2 { margin: 8px 0 8px; }\r\n.app-loading-dialog p { margin: 0; color: var(--muted); line-height: 1.5; }\r\n@media (max-width: 1180px) {\r\n    .profile-chrome { grid-template-columns: auto minmax(0, 1fr) auto; }\r\n    .profile-nav { justify-self: start; }\r\n    .account-menu-shell { display: none; }\r\n    .hero-card,\r\n    .profile-layout,\r\n    .profile-layout--bottom { grid-template-columns: 1fr; }\r\n    .metric-grid { grid-template-columns: repeat(2, minmax(0, 1fr)); }\r\n}";
	}

	private static string BuildLoadingOverlay()
	{
		return "<div class=\"app-loading-overlay\" data-app-loading hidden>\r\n    <div class=\"app-loading-dialog\" tabindex=\"-1\">\r\n        <span class=\"app-loading-kicker\">NESTG STORE</span>\r\n        <h2 data-app-loading-title>Đang xử lý</h2>\r\n        <p data-app-loading-message>Vui lòng chờ trong giây lát.</p>\r\n    </div>\r\n</div>";
	}

	private static string BuildScript()
	{
		return "(() => {\r\n    const post = message => window.chrome?.webview?.postMessage(message);\r\n" + StoreAccountMenuHtml.Script + "\r\n    const toastHost = document.querySelector('[data-toast-host]');\r\n    const appLoadingOverlay = document.querySelector('[data-app-loading]');\r\n    const appLoadingTitle = appLoadingOverlay?.querySelector('[data-app-loading-title]');\r\n    const appLoadingMessage = appLoadingOverlay?.querySelector('[data-app-loading-message]');\r\n\r\n    function showToast(message) {\r\n        if (!toastHost || !message) return;\r\n        const toast = document.createElement('div');\r\n        toast.className = 'toast';\r\n        toast.textContent = message;\r\n        toastHost.append(toast);\r\n        window.setTimeout(() => toast.remove(), 3400);\r\n    }\r\n\r\n    window.__nestgHome = {\r\n        setCartCount() {},\r\n        toast: showToast\r\n    };\r\n\r\n    window.__nestgLoading = {\r\n        show(title, message) {\r\n            document.body.classList.add('app-loading-active');\r\n            if (appLoadingTitle && title) appLoadingTitle.textContent = title;\r\n            if (appLoadingMessage && message) appLoadingMessage.textContent = message;\r\n            if (appLoadingOverlay) appLoadingOverlay.hidden = false;\r\n        },\r\n        hide() {\r\n            document.body.classList.remove('app-loading-active');\r\n            if (appLoadingOverlay) appLoadingOverlay.hidden = true;\r\n        }\r\n    };\r\n\r\n    function isInteractive(target) {\r\n        return Boolean(target.closest('button, input, textarea, select, a, [data-host-action]'));\r\n    }\r\n\r\n    document.addEventListener('pointerdown', event => {\r\n        const target = event.target instanceof Element ? event.target : null;\r\n        if (!target || isInteractive(target)) return;\r\n        if (target.closest('[data-drag-region]')) {\r\n            post({ type: 'dragWindow' });\r\n        }\r\n    });\r\n\r\n    document.addEventListener('click', event => {\r\n        const rawTarget = event.target instanceof Element ? event.target : null;\r\n        if (!rawTarget) return;\r\n        if (rawTarget.closest('[data-account-toggle]')) {\r\n            setAccountMenuOpen(Boolean(accountMenu?.hidden));\r\n            return;\r\n        }\r\n        if (!rawTarget.closest('[data-account-menu]')) {\r\n            setAccountMenuOpen(false);\r\n        }\r\n\r\n        const target = rawTarget.closest('[data-host-action]');\r\n        if (!target) return;\r\n        event.preventDefault();\r\n        event.stopPropagation();\r\n        post({ type: target.getAttribute('data-host-action') });\r\n    });\r\n\r\n    document.addEventListener('error', event => {\r\n        const image = event.target instanceof HTMLImageElement ? event.target : null;\r\n        if (!image) return;\r\n        const fallback = document.createElement('div');\r\n        fallback.className = 'image-fallback';\r\n        fallback.textContent = (image.alt || 'N').trim().charAt(0).toUpperCase();\r\n        image.replaceWith(fallback);\r\n    }, true);\r\n})();";
	}

	private static string Initial(string? value)
	{
		return Html((string.IsNullOrWhiteSpace(value) ? "N" : value.Trim()).Substring(0, 1).ToUpperInvariant());
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
