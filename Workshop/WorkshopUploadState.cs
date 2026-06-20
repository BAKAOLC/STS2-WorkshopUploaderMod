using System.Text.Json.Serialization;

namespace STS2WorkshopUploader.Workshop;

internal sealed class WorkshopUploadState
{
    [JsonPropertyName("workshopItemId")] public ulong? WorkshopItemId { get; set; }

    [JsonPropertyName("fingerprints")]
    public Dictionary<string, string> Fingerprints { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    [JsonPropertyName("lastUploadedUtc")] public DateTimeOffset? LastUploadedUtc { get; set; }

    [JsonPropertyName("contentFiles")]
    public Dictionary<string, ContentPackageFileState> ContentFiles { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
}

internal sealed class ContentPackageFileState
{
    [JsonPropertyName("hash")] public string Hash { get; set; } = string.Empty;

    [JsonPropertyName("size")] public long Size { get; set; }

    [JsonPropertyName("lastWriteUtc")] public DateTimeOffset LastWriteUtc { get; set; }
}