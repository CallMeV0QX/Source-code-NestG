using System;
using System.Globalization;

namespace UI_Desktop.Store;

internal sealed class StoreCustomerReview
{
	public string DisplayName { get; set; }

	public string AvatarText { get; set; }

	public string RatingText { get; set; }

	public string CreatedAtText { get; set; }

	public string ReviewText { get; set; }

	public decimal RatingValue
	{
		get
		{
			decimal.TryParse(RatingText, NumberStyles.Any, CultureInfo.InvariantCulture, out var result);
			return Math.Max(0m, result);
		}
	}
}
