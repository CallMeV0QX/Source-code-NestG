using System;

namespace app_desktop.Services.CloudRedirect;

internal sealed class UnsupportedSteamBuildException : InvalidOperationException
{
	public UnsupportedSteamBuildException()
		: base("Bản Steam này chưa có metadata đã kiểm chứng trong bundle BetterSteamTools. Cần cập nhật bundle NestG trước khi ACTIVE.")
	{
	}
}
