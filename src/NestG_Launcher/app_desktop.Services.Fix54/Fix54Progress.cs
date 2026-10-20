namespace app_desktop.Services.Fix54;

internal sealed record Fix54Progress(string Stage, string Message, int? Percent = null);
