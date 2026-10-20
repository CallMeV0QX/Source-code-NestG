using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace app_desktop.Services.Fix54;

internal sealed class Fix54GameProcessGuard : IFix54GameProcessGuard
{
	public void EnsureClosed(string gameRoot, IReadOnlyList<string> executablePaths)
	{
		foreach (string item in executablePaths.Select(Path.GetFileNameWithoutExtension).Distinct<string>(StringComparer.OrdinalIgnoreCase))
		{
			Process[] processesByName = Process.GetProcessesByName(item);
			foreach (Process process in processesByName)
			{
				using (process)
				{
					try
					{
						if (!process.HasExited)
						{
							string text = process.MainModule?.FileName;
							if (text == null || Fix54Paths.IsInside(text, gameRoot))
							{
								throw new Fix54Exception("Hãy đóng game trước khi chạy FIX54. Launcher không tự tắt game.");
							}
						}
					}
					catch (Fix54Exception)
					{
						throw;
					}
					catch (Exception ex2) when (((ex2 is Win32Exception || ex2 is InvalidOperationException || ex2 is NotSupportedException) ? 1 : 0) != 0)
					{
						throw new Fix54Exception("Chưa xác minh được game đã đóng. Hãy đóng game rồi thử FIX54 lại.");
					}
				}
			}
		}
	}
}
