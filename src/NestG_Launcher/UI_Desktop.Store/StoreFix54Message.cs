using System;

namespace UI_Desktop.Store;

internal static class StoreFix54Message
{
	internal const string Prefix = "fix54:";

	internal static bool TryAuthorize(string message, string source, bool isLibraryDetail, Guid libraryId, string? documentId, out bool isCancellation)
	{
		isCancellation = false;
		if (!isLibraryDetail || libraryId == Guid.Empty || string.IsNullOrEmpty(documentId) || !string.Equals(source, "about:blank", StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}
		string[] array = message.Split(':');
		bool flag = array.Length != 4 || array[0] != "fix54";
		if (!flag)
		{
			string text = array[1];
			bool flag2 = ((text == "run" || text == "cancel") ? true : false);
			flag = !flag2;
		}
		if (flag || !Guid.TryParseExact(array[2], "N", out var result) || result != libraryId || !string.Equals(array[3], documentId, StringComparison.Ordinal))
		{
			return false;
		}
		isCancellation = array[1] == "cancel";
		return true;
	}
}
