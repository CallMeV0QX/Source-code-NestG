using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace app_desktop.Services.CloudRedirect;

internal sealed record SteamProcessSnapshot(SteamProcessState State, IReadOnlyList<CloudRedirectSteamSession> Sessions)
{
	internal SteamProcessObservation Observation => new SteamProcessObservation(State, (State == SteamProcessState.Running && Sessions.Count == 1) ? Sessions[0] : null);

	[CompilerGenerated]
	private bool PrintMembers(StringBuilder builder)
	{
		RuntimeHelpers.EnsureSufficientExecutionStack();
		builder.Append("State = ");
		builder.Append(State.ToString());
		builder.Append(", Sessions = ");
		builder.Append(Sessions);
		return true;
	}
}
