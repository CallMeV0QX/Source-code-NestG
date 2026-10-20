namespace app_desktop.Services.Fix54;

internal sealed class Fix54ProcessStillRunningException : Fix54Exception
{
	public Fix54ProcessStillRunningException()
		: base("Chưa xác minh được tiến trình FIX54 đã dừng. EXE gốc chưa được cập nhật; vùng staging được giữ để kiểm tra.")
	{
	}
}
