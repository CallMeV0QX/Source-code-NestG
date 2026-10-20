using System.Collections.Generic;

namespace app_desktop.Services.SteamExclusions;

internal interface ISteamExclusionOwnership
{
	IReadOnlyList<string> ReadPaths();
}
