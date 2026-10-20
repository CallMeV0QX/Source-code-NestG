using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace app_desktop.Services.Warp;

internal sealed class WarpCliProcessRunner : IWarpCliRunner
{
	private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(8.0);

	private readonly string executablePath;

	public bool IsInstalled => File.Exists(executablePath);

	internal WarpCliProcessRunner(string? executablePath = null)
	{
		this.executablePath = executablePath ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Cloudflare", "Cloudflare WARP", "warp-cli.exe");
	}

	public async Task<WarpCliResult> RunAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
	{
		if (!IsInstalled)
		{
			throw new FileNotFoundException("Cloudflare WARP chưa được cài đặt.");
		}
		ProcessStartInfo processStartInfo = new ProcessStartInfo(executablePath)
		{
			UseShellExecute = false,
			CreateNoWindow = true,
			RedirectStandardOutput = true,
			RedirectStandardError = true
		};
		foreach (string argument in arguments)
		{
			processStartInfo.ArgumentList.Add(argument);
		}
		using Process process = Process.Start(processStartInfo) ?? throw new InvalidOperationException("Không mở được Cloudflare WARP CLI.");
		using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		deadline.CancelAfter(CommandTimeout);
		Task<string> outputTask = process.StandardOutput.ReadToEndAsync(deadline.Token);
		Task<string> errorTask = process.StandardError.ReadToEndAsync(deadline.Token);
		try
		{
			await process.WaitForExitAsync(deadline.Token).ConfigureAwait(continueOnCapturedContext: false);
			string output = await outputTask.ConfigureAwait(continueOnCapturedContext: false);
			await errorTask.ConfigureAwait(continueOnCapturedContext: false);
			if (output.Length > 65536)
			{
				throw new InvalidDataException("Cloudflare WARP trả dữ liệu quá lớn.");
			}
			return new WarpCliResult(process.ExitCode, output);
		}
		catch
		{
			if (!process.HasExited)
			{
				try
				{
					process.Kill(entireProcessTree: true);
				}
				catch
				{
				}
			}
			throw;
		}
	}
}
