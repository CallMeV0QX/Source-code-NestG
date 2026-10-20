using System.Runtime.CompilerServices;
using System.Text;

namespace app_desktop.Services.WindowsUpdate;

internal sealed record WindowsUpdateBlockStatus(WindowsUpdateBlockKind Kind, bool? IsBlocked = null, WubCommandOutcome? LastOutcome = null, bool? UnconfirmedRequest = null, WindowsUpdateBlockKind? MachineKind = null)
{
	internal bool CanToggle
	{
		get
		{
			WindowsUpdateBlockKind kind = Kind;
			if ((uint)(kind - 1) <= 1u)
			{
				return true;
			}
			return false;
		}
	}

	internal bool CanStartToggle
	{
		get
		{
			bool flag = CanToggle;
			bool flag2;
			bool flag3;
			if (!flag)
			{
				flag2 = Kind == WindowsUpdateBlockKind.MissingWub;
				if (flag2)
				{
					WindowsUpdateBlockKind? machineKind = MachineKind;
					if (machineKind.HasValue)
					{
						WindowsUpdateBlockKind valueOrDefault = machineKind.GetValueOrDefault();
						if ((uint)(valueOrDefault - 1) <= 1u)
						{
							flag3 = true;
							goto IL_003d;
						}
					}
					flag3 = false;
					goto IL_003d;
				}
				goto IL_0040;
			}
			goto IL_0042;
			IL_003d:
			flag2 = flag3;
			goto IL_0040;
			IL_0042:
			return flag;
			IL_0040:
			flag = flag2;
			goto IL_0042;
		}
	}

	internal string Label
	{
		get
		{
			switch (Kind)
			{
			case WindowsUpdateBlockKind.MissingWub:
				if (MachineKind == WindowsUpdateBlockKind.Blocked)
				{
					return "Đã chặn · thiếu WUB";
				}
				return "Chưa có WUB";
			case WindowsUpdateBlockKind.Enabled:
				if (UnconfirmedRequest == true)
				{
					return "Chưa chặn được";
				}
				if (LastOutcome == WubCommandOutcome.Cancelled)
				{
					return "Đã hủy";
				}
				return "Cập nhật bật";
			case WindowsUpdateBlockKind.Blocked:
				if (UnconfirmedRequest == false)
				{
					return "Chưa bật được";
				}
				if (LastOutcome == WubCommandOutcome.Cancelled)
				{
					return "Đã hủy";
				}
				return "Đã chặn";
			case WindowsUpdateBlockKind.Partial:
				return "Chặn một phần";
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
			case WindowsUpdateBlockKind.MissingWub:
				if (CanStartToggle)
				{
					return "Gạt công tắc để NestG tự tải WUB v1.8 rồi đổi trạng thái Windows Update. Windows có thể yêu cầu quyền quản trị.";
				}
				return "Không đọc rõ trạng thái Windows Update. NestG sẽ không tải và chạy WUB cho tới khi trạng thái máy được kiểm tra.";
			case WindowsUpdateBlockKind.Enabled:
				if (UnconfirmedRequest == true)
				{
					return "WUB đã chạy nhưng các dịch vụ cập nhật vẫn hoạt động. Kiểm tra Windows Update trước khi thử lại.";
				}
				return "Cập nhật tự động của Windows hiện được cho phép.";
			case WindowsUpdateBlockKind.Blocked:
				if (UnconfirmedRequest == false)
				{
					return "WUB đã chạy nhưng Windows Update vẫn bị chặn. Có thể cần khởi động lại máy.";
				}
				return "Các dịch vụ cập nhật chính và chính sách tự động đã bị WUB chặn trên máy này.";
			case WindowsUpdateBlockKind.Partial:
				return "Chỉ một phần trạng thái Windows Update khớp WUB. Mở WUB để kiểm tra hoặc sửa trước khi dùng công tắc.";
			default:
				return "Không đọc đủ trạng thái Windows Update. Công tắc tạm thời không thể đổi trạng thái.";
			}
		}
	}

	[CompilerGenerated]
	private bool PrintMembers(StringBuilder builder)
	{
		RuntimeHelpers.EnsureSufficientExecutionStack();
		builder.Append("Kind = ");
		builder.Append(Kind.ToString());
		builder.Append(", IsBlocked = ");
		builder.Append(IsBlocked.ToString());
		builder.Append(", LastOutcome = ");
		builder.Append(LastOutcome.ToString());
		builder.Append(", UnconfirmedRequest = ");
		builder.Append(UnconfirmedRequest.ToString());
		builder.Append(", MachineKind = ");
		builder.Append(MachineKind.ToString());
		return true;
	}
}
