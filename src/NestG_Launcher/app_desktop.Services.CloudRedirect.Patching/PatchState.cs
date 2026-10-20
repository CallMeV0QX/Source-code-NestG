namespace app_desktop.Services.CloudRedirect.Patching;

internal enum PatchState
{
	NotInstalled,
	Unpatched,
	Patched,
	PartiallyPatched,
	UnknownVersion,
	OutOfDate
}
