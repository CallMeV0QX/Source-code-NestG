using System.Runtime.CompilerServices;
using System.Text;

namespace app_desktop.Services.Warp;

internal sealed record WarpConnectionStatus(WarpConnectionKind Kind, bool IsConnected = false, bool SwitchLocked = false, bool? UnconfirmedRequest = null)
{
	internal bool CanToggle
	{
		get
		{
			WarpConnectionKind kind = Kind;
			if ((kind == WarpConnectionKind.Disconnected || kind == WarpConnectionKind.Connected) ? true : false)
			{
				return !SwitchLocked;
			}
			return false;
		}
	}

	internal string Label
	{
		get
		{
			switch (Kind)
			{
			case WarpConnectionKind.NotInstalled:
				return "Chưa cài";
			case WarpConnectionKind.Disconnected:
				if (UnconfirmedRequest == true)
				{
					return "Chưa bật được";
				}
				return "Đã tắt";
			case WarpConnectionKind.Connecting:
				return "Đang kết nối";
			case WarpConnectionKind.Connected:
				if (UnconfirmedRequest == false)
				{
					return "Chưa tắt được";
				}
				if (SwitchLocked)
				{
					return "Bị quản lý";
				}
				return "Đã bật";
			case WarpConnectionKind.DnsOnly:
				return "Chỉ DNS";
			case WarpConnectionKind.Restricted:
				return "Bị quản lý";
			case WarpConnectionKind.Unavailable:
				return "Không sẵn sàng";
			default:
				return "Chưa rõ";
			}
		}
	}

	internal string Detail
	{
		get
		{
			switch (Kind)
			{
			case WarpConnectionKind.NotInstalled:
				return "Cài Cloudflare WARP để bật kết nối từ launcher.";
			case WarpConnectionKind.Disconnected:
				if (UnconfirmedRequest == true)
				{
					return "Đã gửi lệnh bật nhưng WARP vẫn ngắt. Hãy kiểm tra ứng dụng Cloudflare rồi thử lại.";
				}
				return "WARP đang ngắt trên máy này.";
			case WarpConnectionKind.Connecting:
				return "Cloudflare đang thiết lập kết nối WARP.";
			case WarpConnectionKind.Connected:
				if (UnconfirmedRequest == false)
				{
					return "Đã gửi lệnh tắt nhưng WARP vẫn kết nối. Hãy kiểm tra ứng dụng Cloudflare rồi thử lại.";
				}
				if (SwitchLocked)
				{
					return "WARP đang bật nhưng chính sách quản lý không cho ngắt.";
				}
				return "WARP đang kết nối cho cả máy, gồm Steam và NestG.";
			case WarpConnectionKind.DnsOnly:
				return "Cloudflare đang ở chế độ chỉ DNS. Chuyển sang WARP trong ứng dụng Cloudflare để dùng công tắc này.";
			case WarpConnectionKind.Restricted:
				return "Chính sách quản lý không cho đổi kết nối Cloudflare.";
			case WarpConnectionKind.Unavailable:
				return "Không đọc được Cloudflare WARP. Hãy kiểm tra ứng dụng và dịch vụ Cloudflare.";
			default:
				return "Không xác định được chế độ Cloudflare. Hãy kiểm tra trong ứng dụng Cloudflare.";
			}
		}
	}

	[CompilerGenerated]
	private bool PrintMembers(StringBuilder builder)
	{
		RuntimeHelpers.EnsureSufficientExecutionStack();
		builder.Append("Kind = ");
		builder.Append(Kind.ToString());
		builder.Append(", IsConnected = ");
		builder.Append(IsConnected.ToString());
		builder.Append(", SwitchLocked = ");
		builder.Append(SwitchLocked.ToString());
		builder.Append(", UnconfirmedRequest = ");
		builder.Append(UnconfirmedRequest.ToString());
		return true;
	}
}
