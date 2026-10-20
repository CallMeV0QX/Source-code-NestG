using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace app_desktop.Services.CloudRedirect;

internal sealed record CloudRedirectReadinessResult(CloudRedirectReadinessState State, CloudRedirectReadinessAction RecommendedAction, bool SteamFound, bool RuntimeInstalled, bool RuntimeTrusted, bool OpenSteamToolConfigReady, bool SteamConfigReady, string Provider, bool ProviderReady, bool AppRegistered, bool HasIncompleteTransaction, string IncompleteTransactionPath, string SteamDirectory, string Message, IReadOnlyList<string> Issues)
{
	internal RuntimeDllOwnershipIssue? OwnershipIssue { get; init; }

	internal bool IsReady => State == CloudRedirectReadinessState.Ready;

	internal CloudRedirectConditionSeverity Severity => RecommendedAction switch
	{
		CloudRedirectReadinessAction.NoChange => CloudRedirectConditionSeverity.Ready, 
		CloudRedirectReadinessAction.Blocked => CloudRedirectConditionSeverity.Blocked, 
		_ => CloudRedirectConditionSeverity.Degraded, 
	};

	internal static CloudRedirectReadinessResult Blocked(CloudRedirectReadinessState state, string message, string steamDirectory = "")
	{
		return new CloudRedirectReadinessResult(state, CloudRedirectReadinessAction.Blocked, state != CloudRedirectReadinessState.SteamMissing, RuntimeInstalled: false, RuntimeTrusted: false, OpenSteamToolConfigReady: false, SteamConfigReady: false, string.Empty, ProviderReady: false, AppRegistered: false, HasIncompleteTransaction: false, string.Empty, steamDirectory, message, new _003C_003Ez__ReadOnlySingleElementList<string>(message));
	}

	[CompilerGenerated]
	private bool PrintMembers(StringBuilder builder)
	{
		RuntimeHelpers.EnsureSufficientExecutionStack();
		builder.Append("State = ");
		builder.Append(State.ToString());
		builder.Append(", RecommendedAction = ");
		builder.Append(RecommendedAction.ToString());
		builder.Append(", SteamFound = ");
		builder.Append(SteamFound.ToString());
		builder.Append(", RuntimeInstalled = ");
		builder.Append(RuntimeInstalled.ToString());
		builder.Append(", RuntimeTrusted = ");
		builder.Append(RuntimeTrusted.ToString());
		builder.Append(", OpenSteamToolConfigReady = ");
		builder.Append(OpenSteamToolConfigReady.ToString());
		builder.Append(", SteamConfigReady = ");
		builder.Append(SteamConfigReady.ToString());
		builder.Append(", Provider = ");
		builder.Append((object?)Provider);
		builder.Append(", ProviderReady = ");
		builder.Append(ProviderReady.ToString());
		builder.Append(", AppRegistered = ");
		builder.Append(AppRegistered.ToString());
		builder.Append(", HasIncompleteTransaction = ");
		builder.Append(HasIncompleteTransaction.ToString());
		builder.Append(", IncompleteTransactionPath = ");
		builder.Append((object?)IncompleteTransactionPath);
		builder.Append(", SteamDirectory = ");
		builder.Append((object?)SteamDirectory);
		builder.Append(", Message = ");
		builder.Append((object?)Message);
		builder.Append(", Issues = ");
		builder.Append(Issues);
		return true;
	}
}
