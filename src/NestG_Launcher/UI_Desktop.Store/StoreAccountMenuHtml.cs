using System;
using System.Net;

namespace UI_Desktop.Store;

internal static class StoreAccountMenuHtml
{
	public static string Styles => ".account-menu-shell { position:relative; min-width:0; }\r\n.account-chip { gap:10px; min-width:0; padding:6px 11px 6px 6px; border:1px solid rgba(102,192,244,.28); border-radius:5px; background:rgba(11,30,45,.9); color:inherit; }\r\n.account-chip:hover,.account-chip:focus-visible,.account-chip.is-open { border-color:rgba(102,192,244,.55); background:rgba(13,39,59,.96); }\r\n.account-avatar { width:32px; height:32px; display:grid; place-items:center; border:1px solid rgba(102,192,244,.48); border-radius:4px; background:linear-gradient(180deg,rgba(28,93,132,.84),rgba(14,47,72,.92)); color:#bfeaff; font-weight:900; }\r\n.account-copy { min-width:0; display:grid; gap:1px; text-align:left; }\r\n.account-copy strong { max-width:160px; overflow:hidden; color:#e8f6ff; font-size:13px; text-overflow:ellipsis; white-space:nowrap; }\r\n.account-copy small { color:#9edfff; font-size:12px; font-weight:800; }\r\n.account-menu { position:absolute; top:calc(100% + 8px); right:0; z-index:65; width:260px; padding:8px; border:1px solid rgba(102,192,244,.22); border-radius:7px; background:linear-gradient(180deg,rgba(13,30,45,.98),rgba(6,15,24,.98)); box-shadow:0 24px 52px rgba(0,0,0,.38); }\r\n.account-menu-item,.account-menu-signout { width:100%; min-height:48px; display:grid; gap:2px; padding:8px 10px; border:0; border-radius:5px; background:transparent; color:#b8d3e5; text-align:left; }\r\n.account-menu-item:hover,.account-menu-item:focus-visible,.account-menu-signout:hover,.account-menu-signout:focus-visible { background:rgba(102,192,244,.11); color:#fff; }\r\n.account-menu-item strong { font-size:13px; font-weight:850; }\r\n.account-menu-item small { color:#82a0b4; font-size:11px; }\r\n.account-menu-item.is-active { background:rgba(102,192,244,.15); color:#fff; }\r\n.account-menu-item.is-active strong { font-weight:950; }\r\n.account-menu-item.is-active small { color:#bfeaff; font-weight:800; }\r\n.account-notification-count { display:inline-grid; min-width:18px; height:18px; margin-left:5px; place-items:center; border-radius:9px; background:#1d9be8; color:#fff; font-size:10px; }\r\n.account-notification-count[hidden] { display:none; }\r\n.account-menu-signout { margin-top:5px; border-top:1px solid rgba(102,192,244,.12); color:#ffb6b6; font-size:12px; font-weight:900; }";

	public static string Script => "    const accountToggle = document.querySelector('[data-account-toggle]');\r\n    const accountMenu = document.querySelector('[data-account-menu]');\r\n\r\n    function setAccountMenuOpen(isOpen) {\r\n        if (!accountToggle || !accountMenu) return;\r\n        accountMenu.hidden = !isOpen;\r\n        accountToggle.classList.toggle('is-open', isOpen);\r\n        accountToggle.setAttribute('aria-expanded', String(isOpen));\r\n    }\r\n\r\n    document.addEventListener('keydown', event => {\r\n        if (event.key === 'Escape') setAccountMenuOpen(false);\r\n    });";

	public static string Build(DesktopAccountResponse account, string activeItem, int notificationUnreadCount = 0)
	{
		return $"<div class=\"account-menu-shell\">\r\n    <button type=\"button\" class=\"account-chip\" data-account-toggle aria-haspopup=\"menu\" aria-expanded=\"false\">\r\n        <span class=\"account-avatar\">{Initial(account.DisplayName)}</span>\r\n        <span class=\"account-copy\">\r\n            <strong>{Html(account.DisplayName)}</strong>\r\n            <small>{Html(account.WalletBalanceText)}</small>\r\n        </span>\r\n    </button>\r\n    <div class=\"account-menu\" data-account-menu role=\"menu\" hidden>\r\n        <button type=\"button\" role=\"menuitem\" class=\"account-menu-item\" data-host-action=\"openNotifications\">\r\n            <strong>Th&#244;ng b&#225;o <span class=\"account-notification-count\" data-notification-count{((notificationUnreadCount <= 0) ? " hidden" : string.Empty)}>{notificationUnreadCount}</span></strong>\r\n            <small>C&#7853;p nh&#7853;t t&#7915; NestG Launcher</small>\r\n        </button>\r\n        <button type=\"button\" role=\"menuitem\" class=\"account-menu-item {Active(activeItem, "profile")}\" data-host-action=\"openProfile\">\r\n            <strong>Cá nhân</strong>\r\n            <small>Thông tin tài khoản</small>\r\n        </button>\r\n        <button type=\"button\" role=\"menuitem\" class=\"account-menu-item {Active(activeItem, "orders")}\" data-host-action=\"openOrderHistory\">\r\n            <strong>Đơn hàng</strong>\r\n            <small>Lịch sử mua hàng</small>\r\n        </button>\r\n        <button type=\"button\" role=\"menuitem\" class=\"account-menu-item {Active(activeItem, "wallet")}\" data-host-action=\"openWalletTopup\">\r\n            <strong>Nạp ví</strong>\r\n            <small>Số dư {Html(account.WalletBalanceText)}</small>\r\n        </button>\r\n        <button type=\"button\" role=\"menuitem\" class=\"account-menu-signout\" data-host-action=\"signOut\">Đăng xuất</button>\r\n    </div>\r\n</div>";
	}

	private static string Active(string activeItem, string item)
	{
		if (!string.Equals(activeItem, item, StringComparison.OrdinalIgnoreCase))
		{
			return string.Empty;
		}
		return "is-active";
	}

	private static string Initial(string? value)
	{
		return Html((string.IsNullOrWhiteSpace(value) ? "N" : value.Trim()).Substring(0, 1).ToUpperInvariant());
	}

	private static string Html(string? value)
	{
		return WebUtility.HtmlEncode(value ?? string.Empty);
	}
}
