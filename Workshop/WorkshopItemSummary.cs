namespace STS2WorkshopUploader.Workshop;

internal sealed record WorkshopItemSummary(
    ulong Id,
    string Title,
    string Summary,
    string? Visibility,
    string Description,
    IReadOnlyList<string> Tags,
    string? PreviewUrl,
    ulong OwnerSteamId);

internal sealed record WorkshopEditPermission(
    ulong ItemId,
    ulong OwnerSteamId,
    ulong CurrentSteamId,
    bool CanEdit);