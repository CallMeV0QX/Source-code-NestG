namespace app_desktop.Services.CloudSave;

internal enum CloudSaveProgressStage
{
	Connecting,
	Inspecting,
	Snapshotting,
	Uploading,
	Verifying,
	Completed,
	ClosingGame,
	WaitingForSync,
	ClosingSteam,
	Downloading,
	Restoring,
	Listing
}
