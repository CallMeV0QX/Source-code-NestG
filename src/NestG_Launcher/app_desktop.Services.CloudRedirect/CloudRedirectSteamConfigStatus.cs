using System.Runtime.CompilerServices;
using System.Text;

namespace app_desktop.Services.CloudRedirect;

internal sealed record CloudRedirectSteamConfigStatus(bool IsValid, bool IsEnabled, string Message)
{
	internal bool IsReady
	{
		get
		{
			if (IsValid)
			{
				return IsEnabled;
			}
			return false;
		}
	}

	[CompilerGenerated]
	private bool PrintMembers(StringBuilder builder)
	{
		RuntimeHelpers.EnsureSufficientExecutionStack();
		builder.Append("IsValid = ");
		builder.Append(IsValid.ToString());
		builder.Append(", IsEnabled = ");
		builder.Append(IsEnabled.ToString());
		builder.Append(", Message = ");
		builder.Append((object?)Message);
		return true;
	}
}
