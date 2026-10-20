using System;

namespace UI_Desktop.Store;

internal sealed record StoreComboMedia(Guid? MediaId, string MediaType, string Url, string? AltText, int DisplayOrder);
