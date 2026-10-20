using System;
using System.Collections.Generic;

namespace app_desktop.Services.SteamConnection;

internal sealed record SteamConnectionUiState(bool AutoEnable, string? Feedback = null)
{
	public string? Feedback { get; set; } = Feedback;
	public bool CleanupPending { get; set; }

	public string? ErrorCode { get; set; }

	public string? ErrorStage { get; set; }

	public bool DnsEnabled { get; set; }

	public bool DpiEnabled { get; set; }

	public bool? WarpConnected { get; set; }

	public bool IndependentVerified { get; set; }

	public string Kind { get; set; } = "Unavailable";

	public bool Enabled { get; set; }

	public bool CanToggle { get; set; }

	public bool Busy { get; set; }

	public string Label { get; set; } = "Chưa tích hợp";

	public string Detail { get; set; } = "Bộ hỗ trợ kết nối Steam chưa được tích hợp.";

	public IReadOnlyDictionary<string, string> Checks { get; set; } = new Dictionary<string, string>();

	public IReadOnlyDictionary<string, string> Reasons { get; set; } = new Dictionary<string, string>();

	public DateTimeOffset? CheckedAt { get; set; }

	public string? OperationId { get; set; }

	public long Revision { get; set; }
}
