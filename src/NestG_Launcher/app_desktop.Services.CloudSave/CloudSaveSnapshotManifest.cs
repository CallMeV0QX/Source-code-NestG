using System;
using System.Collections.Generic;

namespace app_desktop.Services.CloudSave;

internal sealed class CloudSaveSnapshotManifest
{
	public int FormatVersion { get; set; } = 1;

	public string SnapshotId { get; set; } = string.Empty;

	public string OperationId { get; set; } = string.Empty;

	public Guid NestGUserId { get; set; }

	public Guid LibraryId { get; set; }

	public string GameName { get; set; } = string.Empty;

	public uint SteamAccountId { get; set; }

	public int SteamAppId { get; set; }

	public ulong CloudRedirectChangeNumber { get; set; }

	public string CloudRedirectVersion { get; set; } = "2.6.5";

	public DateTimeOffset CreatedAtUtc { get; set; }

	public List<CloudSaveSnapshotFile> Files { get; set; } = new List<CloudSaveSnapshotFile>();

	public List<CloudSaveSnapshotMetadataFile> CloudRedirectMetadata { get; set; } = new List<CloudSaveSnapshotMetadataFile>();
}
