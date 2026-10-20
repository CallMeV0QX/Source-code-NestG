using System;
using System.Globalization;

namespace UI_Desktop.Store;

internal sealed class StoreComboCard
{
	public Guid ComboId { get; set; }

	public string Name { get; set; } = string.Empty;

	public string Slug { get; set; } = string.Empty;

	public decimal Price { get; set; }

	public string PosterUrl { get; set; } = string.Empty;

	public bool IsTrending { get; set; }

	public int ComponentCount { get; set; }

	public int RevisionNumber { get; set; }

	public string PriceText => FormatMoney(Price);

	public static StoreComboCard FromApi(StoreComboCardResponse response)
	{
		return new StoreComboCard
		{
			ComboId = response.ComboId,
			Name = (response.Name?.Trim() ?? "Combo Game"),
			Slug = (response.Slug?.Trim() ?? string.Empty),
			Price = Math.Max(0m, response.Price),
			PosterUrl = (response.PosterUrl?.Trim() ?? string.Empty),
			IsTrending = response.IsTrending,
			ComponentCount = Math.Max(0, response.ComponentCount),
			RevisionNumber = Math.Max(0, response.RevisionNumber)
		};
	}

	internal static string FormatMoney(decimal amount)
	{
		return string.Format(CultureInfo.GetCultureInfo("vi-VN"), "{0:N0} đ", Math.Max(0m, amount));
	}
}
