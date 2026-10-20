using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Security;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace app_desktop.Services.WindowsUpdate;

internal sealed class WubCliRunner(WubPackageStore package) : IWubCliRunner
{
	private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(45.0);

	public bool IsReady => package.IsReady;

	public async Task<WubCommandResult> SetBlockedAsync(bool blocked, CancellationToken cancellationToken)
	{
		if (!IsReady)
		{
			return new WubCommandResult(WubCommandOutcome.Missing, null);
		}
		string session = Path.Combine(Path.GetTempPath(), "NestG-Wub-" + Guid.NewGuid().ToString("N"));
		Process process = null;
		try
		{
			Directory.CreateDirectory(session);
			string text = Path.Combine(session, "Wub_x64.exe");
			string text2 = Path.Combine(session, "Wub.ini");
			File.Copy(package.ExecutablePath, text);
			File.Copy(package.IniPath, text2);
			using FileStream executableLock = new FileStream(text, FileMode.Open, FileAccess.Read, FileShare.Read);
			using FileStream configurationLock = new FileStream(text2, FileMode.Open, FileAccess.Read, FileShare.Read);
			if (!HasPinnedContent(executableLock, 961600L, "187EEEE9E518011DE1B87CFB0ED03E12EA551E9011F0C8DEFDD0E4535E672DA2") || !HasPinnedContent(configurationLock, 106548L, "067E8D757952C5E717A3E8CEF4D339DF8806802D85929B4B23EB2D624A70A48E"))
			{
				return new WubCommandResult(WubCommandOutcome.Missing, null);
			}
			ProcessStartInfo start = CreateStartInfo(text, session, blocked);
			try
			{
				process = await Task.Run(() => Process.Start(start), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			}
			catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
			{
				return new WubCommandResult(WubCommandOutcome.Cancelled, null);
			}
			if (process == null)
			{
				return new WubCommandResult(WubCommandOutcome.Failed, null);
			}
			using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
			deadline.CancelAfter(CommandTimeout);
			try
			{
				await process.WaitForExitAsync(deadline.Token).ConfigureAwait(continueOnCapturedContext: false);
			}
			catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
			{
				TryStop(process);
				return new WubCommandResult(WubCommandOutcome.TimedOut, null);
			}
			int exitCode = process.ExitCode;
			return new WubCommandResult((exitCode != 0) ? WubCommandOutcome.Failed : WubCommandOutcome.Completed, exitCode);
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			if (process != null)
			{
				TryStop(process);
			}
			throw;
		}
		catch (Win32Exception)
		{
			return new WubCommandResult(WubCommandOutcome.Failed, null);
		}
		catch (IOException)
		{
			return new WubCommandResult(WubCommandOutcome.Failed, null);
		}
		catch (UnauthorizedAccessException)
		{
			return new WubCommandResult(WubCommandOutcome.Failed, null);
		}
		finally
		{
			bool num = process?.HasExited ?? true;
			process?.Dispose();
			if (num)
			{
				try
				{
					string fullPath = Path.GetFullPath(session);
					string value = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
					if ((Path.GetDirectoryName(fullPath)?.Equals(value, StringComparison.OrdinalIgnoreCase) ?? false) && Path.GetFileName(fullPath).StartsWith("NestG-Wub-", StringComparison.Ordinal) && (File.GetAttributes(fullPath) & FileAttributes.ReparsePoint) == 0)
					{
						Directory.Delete(fullPath, recursive: true);
					}
				}
				catch (IOException)
				{
				}
				catch (UnauthorizedAccessException)
				{
				}
				catch (SecurityException)
				{
				}
			}
		}
	}

	private static void TryStop(Process process)
	{
		try
		{
			if (!process.HasExited)
			{
				process.Kill(entireProcessTree: true);
			}
		}
		catch (InvalidOperationException)
		{
		}
		catch (Win32Exception)
		{
		}
	}

	private static bool HasPinnedContent(FileStream file, long expectedLength, string expectedHash)
	{
		if (file.Length == expectedLength)
		{
			return Convert.ToHexString(SHA256.HashData(file)).Equals(expectedHash, StringComparison.OrdinalIgnoreCase);
		}
		return false;
	}

	internal static ProcessStartInfo CreateStartInfo(string executable, string session, bool blocked)
	{
		ProcessStartInfo processStartInfo = new ProcessStartInfo(executable)
		{
			WorkingDirectory = session,
			UseShellExecute = true,
			Verb = "runas",
			WindowStyle = ProcessWindowStyle.Hidden
		};
		processStartInfo.ArgumentList.Add(blocked ? "/D" : "/E");
		if (blocked)
		{
			processStartInfo.ArgumentList.Add("/P");
		}
		return processStartInfo;
	}
}
