using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UI_Desktop.Store;
using app_desktop.Services.CloudRedirect;

namespace app_desktop.Services;

internal sealed class GameActivationLog
{
	private readonly object gate = new object();

	private readonly StringBuilder content = new StringBuilder();

	private readonly string directory;

	private bool finished;

	private bool failed;

	private const int MaximumCharacters = 262144;

	internal string Id { get; }

	internal Guid LibraryId { get; }

	internal string FileName { get; }

	internal string InternalPath => Path.Combine(directory, FileName);

	internal bool IsFailed
	{
		get
		{
			lock (gate)
			{
				return finished && failed;
			}
		}
	}

	internal GameActivationLog(DesktopLibraryDetailResponse detail, string desktopVersion, string? directoryOverride = null)
	{
		Id = Guid.NewGuid().ToString("N");
		LibraryId = detail.LibraryId;
		FileName = $"NestG-ACTIVE-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Id}.log";
		directory = directoryOverride ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NestGLauncher", "ActivationLogs");
		Add($"Operation={Id}; Library={LibraryId}; AppId={detail.SteamAppId}; Game={detail.GameName}; Version={detail.VersionName}");
		Add($"Desktop={desktopVersion}; Windows={Environment.OSVersion.Version}; OS64Bit={Environment.Is64BitOperatingSystem}; Process64Bit={Environment.Is64BitProcess}");
	}

	internal void Add(string text)
	{
		lock (gate)
		{
			if (!finished && content.Length < 262144)
			{
				string text2 = $"[{DateTime.UtcNow:O}] {Sanitize(text)}\n";
				content.Append(text2.AsSpan(0, Math.Min(text2.Length, 262144 - content.Length)));
				PersistBestEffort();
			}
		}
	}

	internal void Exception(Exception error, string context)
	{
		Add(context);
		Exception ex = error;
		int num = 0;
		while (ex != null && num < 4)
		{
			Add($"Exception[{num}]={ex.GetType().Name}; HResult=0x{ex.HResult:X8}; Message={ex.Message}");
			if (!string.IsNullOrWhiteSpace(ex.StackTrace))
			{
				Add(ex.StackTrace);
			}
			num++;
			ex = ex.InnerException;
		}
	}

	internal void Result(SteamInstallResult result)
	{
		Add($"Steam={result.SteamDirectory}; Attempts={result.AttemptCount}; BetterOnly={result.BetterOnly}; SteamRunning={result.IsSteamRestarted}; BetterVerified={result.BetterRuntimeVerified}; CloudReady={result.CloudRedirectHealth?.IsReady}; Success={result.ActivationSucceeded}");
		foreach (string message in result.Messages)
		{
			Add(message);
		}
		if (!result.ActivationSucceeded)
		{
			Add("Failure=" + result.FailureMessage);
		}
		Finish(result.ActivationSucceeded ? "SUCCESS" : "FAILED");
	}

	internal void Fail(Exception error)
	{
		Exception(error, "ACTIVE failed before final result");
		Finish("FAILED");
	}

	internal void Finish(string outcome)
	{
		lock (gate)
		{
			if (!finished)
			{
				if (content.Length > 261632)
				{
					content.Length = 261632;
				}
				Add("Outcome=" + outcome);
				failed = outcome == "FAILED";
				finished = true;
			}
		}
	}

	internal string Snapshot()
	{
		lock (gate)
		{
			return content.ToString();
		}
	}

	private void PersistBestEffort()
	{
		string text = null;
		try
		{
			CloudRedirectRuntimeService.EnsureRecoveryPathIsNotLinked(InternalPath);
			Directory.CreateDirectory(directory);
			text = InternalPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
			File.WriteAllText(text, content.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
			CloudRedirectRuntimeService.EnsureRecoveryPathIsNotLinked(InternalPath);
			File.Move(text, InternalPath, overwrite: true);
		}
		catch
		{
		}
		finally
		{
			if (text != null)
			{
				try
				{
					File.Delete(text);
				}
				catch
				{
				}
			}
		}
	}

	internal static string Sanitize(string value)
	{
		return Regex.Replace(Regex.Replace(Regex.Replace(Regex.Replace(Regex.Replace((value.Length > 32768) ? value.Substring(0, 32768) : value, "(?i)\\b(?:https?|wss?)://[^\\s<>\"']+", "[URL omitted]"), "(?i)\\b(?:Bearer|Basic)\\s+[A-Za-z0-9+/_.=\\-]+", "[Authorization omitted]"), "(?im)(\\b(?:authorization|(?:set-)?cookie|connection[_-]?string)\\b[\"']?\\s*[:=]\\s*)[^\\r\\n]+", "$1[REDACTED]"), "(?i)(\\b(?:access[_-]?token|refresh[_-]?token|id[_-]?token|token|password|passwd|pwd|secret|client[_-]?secret|api[_-]?key|authorization|cookie|connection[_-]?string)\\b[\"']?\\s*[:=]\\s*)(?:\"[^\"]*(?:\"|$)|'[^']*(?:'|$)|[^\\s;,}&]+)", "$1[REDACTED]"), "\\beyJ[A-Za-z0-9_\\-]+\\.[A-Za-z0-9_\\-]+\\.[A-Za-z0-9_\\-]+\\b", "[JWT omitted]").Replace('\0', ' ');
	}
}
