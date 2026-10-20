using System.IO;

namespace app_desktop.Services.Fix54;

internal class Fix54Exception : IOException
{
	public Fix54Exception(string message)
		: base(message)
	{
	}
}
