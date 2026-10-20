using System.Runtime.CompilerServices;
using System.Text;

namespace app_desktop.Services.SteamExclusions;

internal sealed record SteamExclusionToggleResult(SteamExclusionToggleOutcome Outcome)
{
	internal bool Success
	{
		get
		{
			SteamExclusionToggleOutcome outcome = Outcome;
			if ((uint)outcome <= 1u || outcome == SteamExclusionToggleOutcome.NoManagedPaths)
			{
				return true;
			}
			return false;
		}
	}

	internal string CompactMessage => Outcome switch
	{
		SteamExclusionToggleOutcome.Enabled => "Đã bật ngoại lệ game.", 
		SteamExclusionToggleOutcome.Disabled => "Đã gỡ ngoại lệ NestG.", 
		SteamExclusionToggleOutcome.NoManagedPaths => "Không có ngoại lệ NestG cần gỡ.", 
		SteamExclusionToggleOutcome.Declined => "Đã hủy thao tác.", 
		SteamExclusionToggleOutcome.NoSteam => "Không tìm thấy thư mục game Steam.", 
		SteamExclusionToggleOutcome.Busy => "Đang có thao tác khác. Thử lại sau.", 
		SteamExclusionToggleOutcome.NeedsElevation => "Cần quyền quản trị. Vui lòng thử lại.", 
		SteamExclusionToggleOutcome.Blocked => "Windows hạn chế thao tác. Chưa thể hoàn tất.", 
		_ => "Chưa hoàn tất. Một phần có thể đã thay đổi; vui lòng thử lại.", 
	};

	internal string Message => Outcome switch
	{
		SteamExclusionToggleOutcome.Enabled => "Đã xác minh các thư mục game Steam có trong ngoại lệ Defender.", 
		SteamExclusionToggleOutcome.Disabled => "Đã gỡ các mục có lịch sử do NestG thêm. Ngoại lệ có sẵn được giữ nguyên.", 
		SteamExclusionToggleOutcome.NoManagedPaths => "Không có mục ngoại lệ nào có lịch sử do NestG thêm để gỡ. Các ngoại lệ có sẵn được giữ nguyên.", 
		SteamExclusionToggleOutcome.NoSteam => "Không tìm thấy thư mục game Steam hợp lệ trên máy.", 
		SteamExclusionToggleOutcome.Declined => "Đã hủy yêu cầu. NestG chưa thay đổi ngoại lệ.", 
		SteamExclusionToggleOutcome.Busy => "Một thao tác ngoại lệ khác đang chạy. Vui lòng thử lại sau.", 
		SteamExclusionToggleOutcome.Blocked => "Windows đang ẩn hoặc chặn quản lý ngoại lệ với quyền hiện tại. Chưa thể hoàn tất; hãy kiểm tra Windows Security.", 
		SteamExclusionToggleOutcome.Unavailable => "Microsoft Defender chưa sẵn sàng. Chưa thể hoàn tất thao tác ngoại lệ.", 
		SteamExclusionToggleOutcome.NeedsElevation => "Chưa được cấp quyền quản trị. Hãy thử lại và chấp thuận yêu cầu của Windows.", 
		SteamExclusionToggleOutcome.OwnershipFailed => "Không đọc hoặc lưu được lịch sử ngoại lệ NestG. Chưa thể hoàn tất; một phần có thể đã được thêm.", 
		SteamExclusionToggleOutcome.Unknown => "Không xác định được danh sách ngoại lệ Defender. Chưa thể hoàn tất; hãy kiểm tra Windows Security.", 
		SteamExclusionToggleOutcome.InvalidPaths => "Thư mục đã thay đổi hoặc yêu cầu đã hết hạn. Chưa thể hoàn tất; hãy thử lại.", 
		SteamExclusionToggleOutcome.TimedOut => "Thao tác quá thời gian. Một phần có thể đã thay đổi; hãy kiểm tra lại.", 
		_ => "Chưa xác minh được kết quả. Một phần ngoại lệ có thể đã thay đổi; hãy thử lại hoặc kiểm tra Windows Security.", 
	};

	[CompilerGenerated]
	private bool PrintMembers(StringBuilder builder)
	{
		RuntimeHelpers.EnsureSufficientExecutionStack();
		builder.Append("Outcome = ");
		builder.Append(Outcome.ToString());
		return true;
	}
}
