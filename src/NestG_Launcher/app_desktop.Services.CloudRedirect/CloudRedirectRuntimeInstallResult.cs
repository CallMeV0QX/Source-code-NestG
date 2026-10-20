using System.Collections.Generic;

namespace app_desktop.Services.CloudRedirect;

internal sealed record CloudRedirectRuntimeInstallResult(IReadOnlyList<string> WrittenFiles, IReadOnlyList<string> Messages, string BackupDirectory, string SyncPath, string Provider, string TransactionPath)
{
	public CloudRedirectReadinessResult? InitialReadiness { get; init; }

	public CloudRedirectReadinessResult? FinalReadiness { get; init; }

	public CloudRedirectReadinessAction AppliedAction { get; init; }

	public bool RecoveredInterruptedTransaction { get; init; }

	public bool RuntimeChanged { get; init; }

	public bool ConfigurationChanged { get; init; }

	public bool ActivationChanged { get; init; }

	public bool RequiresSteamRestart { get; init; }
}
