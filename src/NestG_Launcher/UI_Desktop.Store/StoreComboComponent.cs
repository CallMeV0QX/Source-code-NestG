using System;

namespace UI_Desktop.Store;

internal sealed record StoreComboComponent(Guid GameId, Guid VersionId, string GameName, string? VersionName, string? PhotoUrl, int DisplayOrder);
