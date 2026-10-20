using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Text.RegularExpressions.Generated;

namespace app_desktop.Services.CloudRedirect;

internal static class CloudRedirectConfiguration
{
	private static readonly JsonSerializerOptions IndentedJsonOptions = new JsonSerializerOptions
	{
		WriteIndented = true
	};

	internal static string UpdateOpenSteamToolConfig(string? existingContent)
	{
		return SetOpenSteamToolCloudEnabled(existingContent, enabled: true);
	}

	internal static string SetOpenSteamToolCloudEnabled(string? existingContent, bool enabled)
	{
		string text = ((existingContent != null && existingContent.Contains("\r\n", StringComparison.Ordinal)) ? "\r\n" : Environment.NewLine);
		List<string> list = (string.IsNullOrWhiteSpace(existingContent) ? CreateDefaultOpenSteamToolConfigLines() : existingContent.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n')
			.ToList());
		int[] array = (from item in list.Select((string line, int index) => (line: line, index: index))
			where IsSection(item.line, "cloud")
			select item.index).ToArray();
		if (array.Length > 1)
		{
			throw new InvalidDataException("opensteamtool.toml có nhiều hơn một section [cloud].");
		}
		int num;
		int sectionEnd;
		if (array.Length == 0)
		{
			if (list.Count > 0)
			{
				if (list[list.Count - 1].Length > 0)
				{
					list.Add(string.Empty);
				}
			}
			num = list.Count;
			list.Add("[cloud]");
			sectionEnd = list.Count;
		}
		else
		{
			num = array[0];
			sectionEnd = FindNextSection(list, num + 1);
		}
		UpsertTomlKey(list, num, ref sectionEnd, "enabled", enabled ? "true" : "false");
		if (enabled)
		{
			UpsertTomlKey(list, num, ref sectionEnd, "library", "\"cloud_redirect.dll\"");
		}
		while (list.Count > 1)
		{
			if (list[list.Count - 1].Length != 0)
			{
				break;
			}
			if (list[list.Count - 2].Length != 0)
			{
				break;
			}
			list.RemoveAt(list.Count - 1);
		}
		return string.Join(text, list).TrimEnd('\r', '\n') + text;
	}

	internal static string UpdateUserConfig(string? existingContent, string syncPath)
	{
		if (string.IsNullOrWhiteSpace(syncPath))
		{
			throw new ArgumentException("Folder provider không được để trống.", "syncPath");
		}
		JsonObject jsonObject = ParseJsonObject(existingContent, "CloudRedirect user config");
		string text = ReadOptionalString(jsonObject, "provider", "CloudRedirect user config");
		if (string.IsNullOrWhiteSpace(text))
		{
			text = "folder";
			jsonObject["provider"] = text;
		}
		if (string.Equals(text, "folder", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(ReadOptionalString(jsonObject, "sync_path", "CloudRedirect user config")))
		{
			jsonObject["sync_path"] = Path.GetFullPath(syncPath);
		}
		jsonObject["auto_update_dll"] = false;
		jsonObject["sync_achievements"] = false;
		jsonObject["sync_playtime"] = false;
		return jsonObject.ToJsonString(IndentedJsonOptions) + Environment.NewLine;
	}

	internal static bool IsOpenSteamToolCloudEnabled(string? content)
	{
		return InspectOpenSteamToolConfig(content).IsEnabled;
	}

	internal static CloudRedirectOpenSteamToolConfigStatus InspectOpenSteamToolConfig(string? content)
	{
		if (string.IsNullOrWhiteSpace(content))
		{
			return new CloudRedirectOpenSteamToolConfigStatus(IsValid: true, IsEnabled: false, HasExpectedLibrary: false, "OpenSteamTool cloud config chưa được tạo.");
		}
		string[] array = content.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
		int[] array2 = (from item in array.Select((string line, int index) => (line: line, index: index))
			where IsSection(item.line, "cloud")
			select item.index).ToArray();
		if (array2.Length != 1)
		{
			if (array2.Length != 0)
			{
				return new CloudRedirectOpenSteamToolConfigStatus(IsValid: false, IsEnabled: false, HasExpectedLibrary: false, "OpenSteamTool có nhiều hơn một section [cloud].");
			}
			return new CloudRedirectOpenSteamToolConfigStatus(IsValid: true, IsEnabled: false, HasExpectedLibrary: false, "OpenSteamTool chưa có section [cloud].");
		}
		bool? flag = null;
		string result = null;
		int num = FindNextSection(array, array2[0] + 1);
		for (int i = array2[0] + 1; i < num; i++)
		{
			Match match = TomlAssignmentRegex().Match(array[i]);
			if (!match.Success)
			{
				continue;
			}
			string value = match.Groups["key"].Value;
			string value2 = StripTomlComment(match.Groups["value"].Value).Trim();
			if (string.Equals(value, "enabled", StringComparison.OrdinalIgnoreCase))
			{
				if (flag.HasValue || !bool.TryParse(value2, out var result2))
				{
					return new CloudRedirectOpenSteamToolConfigStatus(IsValid: false, IsEnabled: false, HasExpectedLibrary: false, "OpenSteamTool [cloud].enabled không hợp lệ hoặc bị trùng lặp.");
				}
				flag = result2;
			}
			else if (string.Equals(value, "library", StringComparison.OrdinalIgnoreCase) && (result != null || !TryReadTomlString(value2, out result)))
			{
				return new CloudRedirectOpenSteamToolConfigStatus(IsValid: false, IsEnabled: false, HasExpectedLibrary: false, "OpenSteamTool [cloud].library không hợp lệ hoặc bị trùng lặp.");
			}
		}
		bool flag2 = string.Equals(result, "cloud_redirect.dll", StringComparison.OrdinalIgnoreCase);
		return new CloudRedirectOpenSteamToolConfigStatus(IsValid: true, flag == true, flag2, (flag == true && flag2) ? "OpenSteamTool cloud host đã sẵn sàng." : "OpenSteamTool cloud host chưa được bật đúng cấu hình.");
	}

	internal static CloudRedirectSteamConfigStatus InspectSteamConfig(string? content)
	{
		if (string.IsNullOrWhiteSpace(content))
		{
			return new CloudRedirectSteamConfigStatus(IsValid: true, IsEnabled: false, "CloudRedirect Steam config chưa được tạo.");
		}
		try
		{
			if (!(JsonNode.Parse(content, null) is JsonObject jsonObject))
			{
				return new CloudRedirectSteamConfigStatus(IsValid: false, IsEnabled: false, "CloudRedirect Steam config phải là JSON object.");
			}
			if (jsonObject["cloud_redirect"] == null)
			{
				return new CloudRedirectSteamConfigStatus(IsValid: true, IsEnabled: false, "CloudRedirect Steam config chưa bật cloud_redirect.");
			}
			if (!(jsonObject["cloud_redirect"] is JsonValue jsonValue) || !jsonValue.TryGetValue<bool>(out var value))
			{
				return new CloudRedirectSteamConfigStatus(IsValid: false, IsEnabled: false, "CloudRedirect Steam config.cloud_redirect phải là boolean.");
			}
			return new CloudRedirectSteamConfigStatus(IsValid: true, value, value ? "CloudRedirect Steam config đã sẵn sàng." : "CloudRedirect Steam config đang tắt.");
		}
		catch (JsonException)
		{
			return new CloudRedirectSteamConfigStatus(IsValid: false, IsEnabled: false, "CloudRedirect Steam config không phải JSON hợp lệ.");
		}
	}

	private static bool TryReadTomlString(string value, out string? result)
	{
		result = null;
		if (value.Length < 2)
		{
			return false;
		}
		char c = value[0];
		if (c == '"' || c == '\'')
		{
			if (value[value.Length - 1] == c)
			{
				result = value.Substring(1, value.Length - 1 - 1).Trim();
				return result.Length > 0;
			}
		}
		return false;
	}

	internal static string ResolveProviderName(string? content)
	{
		return ReadOptionalString(ParseJsonObject(content, "CloudRedirect user config"), "provider", "CloudRedirect user config");
	}

	internal static string ResolveSyncPath(string? content)
	{
		return ReadOptionalString(ParseJsonObject(content, "CloudRedirect user config"), "sync_path", "CloudRedirect user config");
	}

	internal static CloudRedirectProviderStatus InspectProvider(string? content)
	{
		JsonObject root = ParseJsonObject(content, "CloudRedirect user config");
		string text = ReadOptionalString(root, "provider", "CloudRedirect user config");
		string text2 = ReadOptionalString(root, "sync_path", "CloudRedirect user config");
		string text3 = ReadOptionalString(root, "token_path", "CloudRedirect user config");
		if (string.IsNullOrWhiteSpace(text))
		{
			return new CloudRedirectProviderStatus(string.Empty, IsReady: false, "CloudRedirect provider chưa được cấu hình.", text2, text3);
		}
		if (string.Equals(text, "folder", StringComparison.OrdinalIgnoreCase))
		{
			bool flag = !string.IsNullOrWhiteSpace(text2) && Path.IsPathFullyQualified(text2);
			return new CloudRedirectProviderStatus(text, flag, (!flag) ? "Folder provider cần sync_path tuyệt đối để tránh ghi save nhầm thư mục." : "Folder provider đã sẵn sàng.", text2, text3);
		}
		bool flag2;
		switch (text.ToLowerInvariant())
		{
		case "gdrive":
		case "onedrive":
		case "r2":
		case "s3":
			flag2 = true;
			break;
		default:
			flag2 = false;
			break;
		}
		if (flag2)
		{
			bool flag3 = !string.IsNullOrWhiteSpace(text3) && File.Exists(text3);
			return new CloudRedirectProviderStatus(text, flag3, flag3 ? ("Provider '" + text + "' có token file.") : ("Provider '" + text + "' chưa có token file hợp lệ; hãy cấu hình bằng CloudRedirect companion app."), text2, text3);
		}
		return new CloudRedirectProviderStatus(text, IsReady: false, "Provider '" + text + "' chưa được launcher hỗ trợ kiểm tra readiness.", text2, text3);
	}

	internal static string UpdateSteamConfig(string? existingContent)
	{
		return SetSteamConfigEnabled(existingContent, enabled: true);
	}

	internal static string SetSteamConfigEnabled(string? existingContent, bool enabled)
	{
		JsonObject jsonObject = ParseJsonObject(existingContent, "CloudRedirect Steam config");
		jsonObject["cloud_redirect"] = enabled;
		return jsonObject.ToJsonString(IndentedJsonOptions) + Environment.NewLine;
	}

	private static List<string> CreateDefaultOpenSteamToolConfigLines()
	{
		int num = 22;
		List<string> list = new List<string>(num);
		CollectionsMarshal.SetCount(list, num);
		Span<string> span = CollectionsMarshal.AsSpan(list);
		span[0] = "[log]";
		span[1] = "level = \"info\"";
		span[2] = string.Empty;
		span[3] = "[manifest]";
		span[4] = "url = \"opensteamtool\"";
		span[5] = "timeout_resolve_ms = 5000";
		span[6] = "timeout_connect_ms = 5000";
		span[7] = "timeout_send_ms = 10000";
		span[8] = "timeout_recv_ms = 10000";
		span[9] = string.Empty;
		span[10] = "[stats]";
		span[11] = "enable_api = true";
		span[12] = string.Empty;
		span[13] = "[lua]";
		span[14] = "paths = []";
		span[15] = string.Empty;
		span[16] = "[inject]";
		span[17] = "enabled = false";
		span[18] = string.Empty;
		span[19] = "[cloud]";
		span[20] = "enabled = true";
		span[21] = "library = \"cloud_redirect.dll\"";
		return list;
	}

	private static int FindNextSection(IReadOnlyList<string> lines, int startIndex)
	{
		for (int i = startIndex; i < lines.Count; i++)
		{
			if (TomlSectionRegex().IsMatch(lines[i]))
			{
				return i;
			}
		}
		return lines.Count;
	}

	private static void UpsertTomlKey(IList<string> lines, int sectionStart, ref int sectionEnd, string key, string value)
	{
		List<int> list = new List<int>();
		for (int i = sectionStart + 1; i < sectionEnd; i++)
		{
			Match match = TomlAssignmentRegex().Match(lines[i]);
			if (match.Success && string.Equals(match.Groups["key"].Value, key, StringComparison.OrdinalIgnoreCase))
			{
				list.Add(i);
			}
		}
		if (list.Count > 1)
		{
			throw new InvalidDataException("Section [cloud] có key " + key + " trùng lặp.");
		}
		string text = key + " = " + value;
		if (list.Count == 1)
		{
			Match match2 = TomlAssignmentRegex().Match(lines[list[0]]);
			string value2 = match2.Groups["indent"].Value;
			string text2 = ExtractTomlComment(match2.Groups["value"].Value);
			lines[list[0]] = value2 + text + text2;
		}
		else
		{
			lines.Insert(sectionEnd, text);
			sectionEnd++;
		}
	}

	private static JsonObject ParseJsonObject(string? content, string displayName)
	{
		if (string.IsNullOrWhiteSpace(content))
		{
			return new JsonObject((JsonNodeOptions?)null);
		}
		try
		{
			return (JsonNode.Parse(content, null) as JsonObject) ?? throw new InvalidDataException(displayName + " phải là JSON object.");
		}
		catch (JsonException innerException)
		{
			throw new InvalidDataException(displayName + " không phải JSON hợp lệ.", innerException);
		}
	}

	private static string ReadOptionalString(JsonObject root, string propertyName, string displayName)
	{
		if (root[propertyName] == null)
		{
			return string.Empty;
		}
		if (root[propertyName] is JsonValue jsonValue && jsonValue.TryGetValue<string>(out string value))
		{
			return value?.Trim() ?? string.Empty;
		}
		throw new InvalidDataException(displayName + "." + propertyName + " phải là chuỗi.");
	}

	private static bool IsSection(string line, string expectedName)
	{
		Match match = TomlSectionRegex().Match(line);
		if (match.Success)
		{
			return string.Equals(match.Groups["name"].Value, expectedName, StringComparison.OrdinalIgnoreCase);
		}
		return false;
	}

	private static string ExtractTomlComment(string value)
	{
		int num = FindTomlCommentIndex(value);
		if (num >= 0)
		{
			return " " + value.Substring(num).TrimStart();
		}
		return string.Empty;
	}

	private static string StripTomlComment(string value)
	{
		int num = FindTomlCommentIndex(value);
		if (num >= 0)
		{
			return value.Substring(0, num);
		}
		return value;
	}

	private static int FindTomlCommentIndex(string value)
	{
		bool flag = false;
		bool flag2 = false;
		bool flag3 = false;
		for (int i = 0; i < value.Length; i++)
		{
			char c = value[i];
			if (flag3)
			{
				flag3 = false;
			}
			else if (c == '\\' && flag)
			{
				flag3 = true;
			}
			else if (c == '"' && !flag2)
			{
				flag = !flag;
			}
			else if (c == '\'' && !flag)
			{
				flag2 = !flag2;
			}
			else if (c == '#' && !flag && !flag2)
			{
				return i;
			}
		}
		return -1;
	}

	// [GeneratedRegex("^\\s*\\[\\s*(?<name>[A-Za-z0-9_.-]+)\\s*\\]\\s*(?:#.*)?$")]
	[GeneratedCode("System.Text.RegularExpressions.Generator", "8.0.14.36720")]
	private static Regex TomlSectionRegex()
	{
		return _003CRegexGenerator_g_003EF0EE122E5B50AE7A6E112AFB8BF1EBFE9D7DDEF874BBD113E5DA346C1494B7BE9__TomlSectionRegex_1.Instance;
	}

	// [GeneratedRegex("^(?<indent>\\s*)(?<key>[A-Za-z0-9_-]+)\\s*=\\s*(?<value>.*)$")]
	[GeneratedCode("System.Text.RegularExpressions.Generator", "8.0.14.36720")]
	private static Regex TomlAssignmentRegex()
	{
		return _003CRegexGenerator_g_003EF0EE122E5B50AE7A6E112AFB8BF1EBFE9D7DDEF874BBD113E5DA346C1494B7BE9__TomlAssignmentRegex_2.Instance;
	}
}
