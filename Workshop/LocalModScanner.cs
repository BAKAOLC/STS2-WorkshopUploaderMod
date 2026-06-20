using System.Text.Json;
using System.Text.Json.Serialization;
using MegaCrit.Sts2.Core.Modding;

namespace STS2WorkshopUploader.Workshop;

internal static class LocalModScanner
{
    public static List<LocalModInfo> Scan(string modsRoot)
    {
        var found = new Dictionary<string, LocalModInfo>(StringComparer.OrdinalIgnoreCase);

        foreach (var mod in ModManager.Mods)
        {
            if (mod.modSource != ModSource.ModsDirectory || mod.manifest?.id == null)
                continue;

            found[Path.GetFullPath(mod.path)] = new LocalModInfo(
                mod.manifest.id,
                mod.manifest.name ?? mod.manifest.id,
                Path.GetFullPath(mod.path),
                "Loaded local",
                mod.state.ToString(),
                mod.state == ModLoadState.Loaded);
        }

        var root = string.IsNullOrWhiteSpace(modsRoot) ? WorkshopPaths.ResolveDefaultModsRoot() : modsRoot;
        if (Directory.Exists(root))
            ScanDirectory(root, found);

        return found.Values
            .OrderByDescending(mod => mod.IsLoaded)
            .ThenBy(mod => mod.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(mod => mod.Path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void ScanDirectory(string root, Dictionary<string, LocalModInfo> found)
    {
        foreach (var manifest in Directory.EnumerateFiles(root, "mod_manifest.json", SearchOption.AllDirectories))
        {
            var modPath = Path.GetDirectoryName(manifest);
            if (string.IsNullOrWhiteSpace(modPath))
                continue;

            var fullPath = Path.GetFullPath(modPath);
            if (found.ContainsKey(fullPath))
                continue;

            try
            {
                var text = File.ReadAllText(manifest);
                var model = JsonSerializer.Deserialize<LooseManifest>(text, WorkshopJson.Options);
                if (string.IsNullOrWhiteSpace(model?.Id))
                    continue;

                found[fullPath] = new LocalModInfo(
                    model.Id,
                    string.IsNullOrWhiteSpace(model.Name) ? model.Id : model.Name,
                    fullPath,
                    "Local",
                    "Not loaded",
                    false);
            }
            catch (Exception ex)
            {
                Main.Logger.Warn($"Failed to read local mod manifest '{manifest}': {ex.Message}");
            }
        }
    }

    private sealed class LooseManifest
    {
        [JsonPropertyName("id")] public string? Id { get; set; }

        [JsonPropertyName("name")] public string? Name { get; set; }
    }
}