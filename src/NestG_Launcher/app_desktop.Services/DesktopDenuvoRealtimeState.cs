using System;

namespace app_desktop.Services;

internal sealed class DesktopDenuvoRealtimeState(long revision, string stage, bool isConnected, Guid? libraryId, bool isLibraryTracked, string? errorCode) : EventArgs()
{
	public long Revision { get; } = revision;

	public string Stage { get; } = stage;

	public bool IsConnected { get; } = isConnected;

	public Guid? LibraryId { get; } = libraryId;

	public bool IsLibraryTracked { get; } = isLibraryTracked;

	public string? ErrorCode { get; } = errorCode;
}
