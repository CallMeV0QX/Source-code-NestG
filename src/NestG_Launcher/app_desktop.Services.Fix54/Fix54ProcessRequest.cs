namespace app_desktop.Services.Fix54;

internal sealed record Fix54ProcessRequest(string ScriptPath, string WrapperPath, string SteamRoot, string TemporaryDirectory, string WorkingDirectory, int SteamAppId);
