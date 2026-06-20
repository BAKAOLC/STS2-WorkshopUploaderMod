using System.Reflection;
using STS2RitsuLib.Settings;
using STS2RitsuLib.Utils;

namespace STS2WorkshopUploader.Ui;

internal static class WorkshopUploaderText
{
    private static readonly Lazy<I18N> Localization = new(() => new I18N(
        "WorkshopUploader",
        resourceFolders: ["STS2WorkshopUploader.Localization"],
        resourceAssembly: Assembly.GetExecutingAssembly()));

    public static string Resolve(string key, string fallback)
    {
        return ModSettingsText.I18N(Localization.Value, key, fallback).Resolve();
    }
}