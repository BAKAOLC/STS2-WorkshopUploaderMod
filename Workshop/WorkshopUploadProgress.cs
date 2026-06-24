namespace STS2WorkshopUploader.Workshop;

internal sealed record WorkshopUploadProgress(
    WorkshopUploadProgressOperation Operation,
    WorkshopItemUpdateProgressStatus Status,
    ulong BytesProcessed,
    ulong BytesTotal,
    string? Language = null);

internal enum WorkshopUploadProgressOperation
{
    MainUpdate,
    LocalizedUpdate
}

internal enum WorkshopItemUpdateProgressStatus
{
    Invalid,
    PreparingConfig,
    PreparingContent,
    UploadingContent,
    UploadingPreviewFile,
    CommittingChanges
}