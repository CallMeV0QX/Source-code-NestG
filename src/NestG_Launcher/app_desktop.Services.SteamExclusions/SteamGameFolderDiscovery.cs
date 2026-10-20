using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace app_desktop.Services.SteamExclusions;

internal sealed class SteamGameFolderDiscovery : ISteamGameFolderDiscovery
{
	internal const int MaxLibraries = 32;

	internal const int MaxVdfLength = 262144;

	public async Task<IReadOnlyList<string>> FindAsync(CancellationToken cancellationToken)
	{
		return await Task.Run((Func<IReadOnlyList<string>>)delegate
		{
			cancellationToken.ThrowIfCancellationRequested();
			string text = SteamInstallLocator.FindSteamDirectory(allowDriveScan: false);
			cancellationToken.ThrowIfCancellationRequested();
			if (text == null)
			{
				return Array.Empty<string>();
			}
			string path = Path.Combine(text, "steamapps", "libraryfolders.vdf");
			string vdf = null;
			if (File.Exists(path))
			{
				using StreamReader streamReader = new StreamReader(path);
				char[] array = new char[262145];
				int num = streamReader.ReadBlock(array, 0, array.Length);
				if (num > 262144)
				{
					throw new InvalidDataException("Steam library metadata is too large.");
				}
				vdf = new string(array, 0, num);
			}
			return (from root in ParseLibraryRoots(text, vdf)
				select Path.Combine(root, "steamapps", "common")).Where(SteamExclusionPaths.IsSafeExistingGameFolder).Distinct<string>(StringComparer.OrdinalIgnoreCase).ToArray();
		}, cancellationToken).WaitAsync(TimeSpan.FromSeconds(10.0), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
	}

	internal static IReadOnlyList<string> ParseLibraryRoots(string steam, string? vdf)
	{
		if (vdf != null && vdf.Length > 262144)
		{
			throw new InvalidDataException("Steam library metadata is too large.");
		}
		HashSet<string> roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		Add(steam);
		foreach (Match item in Regex.Matches(vdf ?? string.Empty, "\"(?:path|[0-9]+)\"\\s+\"(?<path>(?:\\\\.|[^\"\\\\])*)\"", RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(250.0)))
		{
			Add(item.Groups["path"].Value.Replace("\\\\", "\\").Replace("\\\"", "\""));
		}
		return roots.OrderBy<string, string>((string path) => path, StringComparer.OrdinalIgnoreCase).ToArray();
		void Add(string raw)
		{
			string text = SteamExclusionPaths.NormalizeLiteralLocalPath(raw);
			if (text != null)
			{
				roots.Add(text);
				if (roots.Count > 32)
				{
					throw new InvalidDataException("Too many Steam libraries.");
				}
			}
		}
	}
}
