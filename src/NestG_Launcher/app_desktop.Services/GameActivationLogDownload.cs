using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace app_desktop.Services;

internal sealed class GameActivationLogDownload
{
	internal const string MessagePrefix = "activation-log:download:";

	private GameActivationLog? current;

	private int exporting;

	internal GameActivationLog? CurrentLog => current;

	internal string? DownloadId
	{
		get
		{
			GameActivationLog? gameActivationLog = current;
			if (gameActivationLog == null || !gameActivationLog.IsFailed)
			{
				return null;
			}
			return current.Id;
		}
	}

	internal void SetCurrent(GameActivationLog log)
	{
		current = log;
	}

	internal async Task<ActivationLogDownloadOutcome> DownloadAsync(string id, Guid libraryId, Func<string?> chooseFolder, CancellationToken token = default(CancellationToken))
	{
		GameActivationLog log = current;
		if (!Guid.TryParseExact(id, "N", out var _) || log?.Id != id || log.LibraryId != libraryId || !log.IsFailed)
		{
			return ActivationLogDownloadOutcome.Unavailable;
		}
		if (Interlocked.CompareExchange(ref exporting, 1, 0) != 0)
		{
			return ActivationLogDownloadOutcome.Busy;
		}
		try
		{
			token.ThrowIfCancellationRequested();
			string folder = chooseFolder();
			token.ThrowIfCancellationRequested();
			if (string.IsNullOrWhiteSpace(folder))
			{
				return ActivationLogDownloadOutcome.Cancelled;
			}
			if (current != log)
			{
				return ActivationLogDownloadOutcome.Unavailable;
			}
			string snapshot = log.Snapshot();
			await Task.Run(delegate
			{
				Export(folder, log.FileName, snapshot, token);
			}, token);
			return ActivationLogDownloadOutcome.Exported;
		}
		catch (OperationCanceledException) when (token.IsCancellationRequested)
		{
			throw;
		}
		catch (Exception ex2)
		{
			DesktopDiagnosticLog.Write("ActivationLogDownload", "Failed", "ErrorType=" + ex2.GetType().Name);
			return ActivationLogDownloadOutcome.Failed;
		}
		finally
		{
			Volatile.Write(ref exporting, 0);
		}
	}

	private static void Export(string folder, string fileName, string snapshot, CancellationToken token)
	{
		string fullPath = Path.GetFullPath(folder);
		if (!Directory.Exists(fullPath))
		{
			throw new DirectoryNotFoundException();
		}
		string text = Path.Combine(fullPath, fileName);
		if (File.Exists(text) || Directory.Exists(text))
		{
			text = Path.Combine(fullPath, Path.GetFileNameWithoutExtension(fileName) + "-" + Guid.NewGuid().ToString("N") + ".log");
		}
		string text2 = Path.Combine(fullPath, ".NestG-log-" + Guid.NewGuid().ToString("N") + ".tmp");
		try
		{
			token.ThrowIfCancellationRequested();
			using (FileStream fileStream = new FileStream(text2, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.WriteThrough))
			{
				using StreamWriter streamWriter = new StreamWriter(fileStream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), -1, leaveOpen: true);
				streamWriter.Write(snapshot);
				streamWriter.Flush();
				fileStream.Flush(flushToDisk: true);
			}
			token.ThrowIfCancellationRequested();
			File.Move(text2, text, overwrite: false);
		}
		finally
		{
			try
			{
				File.Delete(text2);
			}
			catch
			{
			}
		}
	}
}
