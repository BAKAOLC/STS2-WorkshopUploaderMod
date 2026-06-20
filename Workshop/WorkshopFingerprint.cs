using System.Security.Cryptography;
using System.Text;

namespace STS2WorkshopUploader.Workshop;

internal static class WorkshopFingerprint
{
    public static string Text(string? value)
    {
        return Bytes(Encoding.UTF8.GetBytes(value ?? string.Empty));
    }

    public static string File(string path)
    {
        if (!System.IO.File.Exists(path))
            return string.Empty;

        using var stream = System.IO.File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    public static string ContentManifest(IEnumerable<ContentPackageFile> files)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var file in files.OrderBy(file => file.Path, StringComparer.OrdinalIgnoreCase))
        {
            Add(hash, file.Path);
            Add(hash, file.Hash);
            Add(hash, file.Size.ToString());
        }

        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private static string Bytes(byte[] bytes)
    {
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private static void Add(IncrementalHash hash, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        hash.AppendData(bytes);
        hash.AppendData([0]);
    }
}