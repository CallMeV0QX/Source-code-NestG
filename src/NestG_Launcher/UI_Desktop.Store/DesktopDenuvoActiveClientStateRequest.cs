namespace UI_Desktop.Store;

internal sealed class DesktopDenuvoActiveClientStateRequest
{
	public string? RealtimeConnectionId { get; set; }

	public string? GameInstallPath { get; set; }

	public string? ClientMachineIdHash { get; set; }

	public string? ClientVersion { get; set; }

	public int? AppliedRevisionNumber { get; set; }

	public string? PackageSha256 { get; set; }

	public string? Message { get; set; }
}
