using System;
using System.Collections.Generic;

namespace UI_Desktop.Store;

internal sealed class DesktopDenuvoFixUpdateCheckResponse
{
	public DateTime CheckedAt { get; set; }

	public List<DesktopDenuvoFixUpdateItemResponse> Items { get; set; } = new List<DesktopDenuvoFixUpdateItemResponse>();

	public List<DesktopDenuvoFixUpdateItemResponse> Updates
	{
		get
		{
			return Items;
		}
		set
		{
			Items = value ?? new List<DesktopDenuvoFixUpdateItemResponse>();
		}
	}
}
