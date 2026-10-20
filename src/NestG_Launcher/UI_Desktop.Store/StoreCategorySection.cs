using System;
using System.Collections.Generic;
using System.Linq;

namespace UI_Desktop.Store;

internal sealed class StoreCategorySection
{
	public string Name { get; set; }

	public string Slug { get; set; }

	public IReadOnlyList<StoreGame> Games { get; set; }

	public static StoreCategorySection FromApi(StorefrontCategorySectionResponse section)
	{
		StoreGame[] games = (from game in section.Games.Select(StoreGame.FromApi)
			group game by game.GameId into @group
			select @group.First()).ToArray();
		return new StoreCategorySection
		{
			Name = (string.IsNullOrWhiteSpace(section.Name) ? "Game" : section.Name.Trim()),
			Slug = (string.IsNullOrWhiteSpace(section.Slug) ? Slugify(section.Name) : section.Slug.Trim()),
			Games = games
		};
	}

	internal static string Slugify(string? value)
	{
		string text = new string((string.IsNullOrWhiteSpace(value) ? "game" : value.Trim().ToLowerInvariant()).Select((char character) => (!char.IsLetterOrDigit(character)) ? '-' : character).ToArray()).Trim('-');
		while (text.Contains("--", StringComparison.Ordinal))
		{
			text = text.Replace("--", "-", StringComparison.Ordinal);
		}
		if (!string.IsNullOrWhiteSpace(text))
		{
			return text;
		}
		return "game";
	}
}
