using System;
using System.Runtime.CompilerServices;
using System.Text;

namespace app_desktop.Services;

internal sealed record GameActivationProgress(GameActivationProgressStage Stage, string Message, int? Percentage = null)
{
	internal int OverallPercentage
	{
		get
		{
			int? percentage = Percentage;
			if (percentage.HasValue)
			{
				int valueOrDefault = percentage.GetValueOrDefault();
				return Math.Clamp(valueOrDefault, 0, 100);
			}
			return Stage switch
			{
				GameActivationProgressStage.Checking => 10, 
				GameActivationProgressStage.PreparingActivation => 30, 
				GameActivationProgressStage.PreparingCloudRedirect => 55, 
				GameActivationProgressStage.RestartingSteam => 75, 
				GameActivationProgressStage.Verifying => 90, 
				GameActivationProgressStage.Completed => 100, 
				_ => 10, 
			};
		}
	}

	[CompilerGenerated]
	private bool PrintMembers(StringBuilder builder)
	{
		RuntimeHelpers.EnsureSufficientExecutionStack();
		builder.Append("Stage = ");
		builder.Append(Stage.ToString());
		builder.Append(", Message = ");
		builder.Append((object?)Message);
		builder.Append(", Percentage = ");
		builder.Append(Percentage.ToString());
		return true;
	}
}
