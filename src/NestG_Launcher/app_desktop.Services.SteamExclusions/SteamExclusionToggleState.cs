namespace app_desktop.Services.SteamExclusions;

internal sealed record SteamExclusionToggleState(bool Enabled = false, bool Busy = false, bool CanRemove = false, string Message = "Bật để thêm thư mục game Steam vào ngoại lệ Defender.", string Status = "Chưa kiểm tra")
{
	internal SteamExclusionToggleState Complete(SteamExclusionToggleResult result, bool canRemove)
	{
		return this with
		{
			Busy = false,
			Enabled = (result.Success ? (result.Outcome == SteamExclusionToggleOutcome.Enabled) : Enabled),
			CanRemove = canRemove,
			Message = result.Message,
			Status = result.CompactMessage
		};
	}
}
