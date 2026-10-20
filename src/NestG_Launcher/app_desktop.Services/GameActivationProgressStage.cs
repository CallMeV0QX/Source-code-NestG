namespace app_desktop.Services;

internal enum GameActivationProgressStage
{
	Checking,
	PreparingActivation,
	PreparingCloudRedirect,
	RestartingSteam,
	Verifying,
	Retrying,
	Completed
}
