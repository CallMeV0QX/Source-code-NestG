using System.Collections.Generic;

namespace app_desktop.Services.SteamExclusions;

internal sealed record DefenderSnapshot(DefenderAvailability Status, IReadOnlyList<string> Paths);
