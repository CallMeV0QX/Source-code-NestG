namespace app_desktop.Services.CloudSave;

internal sealed class GoogleDriveCredentialRevokedException : GoogleDriveOAuthException
{
	public GoogleDriveCredentialRevokedException(string stage, string error, string reason, int status)
		: base(stage, error, reason, status, "Quyền Google Drive đã hết hiệu lực. Hãy kết nối lại.")
	{
	}
}
