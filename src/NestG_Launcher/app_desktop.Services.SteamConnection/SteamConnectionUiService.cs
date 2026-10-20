using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace app_desktop.Services.SteamConnection;

internal sealed class SteamConnectionUiService
{
	private readonly Func<string?> findSteamDirectory;

	private readonly Action<ProcessStartInfo> startProcess;

	private bool runtimeInitialized;

	internal bool RuntimeInitialized => runtimeInitialized;

	internal SteamConnectionUiService(string? preferencesDirectory = null, Func<string?>? findSteamDirectory = null, Action<ProcessStartInfo>? startProcess = null)
	{
		this.findSteamDirectory = findSteamDirectory ?? ((Func<string>)(() => SteamInstallLocator.FindSteamDirectory(allowDriveScan: false)));
		this.startProcess = startProcess ?? ((Action<ProcessStartInfo>)delegate(ProcessStartInfo info)
		{
			using (Process.Start(info))
			{
			}
		});
	}

	internal SteamConnectionUiState GetState(string? feedback = null)
	{
		return new SteamConnectionUiState(AutoEnable: false, feedback);
	}

	internal SteamConnectionUiState SetAutoEnable(bool enabled)
	{
		return GetState("Hỗ trợ Steam chỉ bật trong phiên hiện tại. Mỗi lần mở launcher, bạn cần bật lại.");
	}

	internal SteamConnectionUiState MarkRuntimeInitialized()
	{
		runtimeInitialized = true;
		return GetState("Đã chuẩn bị bộ hỗ trợ Steam.");
	}

	internal string OpenSteam(CancellationToken cancellationToken)
	{
		try
		{
			cancellationToken.ThrowIfCancellationRequested();
			string text = findSteamDirectory();
			if (string.IsNullOrWhiteSpace(text) || !File.Exists(Path.Combine(text, "steam.exe")))
			{
				return "Chưa tìm thấy Steam trên máy này. Vui lòng cài hoặc mở Steam trước.";
			}
			cancellationToken.ThrowIfCancellationRequested();
			startProcess(new ProcessStartInfo
			{
				FileName = Path.Combine(text, "steam.exe"),
				WorkingDirectory = text,
				UseShellExecute = true
			});
			return "Đã gửi yêu cầu mở Steam. Trạng thái kết nối chưa được kiểm tra.";
		}
		catch (OperationCanceledException)
		{
			throw;
		}
		catch
		{
			return "Không thể mở Steam. Vui lòng mở Steam trực tiếp.";
		}
	}

	internal static bool IsCommand(string? type)
	{
		switch (type)
		{
		case "steamConnectionReady":
		case "setSteamConnectionEnabled":
		case "setSteamConnectionAutoEnable":
		case "refreshSteamConnection":
		case "openSteamConnectionClient":
			return true;
		default:
			return false;
		}
	}

	internal static bool IsTrustedAction(string source, bool hasStorefront, string? requestedDocumentId, string? currentDocumentId)
	{
		if (source == "about:blank" && hasStorefront && !string.IsNullOrEmpty(currentDocumentId))
		{
			return requestedDocumentId == currentDocumentId;
		}
		return false;
	}
}
