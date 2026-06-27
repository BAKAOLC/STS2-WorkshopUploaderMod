using Godot;

namespace STS2WorkshopUploader.Workshop;

internal static class WorkshopPaths
{
    public const string RecordDirectoryName = ".sts2-workshop-uploader";
    private const string MetadataFileName = "workshop.workshop";
    private const string StateFileName = "state.workshop";
    private const string LegacyMetadataFileName = "workshop.json";
    private const string LegacyStateFileName = "state.json";
    private const string PreviewFileName = "preview.png";
    private const string AdditionalPreviewsDirectoryName = "additional_previews";
    public const string PlaceholderPreviewResourceName = "STS2WorkshopUploader.Data.workshop_placeholder.png";
    public const string TitleFileName = "title.txt";
    public const string DescriptionMarkdownFileName = "description.md";
    private const string ChangeNoteMarkdownFileName = "change_note.md";
    private const string LocalizedDirectoryName = "localized";

    public static string ResolveDefaultModsRoot()
    {
        var executable = OS.GetExecutablePath();
        var gameRoot = Path.GetDirectoryName(executable);
        return string.IsNullOrWhiteSpace(gameRoot) ? string.Empty : Path.Combine(gameRoot, "mods");
    }

    public static string RecordDirectory(string modPath)
    {
        return Path.Combine(modPath, RecordDirectoryName);
    }

    public static string MetadataFile(string modPath)
    {
        return Path.Combine(RecordDirectory(modPath), MetadataFileName);
    }

    private static string LegacyMetadataFile(string modPath)
    {
        return Path.Combine(RecordDirectory(modPath), LegacyMetadataFileName);
    }

    public static string StateFile(string modPath)
    {
        return Path.Combine(RecordDirectory(modPath), StateFileName);
    }

    private static string LegacyStateFile(string modPath)
    {
        return Path.Combine(RecordDirectory(modPath), LegacyStateFileName);
    }

    public static void MigrateLegacyRecordFiles(string modPath)
    {
        MoveLegacyFile(LegacyMetadataFile(modPath), MetadataFile(modPath));
        MoveLegacyFile(LegacyStateFile(modPath), StateFile(modPath));
    }

    private static void MoveLegacyFile(string oldPath, string newPath)
    {
        if (!File.Exists(oldPath) || File.Exists(newPath))
            return;

        var dir = Path.GetDirectoryName(newPath);
        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);
        File.Move(oldPath, newPath);
    }

    public static string PreviewFile(string modPath)
    {
        return Path.Combine(RecordDirectory(modPath), PreviewFileName);
    }

    public static string AdditionalPreviewsDirectory(string modPath)
    {
        return Path.Combine(RecordDirectory(modPath), AdditionalPreviewsDirectoryName);
    }

    public static string AdditionalPreviewFile(string modPath, string fileName)
    {
        return Path.Combine(AdditionalPreviewsDirectory(modPath), NormalizeAdditionalPreviewFileName(fileName));
    }

    public static string NormalizeAdditionalPreviewFileName(string fileName)
    {
        var name = Path.GetFileName(fileName);
        foreach (var invalid in Path.GetInvalidFileNameChars())
            name = name.Replace(invalid, '_');
        return string.IsNullOrWhiteSpace(name) ? "preview.png" : name;
    }

    public static string TitleFile(string modPath)
    {
        return Path.Combine(RecordDirectory(modPath), TitleFileName);
    }

    public static string DescriptionMarkdownFile(string modPath)
    {
        return Path.Combine(RecordDirectory(modPath), DescriptionMarkdownFileName);
    }

    public static string ChangeNoteMarkdownFile(string modPath)
    {
        return Path.Combine(RecordDirectory(modPath), ChangeNoteMarkdownFileName);
    }

    public static string LocalizedDirectory(string modPath)
    {
        return Path.Combine(RecordDirectory(modPath), LocalizedDirectoryName);
    }

    private static string StagingRoot()
    {
        return ProjectSettings.GlobalizePath($"user://mod_data/{Const.ModId}/staging");
    }

    public static string StagingDirectory(string modId)
    {
        var safeId = string.Join("_", modId.Split(Path.GetInvalidFileNameChars()));
        return Path.Combine(StagingRoot(), safeId);
    }

    public static string DisplayPath(string path)
    {
        return string.IsNullOrWhiteSpace(path) ? "(not set)" : path;
    }
}
