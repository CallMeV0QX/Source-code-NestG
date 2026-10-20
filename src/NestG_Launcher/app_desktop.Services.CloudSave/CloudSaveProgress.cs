namespace app_desktop.Services.CloudSave;

internal sealed record CloudSaveProgress(CloudSaveProgressStage Stage, string Message, int Percentage);
