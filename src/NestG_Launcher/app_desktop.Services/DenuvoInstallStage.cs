namespace app_desktop.Services;

internal enum DenuvoInstallStage
{
	Downloading,
	Validating,
	BackingUp,
	Installing,
	RollingBack,
	RequestingToken
}
