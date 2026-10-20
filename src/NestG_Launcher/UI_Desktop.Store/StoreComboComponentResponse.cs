using System;

namespace UI_Desktop.Store;

internal sealed class StoreComboComponentResponse
{
	public Guid GameId { get; set; }

	public Guid VersionId { get; set; }

	public string GameName { get; set; } = string.Empty;

	public string? VersionName { get; set; }

	public string? PhotoUrl { get; set; }

	public int DisplayOrder { get; set; }
}
