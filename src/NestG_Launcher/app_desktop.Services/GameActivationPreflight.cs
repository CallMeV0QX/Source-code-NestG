namespace app_desktop.Services;

internal static class GameActivationPreflight
{
	internal static void ValidateWindowsArchitecture(bool is64BitOperatingSystem)
	{
		if (!is64BitOperatingSystem)
		{
			throw new ActivationRequires64BitWindowsException();
		}
	}
}
