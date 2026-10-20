using System;

namespace app_desktop.Services.CloudRedirect;

internal sealed class CloudRedirectReadinessException(CloudRedirectReadinessResult readiness) : InvalidOperationException(readiness.Message)
{
	internal CloudRedirectReadinessResult Readiness { get; } = readiness;
}
