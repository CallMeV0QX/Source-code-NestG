using System.Collections.Generic;

namespace UI_Desktop.Store;

internal sealed class DesktopDenuvoFixUpdateCheckRequest
{
	public List<DesktopDenuvoFixLocalStateRequest> Items { get; set; } = new List<DesktopDenuvoFixLocalStateRequest>();
}
