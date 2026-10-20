using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace UI_Desktop.Store;

internal sealed class StoreGameDetailData
{
	public Guid GameId { get; set; }

	public string Slug { get; set; }

	public string Name { get; set; }

	public string PageTitle { get; set; }

	public string RatingScoreText { get; set; }

	public decimal RatingScoreValue { get; set; }

	public string RatingCountText { get; set; }

	public string CurrentPriceText { get; set; }

	public string? ReferencePriceText { get; set; }

	public string? DiscountText { get; set; }

	public string StatusText { get; set; }

	public string CoverImageUrl { get; set; }

	public string AgeLetter { get; set; }

	public string AgeCaption { get; set; }

	public string AgeDescription { get; set; }

	public string? ShortDescription { get; set; }

	public string? PurchaseCardDescriptionHtml { get; set; }

	public string? PurchaseCardDescriptionText { get; set; }

	public string? AboutHtml { get; set; }

	public string? AboutText { get; set; }

	public string? DetailedDescriptionHtml { get; set; }

	public string? DetailedDescriptionText { get; set; }

	public string? SteamReviewsHtml { get; set; }

	public string? SteamReviewsText { get; set; }

	public string? Website { get; set; }

	public IReadOnlyList<string> SummaryGenres { get; set; }

	public IReadOnlyList<string> SummaryFeatures { get; set; }

	public IReadOnlyList<string> SummaryTags { get; set; }

	public IReadOnlyList<StoreProductFact> Facts { get; set; }

	public IReadOnlyList<StoreProductGalleryItem> Gallery { get; set; }

	public IReadOnlyList<StoreProductEdition> Editions { get; set; }

	public Guid? PreferredVersionId { get; set; }

	public string EditionNote { get; set; }

	public IReadOnlyList<string> PackageItems { get; set; }

	public IReadOnlyList<StoreProductRequirement> Requirements { get; set; }

	public IReadOnlyList<StoreCustomerReview> CustomerReviews { get; set; }

	public IReadOnlyList<StoreGame> RelatedProducts { get; set; }

	public static StoreGameDetailData FromApi(StorefrontProductDetailResponse response)
	{
		decimal val = ParseRatingScore(response.RatingScoreText);
		List<StoreProductGalleryItem> list = (from item in response.Gallery
			where !string.IsNullOrWhiteSpace(item.FullUrl) || !string.IsNullOrWhiteSpace(item.ThumbnailUrl)
			select new StoreProductGalleryItem
			{
				ThumbnailUrl = (string.IsNullOrWhiteSpace(item.ThumbnailUrl) ? item.FullUrl : item.ThumbnailUrl),
				FullUrl = (string.IsNullOrWhiteSpace(item.FullUrl) ? item.ThumbnailUrl : item.FullUrl),
				Alt = (string.IsNullOrWhiteSpace(item.Alt) ? response.Name : item.Alt.Trim()),
				MainPosition = (string.IsNullOrWhiteSpace(item.MainPosition) ? "center center" : item.MainPosition.Trim())
			} into item
			where !string.IsNullOrWhiteSpace(item.FullUrl) || !string.IsNullOrWhiteSpace(item.ThumbnailUrl)
			select item).ToList();
		if (list.Count == 0 && !string.IsNullOrWhiteSpace(response.CoverImageUrl))
		{
			list.Add(new StoreProductGalleryItem
			{
				ThumbnailUrl = response.CoverImageUrl,
				FullUrl = response.CoverImageUrl,
				Alt = response.Name,
				MainPosition = "center center"
			});
		}
		return new StoreGameDetailData
		{
			GameId = response.GameId,
			Slug = response.Slug.Trim(),
			Name = response.Name.Trim(),
			PageTitle = (string.IsNullOrWhiteSpace(response.PageTitle) ? response.Name.Trim() : response.PageTitle.Trim()),
			RatingScoreText = (string.IsNullOrWhiteSpace(response.RatingScoreText) ? "-" : response.RatingScoreText.Trim()),
			RatingScoreValue = Math.Max(0m, val),
			RatingCountText = (NormalizeGeneratedVietnameseText(response.RatingCountText) ?? "Chưa có dữ liệu đánh giá"),
			CurrentPriceText = (NormalizeGeneratedVietnameseText(response.CurrentPriceText) ?? "Liên hệ"),
			ReferencePriceText = NormalizeOptionalText(response.ReferencePriceText),
			DiscountText = NormalizeOptionalText(response.DiscountText),
			StatusText = (NormalizeGeneratedVietnameseText(response.StatusText) ?? "Còn hàng"),
			CoverImageUrl = response.CoverImageUrl,
			AgeLetter = (string.IsNullOrWhiteSpace(response.AgeLetter) ? "ALL" : response.AgeLetter.Trim()),
			AgeCaption = (string.IsNullOrWhiteSpace(response.AgeCaption) ? "Steam" : response.AgeCaption.Trim()),
			AgeDescription = (NormalizeGeneratedVietnameseText(response.AgeDescription) ?? "Thông tin độ tuổi chưa được cập nhật."),
			ShortDescription = NormalizeOptionalText(response.ShortDescription),
			PurchaseCardDescriptionHtml = NormalizeOptionalHtml(response.PurchaseCardDescriptionHtml),
			PurchaseCardDescriptionText = StoreHtmlText.ToPlainText(response.PurchaseCardDescriptionHtml),
			AboutHtml = NormalizeOptionalHtml(response.AboutTheGameHtml),
			AboutText = StoreHtmlText.ToPlainText(response.AboutTheGameHtml),
			DetailedDescriptionHtml = NormalizeOptionalHtml(response.DetailedDescriptionHtml),
			DetailedDescriptionText = StoreHtmlText.ToPlainText(response.DetailedDescriptionHtml),
			SteamReviewsHtml = NormalizeOptionalHtml(response.SteamReviewsHtml),
			SteamReviewsText = StoreHtmlText.ToPlainText(response.SteamReviewsHtml),
			Website = NormalizeOptionalText(response.Website),
			SummaryGenres = (from item in response.SummaryGenres
				where !string.IsNullOrWhiteSpace(item)
				select item.Trim()).Distinct<string>(StringComparer.OrdinalIgnoreCase).ToArray(),
			SummaryFeatures = (from item in response.SummaryFeatures
				where !string.IsNullOrWhiteSpace(item)
				select item.Trim()).Distinct<string>(StringComparer.OrdinalIgnoreCase).ToArray(),
			SummaryTags = (from item in response.SummaryTags
				where !string.IsNullOrWhiteSpace(item)
				select item.Trim()).Distinct<string>(StringComparer.OrdinalIgnoreCase).ToArray(),
			Facts = (from item in response.Facts
				where !string.IsNullOrWhiteSpace(item.Label) && !string.IsNullOrWhiteSpace(item.Value)
				select new StoreProductFact
				{
					Label = (NormalizeGeneratedVietnameseText(item.Label) ?? item.Label.Trim()),
					Value = (NormalizeGeneratedVietnameseText(item.Value) ?? item.Value.Trim())
				}).ToArray(),
			Gallery = list,
			Editions = response.Editions.Select((StorefrontProductEditionResponse item, int index) => new StoreProductEdition
			{
				VersionId = item.VersionId,
				CurrentPrice = item.CurrentPrice,
				Name = NormalizeEditionDisplayName(item.Name, index),
				Note = (NormalizeGeneratedVietnameseText(item.Note) ?? "Phiên bản này chưa có ghi chú mở rộng."),
				CurrentPriceText = (NormalizeGeneratedVietnameseText(item.CurrentPriceText) ?? "Liên hệ"),
				ReferencePriceText = NormalizeGeneratedVietnameseText(item.ReferencePriceText),
				DiscountText = NormalizeOptionalText(item.DiscountText),
				PriceText = NormalizeGeneratedVietnameseText(item.PriceText),
				IsActive = item.IsActive
			}).ToArray(),
			EditionNote = (NormalizeGeneratedVietnameseText(response.EditionNote) ?? "Hãy chọn phiên bản phù hợp trước khi thêm vào giỏ hàng."),
			PackageItems = (from item in response.PackageItems
				where !string.IsNullOrWhiteSpace(item)
				select NormalizeGeneratedVietnameseText(item) ?? item.Trim()).ToArray(),
			Requirements = (from item in response.Requirements
				where string.Equals(item.Platform?.Trim(), "Windows", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(item.Html)
				select new StoreProductRequirement
				{
					Platform = item.Platform.Trim(),
					Text = (StoreHtmlText.ToPlainText(item.Html) ?? string.Empty)
				} into item
				where !string.IsNullOrWhiteSpace(item.Text)
				select item).ToArray(),
			CustomerReviews = (from item in response.CustomerReviews
				where !string.IsNullOrWhiteSpace(item.DisplayName)
				select new StoreCustomerReview
				{
					DisplayName = item.DisplayName.Trim(),
					AvatarText = (string.IsNullOrWhiteSpace(item.AvatarText) ? item.DisplayName.Trim()[0].ToString().ToUpperInvariant() : item.AvatarText.Trim()),
					RatingText = (string.IsNullOrWhiteSpace(item.RatingText) ? "-" : item.RatingText.Trim()),
					CreatedAtText = (string.IsNullOrWhiteSpace(item.CreatedAtText) ? string.Empty : item.CreatedAtText.Trim()),
					ReviewText = NormalizeCustomerReviewText(item.ReviewText)
				}).ToArray(),
			RelatedProducts = (from item in response.RelatedProducts.Select(StoreGame.FromApi)
				group item by item.GameId into @group
				select @group.First()).ToArray()
		};
	}

	public StoreProductEdition? GetActiveEdition()
	{
		Guid? preferredVersionId = PreferredVersionId;
		if (preferredVersionId.HasValue)
		{
			Guid preferredVersionId2 = preferredVersionId.GetValueOrDefault();
			if (preferredVersionId2 != Guid.Empty)
			{
				StoreProductEdition storeProductEdition = Editions.FirstOrDefault((StoreProductEdition item) => item.VersionId == preferredVersionId2);
				if (storeProductEdition != null)
				{
					return storeProductEdition;
				}
			}
		}
		return Editions.FirstOrDefault((StoreProductEdition item) => item.IsActive) ?? Editions.FirstOrDefault();
	}

	private static string? NormalizeOptionalText(string? value)
	{
		if (!string.IsNullOrWhiteSpace(value))
		{
			return value.Trim();
		}
		return null;
	}

	private static string? NormalizeOptionalHtml(string? value)
	{
		if (!string.IsNullOrWhiteSpace(value))
		{
			return value.Trim();
		}
		return null;
	}

	private static string NormalizeCustomerReviewText(string? value)
	{
		string text = NormalizeGeneratedVietnameseText(value);
		string text2;
		if (!string.Equals(text, "Người dùng chưa để lại nội dung chi tiết.", StringComparison.OrdinalIgnoreCase))
		{
			text2 = text;
			if (text2 == null)
			{
				return string.Empty;
			}
		}
		else
		{
			text2 = string.Empty;
		}
		return text2;
	}

	private static string NormalizeEditionDisplayName(string? value, int index)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return $"Phiên bản {index + 1}";
		}
		string text = NormalizeGeneratedVietnameseText(value) ?? value.Trim();
		string text2 = text.ToLowerInvariant();
		int num = (text2.StartsWith("phiên bản ", StringComparison.Ordinal) ? "phiên bản ".Length : (text2.StartsWith("phien ban ", StringComparison.Ordinal) ? "phien ban ".Length : 0));
		if (num == 0)
		{
			return text.Trim();
		}
		int num2 = IndexOfAnyPhrase(text2, num, " dành cho ", " danh cho ", " đang áp dụng ", " dang ap dung ", " giá ", " gia ", ",");
		string text3;
		if (num2 <= num)
		{
			text3 = text.Substring(num);
		}
		else
		{
			int num3 = num;
			text3 = text.Substring(num3, num2 - num3);
		}
		string text4 = text3;
		if (!string.IsNullOrWhiteSpace(text4))
		{
			return text4.Trim();
		}
		return text.Trim();
	}

	private static int IndexOfAnyPhrase(string value, int startIndex, params string[] phrases)
	{
		int num = -1;
		foreach (string value2 in phrases)
		{
			int num2 = value.IndexOf(value2, startIndex, StringComparison.Ordinal);
			if (num2 >= 0 && (num < 0 || num2 < num))
			{
				num = num2;
			}
		}
		return num;
	}

	private static string? NormalizeGeneratedVietnameseText(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return null;
		}
		string text = value.Trim();
		return text.ToLowerInvariant() switch
		{
			"ban thuong" => "Bản thường", 
			"ban full dlc" => "Bản full DLC", 
			"con hang" => "Còn hàng", 
			"dang sale" => "Đang sale", 
			"mien phi" => "Miễn phí", 
			"mien phi tren steam" => "Miễn phí trên Steam", 
			"lien he" => "Liên hệ", 
			"chua co du lieu danh gia" => "Chưa có dữ liệu đánh giá", 
			"noi dung phu hop voi da so nguoi choi theo thong tin steam." => "Nội dung phù hợp với đa số người chơi theo thông tin Steam.", 
			"thong tin do tuoi chua duoc cap nhat." => "Thông tin độ tuổi chưa được cập nhật.", 
			"phat hanh" => "Phát hành", 
			"phat trien" => "Phát triển", 
			"phat hanh boi" => "Phát hành bởi", 
			"nen tang" => "Nền tảng", 
			"ngon ngu" => "Ngôn ngữ", 
			"nguoi dung chua de lai noi dung chi tiet." => "Người dùng chưa để lại nội dung chi tiết.", 
			_ => NormalizeGeneratedVietnamesePhrases(text), 
		};
	}

	private static string NormalizeGeneratedVietnamesePhrases(string value)
	{
		return value.Replace(" danh gia NestG", " đánh giá NestG", StringComparison.OrdinalIgnoreCase).Replace(" luot de xuat", " lượt đề xuất", StringComparison.OrdinalIgnoreCase).Replace("Khuyen nghi cho nguoi choi tu ", "Khuyến nghị cho người chơi từ ", StringComparison.OrdinalIgnoreCase)
			.Replace(" tuoi tro len.", " tuổi trở lên.", StringComparison.OrdinalIgnoreCase)
			.Replace("Phien ban ", "Phiên bản ", StringComparison.OrdinalIgnoreCase)
			.Replace(" phien ban ", " phiên bản ", StringComparison.OrdinalIgnoreCase)
			.Replace("Phiên bản Bản ", "Phiên bản ", StringComparison.OrdinalIgnoreCase)
			.Replace("Phiên bản Ban ", "Phiên bản ", StringComparison.OrdinalIgnoreCase)
			.Replace(" dang ap dung ", " đang áp dụng ", StringComparison.OrdinalIgnoreCase)
			.Replace(" gia goc ", " giá gốc ", StringComparison.OrdinalIgnoreCase)
			.Replace(" gia hien tai ", " giá hiện tại ", StringComparison.OrdinalIgnoreCase)
			.Replace(" danh cho ", " dành cho ", StringComparison.OrdinalIgnoreCase)
			.Replace(" gia hien hanh ", " giá hiện hành ", StringComparison.OrdinalIgnoreCase)
			.Replace("mac dinh", "mặc định", StringComparison.OrdinalIgnoreCase)
			.Replace("Phat trien boi ", "Phát triển bởi ", StringComparison.OrdinalIgnoreCase)
			.Replace("Phat hanh boi ", "Phát hành bởi ", StringComparison.OrdinalIgnoreCase)
			.Replace("Ho tro ", "Hỗ trợ ", StringComparison.OrdinalIgnoreCase)
			.Replace("Ngay phat hanh tren Steam", "Ngày phát hành trên Steam", StringComparison.OrdinalIgnoreCase)
			.Replace("Ngon ngu noi bat", "Ngôn ngữ nổi bật", StringComparison.OrdinalIgnoreCase)
			.Replace("Steam ghi nhan ", "Steam ghi nhận ", StringComparison.OrdinalIgnoreCase)
			.Replace(" noi dung DLC lien quan", " nội dung DLC liên quan", StringComparison.OrdinalIgnoreCase)
			.Replace("Du lieu goi san pham duoc dong bo truc tiep", "Dữ liệu gói sản phẩm được đồng bộ trực tiếp", StringComparison.OrdinalIgnoreCase);
	}

	private static decimal ParseRatingScore(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return 0m;
		}
		if (!decimal.TryParse(value.Trim().Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var result))
		{
			return 0m;
		}
		return Math.Max(0m, result);
	}
}
