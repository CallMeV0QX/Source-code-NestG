using System;
using System.Collections.Generic;

namespace app_desktop.Services;

internal sealed class DesktopDenuvoActiveTokenAppliedEventArgs(Guid activeRequestId, Guid assignmentId, Guid libraryId, Guid gameId, string gameName, string targetDirectory, string? configFullPath, string? packageVersion, int? configRevisionNumber, int writtenFileCount, int overwrittenFileCount, string? mainExeRelativePath, string? gameExePath, IReadOnlyList<string> messages, DateTime appliedAtUtc, DateTime cooldownUntilUtc) : EventArgs()
{
	public Guid ActiveRequestId { get; } = activeRequestId;

	public Guid AssignmentId { get; } = assignmentId;

	public Guid LibraryId { get; } = libraryId;

	public Guid GameId { get; } = gameId;

	public string GameName { get; } = gameName;

	public string TargetDirectory { get; } = targetDirectory;

	public string? ConfigFullPath { get; } = configFullPath;

	public string? PackageVersion { get; } = packageVersion;

	public int? ConfigRevisionNumber { get; } = configRevisionNumber;

	public int WrittenFileCount { get; } = writtenFileCount;

	public int OverwrittenFileCount { get; } = overwrittenFileCount;

	public string? MainExeRelativePath { get; } = mainExeRelativePath;

	public string? GameExePath { get; } = gameExePath;

	public IReadOnlyList<string> Messages { get; } = messages;

	public DateTime AppliedAtUtc { get; } = appliedAtUtc;

	public DateTime CooldownUntilUtc { get; } = cooldownUntilUtc;

	public bool Handled { get; set; }
}
