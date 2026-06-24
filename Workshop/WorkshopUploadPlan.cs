namespace STS2WorkshopUploader.Workshop;

internal sealed class WorkshopUploadPlan
{
    public required LocalModInfo Mod { get; init; }
    public required WorkshopMetadata Metadata { get; init; }
    public required WorkshopUploadState State { get; init; }
    public required WorkshopUploadMode Mode { get; init; }
    public required Dictionary<string, string> Fingerprints { get; init; }
    public required HashSet<string> ChangedKeys { get; init; }
    public required IReadOnlyList<ContentPackageFile> ContentFiles { get; init; }
    public string? StagingPath { get; init; }
    public bool HasWorkshopItem => Metadata.WorkshopItemId != null || State.WorkshopItemId != null;

    public bool Changed(string key)
    {
        return ChangedKeys.Contains(key);
    }
}