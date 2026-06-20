using System.Text;

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

    public string Describe()
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Mod: {Mod.Name} ({Mod.Id})");
        builder.AppendLine($"Path: {Mod.Path}");
        builder.AppendLine($"Mode: {Mode}");
        builder.AppendLine($"Workshop ID: {Metadata.WorkshopItemId ?? State.WorkshopItemId}");
        builder.AppendLine(
            $"Changed fields: {(ChangedKeys.Count == 0 ? "none" : string.Join(", ", ChangedKeys.Order()))}");
        if (!string.IsNullOrWhiteSpace(StagingPath))
            builder.AppendLine($"Staging: {StagingPath}");
        return builder.ToString().Trim();
    }
}