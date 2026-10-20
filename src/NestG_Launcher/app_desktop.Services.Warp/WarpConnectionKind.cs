namespace app_desktop.Services.Warp;

internal enum WarpConnectionKind
{
	NotInstalled,
	Disconnected,
	Connecting,
	Connected,
	DnsOnly,
	Restricted,
	Unavailable,
	Unknown
}
