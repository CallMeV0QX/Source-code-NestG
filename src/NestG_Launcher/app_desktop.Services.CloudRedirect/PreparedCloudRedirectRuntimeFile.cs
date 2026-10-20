namespace app_desktop.Services.CloudRedirect;

internal sealed record PreparedCloudRedirectRuntimeFile(string SourcePath, string RelativePath, string InstallPath, long Size, string Sha256);
