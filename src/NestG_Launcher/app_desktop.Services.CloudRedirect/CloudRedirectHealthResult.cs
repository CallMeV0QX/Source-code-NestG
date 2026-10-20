using System.Runtime.CompilerServices;
using System.Text;

namespace app_desktop.Services.CloudRedirect;

internal sealed record CloudRedirectHealthResult(bool IsReady, bool IsDefinitiveFailure, string LogPath, string Message)
{
	internal CloudRedirectConditionSeverity Severity
	{
		get
		{
			if (!IsReady)
			{
				if (!IsDefinitiveFailure)
				{
					return CloudRedirectConditionSeverity.Degraded;
				}
				return CloudRedirectConditionSeverity.DefinitiveFailure;
			}
			return CloudRedirectConditionSeverity.Ready;
		}
	}

	[CompilerGenerated]
	private bool PrintMembers(StringBuilder builder)
	{
		RuntimeHelpers.EnsureSufficientExecutionStack();
		builder.Append("IsReady = ");
		builder.Append(IsReady.ToString());
		builder.Append(", IsDefinitiveFailure = ");
		builder.Append(IsDefinitiveFailure.ToString());
		builder.Append(", LogPath = ");
		builder.Append((object?)LogPath);
		builder.Append(", Message = ");
		builder.Append((object?)Message);
		return true;
	}
}
