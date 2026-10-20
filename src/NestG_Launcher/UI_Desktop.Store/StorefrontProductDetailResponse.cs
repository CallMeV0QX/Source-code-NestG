using System;
using System.Collections.Generic;

namespace UI_Desktop.Store;

internal sealed class StorefrontProductDetailResponse
{
	public Guid GameId { get; set; }

	public string Slug { get; set; } = string.Empty;

	public string Name { get; set; } = string.Empty;

	public string PageTitle { get; set; } = string.Empty;

	public string RatingScoreText { get; set; } = string.Empty;

	public string RatingCountText { get; set; } = string.Empty;

	public string CurrentPriceText { get; set; } = string.Empty;

	public string? ReferencePriceText { get; set; }

	public string? DiscountText { get; set; }

	public string StatusText { get; set; } = string.Empty;

	public string CoverImageUrl { get; set; } = string.Empty;

	public string AgeLetter { get; set; } = string.Empty;

	public string AgeCaption { get; set; } = string.Empty;

	public string AgeDescription { get; set; } = string.Empty;

	public string? ShortDescription { get; set; }

	public string? PurchaseCardDescriptionHtml { get; set; }

	public string? AboutTheGameHtml { get; set; }

	public string? DetailedDescriptionHtml { get; set; }

	public string? SteamReviewsHtml { get; set; }

	public string? Website { get; set; }

	public List<string> SummaryGenres { get; set; } = new List<string>();

	public List<string> SummaryFeatures { get; set; } = new List<string>();

	public List<string> SummaryTags { get; set; } = new List<string>();

	public List<StorefrontProductFactResponse> Facts { get; set; } = new List<StorefrontProductFactResponse>();

	public List<StorefrontProductGalleryItemResponse> Gallery { get; set; } = new List<StorefrontProductGalleryItemResponse>();

	public List<StorefrontProductEditionResponse> Editions { get; set; } = new List<StorefrontProductEditionResponse>();

	public string EditionNote { get; set; } = string.Empty;

	public List<string> PackageItems { get; set; } = new List<string>();

	public List<StorefrontProductRequirementResponse> Requirements { get; set; } = new List<StorefrontProductRequirementResponse>();

	public List<StorefrontCustomerReviewResponse> CustomerReviews { get; set; } = new List<StorefrontCustomerReviewResponse>();

	public List<StorefrontProductCardResponse> RelatedProducts { get; set; } = new List<StorefrontProductCardResponse>();
}
