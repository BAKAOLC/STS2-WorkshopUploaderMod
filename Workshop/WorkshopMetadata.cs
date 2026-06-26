using System.Text.Json.Serialization;

namespace STS2WorkshopUploader.Workshop;

internal sealed class WorkshopMetadata
{
    [JsonPropertyName("workshopItemId")] public ulong? WorkshopItemId { get; set; }

    [JsonPropertyName("title")] public string? Title { get; set; }

    [JsonPropertyName("description")] public string? Description { get; set; }

    [JsonPropertyName("visibility")] public string? Visibility { get; set; } = "private";

    [JsonPropertyName("changeNote")] public string? ChangeNote { get; set; }

    [JsonPropertyName("tags")] public List<string> Tags { get; set; } = [];

    [JsonPropertyName("dependencies")] public List<ulong> Dependencies { get; set; } = [];

    [JsonPropertyName("minBranch")] public string? MinBranch { get; set; }

    [JsonPropertyName("maxBranch")] public string? MaxBranch { get; set; }

    [JsonPropertyName("update")] public WorkshopUpdateSelection Update { get; set; } = new();

    [JsonPropertyName("localized")]
    public Dictionary<string, LocalizedWorkshopText> Localized { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    [JsonPropertyName("exclude")] public List<string> Exclude { get; set; } = [];

    [JsonPropertyName("openWorkshopAfterUpload")]
    public bool OpenWorkshopAfterUpload { get; set; }
}

internal sealed class LocalizedWorkshopText
{
    [JsonPropertyName("title")] public string? Title { get; set; }

    [JsonPropertyName("description")] public string? Description { get; set; }
}

internal sealed class WorkshopUpdateSelection
{
    [JsonPropertyName("title")] public bool Title { get; set; } = true;

    [JsonPropertyName("description")] public bool Description { get; set; } = true;

    [JsonPropertyName("visibility")] public bool Visibility { get; set; } = true;

    [JsonPropertyName("tags")] public bool Tags { get; set; } = true;

    [JsonPropertyName("dependencies")] public bool Dependencies { get; set; } = true;

    [JsonPropertyName("gameVersions")] public bool GameVersions { get; set; } = true;

    [JsonPropertyName("preview")] public bool Preview { get; set; } = true;

    [JsonPropertyName("content")] public bool Content { get; set; } = true;

    [JsonPropertyName("forceContent")] public bool ForceContent { get; set; }

    [JsonPropertyName("localized")] public bool Localized { get; set; } = true;
}