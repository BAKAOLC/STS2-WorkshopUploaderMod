using System.Runtime.CompilerServices;
using Godot;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.Core.Nodes.Screens.Settings;
using STS2RitsuLib.Patching.Models;
using STS2RitsuLib.Settings;
using STS2WorkshopUploader.Ui;

namespace STS2WorkshopUploader.Patches;

internal sealed class WorkshopUploaderSubmenuStackPatch : IPatchMethod
{
    internal static readonly ConditionalWeakTable<NSubmenuStack, WorkshopUploaderSubmenu> Submenus = new();

    public static string PatchId => "workshop_uploader_submenu_stack";
    public static string Description => "Inject workshop uploader submenu into the main menu submenu stack";
    public static bool IsCritical => false;

    public static ModPatchTarget[] GetTargets()
    {
        return
        [
            new ModPatchTarget(typeof(NMainMenuSubmenuStack), nameof(NMainMenuSubmenuStack.GetSubmenuType),
                [typeof(Type)])
        ];
    }

    public static bool Prefix(NMainMenuSubmenuStack __instance, Type type, ref NSubmenu __result)
    {
        if (type != typeof(WorkshopUploaderSubmenu))
            return true;

        __result = Submenus.GetValue(__instance, CreateSubmenu);
        return false;
    }

    internal static WorkshopUploaderSubmenu CreateSubmenu(NSubmenuStack stack)
    {
        var submenu = new WorkshopUploaderSubmenu
        {
            Visible = false,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            FocusMode = Control.FocusModeEnum.None
        };
        stack.AddChild(submenu);
        return submenu;
    }
}

internal sealed class WorkshopUploaderRunSubmenuStackPatch : IPatchMethod
{
    public static string PatchId => "workshop_uploader_run_submenu_stack";
    public static string Description => "Inject workshop uploader submenu into the run submenu stack";
    public static bool IsCritical => false;

    public static ModPatchTarget[] GetTargets()
    {
        return [new ModPatchTarget(typeof(NRunSubmenuStack), nameof(NRunSubmenuStack.GetSubmenuType), [typeof(Type)])];
    }

    public static bool Prefix(NRunSubmenuStack __instance, Type type, ref NSubmenu __result)
    {
        if (type != typeof(WorkshopUploaderSubmenu))
            return true;

        __result = WorkshopUploaderSubmenuStackPatch.Submenus.GetValue(
            __instance,
            WorkshopUploaderSubmenuStackPatch.CreateSubmenu);
        return false;
    }
}

internal sealed class WorkshopUploaderSettingsEntryPatch : IPatchMethod
{
    private const string EntryLineNodeName = "WorkshopUploaderEntry";
    private const string EntryDividerNodeName = "WorkshopUploaderEntryDivider";

    public static string PatchId => "workshop_uploader_settings_entry";
    public static string Description => "Add workshop uploader entry point to the vanilla settings screen";
    public static bool IsCritical => false;

    public static ModPatchTarget[] GetTargets()
    {
        return
        [
            new ModPatchTarget(typeof(NSettingsScreen), nameof(NSettingsScreen._Ready)),
            new ModPatchTarget(typeof(NSettingsScreen), nameof(NSettingsScreen.OnSubmenuOpened)),
            new ModPatchTarget(typeof(NSettingsScreen), "OnSubmenuShown")
        ];
    }

    public static void Postfix(NSettingsScreen __instance)
    {
        try
        {
            EnsureEntryPoint(__instance);
        }
        catch (Exception ex)
        {
            Main.Logger.Warn($"Failed to add workshop uploader settings entry: {ex.Message}");
        }
    }

    private static void EnsureEntryPoint(NSettingsScreen screen)
    {
        var panel = screen.GetNode<NSettingsPanel>("%GeneralSettings");
        var content = panel.Content;
        if (content.GetNodeOrNull<Control>(EntryLineNodeName) != null)
            return;

        var divider = new ColorRect
        {
            Name = EntryDividerNodeName,
            Color = new Color(1f, 1f, 1f, 0.16f),
            CustomMinimumSize = new Vector2(0f, 2f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        var line = CreateEntryLine(() => OpenUploader(screen));

        content.AddChild(divider);
        content.AddChild(line);

        var anchor = content.GetNodeOrNull<Control>("ModdingDivider") ??
                     content.GetNodeOrNull<Control>("CreditsDivider");
        if (anchor != null)
        {
            MoveChildBefore(content, line, anchor);
            MoveChildBefore(content, divider, line);
        }
    }

    private static MarginContainer CreateEntryLine(Action open)
    {
        var line = ModSettingsGameSettingsEntryLine.Create(open);
        line.Name = EntryLineNodeName;
        if (line.GetNodeOrNull<MegaRichTextLabel>("ContentRow/Label") is { } label)
            label.SetTextAutoSize(WorkshopUploaderText.Resolve("Workshop Uploader", "Workshop Uploader"));
        if (line.GetNodeOrNull<MegaLabel>("ContentRow/RitsuLibModSettingsButton/Label") is { } buttonLabel)
            buttonLabel.SetTextAutoSize(WorkshopUploaderText.Resolve("Open", "Open"));
        return line;
    }

    private static void OpenUploader(NSettingsScreen screen)
    {
        screen.GetAncestorOfType<NSubmenuStack>()?.PushSubmenuType(typeof(WorkshopUploaderSubmenu));
    }

    private static void MoveChildBefore(VBoxContainer content, Control child, Control anchor)
    {
        var targetIndex = anchor.GetIndex();
        if (child.GetIndex() < targetIndex)
            targetIndex--;

        content.MoveChild(child, targetIndex);
    }
}