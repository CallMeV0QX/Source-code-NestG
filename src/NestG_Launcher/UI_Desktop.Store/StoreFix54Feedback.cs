using System;
using app_desktop.Services.Fix54;

namespace UI_Desktop.Store;

internal sealed record StoreFix54Feedback(Guid LibraryId, string DocumentId, string State, string Message)
{
	internal static string StateFor(Fix54Result result)
	{
		switch (result.Outcome)
		{
		case Fix54Outcome.Changed:
			return "success";
		case Fix54Outcome.Unchanged:
			return "warning";
		case Fix54Outcome.Cancelled:
			if (result.RollbackCompleted)
			{
				return "warning";
			}
			break;
		}
		return "error";
	}

	internal StoreFix54Feedback ForDocument(Guid libraryId, string documentId, bool suppress = false)
	{
		if (suppress || !(LibraryId == libraryId) || !(DocumentId == documentId))
		{
			return new StoreFix54Feedback(libraryId, documentId, "warning", string.Empty);
		}
		return this;
	}
}
