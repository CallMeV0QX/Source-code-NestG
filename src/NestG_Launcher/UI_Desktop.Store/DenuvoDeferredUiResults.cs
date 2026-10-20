using System;
using System.Collections.Generic;
using System.Linq;
using app_desktop.Services;

namespace UI_Desktop.Store;

internal sealed class DenuvoDeferredUiResults
{
	private readonly Dictionary<(Guid LibraryId, Guid RequestId), EventArgs> results = new Dictionary<(Guid, Guid), EventArgs>();

	internal void Remember(DesktopDenuvoActiveTokenAppliedEventArgs value)
	{
		results.TryAdd((value.LibraryId, value.ActiveRequestId), value);
	}

	internal void Remember(DesktopDenuvoActiveRequestCancelledEventArgs value)
	{
		results.TryAdd((value.LibraryId, value.ActiveRequestId), value);
	}

	internal EventArgs? Take(Guid libraryId, Guid requestId)
	{
		results.TryGetValue((libraryId, requestId), out EventArgs value);
		Clear(libraryId);
		return value;
	}

	internal void Clear(Guid libraryId)
	{
		(Guid, Guid)[] array = results.Keys.Where(((Guid LibraryId, Guid RequestId) key) => key.LibraryId == libraryId).ToArray();
		foreach ((Guid, Guid) key2 in array)
		{
			results.Remove(key2);
		}
	}
}
