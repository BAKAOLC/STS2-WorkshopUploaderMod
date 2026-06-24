using System.Text.Json.Serialization;

namespace STS2WorkshopUploader.Workshop;

internal static class WorkshopTemplateService
{
    public static WorkshopMetadata EnsureTemplate(LocalModInfo mod)
    {
        Directory.CreateDirectory(WorkshopPaths.RecordDirectory(mod.Path));
        WorkshopPaths.MigrateLegacyRecordFiles(mod.Path);

        var metadataPath = WorkshopPaths.MetadataFile(mod.Path);
        var metadata = WorkshopJson.Read<WorkshopMetadata>(metadataPath);
        if (metadata == null)
        {
            metadata = CreateDefaultMetadata(mod);
            WorkshopJson.Write(metadataPath, metadata);
        }

        EnsureTextFile(WorkshopPaths.TitleFile(mod.Path), metadata.Title ?? mod.Name);
        EnsureTextFile(WorkshopPaths.DescriptionMarkdownFile(mod.Path), LoadManifestDescription(mod.Path));
        EnsureTextFile(WorkshopPaths.ChangeNoteMarkdownFile(mod.Path), string.Empty);
        Directory.CreateDirectory(WorkshopPaths.LocalizedDirectory(mod.Path));

        return metadata;
    }

    public static WorkshopMetadata LoadEffectiveMetadata(LocalModInfo mod)
    {
        var metadata = EnsureTemplate(mod);

        var title = ReadOptional(WorkshopPaths.TitleFile(mod.Path));
        if (!string.IsNullOrWhiteSpace(title))
            metadata.Title = title.Trim();

        var description = ReadOptional(WorkshopPaths.DescriptionMarkdownFile(mod.Path));
        if (!string.IsNullOrWhiteSpace(description))
            metadata.Description = MarkdownToSteamBbCode.Convert(description);

        var changeNote = ReadOptional(WorkshopPaths.ChangeNoteMarkdownFile(mod.Path));
        if (!string.IsNullOrWhiteSpace(changeNote))
            metadata.ChangeNote = MarkdownToSteamBbCode.Convert(changeNote);

        foreach (var entry in LoadLocalized(mod.Path))
            metadata.Localized[entry.Key] = entry.Value;

        return metadata;
    }

    private static WorkshopMetadata CreateDefaultMetadata(LocalModInfo mod)
    {
        return new WorkshopMetadata
        {
            Title = mod.Name,
            Description = LoadManifestDescription(mod.Path),
            Visibility = "private",
            Tags = ["Mod"],
            Dependencies = [],
            Exclude =
            [
                WorkshopPaths.RecordDirectoryName,
                ".git",
                ".github",
                ".idea",
                ".godot",
                "bin",
                "obj",
                "*.csproj",
                "*.sln",
                "*.user",
                "project.godot",
                "export_presets.cfg",
                "local.props",
                "local.props.template"
            ]
        };
    }

    private static Dictionary<string, LocalizedWorkshopText> LoadLocalized(string modPath)
    {
        var result = new Dictionary<string, LocalizedWorkshopText>(StringComparer.OrdinalIgnoreCase);
        var root = WorkshopPaths.LocalizedDirectory(modPath);
        if (!Directory.Exists(root))
            return result;

        foreach (var dir in Directory.EnumerateDirectories(root))
        {
            var language = WorkshopLanguage.Normalize(Path.GetFileName(dir));
            var title = ReadOptional(Path.Combine(dir, WorkshopPaths.TitleFileName));
            var descriptionMarkdown = ReadOptional(Path.Combine(dir, WorkshopPaths.DescriptionMarkdownFileName));
            if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(descriptionMarkdown))
                continue;

            result[language] = new LocalizedWorkshopText
            {
                Title = string.IsNullOrWhiteSpace(title) ? null : title.Trim(),
                Description = string.IsNullOrWhiteSpace(descriptionMarkdown)
                    ? null
                    : MarkdownToSteamBbCode.Convert(descriptionMarkdown)
            };
        }

        return result;
    }

    private static void EnsureTextFile(string path, string value)
    {
        if (File.Exists(path))
            return;

        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllText(path, value);
    }

    private static string ReadOptional(string path)
    {
        return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
    }

    private static string LoadManifestDescription(string modPath)
    {
        var manifestPath = Path.Combine(modPath, "mod_manifest.json");
        if (!File.Exists(manifestPath))
            return string.Empty;

        try
        {
            var manifest = WorkshopJson.Read<LooseManifest>(manifestPath);
            return manifest?.Description ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private sealed class LooseManifest
    {
        [JsonPropertyName("description")] public string? Description { get; set; }
    }
}