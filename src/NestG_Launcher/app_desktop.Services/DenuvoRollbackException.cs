using System;
using System.IO;

namespace app_desktop.Services;

internal sealed class DenuvoRollbackException : IOException
{
	internal string BackupDirectory { get; }

	public DenuvoRollbackException(string backupDirectory, Exception installationError, Exception rollbackError)
		: base("Chưa khôi phục xong cài đặt Denuvo. Giữ nguyên backup để kiểm tra: " + backupDirectory, new AggregateException(installationError, rollbackError))
	{
		BackupDirectory = backupDirectory;
	}
}
