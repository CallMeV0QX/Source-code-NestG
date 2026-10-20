namespace UI_Desktop.Store;

internal enum CloudRedirectActivationUiState
{
	Success,
	RollbackFailed,
	RolledBack,
	HealthFailed,
	NeedsCloudCheck,
	NeedsSteamRestart,
	SteamMissing,
	AttemptFailed,
	VerificationPending
}
