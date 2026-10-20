using System;

namespace app_desktop.Services.CloudRedirect;

internal interface ISteamProcessHandle : IDisposable
{
	int Id { get; }
}
