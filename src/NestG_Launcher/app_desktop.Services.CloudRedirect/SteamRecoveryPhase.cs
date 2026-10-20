namespace app_desktop.Services.CloudRedirect;

internal enum SteamRecoveryPhase
{
	Installing,
	VerifyActivation,
	Rollback,
	VerifyRollback,
	DetachLoaders,
	VerifyDetached,
	Complete,
	DisableCloud,
	VerifyBetter,
	FilesCommitted
}
