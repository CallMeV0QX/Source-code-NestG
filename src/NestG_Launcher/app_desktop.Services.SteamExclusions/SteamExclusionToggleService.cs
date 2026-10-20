using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace app_desktop.Services.SteamExclusions;

internal sealed class SteamExclusionToggleService(ISteamGameFolderDiscovery discovery, IManagedDefenderExclusionClient defender, ISteamExclusionOwnership ownership)
{
	private int running;

	internal Task<SteamExclusionToggleResult> RunAsync(bool enabled, Func<IReadOnlyList<string>, bool, bool> confirm, CancellationToken cancellationToken)
	{
		return RunAsync(enabled, (IReadOnlyList<string> paths, bool enable, CancellationToken _) => Task.FromResult(confirm(paths, enable)), cancellationToken);
	}

	internal async Task<SteamExclusionToggleResult> RunAsync(bool enabled, Func<IReadOnlyList<string>, bool, CancellationToken, Task<bool>> confirm, CancellationToken cancellationToken)
	{
		if (Interlocked.CompareExchange(ref running, 1, 0) != 0)
		{
			return new SteamExclusionToggleResult(SteamExclusionToggleOutcome.Busy);
		}
		try
		{
			IReadOnlyList<string> paths;
			if (enabled)
			{
				paths = await discovery.FindAsync(cancellationToken);
			}
			else
			{
				try
				{
					paths = ownership.ReadPaths();
				}
				catch
				{
					return new SteamExclusionToggleResult(SteamExclusionToggleOutcome.OwnershipFailed);
				}
			}
			paths = paths.Distinct<string>(StringComparer.OrdinalIgnoreCase).ToArray();
			cancellationToken.ThrowIfCancellationRequested();
			if (paths.Count == 0)
			{
				return new SteamExclusionToggleResult(enabled ? SteamExclusionToggleOutcome.NoSteam : SteamExclusionToggleOutcome.NoManagedPaths);
			}
			if (!(await confirm(paths, enabled, cancellationToken)))
			{
				return new SteamExclusionToggleResult(SteamExclusionToggleOutcome.Declined);
			}
			cancellationToken.ThrowIfCancellationRequested();
			return await defender.SetEnabledAsync(enabled, paths, cancellationToken);
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			throw;
		}
		catch (TimeoutException)
		{
			return new SteamExclusionToggleResult(SteamExclusionToggleOutcome.TimedOut);
		}
		catch
		{
			return new SteamExclusionToggleResult(SteamExclusionToggleOutcome.Failed);
		}
		finally
		{
			Volatile.Write(ref running, 0);
		}
	}

	internal static string BuildConsentMessage(IReadOnlyList<string> paths, bool enable)
	{
		return (enable ? "NestG sẽ thêm các thư mục game Steam sau vào ngoại lệ Microsoft Defender:\n\n" : "NestG sẽ gỡ các ngoại lệ sau nếu lịch sử xác nhận chúng do NestG thêm:\n\n") + string.Join("\n", paths) + (enable ? "\n\nThao tác này làm giảm bảo vệ: Defender bỏ qua quét nội dung trong các thư mục trên. Chỉ đồng ý khi bạn tin cậy các file game." : "\n\nCác ngoại lệ có sẵn và mục chỉ được ghi trong file đồng ý cũ sẽ được giữ nguyên.") + "\n\nThay đổi áp dụng cho máy, bao gồm các tài khoản Windows khác. Windows sẽ hỏi quyền quản trị cho lượt thao tác này. NestG chỉ báo hoàn tất sau khi kiểm tra kết quả. Không tự chạy lại khi mở launcher.\n\nBạn có đồng ý không?";
	}
}
