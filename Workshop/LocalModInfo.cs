namespace STS2WorkshopUploader.Workshop;

internal sealed record LocalModInfo(
    string Id,
    string Name,
    string Path,
    string Source,
    string LoadState,
    bool IsLoaded);