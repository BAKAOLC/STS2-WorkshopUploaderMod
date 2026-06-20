namespace STS2WorkshopUploader.Workshop;

internal static class WorkshopUploadPlanner
{
    public static WorkshopUploadPlan Create(LocalModInfo mod, WorkshopUploadMode mode)
    {
        var metadata = WorkshopTemplateService.LoadEffectiveMetadata(mod);
        var state = WorkshopJson.Read<WorkshopUploadState>(WorkshopPaths.StateFile(mod.Path)) ??
                    new WorkshopUploadState();
        if (metadata.WorkshopItemId == null && state.WorkshopItemId != null)
            metadata.WorkshopItemId = state.WorkshopItemId;

        var contentFiles = StagingBuilder.EnumerateIncludedFiles(mod, metadata);
        string? staging = null;
        if (mode == WorkshopUploadMode.Full && metadata.Update.Content)
            staging = StagingBuilder.Build(mod, metadata);

        var fingerprints = BuildFingerprints(mod, metadata, mode, staging, contentFiles);
        var changed = fingerprints
            .Where(pair => !state.Fingerprints.TryGetValue(pair.Key, out var old) ||
                           !string.Equals(old, pair.Value, StringComparison.Ordinal))
            .Select(pair => pair.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return new WorkshopUploadPlan
        {
            Mod = mod,
            Metadata = metadata,
            State = state,
            Mode = mode,
            Fingerprints = fingerprints,
            ChangedKeys = changed,
            ContentFiles = contentFiles,
            StagingPath = staging
        };
    }

    private static Dictionary<string, string> BuildFingerprints(
        LocalModInfo mod,
        WorkshopMetadata metadata,
        WorkshopUploadMode mode,
        string? staging,
        IReadOnlyList<ContentPackageFile> contentFiles)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["title"] = WorkshopFingerprint.Text(metadata.Title),
            ["description"] = WorkshopFingerprint.Text(metadata.Description),
            ["visibility"] = WorkshopFingerprint.Text(metadata.Visibility),
            ["tags"] = WorkshopFingerprint.Text(
                string.Join('\n', metadata.Tags.Order(StringComparer.OrdinalIgnoreCase))),
            ["dependencies"] = WorkshopFingerprint.Text(string.Join('\n', metadata.Dependencies.Order())),
            ["gameVersions"] = WorkshopFingerprint.Text($"{metadata.MinBranch}\n{metadata.MaxBranch}"),
            ["preview"] = WorkshopFingerprint.File(WorkshopPaths.PreviewFile(mod.Path))
        };

        result["content"] = WorkshopFingerprint.ContentManifest(contentFiles);

        foreach (var (language, text) in metadata.Localized)
        {
            result[$"localized:{language}:title"] = WorkshopFingerprint.Text(text.Title);
            result[$"localized:{language}:description"] = WorkshopFingerprint.Text(text.Description);
        }

        return result;
    }
}