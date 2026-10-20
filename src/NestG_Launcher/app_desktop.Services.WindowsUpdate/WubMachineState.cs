using System.Runtime.CompilerServices;
using System.Text;

namespace app_desktop.Services.WindowsUpdate;

internal sealed record WubMachineState(bool? CoreDisabled, bool? CoreStopped, bool? AutomaticUpdatesDisabled, bool? SupportingServicesDisabled, bool? SupportingServicesEnabled)
{
	internal WindowsUpdateBlockKind Kind
	{
		get
		{
			if (!CoreDisabled.HasValue || !CoreStopped.HasValue || !AutomaticUpdatesDisabled.HasValue || !SupportingServicesDisabled.HasValue || !SupportingServicesEnabled.HasValue)
			{
				return WindowsUpdateBlockKind.Unavailable;
			}
			if (CoreDisabled == true && CoreStopped == true && AutomaticUpdatesDisabled == true && SupportingServicesDisabled == true)
			{
				return WindowsUpdateBlockKind.Blocked;
			}
			if (CoreDisabled == false && AutomaticUpdatesDisabled == false && SupportingServicesEnabled == true)
			{
				return WindowsUpdateBlockKind.Enabled;
			}
			return WindowsUpdateBlockKind.Partial;
		}
	}

	[CompilerGenerated]
	private bool PrintMembers(StringBuilder builder)
	{
		RuntimeHelpers.EnsureSufficientExecutionStack();
		builder.Append("CoreDisabled = ");
		builder.Append(CoreDisabled.ToString());
		builder.Append(", CoreStopped = ");
		builder.Append(CoreStopped.ToString());
		builder.Append(", AutomaticUpdatesDisabled = ");
		builder.Append(AutomaticUpdatesDisabled.ToString());
		builder.Append(", SupportingServicesDisabled = ");
		builder.Append(SupportingServicesDisabled.ToString());
		builder.Append(", SupportingServicesEnabled = ");
		builder.Append(SupportingServicesEnabled.ToString());
		return true;
	}
}
