using System;
using System.Collections.Generic;

namespace UI_Desktop.Store;

internal sealed class DesktopLibraryDetailResponse
{
	public Guid LibraryId { get; set; }

	public Guid GameId { get; set; }

	public Guid VersionId { get; set; }

	public int? SteamAppId { get; set; }

	public string GameName { get; set; } = string.Empty;

	public string VersionName { get; set; } = string.Empty;

	public string OrderCode { get; set; } = string.Empty;

	public DateTime AcquiredAt { get; set; }

	public string AcquiredAtText { get; set; } = string.Empty;

	public string RatingScoreText { get; set; } = string.Empty;

	public string RatingCountText { get; set; } = string.Empty;

	public string CurrentPriceText { get; set; } = string.Empty;

	public string? ReferencePriceText { get; set; }

	public string? DiscountText { get; set; }

	public string StatusText { get; set; } = string.Empty;

	public string CoverImageUrl { get; set; } = string.Empty;

	public string? ShortDescription { get; set; }

	public string? AboutTheGameHtml { get; set; }

	public string? DetailedDescriptionHtml { get; set; }

	public string? Website { get; set; }

	public List<string> SummaryTags { get; set; } = new List<string>();

	public List<DesktopLibraryFactResponse> Facts { get; set; } = new List<DesktopLibraryFactResponse>();

	public List<DesktopLibraryGalleryItemResponse> Gallery { get; set; } = new List<DesktopLibraryGalleryItemResponse>();

	public List<DesktopLibraryEditionResponse> Editions { get; set; } = new List<DesktopLibraryEditionResponse>();

	public List<string> PackageItems { get; set; } = new List<string>();

	public List<DesktopLibraryRequirementResponse> Requirements { get; set; } = new List<DesktopLibraryRequirementResponse>();

	public List<DesktopLibraryReviewResponse> CustomerReviews { get; set; } = new List<DesktopLibraryReviewResponse>();

	public DesktopLibraryDenuvoFixResponse? DenuvoFix { get; set; }

	public DesktopLibraryPypassFixResponse? PypassFix { get; set; }

	public DesktopLibraryOnlineFixResponse? OnlineFix { get; set; }
}
