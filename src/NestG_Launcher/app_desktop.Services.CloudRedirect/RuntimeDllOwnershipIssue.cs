namespace app_desktop.Services.CloudRedirect;

internal sealed record RuntimeDllOwnershipIssue(string FileName, RuntimeDllOwnershipFailure Reason, string Message);
