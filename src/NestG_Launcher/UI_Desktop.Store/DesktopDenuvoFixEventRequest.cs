namespace UI_Desktop.Store;

internal sealed class DesktopDenuvoFixEventRequest
{
	public string EventType { get; set; } = string.Empty;

	public int? ConfigRevisionNumber { get; set; }

	public string? PackageSha256 { get; set; }

	public string? GameInstallPath { get; set; }

	public string? ClientMachineIdHash { get; set; }

	public string? ClientVersion { get; set; }

	public string? Message { get; set; }
}
