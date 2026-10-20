using System.Runtime.CompilerServices;
using System.Text;

namespace app_desktop.Services;

internal sealed record DenuvoInstallProgress(DenuvoInstallStage Stage, int CompletedFiles = 0, int TotalFiles = 0)
{
	internal string Message => Stage switch
	{
		DenuvoInstallStage.Downloading => "Đang tải file Denuvo", 
		DenuvoInstallStage.Validating => "Đang giải nén và kiểm tra file", 
		DenuvoInstallStage.BackingUp => "Đang sao lưu file hiện tại", 
		DenuvoInstallStage.Installing => "Đang cài file Denuvo", 
		DenuvoInstallStage.RollingBack => "Đang khôi phục cài đặt trước đó", 
		_ => "Đang gửi yêu cầu admin cấp token", 
	};

	[CompilerGenerated]
	private bool PrintMembers(StringBuilder builder)
	{
		RuntimeHelpers.EnsureSufficientExecutionStack();
		builder.Append("Stage = ");
		builder.Append(Stage.ToString());
		builder.Append(", CompletedFiles = ");
		builder.Append(CompletedFiles.ToString());
		builder.Append(", TotalFiles = ");
		builder.Append(TotalFiles.ToString());
		return true;
	}
}
