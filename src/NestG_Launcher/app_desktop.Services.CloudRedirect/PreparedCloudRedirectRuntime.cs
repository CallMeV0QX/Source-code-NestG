using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace app_desktop.Services.CloudRedirect;

internal sealed record PreparedCloudRedirectRuntime(string BundleId, string Version, string OpenSteamToolCommit, string CloudRedirectVersion, IReadOnlyList<PreparedCloudRedirectRuntimeFile> Files)
{
	internal bool BetterOnly { get; init; }

	[CompilerGenerated]
	private bool PrintMembers(StringBuilder builder)
	{
		RuntimeHelpers.EnsureSufficientExecutionStack();
		builder.Append("BundleId = ");
		builder.Append((object?)BundleId);
		builder.Append(", Version = ");
		builder.Append((object?)Version);
		builder.Append(", OpenSteamToolCommit = ");
		builder.Append((object?)OpenSteamToolCommit);
		builder.Append(", CloudRedirectVersion = ");
		builder.Append((object?)CloudRedirectVersion);
		builder.Append(", Files = ");
		builder.Append(Files);
		return true;
	}
}
