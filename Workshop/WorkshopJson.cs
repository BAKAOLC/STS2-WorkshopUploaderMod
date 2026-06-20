using System.Text.Json;

namespace STS2WorkshopUploader.Workshop;

internal static class WorkshopJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static T? Read<T>(string path)
    {
        if (!File.Exists(path))
            return default;
        using var stream = File.OpenRead(path);
        return JsonSerializer.Deserialize<T>(stream, Options);
    }

    public static void Write<T>(string path, T value)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);

        using var stream = File.Create(path);
        JsonSerializer.Serialize(stream, value, Options);
    }
}