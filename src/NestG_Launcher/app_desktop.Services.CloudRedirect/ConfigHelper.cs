using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace app_desktop.Services.CloudRedirect;

internal static class ConfigHelper
{
	private static readonly object FileLock = new object();

	public static void SaveConfig(string configPath, IReadOnlyCollection<string> skipKeys, Action<Utf8JsonWriter> writeKeys)
	{
		lock (FileLock)
		{
			string directoryName = Path.GetDirectoryName(configPath);
			if (!string.IsNullOrWhiteSpace(directoryName))
			{
				Directory.CreateDirectory(directoryName);
			}
			JsonElement jsonElement = default(JsonElement);
			if (File.Exists(configPath))
			{
				try
				{
					using JsonDocument jsonDocument = JsonDocument.Parse(File.ReadAllText(configPath));
					jsonElement = jsonDocument.RootElement.Clone();
				}
				catch
				{
					jsonElement = default(JsonElement);
				}
			}
			HashSet<string> hashSet = skipKeys.ToHashSet<string>(StringComparer.OrdinalIgnoreCase);
			using MemoryStream memoryStream = new MemoryStream();
			using (Utf8JsonWriter utf8JsonWriter = new Utf8JsonWriter(memoryStream, new JsonWriterOptions
			{
				Indented = true
			}))
			{
				utf8JsonWriter.WriteStartObject();
				writeKeys(utf8JsonWriter);
				if (jsonElement.ValueKind == JsonValueKind.Object)
				{
					foreach (JsonProperty item in jsonElement.EnumerateObject())
					{
						if (!hashSet.Contains(item.Name))
						{
							item.WriteTo(utf8JsonWriter);
						}
					}
				}
				utf8JsonWriter.WriteEndObject();
			}
			string @string = Encoding.UTF8.GetString(memoryStream.ToArray());
			FileUtils.AtomicWriteAllText(configPath, @string);
		}
	}
}
