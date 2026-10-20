namespace app_desktop.Services.CloudRedirect.Patching;

internal class PatchResult
{
	public bool Succeeded { get; set; }

	public bool DllPatched { get; set; }

	public bool CachePatched { get; set; }

	public string Error { get; set; }

	public PatchResult Fail(string error)
	{
		Succeeded = false;
		Error = error;
		return this;
	}
}
