namespace UI_Desktop.Store;

internal sealed record StoreWebHomeContext(string UserName, string WalletBalanceText, int CartItemCount, bool IsAuthenticated, string VersionText, bool IsWalletBalanceAvailable = true, bool IsCartCountAvailable = true, int NotificationUnreadCount = 0);
