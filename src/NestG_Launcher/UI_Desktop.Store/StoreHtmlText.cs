using System.Net;
using System.Text.RegularExpressions;

namespace UI_Desktop.Store;

internal static class StoreHtmlText
{
	public static string? ToPlainText(string? html)
	{
		if (string.IsNullOrWhiteSpace(html))
		{
			return null;
		}
		string text = Regex.Replace(Regex.Replace(Regex.Replace(WebUtility.HtmlDecode(Regex.Replace(Regex.Replace(Regex.Replace(Regex.Replace(Regex.Replace(Regex.Replace(Regex.Replace(html.Replace("\r", string.Empty), "<br\\s*/?>", "\n", RegexOptions.IgnoreCase), "</(p|div|section|article|h1|h2|h3|h4|h5|h6|ul|ol|table|tr)>", "\n\n", RegexOptions.IgnoreCase), "<li[^>]*>", "• ", RegexOptions.IgnoreCase), "</li>", "\n", RegexOptions.IgnoreCase).Replace("â€¢", "•"), "<hr[^>]*>", "\n----------------\n", RegexOptions.IgnoreCase), "</(td|th)>", " | ", RegexOptions.IgnoreCase), "<.*?>", string.Empty, RegexOptions.Singleline)).Replace('\u00a0', ' '), "[\\t\\f\\v ]+", " "), " *\\n *", "\n"), "\\n{3,}", "\n\n").Trim();
		if (!string.IsNullOrWhiteSpace(text))
		{
			return text;
		}
		return null;
	}
}
