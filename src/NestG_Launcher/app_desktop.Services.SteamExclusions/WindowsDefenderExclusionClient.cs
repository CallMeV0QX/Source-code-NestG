using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace app_desktop.Services.SteamExclusions;

internal sealed class WindowsDefenderExclusionClient(IDefenderPowerShellRunner runner) : IDefenderExclusionClient, IManagedDefenderExclusionClient
{
	public async Task<SteamExclusionToggleResult> SetEnabledAsync(bool enabled, IReadOnlyList<string> paths, CancellationToken cancellationToken)
	{
		if (paths.Count == 0 || paths.Count > (enabled ? 32 : 256) || paths.Any((string path) => !SteamExclusionOwnership.IsManagedPath(path) || (enabled && !SteamExclusionPaths.IsSafeExistingGameFolder(path))))
		{
			return new SteamExclusionToggleResult(SteamExclusionToggleOutcome.InvalidPaths);
		}
		try
		{
			using Process parent = Process.GetCurrentProcess();
			string script = BuildManagedScript(enabled, paths, parent.Id, parent.StartTime.ToUniversalTime(), DateTime.UtcNow.AddSeconds(90.0));
			return FromManagedExitCode((await runner.RunAsync(script, elevated: true, TimeSpan.FromSeconds(90.0), cancellationToken)).ExitCode, enabled);
		}
		catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
		{
			return new SteamExclusionToggleResult(SteamExclusionToggleOutcome.Declined);
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			throw;
		}
		catch (TimeoutException)
		{
			return new SteamExclusionToggleResult(SteamExclusionToggleOutcome.TimedOut);
		}
		catch
		{
			return new SteamExclusionToggleResult(SteamExclusionToggleOutcome.Failed);
		}
	}

	internal static SteamExclusionToggleResult FromManagedExitCode(int code, bool enabled)
	{
		SteamExclusionToggleOutcome outcome;
		switch (code)
		{
		case 0:
			if (enabled)
			{
				outcome = SteamExclusionToggleOutcome.Enabled;
				break;
			}
			goto default;
		case 20:
			if (!enabled)
			{
				outcome = SteamExclusionToggleOutcome.Disabled;
				break;
			}
			goto default;
		case 13:
			outcome = SteamExclusionToggleOutcome.VerificationFailed;
			break;
		case 14:
			outcome = SteamExclusionToggleOutcome.InvalidPaths;
			break;
		case 22:
			outcome = SteamExclusionToggleOutcome.Blocked;
			break;
		case 23:
			outcome = SteamExclusionToggleOutcome.Unavailable;
			break;
		case 24:
			outcome = SteamExclusionToggleOutcome.NeedsElevation;
			break;
		case 25:
			outcome = SteamExclusionToggleOutcome.OwnershipFailed;
			break;
		case 26:
			outcome = SteamExclusionToggleOutcome.Unknown;
			break;
		case 27:
			outcome = SteamExclusionToggleOutcome.Busy;
			break;
		default:
			outcome = SteamExclusionToggleOutcome.Failed;
			break;
		}
		return new SteamExclusionToggleResult(outcome);
	}

	internal static string BuildManagedScript(bool enabled, IReadOnlyList<string> paths, int parentId, DateTime parentStartUtc, DateTime deadlineUtc)
	{
		string text = Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(new
		{
			Action = (enabled ? "Enable" : "Disable"),
			Paths = paths,
			ParentId = parentId,
			ParentStartUtcTicks = parentStartUtc.Ticks,
			DeadlineUtcTicks = deadlineUtc.Ticks
		}));
		return ReadResource("DefenderCommon.ps1") + "\n" + ReadResource("DefenderOwnership.ps1") + "\n$request = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('" + text + "')) | ConvertFrom-Json\n" + ReadResource("DefenderManagedApply.ps1");
	}

	public async Task<DefenderSnapshot> ReadAsync(CancellationToken cancellationToken)
	{
		try
		{
			DefenderProcessResult defenderProcessResult = await runner.RunAsync(ReadResource("DefenderCommon.ps1") + "\nGet-NestGDefenderSnapshot | ConvertTo-Json -Compress -Depth 3", elevated: false, TimeSpan.FromSeconds(15.0), cancellationToken);
			return (defenderProcessResult.ExitCode == 0) ? ParseSnapshot(defenderProcessResult.Output) : new DefenderSnapshot(DefenderAvailability.Unknown, Array.Empty<string>());
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			throw;
		}
		catch (TimeoutException)
		{
			throw;
		}
		catch
		{
			return new DefenderSnapshot(DefenderAvailability.Unknown, Array.Empty<string>());
		}
	}

	public async Task<ExclusionApplyResult> AddAndVerifyAsync(IReadOnlyList<string> paths, CancellationToken cancellationToken)
	{
		if (paths.Count == 0 || paths.Count > 32 || paths.Any((string path) => !SteamExclusionPaths.IsSafeExistingGameFolder(path)))
		{
			return ExclusionApplyResult.Failed;
		}
		try
		{
			using Process parent = Process.GetCurrentProcess();
			string script = BuildApplyScript(paths, parent.Id, parent.StartTime.ToUniversalTime(), DateTime.UtcNow.AddSeconds(90.0));
			return ((await runner.RunAsync(script, elevated: true, TimeSpan.FromSeconds(90.0), cancellationToken)).ExitCode != 0) ? ExclusionApplyResult.Failed : ExclusionApplyResult.Verified;
		}
		catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
		{
			return ExclusionApplyResult.Declined;
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			throw;
		}
		catch (TimeoutException)
		{
			return ExclusionApplyResult.TimedOut;
		}
		catch
		{
			return ExclusionApplyResult.Failed;
		}
	}

	internal static DefenderSnapshot ParseSnapshot(string json)
	{
		try
		{
			using JsonDocument jsonDocument = JsonDocument.Parse(json);
			JsonElement rootElement = jsonDocument.RootElement;
			if (!Enum.TryParse<DefenderAvailability>(rootElement.GetProperty("Status").GetString(), out var result) || !Enum.IsDefined(result))
			{
				return new DefenderSnapshot(DefenderAvailability.Unknown, Array.Empty<string>());
			}
			if (result != 0)
			{
				return new DefenderSnapshot(result, Array.Empty<string>());
			}
			string[] source = (from value in rootElement.GetProperty("Paths").EnumerateArray()
				select value.GetString()).ToArray();
			if (source.Any((string path) => SteamExclusionPaths.NormalizeLiteralLocalPath(path) == null))
			{
				return new DefenderSnapshot(DefenderAvailability.Unknown, Array.Empty<string>());
			}
			return new DefenderSnapshot(result, source.Select((string path) => SteamExclusionPaths.NormalizeLiteralLocalPath(path)).ToArray());
		}
		catch
		{
			return new DefenderSnapshot(DefenderAvailability.Unknown, Array.Empty<string>());
		}
	}

	internal static string BuildApplyScript(IReadOnlyList<string> paths, int parentId, DateTime parentStartUtc, DateTime deadlineUtc)
	{
		string text = Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(new
		{
			Paths = paths,
			ParentId = parentId,
			ParentStartUtcTicks = parentStartUtc.Ticks,
			DeadlineUtcTicks = deadlineUtc.Ticks
		}));
		return ReadResource("DefenderCommon.ps1") + "\n$request = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('" + text + "')) | ConvertFrom-Json\n" + ReadResource("DefenderApply.ps1");
	}

	internal static string ReadResource(string name)
	{
		using Stream stream = typeof(WindowsDefenderExclusionClient).Assembly.GetManifestResourceStream("app_desktop.Services.SteamExclusions." + name) ?? throw new InvalidOperationException("Missing Defender resource.");
		using StreamReader streamReader = new StreamReader(stream);
		return streamReader.ReadToEnd();
	}
}
