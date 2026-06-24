namespace STS2WorkshopUploader.Workshop;

internal static class WorkshopMetadataEditor
{
    public static WorkshopMetadata LoadOrCreate(LocalModInfo mod)
    {
        return WorkshopTemplateService.EnsureTemplate(mod);
    }

    public static void AddDependency(LocalModInfo mod, ulong itemId)
    {
        var metadata = LoadOrCreate(mod);
        if (metadata.Dependencies.Contains(itemId))
            return;

        metadata.Dependencies.Add(itemId);
        metadata.Dependencies.Sort();
        WorkshopJson.Write(WorkshopPaths.MetadataFile(mod.Path), metadata);
    }

    public static void RemoveDependency(LocalModInfo mod, ulong itemId)
    {
        var metadata = LoadOrCreate(mod);
        if (!metadata.Dependencies.Remove(itemId))
            return;

        WorkshopJson.Write(WorkshopPaths.MetadataFile(mod.Path), metadata);
    }
}