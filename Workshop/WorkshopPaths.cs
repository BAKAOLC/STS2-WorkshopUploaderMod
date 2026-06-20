using Godot;
using MegaCrit.Sts2.Core.Modding;

namespace STS2WorkshopUploader.Workshop;

internal static class WorkshopPaths
{
    public const string RecordDirectoryName = ".sts2-workshop-uploader";
    public const string MetadataFileName = "workshop.json";
    public const string StateFileName = "state.json";
    public const string PreviewFileName = "preview.png";
    public const string PlaceholderPreviewResourceName = "STS2WorkshopUploader.Data.workshop_placeholder.png";
    public const string TitleFileName = "title.txt";
    public const string DescriptionMarkdownFileName = "description.md";
    public const string ChangeNoteMarkdownFileName = "change_note.md";
    public const string LocalizedDirectoryName = "localized";

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

    public static string StateFile(string modPath)
    {
        return Path.Combine(RecordDirectory(modPath), StateFileName);
    }

    public static string PreviewFile(string modPath)
    {
        return Path.Combine(RecordDirectory(modPath), PreviewFileName);
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

    public static string StagingRoot()
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

    public static IEnumerable<string> CandidateUploadRoots()
    {
        var localRoot = ResolveDefaultModsRoot();
        if (!string.IsNullOrWhiteSpace(localRoot))
            yield return localRoot;

        foreach (var mod in ModManager.Mods)
            if (!string.IsNullOrWhiteSpace(mod.path))
                yield return mod.path;
    }
}