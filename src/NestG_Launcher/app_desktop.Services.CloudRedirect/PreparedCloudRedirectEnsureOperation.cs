using System.Runtime.CompilerServices;
using System.Text;

namespace app_desktop.Services.CloudRedirect;

internal sealed record PreparedCloudRedirectEnsureOperation(CloudRedirectReadinessResult Readiness, PreparedCloudRedirectRuntime? Runtime)
{
	internal bool BetterOnly { get; init; }

	[CompilerGenerated]
	private bool PrintMembers(StringBuilder builder)
	{
		RuntimeHelpers.EnsureSufficientExecutionStack();
		builder.Append("Readiness = ");
		builder.Append(Readiness);
		builder.Append(", Runtime = ");
		builder.Append(Runtime);
		return true;
	}
}
