namespace STS2WorkshopUploader.Workshop;

internal static class StagingBuilder
{
    private static readonly string[] DefaultExcludedFileNames =
    [
        ".gitignore",
        ".gitattributes",
        "project.godot",
        "export_presets.cfg",
        "local.props",
        "local.props.template"
    ];

    public static string Build(LocalModInfo mod, WorkshopMetadata metadata)
    {
        var target = WorkshopPaths.StagingDirectory(mod.Id);
        if (Directory.Exists(target))
            Directory.Delete(target, true);
        Directory.CreateDirectory(target);

        foreach (var file in Directory.EnumerateFiles(mod.Path, "*", SearchOption.AllDirectories))
        {
            if (ShouldExclude(mod.Path, file, metadata.Exclude))
                continue;

            var rel = Path.GetRelativePath(mod.Path, file);
            var dest = Path.Combine(target, rel);
            var destDir = Path.GetDirectoryName(dest);
            if (!string.IsNullOrWhiteSpace(destDir))
                Directory.CreateDirectory(destDir);
            File.Copy(file, dest, true);
        }

        return target;
    }

    public static IReadOnlyList<ContentPackageFile> EnumerateIncludedFiles(LocalModInfo mod, WorkshopMetadata metadata)
    {
        return Directory.EnumerateFiles(mod.Path, "*", SearchOption.AllDirectories)
            .Where(file => !ShouldExclude(mod.Path, file, metadata.Exclude))
            .Select(file => CreateContentPackageFile(mod.Path, file))
            .OrderBy(file => file.Path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static IReadOnlyList<ContentPackageFile> EnumerateDirectoryFiles(string root)
    {
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
            return [];

        return Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Select(file => CreateContentPackageFile(root, file))
            .OrderBy(file => file.Path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static ContentPackageFile CreateContentPackageFile(string root, string file)
    {
        var info = new FileInfo(file);
        return new ContentPackageFile(
            Path.GetRelativePath(root, file).Replace('\\', '/'),
            info.Length,
            info.LastWriteTimeUtc,
            WorkshopFingerprint.File(file));
    }

    private static bool ShouldExclude(string root, string path, IReadOnlyList<string> configuredExcludes)
    {
        var rel = Path.GetRelativePath(root, path).Replace('\\', '/');
        var segments = rel.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Any(IsDefaultExcludedDirectory))
            return true;

        var fileName = Path.GetFileName(path);
        if (DefaultExcludedFileNames.Contains(fileName, StringComparer.OrdinalIgnoreCase))
            return true;

        if (fileName.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) ||
            fileName.EndsWith(".sln", StringComparison.OrdinalIgnoreCase) ||
            fileName.EndsWith(".user", StringComparison.OrdinalIgnoreCase))
            return true;

        foreach (var pattern in configuredExcludes)
            if (MatchesPattern(rel, segments, pattern))
                return true;

        return false;
    }

    private static bool IsDefaultExcludedDirectory(string segment)
    {
        return segment.Equals(WorkshopPaths.RecordDirectoryName, StringComparison.OrdinalIgnoreCase) ||
               segment.Equals(".git", StringComparison.OrdinalIgnoreCase) ||
               segment.Equals(".github", StringComparison.OrdinalIgnoreCase) ||
               segment.Equals(".idea", StringComparison.OrdinalIgnoreCase) ||
               segment.Equals(".godot", StringComparison.OrdinalIgnoreCase) ||
               segment.Equals("bin", StringComparison.OrdinalIgnoreCase) ||
               segment.Equals("obj", StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesPattern(string rel, string[] segments, string pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
            return false;

        var normalized = pattern.Trim().Replace('\\', '/');
        if (segments.Any(segment => segment.Equals(normalized, StringComparison.OrdinalIgnoreCase)))
            return true;

        if (rel.Equals(normalized, StringComparison.OrdinalIgnoreCase) ||
            rel.StartsWith(normalized.TrimEnd('/') + "/", StringComparison.OrdinalIgnoreCase))
            return true;

        if (normalized.StartsWith("*.", StringComparison.Ordinal))
            return rel.EndsWith(normalized[1..], StringComparison.OrdinalIgnoreCase);

        return false;
    }
}

internal sealed record ContentPackageFile(string Path, long Size, DateTime LastWriteUtc, string Hash);