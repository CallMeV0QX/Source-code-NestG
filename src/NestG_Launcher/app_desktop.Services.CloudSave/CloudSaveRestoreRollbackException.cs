using System;

namespace app_desktop.Services.CloudSave;

internal sealed class CloudSaveRestoreRollbackException : InvalidOperationException
{
	internal string SafetyBackupPath { get; }

	public CloudSaveRestoreRollbackException(string safetyBackupPath, Exception cause)
		: base("Restore bị gián đoạn và rollback tự động thất bại.", cause)
	{
		SafetyBackupPath = safetyBackupPath;
	}
}
