namespace STS2WorkshopUploader.Workshop;

internal static class WorkshopMetadataEditor
{
    public static WorkshopMetadata LoadOrCreate(LocalModInfo mod)
    {
        return WorkshopTemplateService.EnsureTemplate(mod);
    }

    public static bool AddDependency(LocalModInfo mod, ulong itemId)
    {
        var metadata = LoadOrCreate(mod);
        if (metadata.Dependencies.Contains(itemId))
            return false;

        metadata.Dependencies.Add(itemId);
        metadata.Dependencies.Sort();
        WorkshopJson.Write(WorkshopPaths.MetadataFile(mod.Path), metadata);
        return true;
    }

    public static bool RemoveDependency(LocalModInfo mod, ulong itemId)
    {
        var metadata = LoadOrCreate(mod);
        if (!metadata.Dependencies.Remove(itemId))
            return false;

        WorkshopJson.Write(WorkshopPaths.MetadataFile(mod.Path), metadata);
        return true;
    }
}