using System;
using System.IO;
using System.Text;

namespace app_desktop.Services;

internal static class DesktopDiagnosticLog
{
	private static readonly object SyncRoot = new object();

	internal static string CurrentLogPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NestGLauncher", "Logs", $"launcher-{DateTime.UtcNow:yyyyMMdd}.log");

	internal static void Write(string component, string eventName, string? details = null, Exception? exception = null)
	{
		try
		{
			string currentLogPath = CurrentLogPath;
			string directoryName = Path.GetDirectoryName(currentLogPath);
			string contents = BuildLine(component, eventName, details, exception);
			lock (SyncRoot)
			{
				Directory.CreateDirectory(directoryName);
				File.AppendAllText(currentLogPath, contents, Encoding.UTF8);
			}
		}
		catch
		{
		}
	}

	private static string BuildLine(string component, string eventName, string? details, Exception? exception)
	{
		string value = Normalize(component);
		string value2 = Normalize(eventName);
		string text = Normalize(details);
		string text2 = ((exception == null) ? string.Empty : (" Exception=" + Normalize(exception.GetType().Name) + ": " + Normalize(exception.Message)));
		return $"[{DateTimeOffset.Now:O}] [{value}] {value2}" + (string.IsNullOrEmpty(text) ? string.Empty : (" " + text)) + text2 + Environment.NewLine;
	}

	private static string Normalize(string? value)
	{
		if (!string.IsNullOrWhiteSpace(value))
		{
			return value.Replace('\r', ' ').Replace('\n', ' ').Trim();
		}
		return string.Empty;
	}
}
