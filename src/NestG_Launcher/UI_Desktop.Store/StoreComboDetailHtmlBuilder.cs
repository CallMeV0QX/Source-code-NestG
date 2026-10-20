using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;

namespace UI_Desktop.Store;

internal static class StoreComboDetailHtmlBuilder
{
	public static string BuildDocument(StoreComboDetailData combo, StoreWebHomeContext context)
	{
		IReadOnlyList<JsonElement> sections = GetSections(combo.Design);
		return $"<!doctype html>\n<html lang=\"vi\">\n<head>\n<meta charset=\"utf-8\" />\n<meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\" />\n<title>{Html(combo.Name)} - NestG</title>\n<style>\n{StoreWebHomeHtmlBuilder.BuildStyles()}\n{Styles()}\n</style>\n</head>\n<body class=\"combo-detail-body\">\n<div class=\"app-shell combo-shell\" data-combo-detail-root>\n  {StoreWebHomeHtmlBuilder.RenderSharedHeader(context, "store")}\n  <main class=\"combo-page-main\">\n    <div class=\"combo-inline-nav\">\n      <button class=\"combo-inline-back\" type=\"button\" data-host-action=\"backHome\">Quay lại cửa hàng</button>\n      <span aria-hidden=\"true\">-</span>\n      <span class=\"combo-inline-current\">{Html(combo.Name)}</span>\n    </div>\n\n    <section class=\"combo-detail-section\" aria-labelledby=\"comboTitle\">\n      <div class=\"combo-container\">\n        <div class=\"combo-detail-shell\">\n          <div class=\"combo-detail-main-column\">\n            {RenderProductGallery(combo)}\n            {RenderPurchaseBar(combo)}\n            {RenderIncludedGames(combo)}\n            {RenderSections(combo, sections)}\n          </div>\n\n          <aside class=\"combo-detail-sidebar\">\n            {RenderSummary(combo)}\n          </aside>\n        </div>\n      </div>\n    </section>\n  </main>\n\n  <div class=\"combo-toast\" data-combo-toast role=\"status\" aria-live=\"polite\" hidden></div>\n</div>\n<script>\n{BuildScript()}\n</script>\n</body>\n</html>";
	}

	private static string RenderPurchaseBar(StoreComboDetailData combo)
	{
		return $"<article class=\"combo-purchase-bar\" aria-label=\"Mua combo\">\n  <div class=\"combo-purchase-main\">\n    <div class=\"combo-purchase-head\">\n      <h2>Mua {Html(combo.Name)}</h2>\n      <span class=\"combo-purchase-platform\" aria-label=\"Windows\">&#8862;</span>\n    </div>\n    <div class=\"combo-purchase-editions\" aria-label=\"Thông tin combo\">\n      <span class=\"combo-edition-chip\">\n        <span>Combo trọn gói</span>\n        <strong>{combo.Components.Count} game</strong>\n      </span>\n    </div>\n    <div class=\"combo-purchase-note\">\n      <span class=\"combo-purchase-note-icon\" aria-hidden=\"true\">!</span>\n      <p>Giá combo độc lập, không cộng từ giá của từng game thành phần.</p>\n    </div>\n  </div>\n\n  <div class=\"combo-purchase-actions\">\n    <strong class=\"combo-current-price\">{Html(combo.PriceText)}</strong>\n    <button type=\"button\" class=\"combo-purchase-button is-secondary\" data-host-action=\"addComboToCart\" data-combo-id=\"{combo.ComboId:D}\">Thêm vào giỏ</button>\n    <button type=\"button\" class=\"combo-purchase-button is-primary\" data-host-action=\"buyCombo\" data-combo-id=\"{combo.ComboId:D}\">Mua ngay</button>\n  </div>\n</article>";
	}

	private static string RenderSummary(StoreComboDetailData combo)
	{
		bool num = !string.IsNullOrWhiteSpace(combo.CoverUrl);
		string imageUrl = (num ? combo.CoverUrl : combo.PosterUrl);
		string value = (num ? "combo-summary-cover" : "combo-summary-cover combo-summary-cover--poster");
		string value2 = (combo.CanPurchase ? "Đang mở bán" : "Tạm ngừng bán");
		string value3 = (combo.CanPurchase ? "Combo có thể được thêm vào giỏ hàng và mua ngay." : "Combo hiện chưa thể thực hiện giao dịch.");
		return $"<div class=\"combo-summary\">\n  <h1 id=\"comboTitle\" class=\"combo-summary-title\">{Html(combo.Name)}</h1>\n  <div class=\"{value}\">\n    {Image(imageUrl, combo.Name)}\n  </div>\n  {RenderSummaryCopy(combo.ShortDescription)}\n  {RenderSummaryFacts(combo)}\n  <div class=\"combo-status-card\">\n    <div class=\"combo-count-badge\" aria-label=\"{combo.Components.Count} game\">\n      <strong>{combo.Components.Count}</strong>\n      <span>GAME</span>\n    </div>\n    <div class=\"combo-status-copy\">\n      <strong>{Html(value2)}</strong>\n      <span>{Html(value3)}</span>\n    </div>\n  </div>\n  {RenderSummaryTags(combo.Categories)}\n</div>";
	}

	private static string RenderSummaryCopy(string? shortDescription)
	{
		if (string.IsNullOrWhiteSpace(shortDescription))
		{
			return string.Empty;
		}
		return "<div class=\"combo-summary-copy\">\n  <p>" + Html(shortDescription) + "</p>\n</div>";
	}

	private static string RenderSummaryFacts(StoreComboDetailData combo)
	{
		return $"<div class=\"combo-summary-facts\">\n  <div class=\"combo-summary-fact\">\n    <span class=\"combo-summary-fact-label\">Sản phẩm</span>\n    <span class=\"combo-summary-fact-value\">Combo game</span>\n  </div>\n  <div class=\"combo-summary-fact\">\n    <span class=\"combo-summary-fact-label\">Bao gồm</span>\n    <span class=\"combo-summary-fact-value\">{combo.Components.Count} game</span>\n  </div>\n</div>";
	}

	private static string RenderSummaryTags(IReadOnlyList<StoreComboCategory> categories)
	{
		if (categories.Count == 0)
		{
			return string.Empty;
		}
		string text = string.Join(string.Empty, categories.Select((StoreComboCategory item) => "<span class=\"combo-summary-tag\">" + Html(item.Name) + "</span>"));
		return "<div class=\"combo-summary-block\">\n  <h2 class=\"combo-summary-label\">Danh mục</h2>\n  <div class=\"combo-summary-tags\">" + text + "</div>\n</div>";
	}

	private static string RenderSections(StoreComboDetailData combo, IReadOnlyList<JsonElement> sections)
	{
		StringBuilder stringBuilder = new StringBuilder();
		foreach (JsonElement section in sections)
		{
			if (!GetBoolean(section, "visible", fallback: true))
			{
				continue;
			}
			string text = GetString(section, "type") ?? string.Empty;
			JsonElement element = TryGetObject(section, "data");
			string @string = GetString(element, "title");
			string string2 = GetString(element, "text");
			string string3 = GetString(element, "imageUrl");
			switch (text)
			{
			case "richText":
			{
				string value = GetString(element, "text") ?? StoreHtmlText.ToPlainText(GetString(element, "html")) ?? string.Empty;
				StringBuilder stringBuilder2 = stringBuilder;
				StringBuilder stringBuilder5 = stringBuilder2;
				StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(113, 2, stringBuilder2);
				handler.AppendLiteral("<article class=\"combo-content-card combo-rich-text-card\">\n  ");
				handler.AppendFormatted(string.IsNullOrWhiteSpace(@string) ? string.Empty : ("<h2>" + Html(@string) + "</h2>"));
				handler.AppendLiteral("\n  <p class=\"combo-rich-text-content\">");
				handler.AppendFormatted(Html(value));
				handler.AppendLiteral("</p>\n</article>");
				stringBuilder5.Append(ref handler);
				break;
			}
			case "image":
			{
				StringBuilder stringBuilder2 = stringBuilder;
				StringBuilder stringBuilder6 = stringBuilder2;
				StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(70, 2, stringBuilder2);
				handler.AppendLiteral("<article class=\"combo-content-card combo-image-card\">\n  ");
				handler.AppendFormatted(string.IsNullOrWhiteSpace(@string) ? string.Empty : ("<h2>" + Html(@string) + "</h2>"));
				handler.AppendLiteral("\n  ");
				handler.AppendFormatted(Image(string3, GetString(element, "altText") ?? @string ?? combo.Name));
				handler.AppendLiteral("\n</article>");
				stringBuilder6.Append(ref handler);
				break;
			}
			case "imageWithText":
			{
				StringBuilder stringBuilder2 = stringBuilder;
				StringBuilder stringBuilder4 = stringBuilder2;
				StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(99, 3, stringBuilder2);
				handler.AppendLiteral("<article class=\"combo-content-card combo-image-text-card\">\n  ");
				handler.AppendFormatted(Image(string3, GetString(element, "altText") ?? @string ?? combo.Name));
				handler.AppendLiteral("\n  <div>\n    ");
				handler.AppendFormatted(string.IsNullOrWhiteSpace(@string) ? string.Empty : ("<h2>" + Html(@string) + "</h2>"));
				handler.AppendLiteral("\n    ");
				handler.AppendFormatted(string.IsNullOrWhiteSpace(string2) ? string.Empty : ("<p>" + Html(string2) + "</p>"));
				handler.AppendLiteral("\n  </div>\n</article>");
				stringBuilder4.Append(ref handler);
				break;
			}
			case "divider":
				stringBuilder.Append("<hr class=\"combo-divider\" />");
				break;
			case "spacer":
				stringBuilder.Append("<div class=\"combo-spacer\" aria-hidden=\"true\"></div>");
				break;
			case "reviews":
			{
				StringBuilder stringBuilder2 = stringBuilder;
				StringBuilder stringBuilder7 = stringBuilder2;
				StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(249, 1, stringBuilder2);
				handler.AppendLiteral("<article class=\"combo-section-card\">\n  <h2>");
				handler.AppendFormatted(Html(@string ?? "Đánh giá từ khách hàng"));
				handler.AppendLiteral("</h2>\n  <div class=\"combo-empty-note\" role=\"note\">\n    <span class=\"combo-empty-note-icon\" aria-hidden=\"true\">i</span>\n    <span>Chưa có đánh giá nào trong hệ thống cho combo này.</span>\n  </div>\n</article>");
				stringBuilder7.Append(ref handler);
				break;
			}
			default:
				if (!string.IsNullOrWhiteSpace(@string) || !string.IsNullOrWhiteSpace(string2))
				{
					StringBuilder stringBuilder2 = stringBuilder;
					StringBuilder stringBuilder3 = stringBuilder2;
					StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(53, 2, stringBuilder2);
					handler.AppendLiteral("<article class=\"combo-content-card\">\n  ");
					handler.AppendFormatted(string.IsNullOrWhiteSpace(@string) ? string.Empty : ("<h2>" + Html(@string) + "</h2>"));
					handler.AppendLiteral("\n  ");
					handler.AppendFormatted(string.IsNullOrWhiteSpace(string2) ? string.Empty : ("<p>" + Html(string2) + "</p>"));
					handler.AppendLiteral("\n</article>");
					stringBuilder3.Append(ref handler);
				}
				break;
			case "includedGames":
			case "gallery":
			case "hero":
			case "purchaseSummary":
				break;
			}
		}
		return stringBuilder.ToString();
	}

	private static string RenderIncludedGames(StoreComboDetailData combo)
	{
		StringBuilder stringBuilder = new StringBuilder();
		foreach (StoreComboComponent component in combo.Components)
		{
			StringBuilder stringBuilder2 = stringBuilder;
			StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(166, 3, stringBuilder2);
			handler.AppendLiteral("<article class=\"combo-component\">\n  <div class=\"combo-component-image\">");
			handler.AppendFormatted(Image(component.PhotoUrl, component.GameName));
			handler.AppendLiteral("</div>\n  <div class=\"combo-component-copy\">\n    <h3>");
			handler.AppendFormatted(Html(component.GameName));
			handler.AppendLiteral("</h3>\n    <span>");
			handler.AppendFormatted(Html(component.VersionName ?? "Phiên bản mặc định"));
			handler.AppendLiteral("</span>\n  </div>\n</article>");
			stringBuilder2.Append(ref handler);
		}
		string value = ((stringBuilder.Length == 0) ? "<div class=\"combo-empty-note\" role=\"note\">\n  <span class=\"combo-empty-note-icon\" aria-hidden=\"true\">i</span>\n  <span>Combo chưa có game thành phần để hiển thị.</span>\n</div>" : $"<div class=\"combo-component-grid\">{stringBuilder}</div>");
		return $"<article class=\"combo-section-card combo-included-games\">\n  <header class=\"combo-section-header\">\n    <div>\n      <small>NHẬN SAU KHI THANH TOÁN</small>\n      <h2>Game trong combo</h2>\n    </div>\n    <strong>{combo.Components.Count} game</strong>\n  </header>\n  {value}\n</article>";
	}

	private static string RenderProductGallery(StoreComboDetailData combo)
	{
		List<StoreComboMedia> list = (from item in combo.Media
			where item.MediaType.Equals("gallery", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(item.Url)
			orderby item.DisplayOrder
			select item).ToList();
		if (list.Count == 0 && !string.IsNullOrWhiteSpace(combo.CoverUrl))
		{
			list.Add(new StoreComboMedia(null, "cover", combo.CoverUrl, combo.Name, 0));
		}
		else if (list.Count == 0 && !string.IsNullOrWhiteSpace(combo.PosterUrl))
		{
			list.Add(new StoreComboMedia(null, "poster", combo.PosterUrl, combo.Name, 0));
		}
		if (list.Count == 0)
		{
			return string.Empty;
		}
		StoreComboMedia storeComboMedia = list[0];
		bool visible = list.Count > 1;
		string value = string.Join(string.Empty, list.Select((StoreComboMedia item, int index) => $"<button type=\"button\"\n        class=\"combo-gallery-thumb{((index == 0) ? " is-active" : string.Empty)}\"\n        data-combo-gallery-thumb\n        data-gallery-index=\"{index}\"\n        data-full-src=\"{Attr(item.Url)}\"\n        data-full-alt=\"{Attr(item.AltText ?? combo.Name)}\"\n        aria-label=\"Xem ảnh gallery {index + 1}\"\n        aria-pressed=\"{((index == 0) ? "true" : "false")}\">\n  <img src=\"{Attr(item.Url)}\" alt=\"\" loading=\"lazy\" />\n</button>"));
		return $"<section class=\"combo-gallery\" data-combo-gallery>\n  <div class=\"combo-gallery-main\">\n    <button class=\"combo-gallery-control combo-gallery-control--prev\" type=\"button\" aria-label=\"Ảnh trước\" data-combo-gallery-prev {HiddenUnless(visible)}>&#8249;</button>\n    <img src=\"{Attr(storeComboMedia.Url)}\" alt=\"{Attr(storeComboMedia.AltText ?? combo.Name)}\" class=\"combo-gallery-main-image\" data-combo-gallery-main />\n    <button class=\"combo-gallery-control combo-gallery-control--next\" type=\"button\" aria-label=\"Ảnh tiếp theo\" data-combo-gallery-next {HiddenUnless(visible)}>&#8250;</button>\n  </div>\n  <div class=\"combo-gallery-thumbs-shell\" data-combo-gallery-thumbs-shell {HiddenUnless(visible)}>\n    <button class=\"combo-gallery-thumbs-nav combo-gallery-thumbs-nav--prev\" type=\"button\" aria-label=\"Ảnh nhỏ trước\" data-combo-gallery-thumb-prev>&#8249;</button>\n    <div class=\"combo-gallery-thumbs-viewport\" data-combo-gallery-thumb-viewport>\n      <div class=\"combo-gallery-thumbs\">{value}</div>\n    </div>\n    <button class=\"combo-gallery-thumbs-nav combo-gallery-thumbs-nav--next\" type=\"button\" aria-label=\"Ảnh nhỏ tiếp theo\" data-combo-gallery-thumb-next>&#8250;</button>\n  </div>\n  <div class=\"combo-gallery-follow\">Combo trọn gói · {combo.Components.Count} game</div>\n</section>";
	}

	private static IReadOnlyList<JsonElement> GetSections(JsonElement design)
	{
		if (design.ValueKind != JsonValueKind.Object || !design.TryGetProperty("sections", out var value) || value.ValueKind != JsonValueKind.Array)
		{
			return Array.Empty<JsonElement>();
		}
		return (from item in value.EnumerateArray()
			select item.Clone()).ToArray();
	}

	private static JsonElement TryGetObject(JsonElement element, string propertyName)
	{
		if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.Object)
		{
			return default(JsonElement);
		}
		return value;
	}

	private static string? GetString(JsonElement element, string propertyName)
	{
		if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.String)
		{
			return null;
		}
		return value.GetString();
	}

	private static bool GetBoolean(JsonElement element, string propertyName, bool fallback)
	{
		JsonElement value = default(JsonElement);
		bool flag = element.ValueKind == JsonValueKind.Object && element.TryGetProperty(propertyName, out value);
		if (flag)
		{
			JsonValueKind valueKind = value.ValueKind;
			bool flag2 = valueKind - 5 <= JsonValueKind.Object;
			flag = flag2;
		}
		if (!flag)
		{
			return fallback;
		}
		return value.GetBoolean();
	}

	private static string Image(string? imageUrl, string alt)
	{
		if (!string.IsNullOrWhiteSpace(imageUrl))
		{
			return $"<img src=\"{Attr(imageUrl)}\" alt=\"{Attr(alt)}\" loading=\"lazy\" />";
		}
		return "<span class=\"combo-image-fallback\">" + Html(alt) + "</span>";
	}

	private static string HiddenUnless(bool visible)
	{
		if (!visible)
		{
			return "hidden";
		}
		return string.Empty;
	}

	private static string Html(string? value)
	{
		return WebUtility.HtmlEncode(value ?? string.Empty);
	}

	private static string Attr(string? value)
	{
		return WebUtility.HtmlEncode(value ?? string.Empty);
	}

	private static string BuildScript()
	{
		return "(() => {\n  const gallery = document.querySelector('[data-combo-gallery]');\n  const thumbs = Array.from(gallery?.querySelectorAll('[data-combo-gallery-thumb]') || []);\n  const mainImage = gallery?.querySelector('[data-combo-gallery-main]');\n  const previousButton = gallery?.querySelector('[data-combo-gallery-prev]');\n  const nextButton = gallery?.querySelector('[data-combo-gallery-next]');\n  const thumbShell = gallery?.querySelector('[data-combo-gallery-thumbs-shell]');\n  const thumbViewport = gallery?.querySelector('[data-combo-gallery-thumb-viewport]');\n  const thumbPreviousButton = gallery?.querySelector('[data-combo-gallery-thumb-prev]');\n  const thumbNextButton = gallery?.querySelector('[data-combo-gallery-thumb-next]');\n  let galleryIndex = 0;\n\n  const updateThumbnailNavigation = () => {\n    if (!thumbViewport || !thumbShell) return;\n\n    [thumbPreviousButton, thumbNextButton].forEach(button => {\n      if (button) button.hidden = true;\n    });\n\n    const hasOverflow = thumbViewport.scrollWidth > thumbViewport.clientWidth + 2;\n    thumbShell.dataset.hasOverflow = String(hasOverflow);\n    [thumbPreviousButton, thumbNextButton].forEach(button => {\n      if (button) button.hidden = !hasOverflow;\n    });\n\n    if (!hasOverflow) thumbViewport.scrollLeft = 0;\n  };\n\n  const scrollActiveThumbIntoView = (thumb) => {\n    if (!thumbViewport) return;\n\n    const viewportLeft = thumbViewport.scrollLeft;\n    const viewportRight = viewportLeft + thumbViewport.clientWidth;\n    const thumbLeft = thumb.offsetLeft;\n    const thumbRight = thumbLeft + thumb.offsetWidth;\n\n    if (thumbLeft < viewportLeft) {\n      thumbViewport.scrollTo({ left: thumbLeft, behavior: 'smooth' });\n    } else if (thumbRight > viewportRight) {\n      thumbViewport.scrollTo({\n        left: thumbRight - thumbViewport.clientWidth,\n        behavior: 'smooth'\n      });\n    }\n  };\n\n  const selectGalleryImage = (index) => {\n    if (!mainImage || thumbs.length === 0) return;\n\n    galleryIndex = (index + thumbs.length) % thumbs.length;\n    const activeThumb = thumbs[galleryIndex];\n    thumbs.forEach((thumb, thumbIndex) => {\n      const isActive = thumbIndex === galleryIndex;\n      thumb.classList.toggle('is-active', isActive);\n      thumb.setAttribute('aria-pressed', isActive ? 'true' : 'false');\n    });\n\n    mainImage.classList.add('is-transitioning');\n    window.setTimeout(() => {\n      mainImage.src = activeThumb.dataset.fullSrc || mainImage.src;\n      mainImage.alt = activeThumb.dataset.fullAlt || mainImage.alt;\n      mainImage.classList.remove('is-transitioning');\n    }, 120);\n    scrollActiveThumbIntoView(activeThumb);\n  };\n\n  thumbs.forEach((thumb, index) => {\n    thumb.addEventListener('click', () => selectGalleryImage(index));\n    thumb.querySelector('img')?.addEventListener('load', updateThumbnailNavigation);\n  });\n  previousButton?.addEventListener('click', () => selectGalleryImage(galleryIndex - 1));\n  nextButton?.addEventListener('click', () => selectGalleryImage(galleryIndex + 1));\n\n  const thumbScrollAmount = () => Math.max((thumbViewport?.clientWidth || 0) * 0.78, 240);\n  thumbPreviousButton?.addEventListener('click', () => {\n    thumbViewport?.scrollBy({ left: -thumbScrollAmount(), behavior: 'smooth' });\n  });\n  thumbNextButton?.addEventListener('click', () => {\n    thumbViewport?.scrollBy({ left: thumbScrollAmount(), behavior: 'smooth' });\n  });\n\n  updateThumbnailNavigation();\n  window.addEventListener('resize', updateThumbnailNavigation);\n\n  document.addEventListener('click', event => {\n    const action = event.target.closest('[data-host-action]');\n    if (!action) return;\n\n    event.preventDefault();\n    event.stopPropagation();\n    window.chrome.webview.postMessage({\n      type: action.dataset.hostAction,\n      comboId: action.dataset.comboId || null\n    });\n  });\n\n  window.__nestgCombo = {\n    timer: 0,\n    notify(message, tone) {\n      const toast = document.querySelector('[data-combo-toast]');\n      if (!toast) return;\n\n      toast.textContent = message || '';\n      toast.dataset.tone = tone || 'success';\n      toast.hidden = false;\n      window.clearTimeout(window.__nestgCombo.timer);\n      window.__nestgCombo.timer = window.setTimeout(() => {\n        toast.hidden = true;\n      }, 3500);\n    }\n  };\n})();";
	}

	private static string Styles()
	{
		return ":root {\n  --combo-page-bg: #07131c;\n  --combo-surface: #0c1924;\n  --combo-surface-strong: #102231;\n  --combo-line: rgba(111, 154, 186, 0.14);\n  --combo-text: #ffffff;\n  --combo-text-soft: #c7d5e0;\n  --combo-muted: #8fa9ba;\n  --combo-accent: #66c0f4;\n  --combo-green: #75a920;\n}\nbody.combo-detail-body {\n  margin: 0;\n  padding: 0;\n  background: linear-gradient(180deg, #061018 0%, #06121b 52%, #07131c 100%);\n  color: #d8e5ef;\n  font-family: \"Motiva Sans\", \"Segoe UI Variable Text\", \"Segoe UI\", Arial, sans-serif;\n}\n.combo-shell {\n  min-height: 100vh;\n  padding-bottom: 0;\n  background: linear-gradient(180deg, #061018 0%, #06121b 52%, #07131c 100%);\n}\n.combo-page-main {\n  min-height: calc(100vh - 108px);\n  padding: 14px 18px 46px;\n}\n.combo-container,\n.combo-inline-nav {\n  width: min(1190px, 100%);\n  margin-inline: auto;\n}\n.combo-inline-nav {\n  display: flex;\n  align-items: center;\n  gap: 8px;\n  min-height: 30px;\n  margin-bottom: 12px;\n  color: #9fb6c9;\n  font-size: 0.8rem;\n}\n.combo-inline-back {\n  padding: 0;\n  border: 0;\n  background: transparent;\n  color: #70b8e8;\n  font-weight: 800;\n}\n.combo-inline-back:hover,\n.combo-inline-back:focus-visible {\n  color: #ffffff;\n  text-decoration: underline;\n}\n.combo-inline-back:focus-visible,\n.combo-gallery button:focus-visible,\n.combo-purchase-button:focus-visible {\n  outline: 2px solid #8dd5ff;\n  outline-offset: 2px;\n}\n.combo-inline-current {\n  min-width: 0;\n  overflow: hidden;\n  color: rgba(255, 255, 255, 0.82);\n  font-size: 0.78rem;\n  text-overflow: ellipsis;\n  text-transform: uppercase;\n  white-space: nowrap;\n}\n.combo-detail-section { padding: 0 0 34px; }\n.combo-detail-shell {\n  display: grid;\n  grid-template-columns: minmax(0, 745px) minmax(310px, 380px);\n  gap: 20px;\n  align-items: start;\n}\n.combo-detail-main-column,\n.combo-detail-sidebar {\n  display: flex;\n  flex-direction: column;\n  gap: 16px;\n  min-width: 0;\n}\n.combo-gallery {\n  overflow: hidden;\n  border: 1px solid rgba(111, 154, 186, 0.12);\n  border-radius: 3px;\n  background: #08111a;\n  box-shadow: 0 18px 34px rgba(0, 0, 0, 0.34);\n}\n.combo-gallery-main {\n  position: relative;\n  overflow: hidden;\n  aspect-ratio: 16 / 9;\n  background: #000000;\n}\n.combo-gallery-main::after {\n  content: \"\";\n  position: absolute;\n  inset: auto 0 0;\n  height: 72px;\n  pointer-events: none;\n  background: linear-gradient(180deg, rgba(0, 0, 0, 0), rgba(0, 0, 0, 0.6));\n}\n.combo-gallery-main-image {\n  width: 100%;\n  height: 100%;\n  object-fit: cover;\n  opacity: 1;\n  transform: scale(1);\n  filter: brightness(1) saturate(1);\n  transition: opacity 0.3s ease, transform 0.42s ease, filter 0.42s ease;\n}\n.combo-gallery-main-image.is-transitioning {\n  opacity: 0.34;\n  transform: scale(1.018);\n  filter: brightness(0.9) saturate(0.95);\n}\n.combo-gallery-control,\n.combo-gallery-thumbs-nav {\n  position: absolute;\n  z-index: 2;\n  display: grid;\n  place-items: center;\n  border: 1px solid rgba(153, 210, 244, 0.16);\n  background: linear-gradient(180deg, rgba(43, 76, 100, 0.92), rgba(24, 47, 65, 0.92));\n  color: #d7f2ff;\n  box-shadow: 0 12px 24px rgba(0, 0, 0, 0.34), inset 0 1px 0 rgba(255, 255, 255, 0.08);\n  font-size: 1.75rem;\n  line-height: 1;\n}\n.combo-gallery-control {\n  top: 50%;\n  width: 48px;\n  height: 48px;\n  border-radius: 4px;\n  transform: translateY(-50%);\n}\n.combo-gallery-control--prev { left: 10px; }\n.combo-gallery-control--next { right: 10px; }\n.combo-gallery-control:hover,\n.combo-gallery-control:focus-visible,\n.combo-gallery-thumbs-nav:hover,\n.combo-gallery-thumbs-nav:focus-visible {\n  border-color: rgba(180, 226, 255, 0.32);\n  background: linear-gradient(180deg, rgba(68, 116, 148, 0.96), rgba(34, 72, 98, 0.96));\n  color: #ffffff;\n}\n.combo-gallery-thumbs-shell {\n  position: relative;\n  margin: 0;\n  padding: 12px 56px;\n  background: #1b2c3b;\n}\n.combo-gallery-thumbs-viewport {\n  overflow-x: auto;\n  scrollbar-width: none;\n  -ms-overflow-style: none;\n}\n.combo-gallery-thumbs-viewport::-webkit-scrollbar { display: none; }\n.combo-gallery-thumbs {\n  display: flex;\n  gap: 8px;\n  min-width: 0;\n}\n.combo-gallery-thumb {\n  position: relative;\n  flex: 0 0 72px;\n  overflow: hidden;\n  padding: 0;\n  border: 0;\n  border-radius: 2px;\n  aspect-ratio: 16 / 9;\n  background: #3f6f90;\n  box-shadow: inset 0 -2px 0 rgba(102, 192, 244, 0.68);\n}\n.combo-gallery-thumb::after {\n  content: \"\";\n  position: absolute;\n  inset: 0;\n  background: rgba(21, 44, 61, 0.18);\n}\n.combo-gallery-thumb img {\n  width: 100%;\n  height: 100%;\n  object-fit: cover;\n  opacity: 0.42;\n  transition: opacity 0.2s ease;\n}\n.combo-gallery-thumb:hover,\n.combo-gallery-thumb:focus-visible,\n.combo-gallery-thumb.is-active {\n  box-shadow: inset 0 -3px 0 #66c0f4, 0 0 0 1px rgba(102, 192, 244, 0.2);\n}\n.combo-gallery-thumb.is-active img,\n.combo-gallery-thumb:hover img,\n.combo-gallery-thumb:focus-visible img { opacity: 1; }\n.combo-gallery-thumbs-nav {\n  top: 50%;\n  width: 38px;\n  height: 42px;\n  border-radius: 4px;\n  transform: translateY(-50%);\n  font-size: 1.35rem;\n}\n.combo-gallery-thumbs-nav--prev { left: 12px; }\n.combo-gallery-thumbs-nav--next { right: 12px; }\n.combo-gallery-follow {\n  min-height: 34px;\n  padding: 0 16px;\n  background: #263442;\n  color: #9db6ce;\n  display: flex;\n  align-items: center;\n  font-size: 0.78rem;\n  font-weight: 700;\n}\n.combo-purchase-bar {\n  display: grid;\n  grid-template-columns: minmax(0, 1fr) auto;\n  gap: 16px;\n  align-items: center;\n  min-height: 92px;\n  padding: 14px 18px;\n  border: 1px solid rgba(86, 128, 160, 0.26);\n  border-radius: 4px;\n  background: linear-gradient(90deg, rgba(34, 58, 77, 0.98), rgba(42, 67, 88, 0.92) 62%, rgba(31, 52, 69, 0.98));\n  box-shadow: 0 16px 28px rgba(0, 0, 0, 0.22);\n}\n.combo-purchase-main { min-width: 0; }\n.combo-purchase-head {\n  display: flex;\n  align-items: center;\n  justify-content: space-between;\n  gap: 12px;\n  margin-bottom: 10px;\n}\n.combo-purchase-head h2 {\n  margin: 0;\n  color: #ffffff;\n  font-size: 0.98rem;\n  font-weight: 950;\n  line-height: 1.25;\n}\n.combo-purchase-platform { color: #d9e7ef; font-size: 1rem; }\n.combo-purchase-editions { display: flex; flex-wrap: wrap; gap: 7px; }\n.combo-edition-chip {\n  min-height: 25px;\n  padding: 0 10px;\n  border: 1px solid rgba(102, 192, 244, 0.55);\n  border-radius: 2px;\n  background: #284860;\n  color: #ffffff;\n  display: inline-flex;\n  align-items: center;\n  gap: 7px;\n  font-size: 0.68rem;\n  font-weight: 850;\n}\n.combo-edition-chip strong { color: #ffffff; font-weight: 900; white-space: nowrap; }\n.combo-purchase-note {\n  display: flex;\n  gap: 7px;\n  margin-top: 8px;\n}\n.combo-purchase-note-icon,\n.combo-empty-note-icon {\n  flex: 0 0 auto;\n  width: 17px;\n  height: 17px;\n  border-radius: 50%;\n  background: rgba(102, 192, 244, 0.18);\n  color: #8fcaf0;\n  display: grid;\n  place-items: center;\n  font-size: 0.7rem;\n  font-weight: 900;\n}\n.combo-purchase-note p {\n  margin: 0;\n  color: #8fa9ba;\n  font-size: 0.72rem;\n  font-weight: 650;\n  line-height: 1.45;\n}\n.combo-purchase-actions {\n  display: flex;\n  align-items: center;\n  justify-content: flex-end;\n  gap: 8px;\n  min-width: 0;\n}\n.combo-current-price {\n  min-height: 31px;\n  padding: 0 13px;\n  border-radius: 2px;\n  background: var(--combo-green);\n  color: #ffffff;\n  display: inline-flex;\n  align-items: center;\n  font-size: 0.82rem;\n  font-weight: 900;\n  white-space: nowrap;\n}\n.combo-purchase-button {\n  width: auto;\n  min-width: 94px;\n  min-height: 34px;\n  padding: 0 13px;\n  border: 0;\n  border-radius: 2px;\n  color: #ffffff;\n  font-size: 0.78rem;\n  font-weight: 900;\n  white-space: nowrap;\n}\n.combo-purchase-button.is-primary { background: linear-gradient(180deg, #9bd33d 0%, #75a920 100%); }\n.combo-purchase-button.is-secondary { background: linear-gradient(180deg, #567b9b, #3c607d); }\n.combo-purchase-button:hover { filter: brightness(1.1); }\n.combo-summary {\n  display: flex;\n  flex-direction: column;\n  gap: 13px;\n  padding: 18px;\n  border: 1px solid rgba(111, 154, 186, 0.12);\n  border-radius: 3px;\n  background: linear-gradient(180deg, #102231 0%, #0b1a26 100%);\n  box-shadow: 0 18px 34px rgba(0, 0, 0, 0.28);\n}\n.combo-summary-title {\n  margin: 0;\n  color: #ffffff;\n  font-size: clamp(1.02rem, 1.45vw, 1.35rem);\n  font-weight: 900;\n  line-height: 1.16;\n  text-transform: uppercase;\n  overflow-wrap: anywhere;\n}\n.combo-summary-cover {\n  overflow: hidden;\n  aspect-ratio: 1.92 / 1;\n  border-radius: 2px;\n  background: #071018;\n}\n.combo-summary-cover img,\n.combo-summary-cover .combo-image-fallback {\n  width: 100%;\n  height: 100%;\n  object-fit: cover;\n}\n.combo-summary-cover--poster img { object-fit: contain; }\n.combo-summary-copy {\n  color: #c6d4df;\n  font-size: 0.8rem;\n  line-height: 1.45;\n}\n.combo-summary-copy p { margin: 0; }\n.combo-summary-facts { display: grid; gap: 6px; }\n.combo-summary-fact {\n  display: grid;\n  grid-template-columns: 116px minmax(0, 1fr);\n  gap: 8px;\n}\n.combo-summary-fact-label { color: #708596; font-size: 0.74rem; font-weight: 700; }\n.combo-summary-fact-value { color: #c7d5e0; font-size: 0.75rem; line-height: 1.42; font-weight: 800; }\n.combo-status-card {\n  display: grid;\n  grid-template-columns: 76px minmax(0, 1fr);\n  gap: 14px;\n  align-items: center;\n  padding-top: 4px;\n}\n.combo-count-badge {\n  width: 74px;\n  aspect-ratio: 1;\n  border: 4px solid rgba(195, 255, 217, 0.62);\n  border-radius: 50%;\n  background: linear-gradient(180deg, #70d889, #47b56d);\n  color: #ffffff;\n  box-shadow: inset 0 0 0 3px rgba(255, 255, 255, 0.25), 0 8px 18px rgba(59, 176, 106, 0.25);\n  display: grid;\n  place-content: center;\n  text-align: center;\n}\n.combo-count-badge strong { font-size: 1.35rem; font-weight: 950; line-height: 1; }\n.combo-count-badge span { margin-top: 4px; font-size: 0.55rem; font-weight: 900; }\n.combo-status-copy { display: grid; gap: 4px; }\n.combo-status-copy strong { color: #ffffff; font-size: 0.78rem; font-weight: 850; }\n.combo-status-copy span { color: #90a7b7; font-size: 0.72rem; line-height: 1.45; }\n.combo-summary-block { display: grid; gap: 8px; }\n.combo-summary-label { display: none; }\n.combo-summary-tags { display: flex; flex-wrap: wrap; gap: 6px; }\n.combo-summary-tag {\n  min-height: 24px;\n  padding: 0 9px;\n  border-radius: 2px;\n  background: #1d3345;\n  color: #c7d5e0;\n  display: inline-flex;\n  align-items: center;\n  font-size: 0.7rem;\n  font-weight: 800;\n}\n.combo-section-card {\n  padding: 18px;\n  border: 1px solid rgba(111, 154, 186, 0.12);\n  border-radius: 3px;\n  background: rgba(12, 25, 36, 0.78);\n}\n.combo-section-header {\n  display: flex;\n  align-items: end;\n  justify-content: space-between;\n  gap: 16px;\n  margin-bottom: 16px;\n}\n.combo-section-header small {\n  color: var(--combo-accent);\n  font-size: 0.68rem;\n  font-weight: 900;\n  letter-spacing: 0.08em;\n}\n.combo-section-header h2,\n.combo-section-card > h2,\n.combo-content-card h2 {\n  margin: 5px 0 0;\n  color: #ffffff;\n  font-size: 1rem;\n  font-weight: 950;\n  text-transform: uppercase;\n}\n.combo-section-header > strong { color: #8fa9ba; font-size: 0.78rem; white-space: nowrap; }\n.combo-component-grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 10px; }\n.combo-component {\n  display: grid;\n  grid-template-columns: 72px minmax(0, 1fr);\n  gap: 12px;\n  align-items: center;\n  min-width: 0;\n  padding: 9px;\n  border: 1px solid rgba(111, 154, 186, 0.12);\n  border-radius: 3px;\n  background: #10202d;\n}\n.combo-component-image {\n  overflow: hidden;\n  width: 72px;\n  height: 72px;\n  border-radius: 2px;\n  background: #071018;\n}\n.combo-component-image img,\n.combo-component-image .combo-image-fallback { width: 100%; height: 100%; object-fit: cover; }\n.combo-component-copy { min-width: 0; }\n.combo-component-copy h3 {\n  margin: 0 0 5px;\n  overflow: hidden;\n  color: #ffffff;\n  font-size: 0.84rem;\n  font-weight: 850;\n  line-height: 1.3;\n  text-overflow: ellipsis;\n  white-space: nowrap;\n}\n.combo-component-copy span { color: #8fa9ba; font-size: 0.72rem; line-height: 1.4; }\n.combo-content-card {\n  padding: 18px 18px 0;\n  color: #c7d5e0;\n  font-size: 0.82rem;\n  line-height: 1.58;\n}\n.combo-content-card h2 { margin: 0 0 14px; }\n.combo-content-card p { margin: 0 0 16px; overflow-wrap: anywhere; }\n.combo-rich-text-content { white-space: pre-line; }\n.combo-image-card img,\n.combo-image-text-card > img {\n  width: min(720px, 100%);\n  height: auto;\n  max-height: 560px;\n  margin: 16px 0;\n  object-fit: cover;\n}\n.combo-image-text-card {\n  display: grid;\n  grid-template-columns: minmax(0, 1fr) minmax(0, 1fr);\n  gap: 24px;\n  align-items: center;\n}\n.combo-divider { width: 100%; margin: 8px 0; border: 0; border-top: 1px solid rgba(111, 154, 186, 0.18); }\n.combo-spacer { height: 30px; }\n.combo-empty-note {\n  display: flex;\n  gap: 10px;\n  align-items: flex-start;\n  margin-top: 14px;\n  padding: 14px;\n  border: 1px solid rgba(102, 192, 244, 0.14);\n  border-radius: 3px;\n  background: rgba(39, 75, 99, 0.2);\n  color: #a9bdca;\n  font-size: 0.78rem;\n  line-height: 1.5;\n}\n.combo-image-fallback {\n  min-height: 72px;\n  padding: 12px;\n  background: #071018;\n  color: #8fa9ba;\n  display: grid;\n  place-items: center;\n  text-align: center;\n}\n.combo-toast {\n  position: fixed;\n  right: 25px;\n  bottom: 25px;\n  z-index: 520;\n  max-width: min(420px, calc(100vw - 32px));\n  padding: 12px 16px;\n  border: 1px solid #22c55e;\n  border-radius: 4px;\n  background: #0f172a;\n  color: #ffffff;\n  box-shadow: 0 20px 40px rgba(0, 0, 0, 0.55);\n}\n.combo-toast[data-tone=\"error\"] { border-color: #ef4444; }\n@media (prefers-reduced-motion: reduce) {\n  .combo-gallery-main-image,\n  .combo-gallery-thumb img { transition: none; }\n  .combo-gallery-thumbs-viewport { scroll-behavior: auto; }\n}\n@media (max-width: 1180px) {\n  .combo-detail-shell { grid-template-columns: minmax(0, 1fr) minmax(300px, 360px); gap: 18px; }\n  .combo-purchase-bar { grid-template-columns: 1fr; }\n  .combo-purchase-actions { justify-content: flex-start; flex-wrap: wrap; }\n}\n@media (max-width: 900px) {\n  .combo-page-main { padding: 12px 12px 36px; }\n  .combo-detail-shell { grid-template-columns: 1fr; }\n  .combo-purchase-actions { align-items: stretch; }\n  .combo-purchase-button { flex: 1 1 140px; }\n}\n@media (max-width: 720px) {\n  .combo-component-grid,\n  .combo-image-text-card { grid-template-columns: 1fr; }\n}\n@media (max-width: 620px) {\n  .combo-purchase-actions { width: 100%; }\n  .combo-current-price { flex: 1 1 100%; justify-content: center; }\n  .combo-summary-fact { grid-template-columns: 96px minmax(0, 1fr); }\n  .combo-gallery-thumbs-shell { padding: 10px 42px; }\n  .combo-gallery-thumb { flex-basis: 60px; }\n  .combo-section-header { align-items: flex-start; }\n  .combo-toast { right: 16px; bottom: 16px; left: 16px; max-width: none; }\n}";
	}
}
