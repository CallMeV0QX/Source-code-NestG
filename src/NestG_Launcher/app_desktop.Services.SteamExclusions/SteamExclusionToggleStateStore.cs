using System;
using System.IO;
using System.Text.Json;

namespace app_desktop.Services.SteamExclusions;

internal sealed class SteamExclusionToggleStateStore(string directory)
{
	private sealed record LastResult(bool Enabled, DateTimeOffset At, SteamExclusionToggleOutcome Outcome);

	private string FilePath => Path.Combine(directory, "library-toggle-v2.json");

	internal SteamExclusionToggleState Load()
	{
		try
		{
			if (!File.Exists(FilePath) || new FileInfo(FilePath).Length > 4096)
			{
				return new SteamExclusionToggleState();
			}
			LastResult lastResult = JsonSerializer.Deserialize<LastResult>(File.ReadAllText(FilePath));
			if ((object)lastResult == null || !Enum.IsDefined(lastResult.Outcome))
			{
				return new SteamExclusionToggleState();
			}
			return new SteamExclusionToggleState(lastResult.Enabled, Busy: false, CanRemove: false, $"Lần thao tác trước: {lastResult.At.ToLocalTime():dd/MM HH:mm}. " + "Chưa kiểm tra lại trạng thái Defender trong phiên này.", "Chưa kiểm tra lại");
		}
		catch
		{
			return new SteamExclusionToggleState();
		}
	}

	internal bool Save(SteamExclusionToggleState state, SteamExclusionToggleResult result)
	{
		string text = Path.Combine(directory, Guid.NewGuid().ToString("N") + ".tmp");
		try
		{
			Directory.CreateDirectory(directory);
			File.WriteAllText(text, JsonSerializer.Serialize(new LastResult(state.Enabled, DateTimeOffset.UtcNow, result.Outcome)));
			File.Move(text, FilePath, overwrite: true);
			File.WriteAllText(Path.Combine(directory, "last-status.log"), $"{DateTimeOffset.UtcNow:O} Manual {result.Outcome}");
			return true;
		}
		catch
		{
			return false;
		}
		finally
		{
			try
			{
				if (File.Exists(text))
				{
					File.Delete(text);
				}
			}
			catch
			{
			}
		}
	}
}
