using System;
using System.Collections.Generic;
using System.IO;

namespace app_desktop.Services.Fix54;

internal sealed class Fix54PreparedRuntime : IDisposable
{
	internal string ScriptPath { get; }
	private readonly IReadOnlyList<FileStream>? leases;

	public Fix54PreparedRuntime(string scriptPath, IReadOnlyList<FileStream>? leases = null)
	{
		ScriptPath = scriptPath;
		this.leases = leases;
	}

	public void Dispose()
	{
		foreach (FileStream item in leases ?? Array.Empty<FileStream>())
		{
			item.Dispose();
		}
	}
}
