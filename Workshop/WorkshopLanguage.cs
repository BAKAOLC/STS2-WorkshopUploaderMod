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

    private static readonly HashSet<string> KnownSteamLanguages = new(StringComparer.OrdinalIgnoreCase)
    {
        "english",
        "schinese",
        "tchinese",
        "japanese",
        "koreana",
        "german",
        "french",
        "italian",
        "spanish",
        "latam",
        "polish",
        "brazilian",
        "russian",
        "thai",
        "turkish",
        "arabic",
        "bulgarian",
        "czech",
        "danish",
        "dutch",
        "finnish",
        "greek",
        "hungarian",
        "indonesian",
        "malay",
        "norwegian",
        "portuguese",
        "romanian",
        "swedish",
        "ukrainian",
        "vietnamese"
    };

    public static string Normalize(string folderName)
    {
        var key = folderName.Trim();
        return GameToSteam.TryGetValue(key, out var steamLanguage) ? steamLanguage : key.ToLowerInvariant();
    }

    public static bool IsKnownSteamLanguage(string language)
    {
        return KnownSteamLanguages.Contains(language);
    }
}