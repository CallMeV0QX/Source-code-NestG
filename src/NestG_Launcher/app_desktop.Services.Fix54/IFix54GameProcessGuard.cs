using System.Collections.Generic;

namespace app_desktop.Services.Fix54;

internal interface IFix54GameProcessGuard
{
	void EnsureClosed(string gameRoot, IReadOnlyList<string> executablePaths);
}
