using System;

namespace app_desktop.Services.CloudRedirect.Patching;

internal class PatternPatch
{
	public string Name { get; init; }

	public byte[] Pattern { get; init; }

	public byte[] Mask { get; init; }

	public int PatchOffset { get; init; }

	public byte[] Original { get; init; }

	public byte[] Replacement { get; init; }

	public ScanRegion Region { get; init; }

	public int WildcardStart { get; init; }

	public int WildcardLen { get; init; }

	public Func<byte[], int, bool> Validator { get; init; }

	public Func<byte[], int, int> PatchSiteResolver { get; init; }

	public int? RelativeToPatchIndex { get; init; }

	public int RelativeStart { get; init; }

	public int RelativeEnd { get; init; }
}
