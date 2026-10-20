using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace app_desktop.Services.Fix54;

internal sealed class Fix54PowerShell : IFix54ProcessRunner
{
	internal const int MaximumOutputCharacters = 32768;

	internal static readonly TimeSpan ProcessTimeout = TimeSpan.FromMinutes(6.0);

	internal const string Wrapper = "param([ValidateRange(1,2147483647)][int]$AppId)\r\n$ErrorActionPreference = 'Stop'\r\n[Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)\r\nif ([string]::IsNullOrWhiteSpace($env:NESTG_FIX54_STEAM) -or -not [IO.Path]::IsPathRooted($env:NESTG_FIX54_STEAM)) { exit 1 }\r\nfunction Get-ItemProperty {\r\n    param([string]$Path,[string]$Name,[string]$ErrorAction)\r\n    if ($Name -ne 'SteamPath' -or $Path -notin @('HKCU:\\Software\\Valve\\Steam','HKLM:\\Software\\WOW6432Node\\Valve\\Steam','HKLM:\\Software\\Valve\\Steam')) { throw 'Unexpected registry lookup.' }\r\n    [pscustomobject]@{ SteamPath = [IO.Path]::GetFullPath($env:NESTG_FIX54_STEAM) }\r\n}\r\nfunction Get-Content {\r\n    param([string]$Path,[switch]$Raw)\r\n    $resolved = [IO.Path]::GetFullPath($Path)\r\n    $steam = [IO.Path]::GetFullPath($env:NESTG_FIX54_STEAM)\r\n    if (-not $resolved.StartsWith($steam + '\\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Metadata outside staged Steam.' }\r\n    Microsoft.PowerShell.Management\\Get-Content -LiteralPath $resolved -Raw:$Raw -Encoding UTF8 -ErrorAction Stop\r\n}\r\nfunction Get-ChildItem {\r\n    param([string]$Path,[string]$Filter,[switch]$Recurse,[string]$ErrorAction)\r\n    $game = [IO.Path]::Combine([IO.Path]::GetFullPath($env:NESTG_FIX54_STEAM),'steamapps','common','Game')\r\n    if (-not [IO.Path]::GetFullPath($Path).Equals($game,[StringComparison]::OrdinalIgnoreCase) -or $Filter -ne '*.exe') { throw 'Enumeration outside staged game.' }\r\n    Microsoft.PowerShell.Management\\Get-ChildItem -LiteralPath $game -Filter '*.exe' -Recurse:$Recurse -ErrorAction Stop\r\n}\r\nfunction Assert-Fix54File([string]$Path) {\r\n    $resolved = [IO.Path]::GetFullPath($Path)\r\n    $game = [IO.Path]::Combine([IO.Path]::GetFullPath($env:NESTG_FIX54_STEAM),'steamapps','common','Game')\r\n    if (-not $resolved.StartsWith($game + '\\',[StringComparison]::OrdinalIgnoreCase) -or -not $resolved.EndsWith('.exe',[StringComparison]::OrdinalIgnoreCase)) { throw 'Unexpected staged executable.' }\r\n    if (([IO.File]::GetAttributes($resolved) -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw 'Linked staged executable.' }\r\n    $parent = [IO.DirectoryInfo]::new([IO.Path]::GetDirectoryName($resolved))\r\n    while ($parent -and ($parent.FullName.StartsWith($game + '\\',[StringComparison]::OrdinalIgnoreCase) -or $parent.FullName.Equals($game,[StringComparison]::OrdinalIgnoreCase))) {\r\n        if (($parent.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw 'Linked staged directory.' }\r\n        $parent = $parent.Parent\r\n    }\r\n    $resolved\r\n}\r\nfunction Remove-Item {\r\n    param([string]$Path,[switch]$Force)\r\n    $resolved = Assert-Fix54File $Path\r\n    Microsoft.PowerShell.Management\\Remove-Item -LiteralPath $resolved -Force:$Force\r\n}\r\nfunction Rename-Item {\r\n    param([string]$Path,[string]$NewName)\r\n    $resolved = Assert-Fix54File $Path\r\n    $destination = [IO.Path]::GetFullPath($NewName)\r\n    if (-not [IO.Path]::GetDirectoryName($resolved).Equals([IO.Path]::GetDirectoryName($destination),[StringComparison]::OrdinalIgnoreCase) -or\r\n        -not $resolved.Equals($destination + '.unpacked.exe',[StringComparison]::OrdinalIgnoreCase)) { throw 'Unexpected unpacked destination.' }\r\n    Microsoft.PowerShell.Management\\Rename-Item -LiteralPath $resolved -NewName ([IO.Path]::GetFileName($destination))\r\n}\r\ntry { & $env:NESTG_FIX54_SCRIPT -AppId $AppId; exit 0 }\r\ncatch { [Console]::Error.WriteLine('FIX54_SCRIPT_FAILED ' + $_.Exception.GetType().Name + ' line ' + $_.InvocationInfo.ScriptLineNumber); exit 1 }";

	public async Task<Fix54ProcessResult> RunAsync(Fix54ProcessRequest request, CancellationToken cancellationToken)
	{
		using CancellationTokenSource timeout = new CancellationTokenSource(ProcessTimeout);
		using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
		using Fix54ProcessJob job = new Fix54ProcessJob();
		using Fix54SuspendedProcess owned = Fix54SuspendedProcess.Start(CreateStartInfo(request), job);
		Process process = owned.Process;
		Task<(string Text, bool Truncated)> stdout = ReadBoundedAsync(owned.StandardOutput);
		Task<(string Text, bool Truncated)> stderr = ReadBoundedAsync(owned.StandardError);
		try
		{
			await process.WaitForExitAsync(linked.Token);
			if (job.ActiveProcesses != 0)
			{
				await job.StopAndConfirmAsync(process);
			}
			await Task.WhenAll<(string, bool)>(stdout, stderr).WaitAsync(TimeSpan.FromSeconds(15.0), linked.Token);
		}
		catch (OperationCanceledException)
		{
			await job.StopAndConfirmAsync(process);
			await Task.WhenAll<(string, bool)>(stdout, stderr).WaitAsync(TimeSpan.FromSeconds(15.0));
			if (cancellationToken.IsCancellationRequested)
			{
				throw new OperationCanceledException(cancellationToken);
			}
			throw new Fix54Exception("FIX54 chạy quá thời gian cho phép; game được giữ nguyên.");
		}
		catch
		{
			await job.StopAndConfirmAsync(process);
			throw;
		}
		(string Text, bool Truncated) output = await stdout;
		(string, bool) tuple = await stderr;
		return new Fix54ProcessResult(process.ExitCode, output.Text, tuple.Item1, output.Truncated || tuple.Item2);
	}

	internal static ProcessStartInfo CreateStartInfo(Fix54ProcessRequest request)
	{
		if (request.SteamAppId <= 0)
		{
			throw new ArgumentOutOfRangeException("SteamAppId");
		}
		ProcessStartInfo processStartInfo = new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe"))
		{
			UseShellExecute = false,
			CreateNoWindow = true,
			WindowStyle = ProcessWindowStyle.Hidden,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			StandardOutputEncoding = Encoding.UTF8,
			StandardErrorEncoding = Encoding.UTF8,
			WorkingDirectory = Fix54Paths.SafePath(request.WorkingDirectory)
		};
		string[] array = new string[9]
		{
			"-NoLogo",
			"-NoProfile",
			"-NonInteractive",
			"-ExecutionPolicy",
			"Bypass",
			"-File",
			Fix54Paths.SafePath(request.WrapperPath),
			"-AppId",
			request.SteamAppId.ToString(CultureInfo.InvariantCulture)
		};
		foreach (string item in array)
		{
			processStartInfo.ArgumentList.Add(item);
		}
		processStartInfo.Environment["NESTG_FIX54_STEAM"] = Fix54Paths.SafePath(request.SteamRoot);
		processStartInfo.Environment["NESTG_FIX54_SCRIPT"] = Fix54Paths.SafePath(request.ScriptPath);
		IDictionary<string, string?> environment = processStartInfo.Environment;
		string value = (processStartInfo.Environment["TMP"] = Fix54Paths.SafePath(request.TemporaryDirectory));
		environment["TEMP"] = value;
		processStartInfo.Environment["PSModulePath"] = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "Modules");
		array = new string[9] { "DOTNET_STARTUP_HOOKS", "COR_ENABLE_PROFILING", "COR_PROFILER", "COR_PROFILER_PATH", "COR_PROFILER_PATH_32", "COR_PROFILER_PATH_64", "CORECLR_ENABLE_PROFILING", "CORECLR_PROFILER", "CORECLR_PROFILER_PATH" };
		foreach (string key in array)
		{
			processStartInfo.Environment.Remove(key);
		}
		return processStartInfo;
	}

	private static async Task<(string Text, bool Truncated)> ReadBoundedAsync(StreamReader reader)
	{
		StringBuilder output = new StringBuilder();
		char[] buffer = new char[4096];
		bool truncated = false;
		while (true)
		{
			int num = await reader.ReadAsync(buffer);
			if (num <= 0)
			{
				break;
			}
			int num2 = Math.Min(num, 32768 - output.Length);
			output.Append(buffer, 0, num2);
			truncated = truncated || num2 < num;
		}
		return (Text: output.ToString(), Truncated: truncated);
	}
}
