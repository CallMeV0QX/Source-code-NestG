namespace UI_Desktop.Store;

internal sealed class StorefrontCustomerReviewResponse
{
	public string DisplayName { get; set; } = string.Empty;

	public string AvatarText { get; set; } = string.Empty;

	public string RatingText { get; set; } = string.Empty;

	public string CreatedAtText { get; set; } = string.Empty;

	public string? ReviewText { get; set; }
}
