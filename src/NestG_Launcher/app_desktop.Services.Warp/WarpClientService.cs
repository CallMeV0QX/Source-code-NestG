using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace app_desktop.Services.Warp;

internal sealed class WarpClientService
{
	private readonly IWarpCliRunner runner;

	internal WarpClientService(IWarpCliRunner? runner = null)
	{
		this.runner = runner ?? new WarpCliProcessRunner();
	}

	internal async Task<WarpConnectionStatus> GetStatusAsync(CancellationToken cancellationToken = default(CancellationToken))
	{
		if (!runner.IsInstalled)
		{
			return new WarpConnectionStatus(WarpConnectionKind.NotInstalled, IsConnected: false, SwitchLocked: false, null);
		}
		try
		{
			WarpCliResult warpCliResult = await runner.RunAsync(new _003C_003Ez__ReadOnlyArray<string>(new string[2] { "-j", "status" }), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			if (warpCliResult.ExitCode != 0 || !TryReadStatus(warpCliResult.StandardOutput, out string connectionState))
			{
				return new WarpConnectionStatus(WarpConnectionKind.Unavailable, IsConnected: false, SwitchLocked: false, null);
			}
			WarpCliResult warpCliResult2 = await runner.RunAsync(new _003C_003Ez__ReadOnlyArray<string>(new string[3] { "-j", "settings", "list" }), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			if (warpCliResult2.ExitCode != 0 || !TryReadSettings(warpCliResult2.StandardOutput, out string mode, out bool switchLocked))
			{
				return new WarpConnectionStatus(WarpConnectionKind.Unknown, IsConnected: false, SwitchLocked: false, null);
			}
			if (IsDnsOnlyMode(mode))
			{
				return new WarpConnectionStatus(WarpConnectionKind.DnsOnly, IsConnected: false, switchLocked, null);
			}
			if (!IsWarpMode(mode))
			{
				return new WarpConnectionStatus(WarpConnectionKind.Unknown, IsConnected: false, SwitchLocked: false, null);
			}
			if (switchLocked && connectionState != "Connected")
			{
				return new WarpConnectionStatus(WarpConnectionKind.Restricted, IsConnected: false, SwitchLocked: true, null);
			}
			return connectionState switch
			{
				"Connected" => new WarpConnectionStatus(WarpConnectionKind.Connected, IsConnected: true, switchLocked, null), 
				"Disconnected" => new WarpConnectionStatus(WarpConnectionKind.Disconnected, IsConnected: false, SwitchLocked: false, null), 
				"Connecting" => new WarpConnectionStatus(WarpConnectionKind.Connecting, IsConnected: false, SwitchLocked: false, null), 
				_ => new WarpConnectionStatus(WarpConnectionKind.Unknown, IsConnected: false, SwitchLocked: false, null), 
			};
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			throw;
		}
		catch
		{
			return new WarpConnectionStatus(WarpConnectionKind.Unavailable, IsConnected: false, SwitchLocked: false, null);
		}
	}

	internal async Task<WarpConnectionStatus> SetEnabledAsync(bool enabled, CancellationToken cancellationToken = default(CancellationToken))
	{
		WarpConnectionStatus initial = await GetStatusAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		if (!initial.CanToggle || initial.IsConnected == enabled)
		{
			return initial;
		}
		try
		{
			if ((await runner.RunAsync(new _003C_003Ez__ReadOnlySingleElementList<string>(enabled ? "connect" : "disconnect"), cancellationToken).ConfigureAwait(continueOnCapturedContext: false)).ExitCode != 0)
			{
				return new WarpConnectionStatus(WarpConnectionKind.Unavailable, IsConnected: false, SwitchLocked: false, null);
			}
			WarpConnectionStatus current = initial;
			for (int attempt = 0; attempt < 5; attempt++)
			{
				current = await GetStatusAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				WarpConnectionKind kind = current.Kind;
				bool flag = ((kind == WarpConnectionKind.Disconnected || kind == WarpConnectionKind.Connected) ? true : false);
				if (flag && current.IsConnected == enabled)
				{
					return current;
				}
				kind = current.Kind;
				if ((kind == WarpConnectionKind.NotInstalled || (uint)(kind - 4) <= 3u) ? true : false)
				{
					return current;
				}
				if (attempt < 4)
				{
					await Task.Delay(1000, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				}
			}
			return current with
			{
				UnconfirmedRequest = enabled
			};
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			throw;
		}
		catch
		{
			return new WarpConnectionStatus(WarpConnectionKind.Unavailable, IsConnected: false, SwitchLocked: false, null);
		}
	}

	private static bool TryReadStatus(string json, out string state)
	{
		state = string.Empty;
		try
		{
			using JsonDocument jsonDocument = JsonDocument.Parse(json);
			state = jsonDocument.RootElement.GetProperty("status").GetString() ?? string.Empty;
			return state.Length > 0;
		}
		catch (JsonException)
		{
			return false;
		}
		catch (InvalidOperationException)
		{
			return false;
		}
		catch (KeyNotFoundException)
		{
			return false;
		}
	}

	private static bool TryReadSettings(string json, out string mode, out bool switchLocked)
	{
		mode = string.Empty;
		switchLocked = false;
		bool flag;
		try
		{
			using JsonDocument jsonDocument = JsonDocument.Parse(json);
			JsonElement property = jsonDocument.RootElement.GetProperty("settings");
			mode = property.GetProperty("operation_mode").GetString() ?? string.Empty;
			flag = property.TryGetProperty("switch_locked", out var value);
			if (flag)
			{
				JsonValueKind valueKind = value.ValueKind;
				bool flag2 = valueKind - 5 <= JsonValueKind.Object;
				flag = flag2;
			}
			if (flag)
			{
				switchLocked = value.GetBoolean();
			}
			flag = mode.Length > 0;
		}
		catch (JsonException)
		{
			flag = false;
		}
		catch (InvalidOperationException)
		{
			flag = false;
		}
		catch (KeyNotFoundException)
		{
			flag = false;
		}
		return flag;
	}

	private static bool IsWarpMode(string mode)
	{
		if (!mode.Equals("warp", StringComparison.OrdinalIgnoreCase))
		{
			return mode.StartsWith("warp+", StringComparison.OrdinalIgnoreCase);
		}
		return true;
	}

	private static bool IsDnsOnlyMode(string mode)
	{
		if (!mode.Equals("doh", StringComparison.OrdinalIgnoreCase))
		{
			return mode.Equals("dot", StringComparison.OrdinalIgnoreCase);
		}
		return true;
	}
}
