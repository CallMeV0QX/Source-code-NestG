using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace NestG.Updater;

internal static class Program
{
	[STAThread]
	private static int Main(string[] args)
	{
		ApplicationConfiguration.Initialize();
		try
		{
			new UpdaterService(UpdaterOptions.Parse(args)).Apply();
			return 0;
		}
		catch (Exception ex)
		{
			TryWriteFatalLog(args, ex);
			MessageBox.Show("Không thể cập nhật NestG Launcher.\n\n" + ex.Message + "\n\nVui lòng chạy NestG Setup thủ công nếu launcher không mở lại.", "Cập nhật NestG Launcher", MessageBoxButtons.OK, MessageBoxIcon.Hand);
			return 1;
		}
	}

	private static void TryWriteFatalLog(IReadOnlyList<string> args, Exception exception)
	{
		try
		{
			string text = FindLogPath(args);
			if (!string.IsNullOrWhiteSpace(text))
			{
				string directoryName = Path.GetDirectoryName(text);
				if (!string.IsNullOrWhiteSpace(directoryName))
				{
					Directory.CreateDirectory(directoryName);
				}
				StringBuilder stringBuilder = new StringBuilder().Append('[').Append(DateTimeOffset.Now.ToString("O")).AppendLine("] Fatal updater error")
					.AppendLine(exception.ToString());
				File.AppendAllText(text, stringBuilder.ToString());
			}
		}
		catch
		{
		}
	}

	private static string? FindLogPath(IReadOnlyList<string> args)
	{
		for (int i = 0; i < args.Count - 1; i++)
		{
			if (string.Equals(args[i], "--log", StringComparison.OrdinalIgnoreCase))
			{
				return args[i + 1];
			}
		}
		return null;
	}
}
