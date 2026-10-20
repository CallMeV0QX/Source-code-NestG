namespace app_desktop.Services.WindowsUpdate;

internal enum WindowsUpdateBlockKind
{
	MissingWub,
	Enabled,
	Blocked,
	Partial,
	Unavailable
}
