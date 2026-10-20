using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace app_desktop.Services;

internal sealed record GameActivationResult(string TargetDirectory, IReadOnlyList<string> WrittenFiles, SteamInstallResult SteamInstall, string LogFilePath)
{
	internal string? ActivationLogId { get; init; }

	[CompilerGenerated]
	private bool PrintMembers(StringBuilder builder)
	{
		RuntimeHelpers.EnsureSufficientExecutionStack();
		builder.Append("TargetDirectory = ");
		builder.Append((object?)TargetDirectory);
		builder.Append(", WrittenFiles = ");
		builder.Append(WrittenFiles);
		builder.Append(", SteamInstall = ");
		builder.Append(SteamInstall);
		builder.Append(", LogFilePath = ");
		builder.Append((object?)LogFilePath);
		return true;
	}
}
