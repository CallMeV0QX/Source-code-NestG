using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace app_desktop.Services;

internal static class DesktopUserMessage
{
	public static string FromException(Exception exception, string fallback)
	{
		ArgumentNullException.ThrowIfNull(exception, "exception");
		if (exception is TimeoutException || exception is TaskCanceledException)
		{
			return "Máy chủ phản hồi quá chậm. Vui lòng thử lại sau ít phút.";
		}
		if (exception is HttpRequestException)
		{
			return "Không thể kết nối máy chủ. Hãy kiểm tra mạng rồi thử lại.";
		}
		string text = exception.Message?.Trim();
		if (string.IsNullOrWhiteSpace(text))
		{
			return fallback;
		}
		if (text.Contains("401", StringComparison.OrdinalIgnoreCase) || text.Contains("unauthorized", StringComparison.OrdinalIgnoreCase) || text.Contains(HttpStatusCode.Unauthorized.ToString(), StringComparison.OrdinalIgnoreCase))
		{
			return "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.";
		}
		if (text.Length > 240 || text.Contains("System.", StringComparison.Ordinal) || text.Contains("http://", StringComparison.OrdinalIgnoreCase) || text.Contains("https://", StringComparison.OrdinalIgnoreCase))
		{
			return fallback;
		}
		return text;
	}
}
