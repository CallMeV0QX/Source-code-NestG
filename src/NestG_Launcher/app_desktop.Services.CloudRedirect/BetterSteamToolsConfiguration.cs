using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Tomlyn;
using Tomlyn.Model;

namespace app_desktop.Services.CloudRedirect;

internal static class BetterSteamToolsConfiguration
{
	internal static string Update(string? existing, string steamDirectory, bool cloudEnabled = true)
	{
		string text = (string.IsNullOrWhiteSpace(existing) ? CloudRedirectConfiguration.UpdateOpenSteamToolConfig(null) : existing);
		TomlTable model = Parse(text);
		TomlTable table = GetTable(model, "cloud");
		bool num = (cloudEnabled ? (!IsCloudReady(table)) : (!IsCloudDisabled(table)));
		table["enabled"] = cloudEnabled;
		if (cloudEnabled)
		{
			table["library"] = "cloud_redirect.dll";
		}
		TomlTable table2 = GetTable(model, "lua");
		string[] array = ReadPaths(table2);
		IReadOnlyList<string> readOnlyList = MergeLuaPaths(array, steamDirectory);
		if (!(num | !array.SequenceEqual<string>(readOnlyList, StringComparer.Ordinal) | SetDisabled(model, "update") | SetDisabled(model, "donate")))
		{
			return text;
		}
		TomlArray tomlArray = new TomlArray();
		foreach (string item in readOnlyList)
		{
			tomlArray.Add(item);
		}
		table2["paths"] = tomlArray;
		return Toml.FromModel(model);
	}

	internal static bool IsReady(string? content, string steamDirectory, bool cloudEnabled = true)
	{
		TomlTable model = Parse(content ?? string.Empty);
		string[] array = ReadPaths(GetTable(model, "lua"));
		if ((cloudEnabled ? IsCloudReady(GetTable(model, "cloud")) : IsCloudDisabled(GetTable(model, "cloud"))) && array.SequenceEqual<string>(MergeLuaPaths(array, steamDirectory), StringComparer.Ordinal) && IsDisabled(model, "update"))
		{
			return IsDisabled(model, "donate");
		}
		return false;
	}

	internal static CloudRedirectOpenSteamToolConfigStatus InspectCloud(string? content)
	{
		TomlTable table = GetTable(Parse(content ?? string.Empty), "cloud");
		object value;
		bool isEnabled = table.TryGetValue("enabled", out value) && value is bool && (bool)value;
		return new CloudRedirectOpenSteamToolConfigStatus(IsValid: true, isEnabled, IsCloudReady(table), IsCloudReady(table) ? "BetterSteamTools cloud host đã bật." : "BetterSteamTools cloud host cần cấu hình.");
	}

	internal static string SetCloudEnabled(string? content, bool enabled)
	{
		TomlTable model = Parse(content ?? string.Empty);
		TomlTable table = GetTable(model, "cloud");
		if (table.TryGetValue("enabled", out object value) && value is bool flag && flag == enabled)
		{
			return content;
		}
		table["enabled"] = enabled;
		return Toml.FromModel(model);
	}

	private static bool IsCloudReady(TomlTable cloud)
	{
		if (cloud.TryGetValue("enabled", out object value) && value is bool && (bool)value && cloud.TryGetValue("library", out object value2) && value2 is string a)
		{
			return string.Equals(a, "cloud_redirect.dll", StringComparison.OrdinalIgnoreCase);
		}
		return false;
	}

	private static bool IsCloudDisabled(TomlTable cloud)
	{
		if (cloud.TryGetValue("enabled", out object value))
		{
			if (value is bool)
			{
				return !(bool)value;
			}
			return false;
		}
		return false;
	}

	internal static IReadOnlyList<string> MergeLuaPaths(IEnumerable<string> paths, string steamDirectory)
	{
		string fullPath = Path.GetFullPath(steamDirectory);
		List<string> list = new List<string>();
		HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (string item in paths.Append(Path.Combine(fullPath, "config", "lua")))
		{
			if (string.IsNullOrWhiteSpace(item))
			{
				throw new InvalidDataException("Đường dẫn Lua trống; giữ nguyên cấu hình để kiểm tra.");
			}
			string fullPath2;
			try
			{
				fullPath2 = Path.GetFullPath(item.Replace('/', Path.DirectorySeparatorChar), fullPath);
			}
			catch (Exception ex) when (((ex is ArgumentException || ex is NotSupportedException || ex is PathTooLongException) ? 1 : 0) != 0)
			{
				throw new InvalidDataException("Đường dẫn Lua không hợp lệ; giữ nguyên cấu hình để kiểm tra.");
			}
			if (hashSet.Add(Path.TrimEndingDirectorySeparator(fullPath2)))
			{
				list.Add(item);
			}
		}
		return list;
	}

	private static TomlTable Parse(string content)
	{
		try
		{
			return Toml.ToModel(content);
		}
		catch (Exception ex) when (((ex is TomlException || ex is ArgumentException) ? 1 : 0) != 0)
		{
			throw new InvalidDataException("opensteamtool.toml không hợp lệ; launcher giữ nguyên cấu hình hiện có.");
		}
	}

	private static TomlTable GetTable(TomlTable model, string name)
	{
		if (!model.TryGetValue(name, out object value))
		{
			return (TomlTable)(model[name] = new TomlTable());
		}
		return (value as TomlTable) ?? throw new InvalidDataException("Section [" + name + "] không hợp lệ.");
	}

	private static string[] ReadPaths(TomlTable lua)
	{
		if (!lua.TryGetValue("paths", out object value))
		{
			return Array.Empty<string>();
		}
		if (!(value is TomlArray source) || source.Any((object path) => !(path is string)))
		{
			throw new InvalidDataException("[lua].paths phải là danh sách đường dẫn.");
		}
		return source.Cast<string>().ToArray();
	}

	private static bool SetDisabled(TomlTable model, string name)
	{
		TomlTable table = GetTable(model, name);
		object value;
		bool result = !table.TryGetValue("enabled", out value) || !(value is bool) || (bool)value;
		table["enabled"] = false;
		return result;
	}

	private static bool IsDisabled(TomlTable model, string name)
	{
		if (model.TryGetValue(name, out object value) && value is TomlTable tomlTable && tomlTable.TryGetValue("enabled", out object value2))
		{
			if (value2 is bool)
			{
				return !(bool)value2;
			}
			return false;
		}
		return false;
	}
}
