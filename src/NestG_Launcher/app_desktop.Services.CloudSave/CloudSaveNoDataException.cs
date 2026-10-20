using System;

namespace app_desktop.Services.CloudSave;

internal sealed class CloudSaveNoDataException : InvalidOperationException
{
	public CloudSaveNoDataException()
		: base("Game chưa có dữ liệu save để sao lưu.")
	{
	}
}
