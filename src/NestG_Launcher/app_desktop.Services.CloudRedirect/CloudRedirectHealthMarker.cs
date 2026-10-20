using System;

namespace app_desktop.Services.CloudRedirect;

internal sealed record CloudRedirectHealthMarker(string LogPath, long Length, DateTime LastWriteTimeUtc);
