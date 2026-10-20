using System;
using System.Collections.Generic;

namespace app_desktop.Services.CloudRedirect;

internal sealed class CloudRedirectInstallTransaction
{
	public int SchemaVersion { get; set; }

	public string TransactionId { get; set; } = string.Empty;

	public string LocalAppDataDirectory { get; set; } = string.Empty;

	public string RoamingAppDataDirectory { get; set; } = string.Empty;

	public List<SteamActivationFileStamp> ActivationFiles { get; set; } = new List<SteamActivationFileStamp>();

	public bool BetterOnly { get; set; }

	public bool ActivationReceipt { get; set; }

	public string BundleId { get; set; } = "opensteamtool-cloud";

	public string Operation { get; set; } = "install";

	public string BundleVersion { get; set; } = string.Empty;

	public string SteamDirectory { get; set; } = string.Empty;

	public DateTime CreatedAtUtc { get; set; }

	public DateTime? CompletedAtUtc { get; set; }

	public string Status { get; set; } = string.Empty;

	public string? Error { get; set; }

	public List<string> RollbackErrors { get; set; } = new List<string>();

	public List<CloudRedirectInstallTransactionEntry> Entries { get; set; } = new List<CloudRedirectInstallTransactionEntry>();
}
