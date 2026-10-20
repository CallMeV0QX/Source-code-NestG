using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;

namespace NestG.Updater;

internal sealed class UpdaterService(UpdaterOptions options)
{
	private sealed record BackupEntry(string BackupPath, string DestinationPath);

	private const string LauncherExecutableName = "NestG launcher.exe";

	private readonly List<string> createdDestinations = new List<string>();

	private readonly List<BackupEntry> backupEntries = new List<BackupEntry>();

	public void Apply()
	{
		Directory.CreateDirectory(Path.GetDirectoryName(options.LogPath));
		Log("NestG updater started.");
		Log($"Launcher PID: {options.LauncherProcessId}");
		Log("Stage: " + options.StageDirectory);
		Log("Target: " + options.TargetDirectory);
		Log("Restart: " + options.RestartPath);
		ValidatePaths();
		WaitForLauncherExit();
		string[] array = Directory.EnumerateFiles(options.StageDirectory, "*", SearchOption.AllDirectories).ToArray();
		if (array.Length == 0)
		{
			throw new InvalidOperationException("Thư mục _stage không có file để cập nhật.");
		}
		string text = BuildBackupDirectory();
		Directory.CreateDirectory(text);
		Log("Backup: " + text);
		try
		{
			string[] array2 = array;
			foreach (string text2 in array2)
			{
				string relativePath = Path.GetRelativePath(options.StageDirectory, text2);
				string fullPath = Path.GetFullPath(Path.Combine(options.TargetDirectory, relativePath));
				EnsurePathInsideTarget(fullPath);
				BackupDestinationIfNeeded(fullPath, text, relativePath);
				CopyWithRetry(text2, fullPath);
				Log("Copied: " + relativePath);
			}
			Log("Copy completed.");
			RestartLauncher();
			TryDeleteDirectory(text);
			Log("NestG updater completed.");
		}
		catch (Exception value)
		{
			Log($"Copy failed: {value}");
			Rollback();
			throw;
		}
	}

	private void ValidatePaths()
	{
		string text = Path.Combine(options.StageDirectory, "NestG launcher.exe");
		if (!File.Exists(text))
		{
			throw new FileNotFoundException("Package mới không có NestG launcher.exe ở root _stage.", text);
		}
		if (!Directory.Exists(options.TargetDirectory))
		{
			throw new DirectoryNotFoundException("Không tìm thấy thư mục cài đặt: " + options.TargetDirectory);
		}
	}

	private void WaitForLauncherExit()
	{
		try
		{
			Process processById = Process.GetProcessById(options.LauncherProcessId);
			if (processById.HasExited)
			{
				Log("Launcher process already exited.");
				return;
			}
			Log("Waiting for launcher process to exit.");
			if (!processById.WaitForExit(90000))
			{
				throw new TimeoutException("Launcher chưa thoát sau 90 giây, không thể copy đè file đang chạy.");
			}
			Log("Launcher process exited.");
		}
		catch (ArgumentException)
		{
			Log("Launcher process was not found; continuing update.");
		}
	}

	private void BackupDestinationIfNeeded(string destinationPath, string backupDirectory, string relativePath)
	{
		if (!File.Exists(destinationPath))
		{
			createdDestinations.Add(destinationPath);
			return;
		}
		string text = Path.Combine(backupDirectory, relativePath);
		string directoryName = Path.GetDirectoryName(text);
		if (!string.IsNullOrWhiteSpace(directoryName))
		{
			Directory.CreateDirectory(directoryName);
		}
		File.Copy(destinationPath, text, overwrite: true);
		backupEntries.Add(new BackupEntry(text, destinationPath));
	}

	private void CopyWithRetry(string sourcePath, string destinationPath)
	{
		string directoryName = Path.GetDirectoryName(destinationPath);
		if (!string.IsNullOrWhiteSpace(directoryName))
		{
			Directory.CreateDirectory(directoryName);
		}
		for (int i = 1; i <= 8; i++)
		{
			try
			{
				if (File.Exists(destinationPath))
				{
					File.SetAttributes(destinationPath, FileAttributes.Normal);
				}
				File.Copy(sourcePath, destinationPath, overwrite: true);
				break;
			}
			catch (Exception ex) when (i < 8 && (ex is IOException || ex is UnauthorizedAccessException))
			{
				Log($"Copy retry {i}: {destinationPath} - {ex.Message}");
				Thread.Sleep(500);
			}
		}
	}

	private void Rollback()
	{
		Log("Starting rollback.");
		foreach (string item in createdDestinations.AsEnumerable().Reverse())
		{
			TryDeleteFile(item);
		}
		foreach (BackupEntry item2 in backupEntries.AsEnumerable().Reverse())
		{
			try
			{
				string directoryName = Path.GetDirectoryName(item2.DestinationPath);
				if (!string.IsNullOrWhiteSpace(directoryName))
				{
					Directory.CreateDirectory(directoryName);
				}
				File.Copy(item2.BackupPath, item2.DestinationPath, overwrite: true);
				Log("Rolled back: " + item2.DestinationPath);
			}
			catch (Exception value)
			{
				Log($"Rollback failed for {item2.DestinationPath}: {value}");
			}
		}
	}

	private void RestartLauncher()
	{
		string text = (File.Exists(options.RestartPath) ? options.RestartPath : Path.Combine(options.TargetDirectory, "NestG launcher.exe"));
		if (!File.Exists(text))
		{
			throw new FileNotFoundException("Không tìm thấy launcher để mở lại.", text);
		}
		Process.Start(new ProcessStartInfo
		{
			FileName = text,
			WorkingDirectory = (Path.GetDirectoryName(text) ?? options.TargetDirectory),
			UseShellExecute = true
		});
		Log("Restarted launcher.");
	}

	private string BuildBackupDirectory()
	{
		return Path.Combine(Directory.GetParent(options.StageDirectory)?.FullName ?? options.StageDirectory, "_backup", DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture));
	}

	private void EnsurePathInsideTarget(string path)
	{
		string value = Path.GetFullPath(options.TargetDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
		string fullPath = Path.GetFullPath(path);
		if (!fullPath.StartsWith(value, StringComparison.OrdinalIgnoreCase))
		{
			throw new InvalidOperationException("Đường dẫn copy vượt ngoài thư mục cài đặt: " + fullPath);
		}
	}

	private void TryDeleteFile(string path)
	{
		try
		{
			if (File.Exists(path))
			{
				File.SetAttributes(path, FileAttributes.Normal);
				File.Delete(path);
				Log("Deleted created file during rollback: " + path);
			}
		}
		catch (Exception value)
		{
			Log($"Cannot delete created file during rollback {path}: {value}");
		}
	}

	private void TryDeleteDirectory(string path)
	{
		try
		{
			if (Directory.Exists(path))
			{
				Directory.Delete(path, recursive: true);
			}
		}
		catch (Exception ex)
		{
			Log("Cannot delete temp directory " + path + ": " + ex.Message);
		}
	}

	private void Log(string message)
	{
		try
		{
			File.AppendAllText(options.LogPath, $"[{DateTimeOffset.Now:O}] {message}{Environment.NewLine}");
		}
		catch
		{
		}
	}
}
