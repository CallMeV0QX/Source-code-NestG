namespace app_desktop.Services.CloudSave;

internal enum CloudSaveOperationStage
{
	SnapshotReady,
	PreparingDrive,
	Uploading,
	Verifying,
	Completed
}
