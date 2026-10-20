using System;

namespace app_desktop.Services.Warp;

internal sealed class WarpOnboardingSession
{
	internal WarpOnboardingPhase Phase { get; private set; }

	internal void Observe(WarpConnectionStatus status, bool canOffer)
	{
		WarpOnboardingPhase phase = Phase;
		if ((uint)(phase - 3) > 1u)
		{
			if (status.Kind == WarpConnectionKind.Connected && status.IsConnected)
			{
				Phase = WarpOnboardingPhase.Completed;
			}
			else if (canOffer && Phase == WarpOnboardingPhase.Pending && status.Kind != WarpConnectionKind.Connecting)
			{
				Phase = WarpOnboardingPhase.Offered;
			}
		}
	}

	internal void StartGuide()
	{
		if (Phase == WarpOnboardingPhase.Offered)
		{
			Phase = WarpOnboardingPhase.Guiding;
		}
	}

	internal void Dismiss()
	{
		Phase = WarpOnboardingPhase.Dismissed;
	}

	internal void DocumentChanged()
	{
		WarpOnboardingPhase phase = Phase;
		if ((uint)(phase - 1) <= 1u)
		{
			Dismiss();
		}
	}

	internal static bool IsTrustedAction(string source, bool isHome, string action)
	{
		bool flag = isHome && string.Equals(source, "about:blank", StringComparison.OrdinalIgnoreCase);
		if (flag)
		{
			bool flag2 = ((action == "startWarpOnboarding" || action == "dismissWarpOnboarding") ? true : false);
			flag = flag2;
		}
		return flag;
	}
}
