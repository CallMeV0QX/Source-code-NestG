using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace app_desktop.Services.SteamExclusions;

internal sealed class DefenderPowerShellRunner : IDefenderPowerShellRunner
{
	public async Task<DefenderProcessResult> RunAsync(string script, bool elevated, TimeSpan timeout, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		ProcessStartInfo info = CreateStartInfo(script, elevated);
		using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		deadline.CancelAfter(timeout);
		Task<Process> start = Task.Run(() => Process.Start(info) ?? throw new InvalidOperationException("Cannot start Defender command."));
		Process process = null;
		try
		{
			process = await start.WaitAsync(deadline.Token).ConfigureAwait(continueOnCapturedContext: false);
			Task<string> output = (Task<string>)(elevated ? ((Task)Task.FromResult(string.Empty)) : ((Task)process.StandardOutput.ReadToEndAsync(deadline.Token)));
			Task<string> error = (Task<string>)(elevated ? ((Task)Task.FromResult(string.Empty)) : ((Task)process.StandardError.ReadToEndAsync(deadline.Token)));
			await process.WaitForExitAsync(deadline.Token).ConfigureAwait(continueOnCapturedContext: false);
			await error.ConfigureAwait(continueOnCapturedContext: false);
			int exitCode = process.ExitCode;
			return new DefenderProcessResult(exitCode, await output.ConfigureAwait(continueOnCapturedContext: false));
		}
		catch (OperationCanceledException)
		{
			if (process != null)
			{
				TryStop(process);
			}
			else
			{
				start.ContinueWith(delegate(Task<Process> task)
				{
					if (task.Status == TaskStatus.RanToCompletion)
					{
						using (Process process2 = task.Result)
						{
							TryStop(process2);
							return;
						}
					}
					_ = task.Exception;
				}, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
			}
			cancellationToken.ThrowIfCancellationRequested();
			throw new TimeoutException("Defender command timed out.");
		}
		finally
		{
			process?.Dispose();
		}
	}

	internal static ProcessStartInfo CreateStartInfo(string script, bool elevated)
	{
		if (Encoding.UTF8.GetByteCount(script) > 524288)
		{
			throw new InvalidOperationException("Defender command too large.");
		}
		string text = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
		if (text.Length > 30000)
		{
			using MemoryStream memoryStream = new MemoryStream();
			using (GZipStream gZipStream = new GZipStream(memoryStream, CompressionLevel.SmallestSize, leaveOpen: true))
			{
				gZipStream.Write(Encoding.UTF8.GetBytes(script));
			}
			string text2 = Convert.ToBase64String(memoryStream.ToArray());
			string s = "$m=[IO.MemoryStream]::new([Convert]::FromBase64String('" + text2 + "'));$g=[IO.Compression.GZipStream]::new($m,[IO.Compression.CompressionMode]::Decompress);$r=[IO.StreamReader]::new($g,[Text.Encoding]::UTF8);try { & ([ScriptBlock]::Create($r.ReadToEnd())) } finally { $r.Dispose();$g.Dispose();$m.Dispose() }";
			text = Convert.ToBase64String(Encoding.Unicode.GetBytes(s));
		}
		if (text.Length > 30000)
		{
			throw new InvalidOperationException("Too many exclusion paths for one request.");
		}
		string folderPath = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
		string text3 = ((Environment.Is64BitOperatingSystem && !Environment.Is64BitProcess) ? "Sysnative" : "System32");
		ProcessStartInfo processStartInfo = new ProcessStartInfo();
		processStartInfo.FileName = Path.Combine(folderPath, text3, "WindowsPowerShell", "v1.0", "powershell.exe");
		processStartInfo.Arguments = "-NoLogo -NoProfile -NonInteractive -EncodedCommand " + text;
		processStartInfo.UseShellExecute = elevated;
		processStartInfo.Verb = (elevated ? "runas" : string.Empty);
		processStartInfo.WindowStyle = ProcessWindowStyle.Hidden;
		processStartInfo.CreateNoWindow = !elevated;
		processStartInfo.RedirectStandardOutput = !elevated;
		processStartInfo.RedirectStandardError = !elevated;
		processStartInfo.StandardOutputEncoding = (elevated ? null : Encoding.UTF8);
		processStartInfo.StandardErrorEncoding = (elevated ? null : Encoding.UTF8);
		processStartInfo.WorkingDirectory = Path.Combine(folderPath, text3);
		return processStartInfo;
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
		catch (Exception ex) when (((ex is Win32Exception || ex is InvalidOperationException || ex is NotSupportedException) ? 1 : 0) != 0)
		{
		}
	}
}
