namespace STS2WorkshopUploader.Workshop;

internal static class WorkshopLanguage
{
    private static readonly Dictionary<string, string> GameToSteam = new(StringComparer.OrdinalIgnoreCase)
    {
        ["eng"] = "english",
        ["zhs"] = "schinese",
        ["zht"] = "tchinese",
        ["deu"] = "german",
        ["esp"] = "latam",
        ["spa"] = "spanish",
        ["fra"] = "french",
        ["ita"] = "italian",
        ["jpn"] = "japanese",
        ["kor"] = "koreana",
        ["pol"] = "polish",
        ["ptb"] = "brazilian",
        ["rus"] = "russian",
        ["tha"] = "thai",
        ["tur"] = "turkish"
    };

    public static string Normalize(string folderName)
    {
        var key = folderName.Trim();
        return GameToSteam.TryGetValue(key, out var steamLanguage) ? steamLanguage : key.ToLowerInvariant();
    }
}