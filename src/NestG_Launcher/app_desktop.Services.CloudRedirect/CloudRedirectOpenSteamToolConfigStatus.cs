using System.Runtime.CompilerServices;
using System.Text;

namespace app_desktop.Services.CloudRedirect;

internal sealed record CloudRedirectOpenSteamToolConfigStatus(bool IsValid, bool IsEnabled, bool HasExpectedLibrary, string Message)
{
	internal bool IsReady
	{
		get
		{
			if (IsValid && IsEnabled)
			{
				return HasExpectedLibrary;
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
		builder.Append(", HasExpectedLibrary = ");
		builder.Append(HasExpectedLibrary.ToString());
		builder.Append(", Message = ");
		builder.Append((object?)Message);
		return true;
	}
}
