using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using STS2RitsuLib;
using STS2RitsuLib.Patching.Core;
using STS2WorkshopUploader.Patches;

namespace STS2WorkshopUploader;

[ModInitializer(nameof(Initialize))]
public static class Main
{
    public static readonly Logger Logger = RitsuLibFramework.CreateLogger(Const.ModId);

    public static bool IsModActive { get; private set; }

    public static void Initialize()
    {
        Logger.Info($"Mod ID: {Const.ModId}");
        Logger.Info($"Version: {Const.Version}");
        Logger.Info("Initializing workshop uploader mod...");

        try
        {
            ApplyPatches();

            IsModActive = true;
            Logger.Info("Workshop uploader mod initialization complete.");
        }
        catch (Exception ex)
        {
            Logger.Error($"Workshop uploader initialization failed: {ex}");
            IsModActive = false;
        }
    }

    private static void ApplyPatches()
    {
        var patcher = RitsuLibFramework.CreatePatcher(Const.ModId, "workshop-uploader-ui", "workshop uploader ui");
        patcher.RegisterPatch<WorkshopUploaderSubmenuStackPatch>();
        patcher.RegisterPatch<WorkshopUploaderRunSubmenuStackPatch>();
        patcher.RegisterPatch<WorkshopUploaderSettingsEntryPatch>();
        RitsuLibFramework.ApplyRequiredPatcher(patcher, DisableMod);
    }

    private static void DisableMod()
    {
        IsModActive = false;
    }
}