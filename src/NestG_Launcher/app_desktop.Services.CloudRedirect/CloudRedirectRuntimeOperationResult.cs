namespace app_desktop.Services.CloudRedirect;

internal sealed record CloudRedirectRuntimeOperationResult(bool Succeeded, string Message, string BackupDirectory, string TransactionPath);
