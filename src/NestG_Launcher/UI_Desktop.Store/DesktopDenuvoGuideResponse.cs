using System;
using System.Collections.Generic;

namespace UI_Desktop.Store;

internal sealed class DesktopDenuvoGuideResponse
{
	public string Title { get; set; } = string.Empty;

	public string Intro { get; set; } = string.Empty;

	public List<DesktopDenuvoGuideStepResponse> Steps { get; set; } = new List<DesktopDenuvoGuideStepResponse>();

	public List<DesktopDenuvoGuideImageResponse> Images { get; set; } = new List<DesktopDenuvoGuideImageResponse>();

	public DateTime UpdatedAt { get; set; }
}
