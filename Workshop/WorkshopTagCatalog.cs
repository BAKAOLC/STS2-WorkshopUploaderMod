namespace STS2WorkshopUploader.Workshop;

internal static class WorkshopTagCatalog
{
    public static IReadOnlyList<WorkshopTagOption> AppDefinedTags { get; } =
    [
        new("Acts", "Acts", WorkshopTagSource.WorkshopPage),
        new("Ancients", "Ancients", WorkshopTagSource.WorkshopPage),
        new("Audio", "Audio", WorkshopTagSource.WorkshopPage),
        new("Cards", "Cards", WorkshopTagSource.WorkshopPage),
        new("Characters", "Characters", WorkshopTagSource.WorkshopPage),
        new("Cosmetics", "Cosmetics", WorkshopTagSource.WorkshopPage),
        new("Events", "Events", WorkshopTagSource.WorkshopPage),
        new("Expansion", "Expansion", WorkshopTagSource.WorkshopPage),
        new("Extensions", "Extensions", WorkshopTagSource.WorkshopPage),
        new("Humor", "Humor", WorkshopTagSource.WorkshopPage),
        new("Modifiers", "Modifiers", WorkshopTagSource.WorkshopPage),
        new("Monsters", "Monsters", WorkshopTagSource.WorkshopPage),
        new("Potions", "Potions", WorkshopTagSource.WorkshopPage),
        new("QoL", "QoL", WorkshopTagSource.WorkshopPage),
        new("Relics", "Relics", WorkshopTagSource.WorkshopPage),
        new("Rooms", "Rooms", WorkshopTagSource.WorkshopPage),
        new("Tools & APIs", "Tools & APIs", WorkshopTagSource.WorkshopPage),
        new("Utility", "Utility", WorkshopTagSource.WorkshopPage),
        new("Misc", "Misc", WorkshopTagSource.WorkshopPage)
    ];

    public static IReadOnlyList<WorkshopTagOption> PageObservedTags { get; } = AppDefinedTags;

    public static IReadOnlyList<WorkshopLanguageOption> LanguageOptions { get; } =
    [
        new("arabic", "Arabic / العربية"),
        new("bulgarian", "Bulgarian / български език"),
        new("schinese", "Chinese (Simplified) / 简体中文"),
        new("tchinese", "Chinese (Traditional) / 繁體中文"),
        new("czech", "Czech / čeština"),
        new("danish", "Danish / Dansk"),
        new("dutch", "Dutch / Nederlands"),
        new("english", "English"),
        new("finnish", "Finnish / Suomi"),
        new("french", "French / Français"),
        new("german", "German / Deutsch"),
        new("greek", "Greek / Ελληνικά"),
        new("hungarian", "Hungarian / Magyar"),
        new("indonesian", "Indonesian / Bahasa Indonesia"),
        new("italian", "Italian / Italiano"),
        new("japanese", "Japanese / 日本語"),
        new("koreana", "Korean / 한국어"),
        new("malay", "Malay / Bahasa Melayu"),
        new("norwegian", "Norwegian / Norsk"),
        new("polish", "Polish / Polski"),
        new("portuguese", "Portuguese / Português"),
        new("brazilian", "Portuguese-Brazil / Português-Brasil"),
        new("romanian", "Romanian / Română"),
        new("russian", "Russian / Русский"),
        new("spanish", "Spanish-Spain / Español-España"),
        new("latam", "Spanish-Latin America / Español-Latinoamérica"),
        new("swedish", "Swedish / Svenska"),
        new("thai", "Thai / ไทย"),
        new("turkish", "Turkish / Türkçe"),
        new("ukrainian", "Ukrainian / Українська"),
        new("vietnamese", "Vietnamese / Tiếng Việt")
    ];

    public static IReadOnlyList<WorkshopTagOption> LanguageTags { get; } = LanguageOptions
        .Select(language => new WorkshopTagOption(language.Code, language.DisplayName, WorkshopTagSource.Language))
        .ToArray();

    public static IReadOnlyList<string> All { get; } = PageObservedTags.Concat(LanguageTags)
        .Select(tag => tag.Value)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Order(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    public static WorkshopTagOption OptionFor(string value)
    {
        return PageObservedTags.Concat(LanguageTags).FirstOrDefault(option =>
                   string.Equals(option.Value, value, StringComparison.OrdinalIgnoreCase)) ??
               new WorkshopTagOption(value, value, WorkshopTagSource.Custom);
    }

    public static WorkshopLanguageOption? LanguageFor(string code)
    {
        return LanguageOptions.FirstOrDefault(option =>
            string.Equals(option.Code, code, StringComparison.OrdinalIgnoreCase));
    }
}

internal sealed record WorkshopTagOption(string Value, string DisplayName, WorkshopTagSource Source);

internal sealed record WorkshopLanguageOption(string Code, string DisplayName);

internal enum WorkshopTagSource
{
    Remote,
    WorkshopPage,
    Language,
    Custom
}