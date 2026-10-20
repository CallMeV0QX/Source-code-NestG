namespace app_desktop.Services.Warp;

internal interface IWarpPackageVerifier
{
	bool IsOfficialInstaller(string path);
}
