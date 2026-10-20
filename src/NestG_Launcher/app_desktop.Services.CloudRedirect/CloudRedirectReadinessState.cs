namespace app_desktop.Services.CloudRedirect;

internal enum CloudRedirectReadinessState
{
	Ready,
	SteamMissing,
	InvalidSteamAppId,
	NotInstalled,
	NeedsRepair,
	ConfigurationBlocked,
	Disabled,
	ProviderMissing,
	ProviderBlocked,
	PendingRecovery,
	UpgradeAvailable
}
