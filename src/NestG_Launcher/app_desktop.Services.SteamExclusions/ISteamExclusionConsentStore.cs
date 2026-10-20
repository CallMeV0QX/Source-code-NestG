namespace app_desktop.Services.SteamExclusions;

internal interface ISteamExclusionConsentStore
{
	SteamExclusionConsent Load();

	void Save(SteamExclusionConsent consent);
}
