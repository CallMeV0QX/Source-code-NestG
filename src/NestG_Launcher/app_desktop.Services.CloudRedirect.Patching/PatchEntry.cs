namespace app_desktop.Services.CloudRedirect.Patching;

internal record PatchEntry(int Offset, byte[] Original, byte[] Replacement);
