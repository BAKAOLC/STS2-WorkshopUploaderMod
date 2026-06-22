using Godot;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using STS2RitsuLib.Settings;
using STS2RitsuLib.Ui.Shell;
using STS2RitsuLib.Ui.Shell.Theme;
using STS2RitsuLib.Ui.Toast;
using STS2WorkshopUploader.Workshop;
using HttpClient = System.Net.Http.HttpClient;

namespace STS2WorkshopUploader.Ui;

internal sealed partial class WorkshopUploaderSubmenu : NSubmenu
{
    private const int PageMarginTop = 78;
    private const int PageMarginBottom = 72;
    private const int PanelMargin = 12;
    private const int DenseMargin = 8;
    private const int PopupMargin = 14;
    private const int RowGap = 10;
    private const int ScrollbarGutter = 18;
    private const int ButtonMinWidth = 220;
    private const int ButtonMinHeight = 46;
    private const double ToastDurationSeconds = 10d;
    private readonly Dictionary<string, List<Label>> _changeBadgeLabels = new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<ulong, WorkshopItemSummary> _dependencyTitles = [];
    private readonly HashSet<string> _draftChangedSlots = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _excludePatterns = [];
    private readonly List<LocalModInfo> _mods = [];
    private readonly List<Button> _openWorkshopButtons = [];
    private readonly HashSet<string> _tagSelection = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, bool> _updateChecks = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<Button> _uploadButtons = [];
    private Label _activity = null!;
    private WorkshopUploadMode? _activeUploadMode;
    private bool _autoOpenWorkshopAfterUpload;
    private Control _bindPopup = null!;
    private VBoxContainer _bindPopupBody = null!;
    private VBoxContainer _bindResults = null!;
    private LineEdit _bindSearch = null!;
    private bool _canUploadBoundItem = true;
    private TextEdit _changeNote = null!;
    private VBoxContainer _content = null!;
    private VBoxContainer? _currentSection;
    private LineEdit _customTag = null!;
    private GridContainer _dependencyCurrentList = null!;
    private Control _dependencyPopup = null!;
    private VBoxContainer _dependencyPopupBody = null!;
    private GridContainer _dependencyResults = null!;
    private LineEdit _dependencySearch = null!;
    private Label _dependencySummary = null!;
    private TextEdit _description = null!;
    private HashSet<string>? _detectedChanges;
    private bool _draftTextDirty;
    private Control _excludePopup = null!;
    private VBoxContainer _excludePopupBody = null!;
    private GridContainer _excludeRows = null!;
    private Control _languagePopup = null!;
    private VBoxContainer _languagePopupBody = null!;
    private string _languageToAdd = "schinese";
    private bool _loadingMetadataLanguage;
    private LineEdit _maxBranch = null!;
    private string _metadataLanguage = "english";
    private ModSettingsDropdownChoiceControl<string> _metadataLanguageSelect = null!;
    private LineEdit _minBranch = null!;
    private VBoxContainer _modList = null!;
    private MarginContainer _pageFrame = null!;
    private int _permissionCheckSerial;
    private Label _permissionStatus = null!;
    private readonly Dictionary<Control, Vector2I> _popupDesiredSizes = [];
    private TextureRect _previewImage = null!;
    private Label _previewStatus = null!;
    private bool _resizeLayoutRefreshQueued;
    private LocalModInfo? _selected;
    private PanelContainer _sidebarPanel = null!;
    private List<WorkshopTagOption> _tagOptions = [];
    private Control _tagPopup = null!;
    private VBoxContainer _tagPopupBody = null!;
    private bool _tagRefreshStarted;
    private Label _tagSourceStatus = null!;
    private Label _tagSummary = null!;
    private Control _taskOverlay = null!;
    private Button _taskOverlayClose = null!;
    private Label _taskOverlayError = null!;
    private Label _taskOverlayMessage = null!;
    private Panel _taskOverlayPanel = null!;
    private ProgressBar _taskOverlayProgress = null!;
    private Label _taskOverlayTitle = null!;
    private LineEdit _title = null!;
    private ModSettingsDropdownChoiceControl<string> _visibility = null!;
    private string _visibilityValue = "private";

    private LineEdit _workshopId = null!;

    protected override Control? InitialFocusedControl => null;

    public override void _Ready()
    {
        BuildShell();
        ConnectSignals();
        EnsureTagsRefreshedInBackground();
        RefreshMods();
    }

    public override void _Notification(int what)
    {
        if (what == NotificationResized)
            QueueResizeLayoutRefresh();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        TryClosePopupFromCancel(@event);
    }

    public override void _Input(InputEvent @event)
    {
        if (_taskOverlay?.Visible != true || !IsKeyboardOrGamepadInput(@event))
            return;

        if (IsCancelInput(@event))
        {
            if (_taskOverlayClose.Visible)
                _taskOverlay.Hide();
            ConsumeInput();
            return;
        }

        if (_taskOverlayClose.Visible && IsAcceptInput(@event))
        {
            _taskOverlay.Hide();
            ConsumeInput();
            return;
        }

        ConsumeInput();
    }

    private bool TryClosePopupFromCancel(InputEvent @event)
    {
        if (!IsCancelInput(@event))
            return false;

        if (_taskOverlay?.Visible == true)
        {
            if (_taskOverlayClose.Visible)
                _taskOverlay.Hide();
            GetViewport().SetInputAsHandled();
            AcceptEvent();
            return true;
        }

        var popup = FirstVisiblePopup();
        if (popup == null)
            return false;

        popup.Hide();
        GetViewport().SetInputAsHandled();
        AcceptEvent();
        return true;
    }

    private static bool IsKeyboardOrGamepadInput(InputEvent @event)
    {
        return @event is InputEventKey or InputEventJoypadButton or InputEventJoypadMotion or InputEventAction;
    }

    private static bool IsCancelInput(InputEvent @event)
    {
        return @event.IsActionPressed("ui_cancel") ||
               @event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape };
    }

    private static bool IsAcceptInput(InputEvent @event)
    {
        return @event.IsActionPressed("ui_accept") ||
               @event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Enter or Key.KpEnter };
    }

    private void ConsumeInput()
    {
        GetViewport().SetInputAsHandled();
        AcceptEvent();
    }

    private void BuildShell()
    {
        Name = "WorkshopUploaderSubmenu";
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        _pageFrame = new MarginContainer { MouseFilter = MouseFilterEnum.Ignore };
        _pageFrame.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(_pageFrame);

        var root = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore
        };
        root.AddThemeConstantOverride("separation", 12);
        _pageFrame.AddChild(root);

        var header = new HBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            Alignment = BoxContainer.AlignmentMode.Center
        };
        header.AddThemeConstantOverride("separation", RowGap);
        root.AddChild(header);
        header.AddChild(CreateTitle(T("Workshop Uploader"), 32));

        var body = new HBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        body.AddThemeConstantOverride("separation", 14);
        root.AddChild(body);

        _sidebarPanel = CreatePanel();
        _sidebarPanel.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
        _sidebarPanel.SizeFlagsVertical = SizeFlags.ExpandFill;
        var sidebarScroll = new ScrollContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        ThemeScroll(sidebarScroll);
        _modList = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _modList.AddThemeConstantOverride("separation", 6);
        AddScrollableContent(sidebarScroll, _modList);
        _sidebarPanel.AddChild(WrapWithMargin(sidebarScroll, DenseMargin));
        body.AddChild(_sidebarPanel);

        var contentPanel = CreatePanel();
        contentPanel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        contentPanel.SizeFlagsVertical = SizeFlags.ExpandFill;
        var contentScroll = new ScrollContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            FollowFocus = true
        };
        ThemeScroll(contentScroll);
        _content = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _content.AddThemeConstantOverride("separation", 10);
        AddScrollableContent(contentScroll, _content);
        contentPanel.AddChild(WrapWithMargin(contentScroll, 14));
        body.AddChild(contentPanel);

        var backButton = PreloadManager.Cache.GetScene(SceneHelper.GetScenePath("ui/back_button"))
            .Instantiate<NBackButton>();
        backButton.Name = "BackButton";
        AddChild(backButton);

        BuildBindPopup();
        BuildLanguagePopup();
        BuildDependencyPopup();
        BuildTagPopup();
        BuildExcludePopup();
        BuildTaskOverlay();
        ApplyResponsiveShellLayout();
    }

    private void ApplyResponsiveShellLayout()
    {
        if (_pageFrame == null || !IsInstanceValid(_pageFrame) ||
            _sidebarPanel == null || !IsInstanceValid(_sidebarPanel))
            return;

        var viewport = GetViewportRect().Size;
        var horizontalMargin = ResolvePageHorizontalMargin(viewport.X);
        _pageFrame.AddThemeConstantOverride("margin_left", horizontalMargin);
        _pageFrame.AddThemeConstantOverride("margin_right", horizontalMargin);
        _pageFrame.AddThemeConstantOverride("margin_top", ResolvePageTopMargin(viewport.Y));
        _pageFrame.AddThemeConstantOverride("margin_bottom", ResolvePageBottomMargin(viewport.Y));
        _sidebarPanel.CustomMinimumSize = new Vector2(ResolveSidebarWidth(viewport.X), 0f);
    }

    private void QueueResizeLayoutRefresh()
    {
        if (!IsInsideTree())
            return;

        RefreshLayoutAfterResize();
        if (_resizeLayoutRefreshQueued)
            return;

        _resizeLayoutRefreshQueued = true;
        _ = FlushResizeLayoutRefreshDeferredAsync();
    }

    private async Task FlushResizeLayoutRefreshDeferredAsync()
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (IsInstanceValid(this))
            FlushResizeLayoutRefresh();
    }

    private void FlushResizeLayoutRefresh()
    {
        _resizeLayoutRefreshQueued = false;
        RefreshLayoutAfterResize();
    }

    private void RefreshLayoutAfterResize()
    {
        if (!IsInsideTree() || _pageFrame == null || !IsInstanceValid(_pageFrame))
            return;

        ApplyResponsiveShellLayout();
        RequestRecursiveLayout(_pageFrame);
        RefreshVisiblePopupLayouts();
        if (_taskOverlay?.Visible == true)
            ShowTaskOverlay();
    }

    private void RefreshVisiblePopupLayouts()
    {
        foreach (var (popup, size) in _popupDesiredSizes.ToArray())
        {
            if (!IsInstanceValid(popup))
            {
                _popupDesiredSizes.Remove(popup);
                continue;
            }

            if (popup.Visible)
                ShowPopup(popup, size.X, size.Y);
        }
    }

    private static void RequestRecursiveLayout(Control root)
    {
        if (!IsInstanceValid(root))
            return;

        root.UpdateMinimumSize();
        if (root is Container container)
            container.QueueSort();

        foreach (var child in root.GetChildren())
            if (child is Control control)
                RequestRecursiveLayout(control);
    }

    private static int ResolvePageHorizontalMargin(float width)
    {
        return (int)Mathf.Clamp(width * 0.07f, 36f, 160f);
    }

    private static int ResolvePageTopMargin(float height)
    {
        return (int)Mathf.Clamp(height * 0.08f, 52f, PageMarginTop);
    }

    private static int ResolvePageBottomMargin(float height)
    {
        return (int)Mathf.Clamp(height * 0.065f, 42f, PageMarginBottom);
    }

    private static float ResolveSidebarWidth(float width)
    {
        return Mathf.Clamp(width * 0.22f, 240f, 320f);
    }

    private void BuildBindPopup()
    {
        _bindPopup = CreatePopup("BindPopup");
        _bindPopupBody = CreatePopupBody(_bindPopup);
        AddChild(_bindPopup);
    }

    private void BuildDependencyPopup()
    {
        _dependencyPopup = CreatePopup("DependencyPopup");
        _dependencyPopupBody = CreatePopupBody(_dependencyPopup, false);
        AddChild(_dependencyPopup);
    }

    private void BuildLanguagePopup()
    {
        _languagePopup = CreatePopup("LanguagePopup");
        _languagePopupBody = CreatePopupBody(_languagePopup);
        AddChild(_languagePopup);
    }

    private void BuildTagPopup()
    {
        _tagPopup = CreatePopup("TagPopup");
        _tagPopupBody = CreatePopupBody(_tagPopup);
        AddChild(_tagPopup);
    }

    private void BuildExcludePopup()
    {
        _excludePopup = CreatePopup("ExcludePopup");
        _excludePopupBody = CreatePopupBody(_excludePopup);
        AddChild(_excludePopup);
    }

    private void BuildTaskOverlay()
    {
        _taskOverlay = new Control
        {
            Name = "TaskOverlay",
            Visible = false,
            FocusMode = FocusModeEnum.All,
            MouseFilter = MouseFilterEnum.Stop
        };
        _taskOverlay.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var dim = new ColorRect
        {
            Color = RitsuShellTheme.Current.Color.ModalBackdrop,
            MouseFilter = MouseFilterEnum.Stop
        };
        dim.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _taskOverlay.AddChild(dim);

        _taskOverlayPanel = new Panel
        {
            Name = "Panel",
            ClipContents = true,
            MouseFilter = MouseFilterEnum.Stop,
            CustomMinimumSize = Vector2.Zero
        };
        _taskOverlayPanel.AddThemeStyleboxOverride("panel", ModSettingsUiFactory.CreateListShellStyle());
        _taskOverlayTitle = CreateTitle("", 24);
        _taskOverlayMessage = CreateMuted("");
        _taskOverlayMessage.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _taskOverlayMessage.TextOverrunBehavior = TextServer.OverrunBehavior.NoTrimming;
        _taskOverlayMessage.ClipContents = true;
        _taskOverlayProgress = new ProgressBar
        {
            MinValue = 0,
            MaxValue = 100,
            CustomMinimumSize = new Vector2(0f, 20f)
        };
        _taskOverlayError = CreateMuted("");
        _taskOverlayError.Visible = false;
        _taskOverlayError.ClipContents = true;
        _taskOverlayError.AddThemeColorOverride("font_color", new Color(0.95f, 0.42f, 0.38f));
        _taskOverlayClose = CreateButton(T("Close"), () => _taskOverlay.Hide());
        _taskOverlayClose.Visible = false;
        _taskOverlayPanel.AddChild(_taskOverlayTitle);
        _taskOverlayPanel.AddChild(_taskOverlayMessage);
        _taskOverlayPanel.AddChild(_taskOverlayProgress);
        _taskOverlayPanel.AddChild(_taskOverlayError);
        _taskOverlayPanel.AddChild(_taskOverlayClose);
        _taskOverlay.AddChild(_taskOverlayPanel);
        AddChild(_taskOverlay);
    }

    private void RefreshMods()
    {
        _mods.Clear();
        _mods.AddRange(LocalModScanner.Scan(WorkshopPaths.ResolveDefaultModsRoot()));
        RebuildModList();
        if (_mods.Count == 0)
        {
            ShowEmpty();
            return;
        }

        if (_selected == null)
        {
            ShowNoModSelected();
            return;
        }

        var selected = _mods.FirstOrDefault(m => m.Path == _selected.Path);
        if (selected == null)
            ShowNoModSelected();
        else
            SelectMod(selected);
    }

    private void RebuildModList()
    {
        Clear(_modList);
        foreach (var mod in _mods)
            _modList.AddChild(CreateModListItem(mod));
    }

    private Control CreateModListItem(LocalModInfo mod)
    {
        var selected = _selected != null &&
                       string.Equals(_selected.Path, mod.Path, StringComparison.OrdinalIgnoreCase);
        var card = new PanelContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0f, 68f),
            MouseFilter = MouseFilterEnum.Stop
        };
        ApplyModListItemStyle(card, selected, false);

        var hit = new Button
        {
            Flat = true,
            Text = "",
            FocusMode = FocusModeEnum.All,
            MouseFilter = MouseFilterEnum.Stop,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        hit.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        hit.Pressed += () => SelectMod(mod);
        hit.MouseEntered += () => ApplyModListItemStyle(card, selected, true);
        hit.MouseExited += () => ApplyModListItemStyle(card, selected, false);
        hit.FocusEntered += () => ApplyModListItemStyle(card, true, true);
        hit.FocusExited += () => ApplyModListItemStyle(card, selected, false);
        hit.AddThemeStyleboxOverride("normal", new StyleBoxEmpty());
        hit.AddThemeStyleboxOverride("hover", new StyleBoxEmpty());
        hit.AddThemeStyleboxOverride("pressed", new StyleBoxEmpty());
        hit.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        card.AddChild(hit);

        var content = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore
        };
        content.AddThemeConstantOverride("separation", 2);

        var title = CreateInlineStrong(mod.Name);
        title.MouseFilter = MouseFilterEnum.Ignore;
        title.AddThemeFontSizeOverride("font_size", RitsuShellTheme.Current.Metric.FontSize.Button);
        title.AddThemeColorOverride("font_color",
            selected ? RitsuShellTheme.Current.Text.HoverHighlight : RitsuShellTheme.Current.Text.RichTitle);

        var meta = CreateMuted($"{mod.Id} | {mod.LoadState}");
        meta.MouseFilter = MouseFilterEnum.Ignore;
        meta.AutowrapMode = TextServer.AutowrapMode.Off;
        meta.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        meta.AddThemeFontSizeOverride("font_size", RitsuShellTheme.Current.Metric.FontSize.HintSmall);
        meta.AddThemeColorOverride("font_color",
            selected ? RitsuShellTheme.Current.Text.RichBody : RitsuShellTheme.Current.Text.RichMuted);

        content.AddChild(title);
        content.AddChild(meta);
        card.AddChild(WrapWithMargin(content, 10));
        return card;
    }

    private void SelectMod(LocalModInfo mod)
    {
        _selected = mod;
        RebuildModList();
        WorkshopTemplateService.EnsureTemplate(mod);
        RebuildEditor(mod);
    }

    private void ShowEmpty()
    {
        Clear(_content);
        _content.AddChild(CreateMuted(T("No local mods were found in the game's mods directory.")));
    }

    private void ShowNoModSelected()
    {
        _selected = null;
        Clear(_content);
        _changeBadgeLabels.Clear();
        _content.AddChild(CreateTitle(T("Select a local mod")));
        _content.AddChild(CreateMuted(T("Choose a mod from the list to edit its Workshop upload information.")));
    }

    private void RebuildEditor(LocalModInfo mod)
    {
        Clear(_content);
        _openWorkshopButtons.Clear();
        _uploadButtons.Clear();
        _changeBadgeLabels.Clear();
        _updateChecks.Clear();
        _dependencyTitles.Clear();
        _currentSection = null;

        var metadata = WorkshopMetadataEditor.LoadOrCreate(mod);
        _detectedChanges = TryDetectMetadataChanges(mod);
        AddHeader(mod);

        AddSection(T("Workshop Binding"));
        _workshopId = CreateLine(metadata.WorkshopItemId?.ToString() ??
                                 WorkshopJson.Read<WorkshopUploadState>(WorkshopPaths.StateFile(mod.Path))
                                     ?.WorkshopItemId?.ToString() ?? "");
        _workshopId.TextChanged += _text =>
        {
            UpdateWorkshopPageButtons();
            MarkDraftTextDirty();
        };
        _workshopId.FocusExited += () =>
        {
            CommitDraftTextIfDirty();
            _ = RefreshUploadPermissionAsync();
        };
        AddRow(T("Workshop item id"), _workshopId, CreateButton(T("Unbind"), () =>
        {
            _workshopId.Text = "";
            SaveMetadata();
            UpdateWorkshopPageButtons();
        }));
        CurrentContainer().AddChild(Row(
            CreateButton(T("Search / Bind Remote"), OpenBindPopup),
            CreateButton(T("Sync Remote Info"), () => _ = SyncBoundWorkshopAsync()),
            CreateOpenWorkshopButton()));

        AddSection(T("Store Metadata"));
        _metadataLanguage = "english";
        _metadataLanguageSelect = CreateDropdown(GetMetadataLanguageChoices(mod), _metadataLanguage, language =>
        {
            if (_loadingMetadataLanguage)
                return;
            CommitDraftTextIfDirty();
            SaveCurrentLanguageText();
            _metadataLanguage = language;
            LoadLanguageText();
            RefreshChangeBadge("title");
            RefreshChangeBadge("description");
        });
        AddRow(T("Text language"), Row(FlexibleField(_metadataLanguageSelect, 320f),
                CreateButton(T("Manage Languages"), OpenLanguagePopup)), updateKey: "localized",
            updateLabel: T("Allow"), updateValue: metadata.Update.Localized);
        _title = CreateLine(ReadText(WorkshopPaths.TitleFile(mod.Path), metadata.Title ?? mod.Name));
        TrackDraftChanges(_title, "title");
        AddRow(T("Title"), _title, updateKey: "title", updateLabel: T("Allow"),
            updateValue: metadata.Update.Title);
        _description = CreateText(ReadText(WorkshopPaths.DescriptionMarkdownFile(mod.Path), ""));
        TrackDraftChanges(_description, "description");
        AddTextEditorRow(T("Summary / Description (Markdown)"), _description, updateKey: "description",
            updateLabel: T("Allow"),
            updateValue: metadata.Update.Description);
        _visibilityValue = metadata.Visibility ?? "private";
        _visibility = CreateDropdown(
            [
                ("private", T("Private")),
                ("friends_only", T("Friends only")),
                ("unlisted", T("Unlisted")),
                ("public", T("Public"))
            ],
            _visibilityValue,
            value =>
            {
                _visibilityValue = value;
                SaveDraftAndRefreshChangedSlots("visibility");
            });
        AddRow(T("Visibility"), RightAligned(FlexibleField(_visibility, 260f)),
            updateKey: "visibility", updateLabel: T("Allow"), updateValue: metadata.Update.Visibility);

        _tagSelection.Clear();
        foreach (var tag in metadata.Tags)
            _tagSelection.Add(tag);
        _tagSummary = CreateMuted(FormatTags());
        AddRow(T("Tags"), _tagSummary, CreateButton(T("Manage Tags"), OpenTagPopup), "tags",
            T("Allow"), metadata.Update.Tags);
        _minBranch = CreateLine(metadata.MinBranch ?? "");
        _maxBranch = CreateLine(metadata.MaxBranch ?? "");
        TrackDraftChanges(_minBranch, "gameVersions");
        TrackDraftChanges(_maxBranch, "gameVersions");
        AddRow(T("Required game versions"), Row(_minBranch, _maxBranch), updateKey: "gameVersions",
            updateLabel: T("Allow"),
            updateValue: metadata.Update.GameVersions);
        _previewStatus = CreateMuted(WorkshopPaths.PreviewFile(mod.Path));
        _previewImage = CreatePreviewImage(mod);
        AddRow(T("Preview image"),
            Row(FixedSize(_previewImage, 260f, 146f), _previewStatus, CreateButton(T("Import Preview"), ImportPreview)),
            updateKey: "preview", updateLabel: T("Allow"), updateValue: metadata.Update.Preview);

        AddSection(T("Content Package"));
        _excludePatterns.Clear();
        _excludePatterns.AddRange(metadata.Exclude);
        CurrentContainer().AddChild(CreateContentPackageTree(mod, metadata));
        AddRow(T("Content exclude patterns"), CreateMuted(FormatExcludes()), CreateButton(T("Manage Excludes"),
                OpenExcludePopup), "content",
            T("Allow content"),
            metadata.Update.Content);

        BuildDependencySection(mod, metadata);
        _ = ResolveDependencyTitlesAsync();
        BuildUploadSection();
        _ = EnsureRemoteBaselineAsync(mod);
    }

    private void BuildDependencySection(LocalModInfo mod, WorkshopMetadata metadata)
    {
        AddSection(T("Dependencies"), "dependencies", T("Allow dependencies"),
            metadata.Update.Dependencies);
        _dependencySummary = CreateMuted(FormatDependencies(metadata));
        CurrentContainer().AddChild(CreateDependencySummaryRow());
    }

    private void BuildUploadSection()
    {
        AddSection(T("Upload"));
        var metadata = _selected == null ? null : WorkshopMetadataEditor.LoadOrCreate(_selected);
        _autoOpenWorkshopAfterUpload = metadata?.OpenWorkshopAfterUpload == true;
        _changeNote = CreateText(ReadText(WorkshopPaths.ChangeNoteMarkdownFile(_selected!.Path), ""));
        TrackDraftChanges(_changeNote);
        AddTextEditorRow(T("Changelog (Markdown)"), _changeNote,
            T(
                "Leave empty to skip the changelog. Steam change notes are submitted once per update and are not localized."));
        AddRow(T("Open Workshop page after upload"),
            RightAligned(new ModSettingsToggleControl(_autoOpenWorkshopAfterUpload,
                value =>
                {
                    _autoOpenWorkshopAfterUpload = value;
                    SaveDraftMetadata();
                })));
        _permissionStatus = CreateMuted(T("Checking Workshop edit permission..."));
        CurrentContainer().AddChild(_permissionStatus);
        var uploadInfo = CreateUploadButton(T("Upload Info"), WorkshopUploadMode.MetadataOnly);
        var uploadFull = CreateUploadButton(T("Upload Content + Info"), WorkshopUploadMode.Full);
        CurrentContainer().AddChild(ActionRow(
            CreateButton(T("Save Metadata"), SaveMetadata),
            CreateOpenWorkshopButton(),
            uploadInfo,
            uploadFull));
        _activity = CreateMuted(T("Idle"));
        CurrentContainer().AddChild(_activity);
        _ = RefreshUploadPermissionAsync();
    }

    private Button CreateUploadButton(string text, WorkshopUploadMode mode)
    {
        var button = CreateButton(text, () => _ = ConfirmUploadAsync(mode), ModSettingsButtonTone.Accent);
        _uploadButtons.Add(button);
        return button;
    }

    private async Task ConfirmUploadAsync(WorkshopUploadMode mode)
    {
        if (_selected == null)
            return;

        try
        {
            SaveMetadata();
            if (!await EnsureCanUploadAsync())
                return;

            var plan = BuildPlan(mode);
            ModSettingsUiFactory.ShowStyledConfirm(
                this,
                mode == WorkshopUploadMode.Full ? T("Upload Content + Info") : T("Upload Info"),
                CreatePlanSummary(plan),
                T("Cancel"),
                T("Upload"),
                false,
                () => _ = UploadAsync(mode));
        }
        catch (Exception ex)
        {
            Fail(T("Upload preparation failed"), ex);
        }
    }

    private void RebuildDependencies(WorkshopMetadata metadata)
    {
        if (_dependencySummary != null)
            _dependencySummary.Text = FormatDependencies(metadata);
    }

    private string FormatDependencies(WorkshopMetadata metadata)
    {
        if (metadata.Dependencies.Count == 0)
            return T("No dependencies.");

        var names = metadata.Dependencies
            .Select(id => _dependencyTitles.TryGetValue(id, out var item) ? item.Title : id.ToString());
        return string.Join(", ", names);
    }

    private string FormatTags()
    {
        return _tagSelection.Count == 0
            ? T("No tags selected.")
            : string.Join(", ", _tagSelection
                .Order(StringComparer.OrdinalIgnoreCase)
                .Select(tag => WorkshopTagCatalog.OptionFor(tag).DisplayName));
    }

    private string FormatExcludes()
    {
        var count = _excludePatterns.Count(pattern => !string.IsNullOrWhiteSpace(pattern));
        return count == 0 ? T("No exclude patterns.") : string.Format(T("{0} exclude patterns."), count);
    }

    private static string FormatBytes(long bytes)
    {
        return FormatBytes((ulong)Math.Max(0, bytes));
    }

    private static string FormatBytes(ulong bytes)
    {
        string[] units = ["B", "KB", "MB", "GB"];
        var value = (double)bytes;
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return unit == 0 ? $"{bytes} {units[unit]}" : $"{value:0.##} {units[unit]}";
    }

    private string FormatContentFileStatus(
        ContentPackageFile file,
        IReadOnlyDictionary<string, ContentPackageFileState> referenceFiles)
    {
        return ResolveContentFileStatus(file, referenceFiles) switch
        {
            ContentFileStatus.New => T("New"),
            ContentFileStatus.Modified => T("Modified"),
            ContentFileStatus.Unchanged => T("Unchanged"),
            ContentFileStatus.Deleted => T("Deleted"),
            _ => T("Unknown")
        };
    }

    private static ContentFileStatus ResolveContentFileStatus(
        ContentPackageFile file,
        IReadOnlyDictionary<string, ContentPackageFileState> referenceFiles)
    {
        return !referenceFiles.TryGetValue(file.Path, out var old)
            ? ContentFileStatus.New
            : string.Equals(old.Hash, file.Hash, StringComparison.OrdinalIgnoreCase) && old.Size == file.Size
                ? ContentFileStatus.Unchanged
                : ContentFileStatus.Modified;
    }

    private static string ShortHash(string hash)
    {
        return string.IsNullOrWhiteSpace(hash) ? "" : hash[..Math.Min(hash.Length, 12)];
    }

    private static void ApplyContentFileStatusColor(TreeItem item, ContentFileStatus status)
    {
        var color = status switch
        {
            ContentFileStatus.New => new Color(0.58f, 0.74f, 0.62f),
            ContentFileStatus.Modified => new Color(0.95f, 0.72f, 0.38f),
            ContentFileStatus.Deleted => new Color(0.92f, 0.42f, 0.38f),
            _ => RitsuShellTheme.Current.Text.LabelPrimary
        };

        for (var column = 0; column < 4; column++)
            item.SetCustomColor(column, color);
    }

    private IReadOnlyList<(string Value, string Label)> GetMetadataLanguageChoices(LocalModInfo mod)
    {
        var languages = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "english" };
        var root = WorkshopPaths.LocalizedDirectory(mod.Path);
        Directory.CreateDirectory(root);
        foreach (var dir in Directory.EnumerateDirectories(root))
        {
            var language = WorkshopLanguage.Normalize(Path.GetFileName(dir));
            if (!string.IsNullOrWhiteSpace(language))
                languages.Add(language);
        }

        return languages
            .Order(StringComparer.OrdinalIgnoreCase)
            .Select(language => (language, FormatLanguageChoice(language)))
            .ToArray();
    }

    private IReadOnlyList<(string Value, string Label)> GetAddableLanguageChoices(LocalModInfo mod)
    {
        var existing = GetMetadataLanguageChoices(mod)
            .Select(choice => choice.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var choices = WorkshopTagCatalog.LanguageOptions
            .Where(option => !existing.Contains(option.Code))
            .Select(option => (option.Code, option.DisplayName))
            .ToArray();

        return choices.Length == 0 ? [(string.Empty, T("No more languages"))] : choices;
    }

    private string GetDefaultLanguageToAdd(LocalModInfo mod)
    {
        return GetAddableLanguageChoices(mod).FirstOrDefault().Value ?? string.Empty;
    }

    private static string FormatLanguage(string code)
    {
        var option = WorkshopTagCatalog.LanguageFor(code);
        return option == null ? code : $"{option.DisplayName} ({option.Code})";
    }

    private string FormatLanguageChoice(string code)
    {
        var label = FormatLanguage(code);
        return IsLanguageChanged(code) ? string.Format(T("{0} (changed)"), label) : label;
    }

    private bool IsLanguageChanged(string code)
    {
        if (_detectedChanges == null)
            return false;

        var language = WorkshopLanguage.Normalize(code);
        if (string.Equals(language, "english", StringComparison.OrdinalIgnoreCase))
            return _detectedChanges.Contains("title") || _detectedChanges.Contains("description");

        return _detectedChanges.Contains($"localized:{language}:title") ||
               _detectedChanges.Contains($"localized:{language}:description");
    }

    private void OpenLanguagePopup()
    {
        RebuildLanguagePopup();
        ShowPopup(_languagePopup, 980, 640);
    }

    private void RebuildLanguagePopup()
    {
        if (_selected == null)
            return;

        Clear(_languagePopupBody);
        _languagePopupBody.AddChild(CreateTitle(T("Text languages"), 24));

        _languagePopupBody.AddChild(CreateTitle(T("Existing languages"), 18));
        var existing = GetMetadataLanguageChoices(_selected);
        foreach (var language in existing)
        {
            var actions = string.Equals(language.Value, "english", StringComparison.OrdinalIgnoreCase)
                ? [CreateSmallButton(T("Edit"), () => SelectLanguage(language.Value), ModSettingsButtonTone.Accent)]
                : new[]
                {
                    CreateSmallButton(T("Edit"), () => SelectLanguage(language.Value), ModSettingsButtonTone.Accent),
                    CreateSmallButton(T("Remove"), () => RemoveTextLanguage(language.Value),
                        ModSettingsButtonTone.Danger)
                };
            _languagePopupBody.AddChild(ResultRow(language.Label, actions));
        }

        _languagePopupBody.AddChild(CreateTitle(T("Add Language"), 18));
        var addGrid = new GridContainer
        {
            Columns = 3,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        addGrid.AddThemeConstantOverride("h_separation", DenseMargin);
        addGrid.AddThemeConstantOverride("v_separation", DenseMargin);
        foreach (var language in GetAddableLanguageChoices(_selected)
                     .Where(choice => !string.IsNullOrWhiteSpace(choice.Value)))
            addGrid.AddChild(CreateButton(language.Label, () => AddTextLanguage(language.Value)));
        _languagePopupBody.AddChild(addGrid);

        SetPopupFooter(_languagePopup, CreateButton(T("Close"), () => _languagePopup.Hide()));
    }

    private void SelectLanguage(string language)
    {
        if (_selected == null)
            return;

        SaveCurrentLanguageText();
        _metadataLanguage = WorkshopLanguage.Normalize(language);
        _metadataLanguageSelect.SetValue(_metadataLanguage);
        LoadLanguageText();
        _languagePopup.Hide();
    }

    private void AddTextLanguage(string languageToAdd)
    {
        if (_selected == null)
            return;

        var language = WorkshopLanguage.Normalize(languageToAdd);
        if (string.IsNullOrWhiteSpace(language))
            return;

        SaveCurrentLanguageText();
        Directory.CreateDirectory(Path.Combine(WorkshopPaths.LocalizedDirectory(_selected.Path), language));
        _metadataLanguage = language;
        _metadataLanguageSelect.SetOptions(GetMetadataLanguageChoices(_selected).ToArray(), _metadataLanguage);
        LoadLanguageText();
        RebuildLanguagePopup();
    }

    private void RemoveTextLanguage(string languageToRemove)
    {
        if (_selected == null)
            return;

        var language = WorkshopLanguage.Normalize(languageToRemove);
        if (string.IsNullOrWhiteSpace(language) ||
            string.Equals(language, "english", StringComparison.OrdinalIgnoreCase))
            return;

        var dir = Path.Combine(WorkshopPaths.LocalizedDirectory(_selected.Path), language);
        if (Directory.Exists(dir))
            Directory.Delete(dir, true);
        if (string.Equals(_metadataLanguage, language, StringComparison.OrdinalIgnoreCase))
        {
            _metadataLanguage = "english";
            LoadLanguageText();
        }

        _metadataLanguageSelect.SetOptions(GetMetadataLanguageChoices(_selected).ToArray(), _metadataLanguage);
        RebuildLanguagePopup();
    }

    private void SaveCurrentLanguageText()
    {
        if (_selected == null || _title == null || _description == null)
            return;

        if (string.Equals(_metadataLanguage, "english", StringComparison.OrdinalIgnoreCase))
        {
            File.WriteAllText(WorkshopPaths.TitleFile(_selected.Path), _title.Text);
            File.WriteAllText(WorkshopPaths.DescriptionMarkdownFile(_selected.Path), _description.Text);
            return;
        }

        var dir = Path.Combine(WorkshopPaths.LocalizedDirectory(_selected.Path), _metadataLanguage);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, WorkshopPaths.TitleFileName), _title.Text);
        File.WriteAllText(Path.Combine(dir, WorkshopPaths.DescriptionMarkdownFileName), _description.Text);
    }

    private void LoadLanguageText()
    {
        if (_selected == null)
            return;

        _loadingMetadataLanguage = true;
        try
        {
            if (string.Equals(_metadataLanguage, "english", StringComparison.OrdinalIgnoreCase))
            {
                _title.Text = ReadText(WorkshopPaths.TitleFile(_selected.Path), _selected.Name);
                _description.Text = ReadText(WorkshopPaths.DescriptionMarkdownFile(_selected.Path), "");
                return;
            }

            var dir = Path.Combine(WorkshopPaths.LocalizedDirectory(_selected.Path), _metadataLanguage);
            _title.Text = ReadText(Path.Combine(dir, WorkshopPaths.TitleFileName), "");
            _description.Text = ReadText(Path.Combine(dir, WorkshopPaths.DescriptionMarkdownFileName), "");
        }
        finally
        {
            _loadingMetadataLanguage = false;
        }
    }

    private void SaveMetadata()
    {
        SaveMetadataCore(true);
    }

    private void SaveDraftAndRefreshChangedSlots(params string[] keys)
    {
        if (_loadingMetadataLanguage || _selected == null)
            return;

        SaveDraftMetadata();
        RefreshChangedSlots(keys);
    }

    private void MarkDraftTextDirty(params string[] keys)
    {
        if (_loadingMetadataLanguage || _selected == null)
            return;

        _draftTextDirty = true;
        foreach (var key in keys)
            _draftChangedSlots.Add(key);
        RefreshChangedSlots(keys);
    }

    private void CommitDraftTextIfDirty()
    {
        if (!_draftTextDirty)
            return;

        var keys = _draftChangedSlots.ToArray();
        SaveDraftMetadata();
        RefreshChangedSlots(keys);
    }

    private void SaveMetadataCore(bool showActivity)
    {
        SaveDraftMetadata();
        RefreshChangeState();
        if (showActivity)
            SetActivity(T("Metadata saved."));
    }

    private void SaveDraftMetadata()
    {
        if (_selected == null)
            return;

        var metadata = WorkshopMetadataEditor.LoadOrCreate(_selected);
        if (_title != null && _description != null)
            SaveCurrentLanguageText();
        metadata.WorkshopItemId = ulong.TryParse(_workshopId.Text.Trim(), out var id) ? id : null;
        metadata.Visibility = _visibilityValue;
        metadata.Tags = _tagSelection.Order(StringComparer.OrdinalIgnoreCase).ToList();
        metadata.Exclude = _excludePatterns.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        metadata.MinBranch = BlankToNull(_minBranch.Text);
        metadata.MaxBranch = BlankToNull(_maxBranch.Text);
        metadata.OpenWorkshopAfterUpload = _autoOpenWorkshopAfterUpload;
        metadata.Update.Title = IsChecked("title");
        metadata.Update.Description = IsChecked("description");
        metadata.Update.Visibility = IsChecked("visibility");
        metadata.Update.Tags = IsChecked("tags");
        metadata.Update.Dependencies = IsChecked("dependencies");
        metadata.Update.GameVersions = IsChecked("gameVersions");
        metadata.Update.Preview = IsChecked("preview");
        metadata.Update.Content = IsChecked("content");
        metadata.Update.Localized = IsChecked("title") || IsChecked("description");
        WorkshopJson.Write(WorkshopPaths.MetadataFile(_selected.Path), metadata);
        if (_changeNote != null)
            File.WriteAllText(WorkshopPaths.ChangeNoteMarkdownFile(_selected.Path), _changeNote.Text);
        _draftTextDirty = false;
        _draftChangedSlots.Clear();
    }

    private async Task RefreshUploadPermissionAsync()
    {
        if (_selected == null || _permissionStatus == null)
            return;

        var serial = ++_permissionCheckSerial;
        var itemId = ResolveCurrentWorkshopItemId();
        if (itemId is null or 0)
        {
            _canUploadBoundItem = true;
            _permissionStatus.Text = T("No bound Workshop item. Uploading content will create a new item.");
            UpdateUploadPermissionControls();
            return;
        }

        _canUploadBoundItem = false;
        _permissionStatus.Text = T("Checking Workshop edit permission...");
        UpdateUploadPermissionControls();

        try
        {
            var permission = await SteamWorkshopLookup.GetEditPermissionAsync(itemId.Value);
            if (serial != _permissionCheckSerial)
                return;

            _canUploadBoundItem = permission.CanEdit;
            _permissionStatus.Text = permission.CanEdit
                ? T("Current Steam account can edit this Workshop item.")
                : T("Current Steam account cannot edit this Workshop item.");
            UpdateUploadPermissionControls();
        }
        catch (Exception ex)
        {
            if (serial != _permissionCheckSerial)
                return;

            _canUploadBoundItem = false;
            _permissionStatus.Text = string.Format(T("Workshop edit permission check failed: {0}"), ex.Message);
            UpdateUploadPermissionControls();
        }
    }

    private async Task<bool> EnsureCanUploadAsync()
    {
        var itemId = ResolveCurrentWorkshopItemId();
        if (itemId is null or 0)
            return true;

        try
        {
            var permission = await SteamWorkshopLookup.GetEditPermissionAsync(itemId.Value);
            _canUploadBoundItem = permission.CanEdit;
            _permissionStatus.Text = permission.CanEdit
                ? T("Current Steam account can edit this Workshop item.")
                : T("Current Steam account cannot edit this Workshop item.");
            UpdateUploadPermissionControls();
            if (permission.CanEdit)
                return true;

            ShowErrorToast(T("Current Steam account cannot edit this Workshop item."), T("Upload failed"));
            return false;
        }
        catch (Exception ex)
        {
            _canUploadBoundItem = false;
            _permissionStatus.Text = string.Format(T("Workshop edit permission check failed: {0}"), ex.Message);
            UpdateUploadPermissionControls();
            Main.Logger.Error($"[Audit] Workshop edit permission check failed. ItemId={itemId.Value}, Error={ex}");
            ShowErrorToast(T("Upload failed. See log for details."), T("Upload failed"));
            return false;
        }
    }

    private void UpdateUploadPermissionControls()
    {
        foreach (var button in _uploadButtons.Where(IsInstanceValid))
            button.Disabled = !_canUploadBoundItem;
    }

    private async Task UploadAsync(WorkshopUploadMode mode)
    {
        if (_selected == null)
            return;

        var selectedPath = _selected.Path;
        try
        {
            BeginTaskOverlay(mode == WorkshopUploadMode.Full ? T("Uploading content and info") : T("Uploading info"));
            SetBusy(true);
            UpdateTaskOverlay(T("Saving metadata..."),
                T("Writing the current form values before building the upload request."), 8);
            SaveMetadata();
            SetBusy(true);
            SetActivity(T("Preparing upload..."));
            UpdateTaskOverlay(T("Preparing upload..."),
                mode == WorkshopUploadMode.Full
                    ? T("Building the filtered content package and calculating changed metadata.")
                    : T("Calculating changed metadata and language entries."),
                24);
            var plan = BuildPlan(mode);
            Main.Logger.Info(
                $"[Audit] Upload started. ModId={plan.Mod.Id}, Mode={plan.Mode}, ItemId={(plan.Metadata.WorkshopItemId ?? plan.State.WorkshopItemId)?.ToString() ?? "<new>"}, ChangedKeys={FormatKeys(plan.ChangedKeys)}, StagingPath={plan.StagingPath ?? "<none>"}.");
            SetBusy(true);
            SetActivity(mode == WorkshopUploadMode.MetadataOnly
                ? T("Submitting metadata to Steam Workshop...")
                : T("Submitting content and metadata to Steam Workshop..."));
            UpdateTaskOverlay(mode == WorkshopUploadMode.MetadataOnly
                    ? T("Submitting metadata to Steam Workshop...")
                    : T("Submitting content and metadata to Steam Workshop..."),
                FormatUploadTaskDetail(plan), 58);
            var uploadProgress = new Progress<WorkshopUploadProgress>(UpdateSteamUploadProgress);
            _activeUploadMode = mode;
            var result = await SteamWorkshopUploader.UploadAsync(plan, uploadProgress);
            SetActivity(result);
            UpdateTaskOverlay(T("Upload complete."), T("Refreshing local upload records and change state."), 100);
            var itemId = ResolveCurrentWorkshopItemId();
            EndTaskOverlay();
            Main.Logger.Info(
                $"[Audit] Upload completed. ModId={plan.Mod.Id}, ItemId={itemId?.ToString() ?? "<unknown>"}, Result={result}.");
            ShowInfoToast(T("Upload completed."), T("Upload complete"), itemId);
            if (_autoOpenWorkshopAfterUpload)
                OpenWorkshopPage(itemId);
            if (_selected != null && string.Equals(_selected.Path, selectedPath, StringComparison.OrdinalIgnoreCase))
                RebuildEditor(_selected);
        }
        catch (Exception ex)
        {
            if (_selected != null && string.Equals(_selected.Path, selectedPath, StringComparison.OrdinalIgnoreCase))
                RebuildEditor(_selected);
            Fail(T("Upload failed"), ex);
            ShowTaskError(T("Upload failed"), ex);
            ShowErrorToast(T("Upload failed. See log for details."), T("Upload failed"));
        }
        finally
        {
            SetBusy(false);
            _activeUploadMode = null;
            UpdateUploadPermissionControls();
        }
    }

    private void OpenBindPopup()
    {
        if (_selected == null)
            return;

        RebuildBindPopup();
        ShowPopup(_bindPopup, 1280, 680);
    }

    private void RebuildBindPopup()
    {
        if (_selected == null)
            return;

        Clear(_bindPopupBody);
        _bindPopupBody.AddChild(CreateTitle(T("Bind Workshop Item"), 24));
        _bindPopupBody.AddChild(
            CreateMuted(T("Search your target Workshop item, then bind its id to this local mod record.")));

        var searchRow = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        searchRow.AddThemeConstantOverride("separation", 8);
        _bindSearch = CreateLine(_workshopId.Text);
        _bindSearch.PlaceholderText = T("Workshop item id or search text");
        _bindSearch.TextSubmitted += _text => _ = SearchBindTargetsAsync();
        searchRow.AddChild(_bindSearch);
        searchRow.AddChild(CreateButton(T("Search"), () => _ = SearchBindTargetsAsync()));
        searchRow.AddChild(CreateButton(T("Bind ID"), () =>
        {
            if (_selected == null || !ulong.TryParse(_bindSearch.Text.Trim(), out var id))
                return;
            BindWorkshopItem(id, null);
        }, ModSettingsButtonTone.Accent));
        _bindPopupBody.AddChild(searchRow);

        _bindResults = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _bindResults.AddThemeConstantOverride("separation", DenseMargin);
        _bindPopupBody.AddChild(_bindResults);
        SetPopupFooter(_bindPopup, CreateButton(T("Close"), () => _bindPopup.Hide()));
    }

    private async Task SearchBindTargetsAsync()
    {
        if (_selected == null)
            return;

        try
        {
            var query = _bindSearch.Text.Trim();
            List<WorkshopItemSummary> results;
            if (ulong.TryParse(query, out var id))
                results = await SteamWorkshopLookup.GetItemsAsync([id]);
            else
                results = await SteamWorkshopLookup.SearchAsync(query);

            Clear(_bindResults);
            if (results.Count == 0)
            {
                _bindResults.AddChild(CreateMuted(T("No matching Workshop items.")));
                return;
            }

            foreach (var result in results)
                _bindResults.AddChild(ResultRow(
                    FormatWorkshopResult(result),
                    CreateButton(T("Bind"), () => BindWorkshopItem(result.Id, result), ModSettingsButtonTone.Accent),
                    CreateButton(T("Bind + Sync"), () =>
                    {
                        BindWorkshopItem(result.Id, result);
                        _ = ApplyRemoteInfoSafelyAsync(result);
                    })));
        }
        catch (Exception ex)
        {
            Fail(T("Workshop item search failed"), ex);
        }
    }

    private void BindWorkshopItem(ulong id, WorkshopItemSummary? remote)
    {
        if (_selected == null)
            return;

        _workshopId.Text = id.ToString();
        SaveMetadata();
        UpdateWorkshopPageButtons();
        _ = RefreshUploadPermissionAsync();
        _ = RefreshRemoteBaselineAsync(_selected, id, true);
        _bindPopup.Hide();
        SetActivity(remote == null
            ? string.Format(T("Bound Workshop item {0}."), id)
            : string.Format(T("Bound Workshop item {0} ({1})."), remote.Title, id));
    }

    private async Task EnsureRemoteBaselineAsync(LocalModInfo mod)
    {
        var metadata = WorkshopMetadataEditor.LoadOrCreate(mod);
        var state = WorkshopJson.Read<WorkshopUploadState>(WorkshopPaths.StateFile(mod.Path)) ??
                    new WorkshopUploadState();
        var itemId = metadata.WorkshopItemId ?? state.WorkshopItemId;
        if (itemId is null or 0 || !NeedsRemoteBaseline(state, itemId.Value))
            return;

        await RefreshRemoteBaselineAsync(mod, itemId.Value, false);
    }

    private static bool NeedsRemoteBaseline(WorkshopUploadState state, ulong itemId)
    {
        return state.WorkshopItemId != itemId || state.Fingerprints.Count == 0;
    }

    private async Task RefreshRemoteBaselineAsync(LocalModInfo mod, ulong itemId, bool force)
    {
        try
        {
            var state = WorkshopJson.Read<WorkshopUploadState>(WorkshopPaths.StateFile(mod.Path)) ??
                        new WorkshopUploadState();
            if (!force && !NeedsRemoteBaseline(state, itemId))
                return;

            SetActivity(T("Refreshing remote baseline..."));
            Main.Logger.Info(
                $"[Audit] Remote baseline refresh started. ModId={mod.Id}, ItemId={itemId}, Force={force}.");
            var item = (await SteamWorkshopLookup.GetItemsAsync([itemId], "english")).FirstOrDefault() ??
                       (await SteamWorkshopLookup.GetItemsAsync([itemId])).FirstOrDefault();
            if (item == null)
            {
                SetActivity(string.Format(T("Workshop item {0} was not found."), itemId));
                return;
            }

            var dependencies = await SteamWorkshopLookup.GetDependenciesAsync(itemId);
            var localized = await SteamWorkshopLookup.GetLocalizedItemAsync(
                itemId,
                WorkshopTagCatalog.LanguageOptions.Select(language => language.Code));
            var localMetadata = WorkshopTemplateService.LoadEffectiveMetadata(mod);
            state.WorkshopItemId = itemId;
            state.Fingerprints = BuildRemoteBaselineFingerprints(mod, item, dependencies, localized, localMetadata);
            var installedPath = SteamWorkshopLookup.TryGetInstalledContentPath(itemId);
            if (!string.IsNullOrWhiteSpace(installedPath))
                state.ContentFiles = StagingBuilder.EnumerateDirectoryFiles(installedPath)
                    .ToDictionary(
                        file => file.Path,
                        file => new ContentPackageFileState
                        {
                            Hash = file.Hash,
                            Size = file.Size,
                            LastWriteUtc = file.LastWriteUtc
                        },
                        StringComparer.OrdinalIgnoreCase);

            WorkshopJson.Write(WorkshopPaths.StateFile(mod.Path), state);
            Main.Logger.Info(
                $"[Audit] Remote baseline refresh completed. ModId={mod.Id}, ItemId={itemId}, Fingerprints={FormatKeys(state.Fingerprints.Keys)}, ContentFiles={state.ContentFiles.Count}.");
            if (_selected != null && string.Equals(_selected.Path, mod.Path, StringComparison.OrdinalIgnoreCase))
            {
                RefreshChangeState();
                SetActivity(T("Remote baseline refreshed."));
            }
        }
        catch (Exception ex)
        {
            Fail(T("Remote baseline refresh failed"), ex);
            if (_selected != null && string.Equals(_selected.Path, mod.Path, StringComparison.OrdinalIgnoreCase))
                SetActivity(string.Format(T("Remote baseline refresh failed: {0}"), ex.Message));
        }
    }

    private static Dictionary<string, string> BuildRemoteBaselineFingerprints(
        LocalModInfo mod,
        WorkshopItemSummary item,
        IReadOnlyList<ulong> dependencies,
        IReadOnlyDictionary<string, WorkshopItemSummary> localized,
        WorkshopMetadata localMetadata)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["title"] = WorkshopFingerprint.Text(item.Title),
            ["description"] = WorkshopFingerprint.Text(NormalizeRemoteDescription(item.Description)),
            ["visibility"] = WorkshopFingerprint.Text(item.Visibility),
            ["tags"] = WorkshopFingerprint.Text(string.Join('\n', item.Tags.Order(StringComparer.OrdinalIgnoreCase))),
            ["dependencies"] = WorkshopFingerprint.Text(string.Join('\n', dependencies.Order())),
            ["gameVersions"] = WorkshopFingerprint.Text($"{localMetadata.MinBranch}\n{localMetadata.MaxBranch}"),
            ["preview"] = WorkshopFingerprint.File(WorkshopPaths.PreviewFile(mod.Path))
        };

        var installedPath = SteamWorkshopLookup.TryGetInstalledContentPath(item.Id);
        if (!string.IsNullOrWhiteSpace(installedPath))
            result["content"] =
                WorkshopFingerprint.ContentManifest(StagingBuilder.EnumerateDirectoryFiles(installedPath));

        foreach (var (language, localizedItem) in localized)
        {
            if (string.Equals(language, "english", StringComparison.OrdinalIgnoreCase))
                continue;

            var hasDistinctText =
                !string.Equals(localizedItem.Title, item.Title, StringComparison.Ordinal) ||
                !string.Equals(localizedItem.Description, item.Description, StringComparison.Ordinal);
            if (!hasDistinctText)
                continue;

            result[$"localized:{language}:title"] = WorkshopFingerprint.Text(localizedItem.Title);
            result[$"localized:{language}:description"] =
                WorkshopFingerprint.Text(NormalizeRemoteDescription(localizedItem.Description));
        }

        return result;
    }

    private static string NormalizeRemoteDescription(string description)
    {
        return MarkdownToSteamBbCode.Convert(SteamBbCodeToMarkdown.Convert(description));
    }

    private async Task SyncBoundWorkshopAsync()
    {
        if (_selected == null)
            return;

        var selectedPath = _selected.Path;
        try
        {
            BeginTaskOverlay(T("Syncing remote info"));
            UpdateTaskOverlay(T("Saving metadata..."), T("Writing the current binding before querying Steam."), 8);
            SaveMetadata();
            var metadata = WorkshopMetadataEditor.LoadOrCreate(_selected);
            if (metadata.WorkshopItemId == null)
            {
                SetActivity(T("No Workshop item id is bound."));
                ShowTaskError(T("Remote sync failed"), T("No Workshop item id is bound."));
                ShowErrorToast(T("No Workshop item id is bound."), T("Remote sync failed"));
                return;
            }

            UpdateTaskOverlay(T("Fetching Workshop item..."),
                T("Requesting title, description, tags, preview, and dependencies."), 30);
            var defaultItem = (await SteamWorkshopLookup.GetItemsAsync([metadata.WorkshopItemId.Value], "english"))
                              .FirstOrDefault() ??
                              (await SteamWorkshopLookup.GetItemsAsync([metadata.WorkshopItemId.Value]))
                              .FirstOrDefault();
            if (defaultItem == null)
            {
                var notFoundMessage = string.Format(T("Workshop item {0} was not found."),
                    metadata.WorkshopItemId.Value);
                SetActivity(notFoundMessage);
                ShowTaskError(T("Remote sync failed"), notFoundMessage);
                ShowErrorToast(notFoundMessage, T("Remote sync failed"));
                return;
            }

            UpdateTaskOverlay(T("Applying remote info..."),
                T("Updating local metadata files and remote comparison baseline."), 65);
            Main.Logger.Info(
                $"[Audit] Remote sync started. ModId={_selected.Id}, ItemId={metadata.WorkshopItemId.Value}.");
            await ApplyRemoteInfoAsync(defaultItem);
            var syncedLanguages = await SyncRemoteLocalizedInfoAsync(metadata.WorkshopItemId.Value, defaultItem);
            await RefreshRemoteBaselineAsync(_selected, metadata.WorkshopItemId.Value, true);
            var message = string.Format(T("Synced remote info from {0} ({1})."), defaultItem.Title,
                defaultItem.Id) + (syncedLanguages.Count == 0 ? "" : $" {string.Join(", ", syncedLanguages)}");
            SetActivity(message);
            UpdateTaskOverlay(T("Remote sync complete."), T("Refreshing the editor with the synced values."), 100);
            EndTaskOverlay();
            Main.Logger.Info(
                $"[Audit] Remote sync completed. ModId={_selected.Id}, ItemId={defaultItem.Id}, SyncedLanguages={FormatKeys(syncedLanguages)}.");
            ShowInfoToast(T("Remote information synced."), T("Remote sync complete"), defaultItem.Id);
            if (_selected != null && string.Equals(_selected.Path, selectedPath, StringComparison.OrdinalIgnoreCase))
                RebuildEditor(_selected);
        }
        catch (Exception ex)
        {
            Fail(T("Remote sync failed"), ex);
            ShowTaskError(T("Remote sync failed"), ex);
            ShowErrorToast(T("Remote sync failed. See log for details."), T("Remote sync failed"));
        }
    }

    private async Task ApplyRemoteInfoAsync(WorkshopItemSummary item)
    {
        if (_selected == null)
            return;

        _title.Text = item.Title;
        File.WriteAllText(WorkshopPaths.TitleFile(_selected.Path), item.Title);
        var markdownDescription = SteamBbCodeToMarkdown.Convert(item.Description);
        if (!string.IsNullOrWhiteSpace(item.Description))
        {
            _description.Text = markdownDescription;
            File.WriteAllText(WorkshopPaths.DescriptionMarkdownFile(_selected.Path), markdownDescription);
        }

        var metadata = WorkshopMetadataEditor.LoadOrCreate(_selected);
        metadata.Title = item.Title;
        metadata.Description = MarkdownToSteamBbCode.Convert(markdownDescription);
        if (!string.IsNullOrWhiteSpace(item.Visibility))
        {
            metadata.Visibility = item.Visibility;
            _visibilityValue = item.Visibility;
            _visibility.SetValue(item.Visibility);
        }

        if (item.Tags.Count > 0)
        {
            metadata.Tags = item.Tags.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            _tagSelection.Clear();
            foreach (var tag in metadata.Tags)
                _tagSelection.Add(tag);
            if (_tagSummary != null)
                _tagSummary.Text = FormatTags();
        }

        if (!string.IsNullOrWhiteSpace(item.PreviewUrl))
            await DownloadPreviewAsync(item.PreviewUrl);

        var dependencies = await SteamWorkshopLookup.GetDependenciesAsync(item.Id);
        metadata.Dependencies = dependencies.Distinct().Order().ToList();
        _dependencyTitles.Clear();
        foreach (var dependency in await SteamWorkshopLookup.GetItemsAsync(metadata.Dependencies))
            _dependencyTitles[dependency.Id] = dependency;
        RebuildDependencies(metadata);
        if (_dependencyPopup?.Visible == true)
            RebuildCurrentDependencyList();

        WorkshopJson.Write(WorkshopPaths.MetadataFile(_selected.Path), metadata);
    }

    private async Task ApplyRemoteInfoSafelyAsync(WorkshopItemSummary item)
    {
        try
        {
            BeginTaskOverlay(T("Syncing remote info"));
            UpdateTaskOverlay(T("Fetching Workshop item..."),
                T("Requesting title, description, tags, preview, and dependencies."), 30);
            var fullItem = (await SteamWorkshopLookup.GetItemsAsync([item.Id], "english")).FirstOrDefault() ?? item;
            UpdateTaskOverlay(T("Applying remote info..."),
                T("Updating local metadata files and remote comparison baseline."), 65);
            await ApplyRemoteInfoAsync(fullItem);
            await SyncRemoteLocalizedInfoAsync(fullItem.Id, fullItem);
            if (_selected != null)
                await RefreshRemoteBaselineAsync(_selected, fullItem.Id, true);
            var message = string.Format(T("Synced remote info from {0} ({1})."), fullItem.Title, fullItem.Id);
            SetActivity(message);
            UpdateTaskOverlay(T("Remote sync complete."), T("Refreshing the editor with the synced values."), 100);
            EndTaskOverlay();
            Main.Logger.Info(
                $"[Audit] Remote sync completed from bind flow. ItemId={fullItem.Id}, Title={fullItem.Title}.");
            ShowInfoToast(T("Remote information synced."), T("Remote sync complete"), fullItem.Id);
            if (_selected != null)
                RebuildEditor(_selected);
        }
        catch (Exception ex)
        {
            Fail(T("Remote sync failed"), ex);
            ShowTaskError(T("Remote sync failed"), ex);
            ShowErrorToast(T("Remote sync failed. See log for details."), T("Remote sync failed"));
        }
    }

    private async Task<List<string>> SyncRemoteLocalizedInfoAsync(ulong itemId, WorkshopItemSummary defaultItem)
    {
        if (_selected == null)
            return [];

        var languages = WorkshopTagCatalog.LanguageOptions
            .Select(language => language.Code)
            .Append(_metadataLanguage)
            .Where(language => !string.Equals(language, "english", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var remote = await SteamWorkshopLookup.GetLocalizedItemAsync(itemId, languages);
        var synced = new List<string>();
        foreach (var (language, item) in remote)
        {
            var isCurrent = string.Equals(language, _metadataLanguage, StringComparison.OrdinalIgnoreCase);
            var hasDistinctRemoteText =
                !string.Equals(item.Title, defaultItem.Title, StringComparison.Ordinal) ||
                !string.Equals(item.Description, defaultItem.Description, StringComparison.Ordinal);
            var localExists =
                Directory.Exists(Path.Combine(WorkshopPaths.LocalizedDirectory(_selected.Path), language));
            if (!isCurrent && !localExists && !hasDistinctRemoteText)
                continue;

            WriteRemoteLanguage(language, item);
            synced.Add(language);
        }

        _metadataLanguageSelect.SetOptions(GetMetadataLanguageChoices(_selected).ToArray(), _metadataLanguage);
        if (!string.Equals(_metadataLanguage, "english", StringComparison.OrdinalIgnoreCase) &&
            synced.Contains(_metadataLanguage, StringComparer.OrdinalIgnoreCase))
            LoadLanguageText();
        return synced;
    }

    private void WriteRemoteLanguage(string language, WorkshopItemSummary item)
    {
        if (_selected == null)
            return;

        var dir = Path.Combine(WorkshopPaths.LocalizedDirectory(_selected.Path), language);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, WorkshopPaths.TitleFileName), item.Title);
        File.WriteAllText(
            Path.Combine(dir, WorkshopPaths.DescriptionMarkdownFileName),
            SteamBbCodeToMarkdown.Convert(item.Description));
    }

    private async Task DownloadPreviewAsync(string url)
    {
        if (_selected == null)
            return;

        using var client = new HttpClient();
        await using var source = await client.GetStreamAsync(url);
        var target = WorkshopPaths.PreviewFile(_selected.Path);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        {
            await using var destination = File.Create(target);
            await source.CopyToAsync(destination);
        }

        RefreshPreviewTexture(target);
        _previewStatus.Text = target;
    }

    private void OpenDependencyPopup()
    {
        if (_selected == null)
            return;

        RebuildDependencyPopup();
        ShowPopup(_dependencyPopup, 1280, 720);
        _ = ResolveDependencyTitlesAsync();
    }

    private TextureRect CreatePreviewImage(LocalModInfo mod)
    {
        EnsurePreviewPlaceholder(mod);
        var preview = new TextureRect
        {
            CustomMinimumSize = new Vector2(260f, 146f),
            SizeFlagsHorizontal = SizeFlags.ShrinkBegin,
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered
        };
        LoadPreviewTexture(preview, WorkshopPaths.PreviewFile(mod.Path));
        return preview;
    }

    private static void EnsurePreviewPlaceholder(LocalModInfo mod)
    {
        var target = WorkshopPaths.PreviewFile(mod.Path);
        if (File.Exists(target))
            return;

        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        using var source = typeof(WorkshopUploaderSubmenu).Assembly
            .GetManifestResourceStream(WorkshopPaths.PlaceholderPreviewResourceName);
        if (source == null)
            return;

        using var destination = File.Create(target);
        source.CopyTo(destination);
    }

    private void LoadPreviewTexture(TextureRect target, string path)
    {
        if (!File.Exists(path))
        {
            target.Texture = null;
            return;
        }

        target.Texture = null;
        var image = Image.LoadFromFile(path);
        target.Texture = image == null ? null : ImageTexture.CreateFromImage(image);
        target.QueueRedraw();
    }

    private void RefreshPreviewTexture(string path)
    {
        LoadPreviewTexture(_previewImage, path);
        _previewImage.Show();
    }

    private void ImportPreview()
    {
        if (_selected == null)
            return;

        var dialog = new FileDialog
        {
            FileMode = FileDialog.FileModeEnum.OpenFile,
            Access = FileDialog.AccessEnum.Filesystem,
            Title = T("Import Preview"),
            Filters = ["*.png ; PNG", "*.jpg, *.jpeg ; JPEG"]
        };
        AddChild(dialog);
        dialog.FileSelected += source =>
        {
            try
            {
                var target = WorkshopPaths.PreviewFile(_selected.Path);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(source, target, true);
                RefreshPreviewTexture(target);
                _previewStatus.Text = target;
                SaveDraftAndRefreshChangedSlots("preview");
            }
            finally
            {
                dialog.QueueFree();
            }
        };
        dialog.Canceled += () => dialog.QueueFree();
        dialog.PopupCentered(new Vector2I(900, 620));
    }

    private void OpenTagPopup()
    {
        EnsureTagsRefreshedInBackground();
        RebuildTagPopup();
        ShowPopup(_tagPopup, 1360, 780);
    }

    private void RebuildTagPopup()
    {
        Clear(_tagPopupBody);
        _tagPopupBody.AddChild(CreateTitle(T("Tags"), 24));
        _tagPopupBody.AddChild(
            CreateMuted(T("Tags come from the Workshop page tag definitions and Steam query results.")));
        _tagSourceStatus = CreateMuted(FormatTagSourceStatus());
        _tagPopupBody.AddChild(_tagSourceStatus);
        var customRow = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        customRow.AddThemeConstantOverride("separation", DenseMargin);
        _customTag = CreateLine("");
        _customTag.PlaceholderText = T("Custom tag");
        _customTag.TextSubmitted += _ => AddCustomTag();
        customRow.AddChild(_customTag);
        customRow.AddChild(CreateButton(T("Add Custom Tag"), AddCustomTag));
        _tagPopupBody.AddChild(customRow);
        _tagOptions = MergeTagOptions(_tagOptions);
        AddTagGroup(T("Official tags"), WorkshopTagCatalog.PageObservedTags);
        AddTagGroup(T("Language tags"), WorkshopTagCatalog.LanguageTags);
        AddTagGroup(T("Custom tags"), _tagOptions, T("No custom tags."));
        SetPopupFooter(_tagPopup,
            CreateButton(T("Apply"), () =>
            {
                if (_tagSummary != null)
                    _tagSummary.Text = FormatTags();
                _tagPopup.Hide();
                SaveDraftAndRefreshChangedSlots("tags");
            }, ModSettingsButtonTone.Accent),
            CreateButton(T("Close"), () => _tagPopup.Hide()));
    }

    private void AddCustomTag()
    {
        var value = _customTag.Text.Trim();
        if (string.IsNullOrWhiteSpace(value))
            return;

        _tagSelection.Add(value);
        _tagOptions = MergeTagOptions(_tagOptions.Append(WorkshopTagCatalog.OptionFor(value)));
        RebuildTagPopup();
    }

    private void EnsureTagsRefreshedInBackground()
    {
        if (_tagRefreshStarted)
            return;

        _tagRefreshStarted = true;
        _ = RefreshTagsFromWorkshopAsync();
    }

    private void AddTagGroup(string title, IReadOnlyList<WorkshopTagOption> tags, string? emptyText = null)
    {
        _tagPopupBody.AddChild(CreateTitle(title, 18));
        if (tags.Count == 0)
        {
            if (!string.IsNullOrWhiteSpace(emptyText))
                _tagPopupBody.AddChild(CreateMuted(emptyText));
            return;
        }

        var grid = new GridContainer
        {
            Columns = 4,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        grid.AddThemeConstantOverride("h_separation", DenseMargin);
        grid.AddThemeConstantOverride("v_separation", DenseMargin);
        foreach (var tag in tags)
            grid.AddChild(CreateTagButton(tag));
        _tagPopupBody.AddChild(grid);
    }

    private Button CreateTagButton(WorkshopTagOption tag)
    {
        var selected = _tagSelection.Contains(tag.Value);
        var text = selected ? string.Format(T("{0} selected"), tag.DisplayName) : tag.DisplayName;
        var button = CreateButton(text, () =>
        {
            if (selected)
                _tagSelection.Remove(tag.Value);
            else
                _tagSelection.Add(tag.Value);
            RebuildTagPopup();
        }, selected ? ModSettingsButtonTone.Accent : ModSettingsButtonTone.Normal);
        button.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        button.CustomMinimumSize = new Vector2(0f, 44f);
        button.TooltipText = $"{T("Steam tag")}: {tag.Value} · {FormatTagSource(tag.Source)}";
        return button;
    }

    private async Task RefreshTagsFromWorkshopAsync()
    {
        try
        {
            if (_tagSourceStatus != null)
                _tagSourceStatus.Text = T("Refreshing Workshop tags...");
            _tagOptions = MergeTagOptions(await SteamWorkshopLookup.GetObservedTagsAsync());
            RebuildTagPopup();
        }
        catch (Exception ex)
        {
            Fail(T("Workshop tag refresh failed"), ex);
            if (_tagSourceStatus != null)
                _tagSourceStatus.Text = T("Workshop tag refresh failed");
        }
    }

    private List<WorkshopTagOption> MergeTagOptions(IEnumerable<WorkshopTagOption> observedTags)
    {
        var options = new Dictionary<string, WorkshopTagOption>(StringComparer.OrdinalIgnoreCase);
        foreach (var tag in observedTags)
        {
            var option = WorkshopTagCatalog.OptionForObservedTag(tag);
            if (option.Source == WorkshopTagSource.Custom)
                options.TryAdd(option.Value, option);
        }

        foreach (var tag in _tagSelection)
        {
            var option = WorkshopTagCatalog.OptionFor(tag);
            if (option.Source == WorkshopTagSource.Custom)
                options.TryAdd(option.Value, option);
        }

        return options.Values
            .OrderBy(TagSortKey)
            .ThenBy(tag => tag.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static int TagSortKey(WorkshopTagOption tag)
    {
        var index = WorkshopTagCatalog.PageObservedTagIndex(tag.Value);
        return index >= 0 ? index : int.MaxValue;
    }

    private string FormatTagSourceStatus()
    {
        return _tagOptions.Count == 0
            ? T("Using official tag definitions.")
            : string.Format(T("{0} custom tags loaded."), _tagOptions.Count);
    }

    private string FormatTagSource(WorkshopTagSource source)
    {
        return source switch
        {
            WorkshopTagSource.Remote => T("Source: Steam query result"),
            WorkshopTagSource.WorkshopPage => T("Source: Workshop page definition"),
            WorkshopTagSource.Language => T("Source: Steam language tag"),
            _ => T("Source: saved custom tag")
        };
    }

    private void OpenExcludePopup()
    {
        RebuildExcludePopup();
        ShowPopup(_excludePopup, 1180, 680);
    }

    private void RebuildExcludePopup()
    {
        Clear(_excludePopupBody);
        _excludePopupBody.AddChild(CreateTitle(T("Content exclude patterns"), 24));
        _excludeRows = new GridContainer
        {
            Columns = 2,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _excludeRows.AddThemeConstantOverride("h_separation", DenseMargin);
        _excludeRows.AddThemeConstantOverride("v_separation", DenseMargin);
        _excludePopupBody.AddChild(_excludeRows);
        RebuildExcludeRows();
        SetPopupFooter(_excludePopup,
            CreateButton(T("Add Pattern"), () =>
            {
                _excludePatterns.Add("");
                RebuildExcludeRows();
            }),
            CreateButton(T("Apply"), () =>
            {
                _excludePatterns.RemoveAll(string.IsNullOrWhiteSpace);
                _excludePopup.Hide();
                SaveMetadata();
                RebuildEditor(_selected!);
            }, ModSettingsButtonTone.Accent),
            CreateButton(T("Close"), () => _excludePopup.Hide()));
    }

    private void RebuildExcludeRows()
    {
        Clear(_excludeRows);
        for (var i = 0; i < _excludePatterns.Count; i++)
        {
            var index = i;
            var edit = CreateLine(_excludePatterns[index]);
            edit.TextChanged += value => _excludePatterns[index] = value;
            _excludeRows.AddChild(Row(edit, CreateSmallButton(T("Remove"), () =>
            {
                _excludePatterns.RemoveAt(index);
                RebuildExcludeRows();
            }, ModSettingsButtonTone.Danger)));
        }
    }

    private void RebuildDependencyPopup()
    {
        if (_selected == null)
            return;

        Clear(_dependencyPopupBody);
        _dependencyPopupBody.AddChild(CreateTitle(T("Dependencies"), 24));

        var searchRow = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        searchRow.AddThemeConstantOverride("separation", 8);
        _dependencySearch = CreateLine("");
        _dependencySearch.PlaceholderText = T("Search text or Workshop item id");
        _dependencySearch.TextSubmitted += _text => _ = SearchDependenciesAsync();
        searchRow.AddChild(_dependencySearch);
        searchRow.AddChild(CreateButton(T("Search"), () => _ = SearchDependenciesAsync()));
        searchRow.AddChild(CreateButton(T("Add ID"), () =>
        {
            if (_selected == null || !ulong.TryParse(_dependencySearch.Text.Trim(), out var id))
                return;
            AddDependency(id, null);
        }));
        _dependencyPopupBody.AddChild(searchRow);

        _dependencyCurrentList = CreateDependencyGrid();
        _dependencyCurrentList.AddThemeConstantOverride("separation", DenseMargin);
        _dependencyPopupBody.AddChild(CreateScrollableListSection(T("Current dependencies"), _dependencyCurrentList,
            210f));
        RebuildCurrentDependencyList();

        _dependencyResults = CreateDependencyGrid();
        _dependencyResults.AddChild(CreateMuted(T("Search text or Workshop item id")));
        _dependencyPopupBody.AddChild(CreateScrollableListSection(T("Search results"), _dependencyResults, 330f));

        SetPopupFooter(_dependencyPopup, CreateButton(T("Close"), () => _dependencyPopup.Hide()));
    }

    private void RebuildCurrentDependencyList()
    {
        if (_selected == null || _dependencyCurrentList == null)
            return;

        Clear(_dependencyCurrentList);
        var metadata = WorkshopMetadataEditor.LoadOrCreate(_selected);
        if (metadata.Dependencies.Count == 0)
        {
            _dependencyCurrentList.AddChild(CreateMuted(T("No dependencies.")));
            return;
        }

        foreach (var id in metadata.Dependencies)
            _dependencyCurrentList.AddChild(DependencyRow(
                FormatDependency(id),
                CreateSmallButton(T("Remove"), () => RemoveDependency(id), ModSettingsButtonTone.Danger)));
    }

    private async Task SearchDependenciesAsync()
    {
        if (_selected == null)
            return;

        try
        {
            List<WorkshopItemSummary> results;
            var query = _dependencySearch.Text.Trim();
            if (ulong.TryParse(query, out var id))
                results = await SteamWorkshopLookup.GetItemsAsync([id]);
            else
                results = await SteamWorkshopLookup.SearchAsync(query);

            Clear(_dependencyResults);
            if (results.Count == 0)
            {
                _dependencyResults.AddChild(CreateMuted(T("No matching Workshop items.")));
                return;
            }

            foreach (var result in results)
            {
                _dependencyTitles[result.Id] = result;
                _dependencyResults.AddChild(DependencyRow(
                    FormatWorkshopResult(result),
                    CreateSmallButton(T("Add"), () => AddDependency(result.Id, result))));
            }
        }
        catch (Exception ex)
        {
            Fail(T("Dependency search failed"), ex);
        }
    }

    private void AddDependency(ulong id, WorkshopItemSummary? item)
    {
        if (_selected == null)
            return;

        if (item != null)
            _dependencyTitles[id] = item;
        WorkshopMetadataEditor.AddDependency(_selected, id);
        var metadata = WorkshopMetadataEditor.LoadOrCreate(_selected);
        RebuildDependencies(metadata);
        RebuildCurrentDependencyList();
        RefreshChangedSlots("dependencies");
    }

    private void RemoveDependency(ulong id)
    {
        if (_selected == null)
            return;

        WorkshopMetadataEditor.RemoveDependency(_selected, id);
        var metadata = WorkshopMetadataEditor.LoadOrCreate(_selected);
        RebuildDependencies(metadata);
        RebuildCurrentDependencyList();
        RefreshChangedSlots("dependencies");
    }

    private async Task ResolveDependencyTitlesAsync()
    {
        if (_selected == null)
            return;

        try
        {
            var metadata = WorkshopMetadataEditor.LoadOrCreate(_selected);
            var results = await SteamWorkshopLookup.GetItemsAsync(metadata.Dependencies);
            foreach (var item in results)
                _dependencyTitles[item.Id] = item;
            RebuildDependencies(metadata);
            if (_dependencyPopup.Visible)
                RebuildCurrentDependencyList();
        }
        catch (Exception ex)
        {
            Fail(T("Dependency title lookup failed"), ex);
        }
    }

    private WorkshopUploadPlan BuildPlan(WorkshopUploadMode mode)
    {
        if (_selected == null)
            throw new InvalidOperationException(T("No mod selected."));

        return WorkshopUploadPlanner.Create(_selected, mode);
    }

    private string CreatePlanSummary(WorkshopUploadPlan plan)
    {
        var rows = new List<string>
        {
            $"{T("Workshop item")}: {FormatWorkshopId(plan)}",
            $"{T("Upload scope")}: {(plan.Mode == WorkshopUploadMode.Full ? T("Info and content package") : T("Info only"))}",
            $"{T("Change note")}: {FormatChangeNoteStatus(plan)}",
            ""
        };

        AddSection(T("Main Workshop item"), BuildMainUpdates(plan));
        AddSection(T("Language entries"), BuildLanguageUpdates(plan));
        AddSection(T("Separate Steam operations"), BuildSeparateUpdates(plan));
        AddSection(T("Content Package"), BuildContentUpdates(plan));

        return string.Join('\n', rows).Trim();

        void AddSection(string title, IReadOnlyList<string> items)
        {
            rows.Add(title);
            if (items.Count == 0)
            {
                rows.Add($"  {T("No queued changes.")}");
                rows.Add("");
                return;
            }

            foreach (var item in items)
                rows.Add($"  {item}");
            rows.Add("");
        }
    }

    private string FormatUploadTaskDetail(WorkshopUploadPlan plan)
    {
        var changed = plan.ChangedKeys.Count == 0 ? T("No queued changes.") : FormatKeys(plan.ChangedKeys);
        var content = plan.Mode == WorkshopUploadMode.Full
            ? string.Format(T("{0} content files are included."), plan.ContentFiles.Count)
            : T("Content files are not included in this upload.");
        return $"{content} {string.Format(T("Queued changes: {0}"), changed)}";
    }

    private string FormatChangeNoteStatus(WorkshopUploadPlan plan)
    {
        if (string.IsNullOrWhiteSpace(plan.Metadata.ChangeNote))
            return T("Empty");

        if (HasMainItemUpdate(plan))
            return T("Will send with the main update");

        return plan.ChangedKeys.Count == 0
            ? T("No queued changes.")
            : T("Will send as changelog-only update");
    }

    private bool HasMainItemUpdate(WorkshopUploadPlan plan)
    {
        return BuildMainUpdates(plan).Count > 0 || BuildContentUpdates(plan).Count > 0;
    }

    private IReadOnlyList<string> BuildMainUpdates(WorkshopUploadPlan plan)
    {
        var rows = new List<string>();
        Add("title", T("Title"), plan.Metadata.Update.Title);
        Add("description", T("Description"), plan.Metadata.Update.Description);
        Add("visibility", T("Visibility"), plan.Metadata.Update.Visibility);
        Add("tags", T("Tags"), plan.Metadata.Update.Tags);
        Add("gameVersions", T("Required game versions"), plan.Metadata.Update.GameVersions);
        Add("preview", T("Preview image"), plan.Metadata.Update.Preview);
        return rows;

        void Add(string key, string label, bool allowed)
        {
            if (allowed && plan.Changed(key))
                rows.Add(label);
        }
    }

    private IReadOnlyList<string> BuildLanguageUpdates(WorkshopUploadPlan plan)
    {
        if (!plan.Metadata.Update.Localized)
            return [];

        var rows = new List<string>();
        foreach (var language in plan.Metadata.Localized.Keys.Order(StringComparer.OrdinalIgnoreCase))
        {
            var fields = new List<string>();
            if (plan.Changed($"localized:{language}:title"))
                fields.Add(T("Title"));
            if (plan.Changed($"localized:{language}:description"))
                fields.Add(T("Description"));
            if (fields.Count > 0)
                rows.Add($"{language}: {string.Join(", ", fields)}");
        }

        return rows;
    }

    private IReadOnlyList<string> BuildSeparateUpdates(WorkshopUploadPlan plan)
    {
        var rows = new List<string>();
        if (plan.Metadata.Update.Dependencies && plan.Changed("dependencies"))
            rows.Add(T("Dependencies"));
        return rows;
    }

    private IReadOnlyList<string> BuildContentUpdates(WorkshopUploadPlan plan)
    {
        if (plan.Mode != WorkshopUploadMode.Full)
            return [];

        var rows = new List<string>();
        if (plan.Metadata.Update.Content && plan.Changed("content"))
            rows.Add(T("Content files"));
        return rows;
    }

    private string FormatWorkshopId(WorkshopUploadPlan plan)
    {
        var id = plan.Metadata.WorkshopItemId ?? plan.State.WorkshopItemId;
        return id?.ToString() ?? T("New item");
    }

    private bool IsChecked(string key)
    {
        return _updateChecks.TryGetValue(key, out var value) && value;
    }

    private void SetBusy(bool busy)
    {
        SetInteractionEnabled(_content, !busy);
        SetInteractionEnabled(_modList, !busy);
    }

    private static void SetInteractionEnabled(Node node, bool enabled)
    {
        foreach (var child in node.GetChildren())
        {
            switch (child)
            {
                case Button button:
                    button.Disabled = !enabled;
                    break;
                case LineEdit line:
                    line.Editable = enabled;
                    break;
                case TextEdit text:
                    text.Editable = enabled;
                    break;
            }

            SetInteractionEnabled(child, enabled);
        }
    }

    private VBoxContainer CurrentContainer()
    {
        return _currentSection ?? _content;
    }

    private void AddHeader(LocalModInfo mod)
    {
        var header = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        header.AddThemeConstantOverride("separation", 4);
        header.AddChild(CreateTitle($"{mod.Name} ({mod.Id})", 28));
        header.AddChild(CreateMuted($"{mod.LoadState} | {mod.Path}"));
        _content.AddChild(header);
    }

    private void AddSection(string title, string? updateKey = null, string? updateLabel = null, bool updateValue = true)
    {
        var panel = CreatePanel();
        panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        var root = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        root.AddThemeConstantOverride("separation", RowGap);
        panel.AddChild(WrapWithMargin(root, PanelMargin));

        var titleRow = new HBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            Alignment = BoxContainer.AlignmentMode.Center
        };
        titleRow.AddThemeConstantOverride("separation", RowGap);
        Button? collapseButton = null;
        var content = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        content.AddThemeConstantOverride("separation", RowGap);
        collapseButton = CreateIconButton("-", () =>
        {
            content.Visible = !content.Visible;
            collapseButton!.Text = content.Visible ? "-" : "+";
        });
        titleRow.AddChild(collapseButton);
        titleRow.AddChild(CreateTitle(title, 21));
        if (updateKey != null)
            titleRow.AddChild(CreateUpdateCheck(updateKey, updateLabel ?? "Update", updateValue));
        root.AddChild(titleRow);
        root.AddChild(content);

        _content.AddChild(panel);
        _currentSection = content;
    }

    private void AddTextEditorRow(
        string label,
        TextEdit editor,
        string? hint = null,
        string? updateKey = null,
        string? updateLabel = null,
        bool updateValue = true)
    {
        var panel = CreatePanel(ModSettingsUiFactory.CreateInsetSurfaceStyle());
        panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;

        var body = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("separation", DenseMargin);
        var header = new HBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            Alignment = BoxContainer.AlignmentMode.Center
        };
        header.AddThemeConstantOverride("separation", DenseMargin);
        header.AddChild(CreateInlineStrong(label));
        if (updateKey != null)
            header.AddChild(CreateUpdateCheck(updateKey, updateLabel ?? "Update", updateValue));
        body.AddChild(header);
        if (!string.IsNullOrWhiteSpace(hint))
            body.AddChild(CreateMuted(hint));
        body.AddChild(editor);
        panel.AddChild(WrapWithMargin(body, DenseMargin));
        (_currentSection ?? _content).AddChild(panel);
    }

    private Control CreateDependencySummaryRow()
    {
        var panel = CreatePanel(ModSettingsUiFactory.CreateInsetSurfaceStyle());
        panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        panel.CustomMinimumSize = new Vector2(0f, 58f);

        var row = new HBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            Alignment = BoxContainer.AlignmentMode.Center
        };
        row.AddThemeConstantOverride("separation", RowGap);
        row.AddChild(CreateInlineStrong(T("Current dependencies")));
        _dependencySummary.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _dependencySummary.AutowrapMode = TextServer.AutowrapMode.Off;
        _dependencySummary.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        row.AddChild(_dependencySummary);
        row.AddChild(CreateButton(T("Manage Dependencies"), OpenDependencyPopup));
        panel.AddChild(WrapWithMargin(row, DenseMargin));
        return panel;
    }

    private Control CreateContentPackageTree(LocalModInfo mod, WorkshopMetadata metadata)
    {
        var panel = CreatePanel(ModSettingsUiFactory.CreateInsetSurfaceStyle());
        panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;

        var files = StagingBuilder.EnumerateIncludedFiles(mod, metadata);
        var state = WorkshopJson.Read<WorkshopUploadState>(WorkshopPaths.StateFile(mod.Path)) ??
                    new WorkshopUploadState();
        var reference = BuildContentReference(metadata, state);
        var currentByPath = files.ToDictionary(file => file.Path, StringComparer.OrdinalIgnoreCase);
        var deletedFiles = reference.Files.Keys
            .Where(path => !currentByPath.ContainsKey(path))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var body = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("separation", DenseMargin);
        body.AddChild(CreateInlineStrong(T("Files to upload")));
        body.AddChild(CreateMuted(reference.Label));
        body.AddChild(CreateMuted(files.Count == 0 && deletedFiles.Length == 0
            ? T("No files will be uploaded.")
            : string.Format(T("{0} files, {1}"), files.Count, FormatBytes(files.Sum(file => file.Size)))));

        var tree = new Tree
        {
            Columns = 4,
            HideRoot = true,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0f, 440f)
        };
        ThemeContentTree(tree);
        tree.SetColumnTitle(0, T("Path"));
        tree.SetColumnTitle(1, T("Size"));
        tree.SetColumnTitle(2, T("Hash"));
        tree.SetColumnTitle(3, T("Status"));
        tree.SetColumnTitleAlignment(1, HorizontalAlignment.Right);
        tree.SetColumnTitleAlignment(2, HorizontalAlignment.Left);
        tree.SetColumnTitleAlignment(3, HorizontalAlignment.Left);
        tree.SetColumnExpand(0, true);
        tree.SetColumnExpand(1, false);
        tree.SetColumnExpand(2, false);
        tree.SetColumnExpand(3, false);
        tree.SetColumnCustomMinimumWidth(1, 110);
        tree.SetColumnCustomMinimumWidth(2, 120);
        tree.SetColumnCustomMinimumWidth(3, 110);

        var root = tree.CreateItem();
        var directories = new Dictionary<string, TreeItem>(StringComparer.OrdinalIgnoreCase)
        {
            [""] = root
        };

        foreach (var file in files)
            AddContentTreeFile(
                tree,
                root,
                directories,
                file.Path,
                file.Size,
                file.Hash,
                FormatContentFileStatus(file, reference.Files),
                ResolveContentFileStatus(file, reference.Files));

        foreach (var path in deletedFiles)
        {
            var old = reference.Files[path];
            AddContentTreeFile(
                tree,
                root,
                directories,
                path,
                old.Size,
                old.Hash,
                T("Deleted"),
                ContentFileStatus.Deleted);
        }

        body.AddChild(tree);
        panel.AddChild(WrapWithMargin(body, DenseMargin));
        return panel;
    }

    private static void AddContentTreeFile(
        Tree tree,
        TreeItem root,
        Dictionary<string, TreeItem> directories,
        string path,
        long size,
        string hash,
        string statusText,
        ContentFileStatus status)
    {
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var parent = root;
        var currentPath = string.Empty;
        for (var i = 0; i < segments.Length - 1; i++)
        {
            currentPath = string.IsNullOrEmpty(currentPath)
                ? segments[i]
                : $"{currentPath}/{segments[i]}";
            if (!directories.TryGetValue(currentPath, out var dir))
            {
                dir = tree.CreateItem(parent);
                dir.SetText(0, segments[i]);
                dir.Collapsed = false;
                directories[currentPath] = dir;
            }

            parent = dir;
        }

        var item = tree.CreateItem(parent);
        item.SetText(0, segments.Length == 0 ? path : segments[^1]);
        item.SetText(1, FormatBytes(size));
        item.SetText(2, ShortHash(hash));
        item.SetText(3, statusText);
        item.SetTextAlignment(1, HorizontalAlignment.Right);
        item.SetTextAlignment(2, HorizontalAlignment.Left);
        item.SetTextAlignment(3, HorizontalAlignment.Left);
        item.SetTooltipText(0, path);
        item.SetTooltipText(2, hash);
        ApplyContentFileStatusColor(item, status);
    }

    private (Dictionary<string, ContentPackageFileState> Files, string Label) BuildContentReference(
        WorkshopMetadata metadata,
        WorkshopUploadState state)
    {
        var itemId = metadata.WorkshopItemId ?? state.WorkshopItemId;
        if (itemId is > 0)
        {
            var installedPath = SteamWorkshopLookup.TryGetInstalledContentPath(itemId.Value);
            if (!string.IsNullOrWhiteSpace(installedPath))
            {
                var remoteFiles = StagingBuilder.EnumerateDirectoryFiles(installedPath)
                    .ToDictionary(
                        file => file.Path,
                        file => new ContentPackageFileState
                        {
                            Hash = file.Hash,
                            Size = file.Size,
                            LastWriteUtc = file.LastWriteUtc
                        },
                        StringComparer.OrdinalIgnoreCase);
                return (remoteFiles,
                    string.Format(T("Comparing with installed Workshop copy: {0}"),
                        WorkshopPaths.DisplayPath(installedPath)));
            }
        }

        var label = itemId is > 0
            ? T("Installed Workshop copy was not found. Comparing with last uploaded record instead.")
            : T("Comparing with last uploaded record.");
        return (new Dictionary<string, ContentPackageFileState>(state.ContentFiles, StringComparer.OrdinalIgnoreCase),
            label);
    }

    private void AddRow(
        string label,
        Control control,
        Control? extra = null,
        string? updateKey = null,
        string? updateLabel = null,
        bool updateValue = true)
    {
        var isLargeEditor = control is TextEdit ||
                            control.CustomMinimumSize.Y >= 100f ||
                            control is TextureRect ||
                            (control is HBoxContainer textureRow &&
                             textureRow.GetChildren().OfType<TextureRect>().Any()) ||
                            (control is HBoxContainer hbox && hbox.GetChildren().OfType<TextEdit>().Any());
        if (!isLargeEditor)
        {
            var compactRow = new HBoxContainer
            {
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                Alignment = BoxContainer.AlignmentMode.Center
            };
            compactRow.CustomMinimumSize = new Vector2(0f, 54f);
            compactRow.AddThemeConstantOverride("separation", RowGap);

            var rowLabel = CreateFormLabel(label);
            compactRow.AddChild(rowLabel);
            compactRow.AddChild(extra == null ? control : Row(control, extra));
            if (updateKey != null)
                compactRow.AddChild(CreateUpdateCheck(updateKey, updateLabel ?? "Update", updateValue));

            (_currentSection ?? _content).AddChild(compactRow);
            return;
        }

        var row = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        row.CustomMinimumSize = new Vector2(0f, ResolveLargeRowMinHeight(control));
        row.AddThemeConstantOverride("separation", 6);
        var labelRow = new HBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            Alignment = BoxContainer.AlignmentMode.Center
        };
        labelRow.AddThemeConstantOverride("separation", DenseMargin);
        labelRow.AddChild(CreateFormLabel(label, true));
        if (updateKey != null)
            labelRow.AddChild(CreateUpdateCheck(updateKey, updateLabel ?? "Update", updateValue));
        row.AddChild(labelRow);
        row.AddChild(extra == null ? control : Row(control, extra));
        (_currentSection ?? _content).AddChild(row);
    }

    private Control CreateUpdateCheck(string key, string label, bool value)
    {
        var row = new HBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ShrinkEnd,
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
            Alignment = BoxContainer.AlignmentMode.Center
        };
        row.AddThemeConstantOverride("separation", DenseMargin);

        row.AddChild(CreateChangeBadge(key));
        _updateChecks[key] = value;
        row.AddChild(CreateInlineLabel(label));
        var check = new ModSettingsToggleControl(value, checkedValue =>
        {
            _updateChecks[key] = checkedValue;
            SaveDraftMetadata();
        })
        {
            SizeFlagsHorizontal = SizeFlags.ShrinkEnd,
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
            CustomMinimumSize = new Vector2(88f, 38f)
        };
        row.AddChild(check);
        return row;
    }

    private static HBoxContainer ActionRow(params Control[] controls)
    {
        var row = Row(controls);
        row.Alignment = BoxContainer.AlignmentMode.End;
        return row;
    }

    private static Control ResultRow(string text, params Control[] actions)
    {
        var row = new HBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0f, 56f),
            Alignment = BoxContainer.AlignmentMode.Center
        };
        row.AddThemeConstantOverride("separation", DenseMargin);
        var label = CreateMuted(text);
        label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        label.TextOverrunBehavior = TextServer.OverrunBehavior.NoTrimming;
        row.AddChild(label);
        foreach (var action in actions)
            row.AddChild(action);
        return row;
    }

    private static Control DependencyRow(string text, params Control[] actions)
    {
        var row = new HBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0f, 42f),
            Alignment = BoxContainer.AlignmentMode.Center
        };
        row.AddThemeConstantOverride("separation", DenseMargin);
        var label = CreateMuted(text);
        label.AutowrapMode = TextServer.AutowrapMode.Off;
        label.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        row.AddChild(label);
        foreach (var action in actions)
            row.AddChild(action);
        return row;
    }

    private static GridContainer CreateDependencyGrid()
    {
        var grid = new GridContainer
        {
            Columns = 2,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        grid.AddThemeConstantOverride("h_separation", DenseMargin);
        grid.AddThemeConstantOverride("v_separation", 4);
        return grid;
    }

    private static float ResolveLargeRowMinHeight(Control control)
    {
        var min = control.CustomMinimumSize.Y;
        if (control is HBoxContainer hbox)
            min = Mathf.Max(min, hbox.GetChildren().OfType<Control>()
                .Select(child => child.CustomMinimumSize.Y)
                .DefaultIfEmpty(0f)
                .Max());

        return Mathf.Max(96f, min + 42f);
    }

    private Control CreateChangeBadge(string key)
    {
        var (text, color) = ResolveChangeBadge(key);
        var (panel, label) = CreateBadgeWithLabel(text, color);
        if (!_changeBadgeLabels.TryGetValue(key, out var labels))
        {
            labels = [];
            _changeBadgeLabels[key] = labels;
        }

        labels.Add(label);
        return panel;
    }

    private (string Text, Color Color) ResolveChangeBadge(string key)
    {
        if (_detectedChanges == null)
            return (T("Unknown"), new Color(0.72f, 0.66f, 0.56f, 0.95f));

        if (key.Equals("content", StringComparison.OrdinalIgnoreCase))
            return (T("Preview required"), new Color(0.72f, 0.66f, 0.56f, 0.95f));

        var compareKey = ResolveChangeKeyForCurrentLanguage(key);
        if (compareKey.Equals("localized", StringComparison.OrdinalIgnoreCase))
        {
            var count = CountChangedLanguages();
            return count > 0
                ? (string.Format(T("{0} languages changed"), count), new Color(0.95f, 0.72f, 0.38f, 0.98f))
                : (T("Unchanged"), new Color(0.58f, 0.74f, 0.62f, 0.95f));
        }

        var known = compareKey.Equals("localized", StringComparison.OrdinalIgnoreCase)
            ? HasAnyLocalizedChange()
            : _detectedChanges.Contains(compareKey);
        return known
            ? (T("Changed"), new Color(0.95f, 0.72f, 0.38f, 0.98f))
            : (T("Unchanged"), new Color(0.58f, 0.74f, 0.62f, 0.95f));
    }

    private string ResolveChangeKeyForCurrentLanguage(string key)
    {
        if ((key.Equals("title", StringComparison.OrdinalIgnoreCase) ||
             key.Equals("description", StringComparison.OrdinalIgnoreCase)) &&
            !string.Equals(_metadataLanguage, "english", StringComparison.OrdinalIgnoreCase))
            return $"localized:{_metadataLanguage}:{key}";

        return key;
    }

    private bool HasAnyLocalizedChange()
    {
        return CountChangedLanguages() > 0;
    }

    private int CountChangedLanguages()
    {
        if (_detectedChanges == null)
            return 0;

        var languages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (_detectedChanges.Contains("title") || _detectedChanges.Contains("description"))
            languages.Add("english");
        foreach (var change in _detectedChanges)
        {
            const string prefix = "localized:";
            if (!change.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                continue;

            var rest = change[prefix.Length..];
            var separator = rest.IndexOf(':', StringComparison.Ordinal);
            if (separator > 0)
                languages.Add(rest[..separator]);
        }

        return languages.Count;
    }

    private void RefreshChangedSlots(params string[] keys)
    {
        if (_selected == null || _detectedChanges == null || keys.Length == 0)
            return;

        var changedLanguageState = false;
        foreach (var rawKey in keys.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(rawKey))
                continue;

            var key = ResolveChangeKeyForCurrentLanguage(rawKey);
            changedLanguageState |= RefreshDetectedChangeKey(key);
            RefreshChangeBadge(rawKey);
            if (!string.Equals(key, rawKey, StringComparison.OrdinalIgnoreCase))
                RefreshChangeBadge(key);
        }

        if (keys.Any(key => key.Equals("title", StringComparison.OrdinalIgnoreCase) ||
                            key.Equals("description", StringComparison.OrdinalIgnoreCase) ||
                            key.StartsWith("localized:", StringComparison.OrdinalIgnoreCase)))
        {
            RefreshChangeBadge("localized");
            if (changedLanguageState)
                RefreshLanguageChoiceLabels();
        }
    }

    private bool RefreshDetectedChangeKey(string key)
    {
        if (_selected == null || _detectedChanges == null)
            return false;

        var fingerprint = ComputeCurrentFingerprint(key);
        if (fingerprint == null)
            return false;

        var state = WorkshopJson.Read<WorkshopUploadState>(WorkshopPaths.StateFile(_selected.Path)) ??
                    new WorkshopUploadState();
        var changed = !state.Fingerprints.TryGetValue(key, out var old) ||
                      !string.Equals(old, fingerprint, StringComparison.Ordinal);
        return changed ? _detectedChanges.Add(key) : _detectedChanges.Remove(key);
    }

    private string? ComputeCurrentFingerprint(string key)
    {
        if (_selected == null)
            return null;

        if (TryParseLocalizedChangeKey(key, out var language, out var field))
            return ComputeLocalizedFingerprint(language, field);

        var metadata = WorkshopMetadataEditor.LoadOrCreate(_selected);
        return key.ToLowerInvariant() switch
        {
            "title" => WorkshopFingerprint.Text(string.IsNullOrWhiteSpace(_title?.Text)
                ? metadata.Title
                : _title.Text.Trim()),
            "description" => WorkshopFingerprint.Text(string.IsNullOrWhiteSpace(_description?.Text)
                ? metadata.Description
                : MarkdownToSteamBbCode.Convert(_description.Text)),
            "visibility" => WorkshopFingerprint.Text(_visibilityValue),
            "tags" => WorkshopFingerprint.Text(string.Join('\n',
                _tagSelection.Order(StringComparer.OrdinalIgnoreCase))),
            "dependencies" => WorkshopFingerprint.Text(string.Join('\n',
                metadata.Dependencies.Order())),
            "gameversions" => WorkshopFingerprint.Text(FormatGameVersionFingerprintText()),
            "preview" => WorkshopFingerprint.File(WorkshopPaths.PreviewFile(_selected.Path)),
            _ => null
        };
    }

    private string? ComputeLocalizedFingerprint(string language, string field)
    {
        if (_selected == null)
            return null;

        LocalizedWorkshopText? text = null;
        if (string.Equals(language, _metadataLanguage, StringComparison.OrdinalIgnoreCase))
            text = new LocalizedWorkshopText
            {
                Title = string.IsNullOrWhiteSpace(_title?.Text) ? null : _title.Text.Trim(),
                Description = string.IsNullOrWhiteSpace(_description?.Text)
                    ? null
                    : MarkdownToSteamBbCode.Convert(_description.Text)
            };
        else
            text = WorkshopTemplateService.LoadEffectiveMetadata(_selected)
                .Localized.GetValueOrDefault(language);

        return field.ToLowerInvariant() switch
        {
            "title" => WorkshopFingerprint.Text(text?.Title),
            "description" => WorkshopFingerprint.Text(text?.Description),
            _ => null
        };
    }

    private string FormatGameVersionFingerprintText()
    {
        var min = BlankToNull(_minBranch?.Text ?? string.Empty);
        var max = BlankToNull(_maxBranch?.Text ?? string.Empty);
        return $"{min}\n{max}";
    }

    private static bool TryParseLocalizedChangeKey(string key, out string language, out string field)
    {
        language = string.Empty;
        field = string.Empty;
        const string prefix = "localized:";
        if (!key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return false;

        var rest = key[prefix.Length..];
        var separator = rest.IndexOf(':', StringComparison.Ordinal);
        if (separator <= 0 || separator == rest.Length - 1)
            return false;

        language = rest[..separator];
        field = rest[(separator + 1)..];
        return true;
    }

    private void RefreshChangeBadge(string key)
    {
        if (!_changeBadgeLabels.TryGetValue(key, out var labels))
            return;

        var (text, color) = ResolveChangeBadge(key);
        foreach (var label in labels.Where(IsInstanceValid))
        {
            label.Text = text;
            label.AddThemeColorOverride("font_color", color);
        }
    }

    private void RefreshChangeState()
    {
        if (_selected == null)
            return;

        _detectedChanges = TryDetectMetadataChanges(_selected);
        foreach (var (key, labels) in _changeBadgeLabels)
        {
            var (text, color) = ResolveChangeBadge(key);
            foreach (var label in labels.Where(IsInstanceValid))
            {
                label.Text = text;
                label.AddThemeColorOverride("font_color", color);
            }
        }

        RefreshLanguageChoiceLabels();
    }

    private void RefreshLanguageChoiceLabels()
    {
        if (_selected == null || _metadataLanguageSelect == null || !IsInstanceValid(_metadataLanguageSelect))
            return;

        _loadingMetadataLanguage = true;
        try
        {
            _metadataLanguageSelect.SetOptions(GetMetadataLanguageChoices(_selected).ToArray(), _metadataLanguage);
        }
        finally
        {
            _loadingMetadataLanguage = false;
        }
    }

    private void TrackDraftChanges(LineEdit line, params string[] keys)
    {
        line.TextChanged += _ => MarkDraftTextDirty(keys);
        line.FocusExited += CommitDraftTextIfDirty;
    }

    private void TrackDraftChanges(TextEdit text, params string[] keys)
    {
        text.TextChanged += () => MarkDraftTextDirty(keys);
        text.FocusExited += CommitDraftTextIfDirty;
    }

    private static HashSet<string>? TryDetectMetadataChanges(LocalModInfo mod)
    {
        try
        {
            return WorkshopUploadPlanner.Create(mod, WorkshopUploadMode.MetadataOnly).ChangedKeys;
        }
        catch (Exception ex)
        {
            Main.Logger.Warn($"Could not detect metadata changes for {mod.Id}: {ex.Message}");
            return null;
        }
    }

    private static HBoxContainer Row(params Control[] controls)
    {
        var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        row.AddThemeConstantOverride("separation", DenseMargin);
        foreach (var control in controls)
        {
            if (control.SizeFlagsHorizontal == 0)
                control.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            row.AddChild(control);
        }

        return row;
    }

    private static Control FixedSize(Control child, float width, float height)
    {
        var frame = new Control
        {
            ClipContents = true,
            SizeFlagsHorizontal = SizeFlags.Fill,
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
            CustomMinimumSize = new Vector2(width, height)
        };
        child.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        child.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        child.SizeFlagsVertical = SizeFlags.ExpandFill;
        frame.AddChild(child);
        return frame;
    }

    private static Control FixedField(Control child, float width, float height = 44f)
    {
        var frame = new Control
        {
            ClipContents = true,
            SizeFlagsHorizontal = SizeFlags.Fill,
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
            CustomMinimumSize = new Vector2(width, height)
        };
        child.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        child.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        child.SizeFlagsVertical = SizeFlags.ExpandFill;
        frame.AddChild(child);
        return frame;
    }

    private static Control FlexibleField(Control child, float minWidth, float height = 44f)
    {
        var frame = new Control
        {
            ClipContents = true,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
            CustomMinimumSize = new Vector2(minWidth, height)
        };
        child.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        child.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        child.SizeFlagsVertical = SizeFlags.ExpandFill;
        frame.AddChild(child);
        return frame;
    }

    private static HBoxContainer RightAligned(params Control[] controls)
    {
        var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        row.AddThemeConstantOverride("separation", DenseMargin);
        row.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        foreach (var control in controls)
            row.AddChild(control);
        return row;
    }


    private static Control ClipControl(Control child, float minWidth = 260f, bool expand = true)
    {
        var clip = new Control
        {
            ClipContents = true,
            SizeFlagsHorizontal = expand ? SizeFlags.ExpandFill : SizeFlags.ShrinkBegin,
            CustomMinimumSize = new Vector2(minWidth, 42f)
        };
        child.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        child.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        clip.AddChild(child);
        return clip;
    }

    private static (Control Panel, Label Label) CreateBadgeWithLabel(string text, Color color)
    {
        var panel = new PanelContainer
        {
            SizeFlagsHorizontal = SizeFlags.ShrinkEnd,
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
            CustomMinimumSize = new Vector2(150f, 30f),
            MouseFilter = MouseFilterEnum.Ignore
        };
        panel.AddThemeStyleboxOverride("panel", ModSettingsUiFactory.CreatePillStyle());

        var label = new Label
        {
            Text = text,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
            MouseFilter = MouseFilterEnum.Ignore
        };
        label.AddThemeFontOverride("font", RitsuShellTheme.Current.Font.BodyBold);
        label.AddThemeFontSizeOverride("font_size", RitsuShellTheme.Current.Metric.FontSize.HintSmall);
        label.AddThemeColorOverride("font_color", color);
        panel.AddChild(WrapWithMargin(label, 6));
        return (panel, label);
    }

    private static void AddPopupRow(VBoxContainer parent, string label, Control control)
    {
        var row = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        row.AddThemeConstantOverride("separation", 6);
        row.AddChild(CreateMuted(label));
        row.AddChild(control);
        parent.AddChild(row);
    }

    private static Control CreatePopup(string name)
    {
        var popup = new Control
        {
            Name = name,
            Visible = false,
            MouseFilter = MouseFilterEnum.Stop
        };
        popup.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var dim = new ColorRect
        {
            Color = RitsuShellTheme.Current.Color.ModalBackdrop,
            MouseFilter = MouseFilterEnum.Stop
        };
        dim.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        popup.AddChild(dim);

        var panel = CreatePanel();
        panel.Name = "Panel";
        panel.MouseFilter = MouseFilterEnum.Stop;
        popup.AddChild(panel);
        return popup;
    }

    private static VBoxContainer CreatePopupBody(Control popup, bool scrollBody = true)
    {
        var panel = popup.GetNode<PanelContainer>("Panel");
        var root = new VBoxContainer
        {
            Name = "PopupRoot",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        root.AddThemeConstantOverride("separation", RowGap);

        var body = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        body.AddThemeConstantOverride("separation", RowGap);
        if (scrollBody)
        {
            var scroll = new ScrollContainer
            {
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                SizeFlagsVertical = SizeFlags.ExpandFill,
                HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
                VerticalScrollMode = ScrollContainer.ScrollMode.Auto
            };
            ThemeScroll(scroll);
            AddScrollableContent(scroll, body);
            root.AddChild(scroll);
        }
        else
        {
            root.AddChild(body);
        }

        var footer = new HBoxContainer
        {
            Name = "Footer",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            Alignment = BoxContainer.AlignmentMode.End
        };
        footer.AddThemeConstantOverride("separation", DenseMargin);
        root.AddChild(footer);

        var wrap = WrapWithMargin(root, PopupMargin);
        wrap.Name = "PopupMargin";
        panel.AddChild(wrap);
        return body;
    }

    private static Control CreateScrollableListSection(string title, Control list, float minHeight)
    {
        var panel = CreatePanel(ModSettingsUiFactory.CreateInsetSurfaceStyle());
        panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        panel.SizeFlagsVertical = SizeFlags.ExpandFill;
        panel.CustomMinimumSize = new Vector2(0f, minHeight);

        var body = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        body.AddThemeConstantOverride("separation", DenseMargin);
        body.AddChild(CreateTitle(title, 18));

        var scroll = new ScrollContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto
        };
        ThemeScroll(scroll);
        AddScrollableContent(scroll, list);
        body.AddChild(scroll);

        panel.AddChild(WrapWithMargin(body, DenseMargin));
        return panel;
    }

    private static void SetPopupFooter(Control popup, params Control[] controls)
    {
        var footer = popup.GetNode<HBoxContainer>("Panel/PopupMargin/PopupRoot/Footer");
        Clear(footer);
        foreach (var control in controls)
            footer.AddChild(control);
    }

    private void ShowPopup(Control popup, int desiredWidth, int desiredHeight)
    {
        _popupDesiredSizes[popup] = new Vector2I(desiredWidth, desiredHeight);
        var viewport = GetViewportRect().Size;
        var sideMargin = ResolvePopupSideMargin(viewport.X);
        var topMargin = ResolvePopupTopMargin(viewport.Y);
        var availableWidth = Math.Max(420f, viewport.X - sideMargin * 2f);
        var availableHeight = Math.Max(320f, viewport.Y - topMargin * 2f);
        var width = Mathf.Min(desiredWidth, availableWidth);
        var height = Mathf.Min(desiredHeight, availableHeight);
        popup.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        popup.Show();
        popup.MoveToFront();

        var panel = popup.GetNode<PanelContainer>("Panel");
        panel.SetAnchorsAndOffsetsPreset(LayoutPreset.TopLeft);
        panel.Scale = Vector2.One;
        panel.Rotation = 0f;
        panel.ResetSize();
        panel.Position = new Vector2(Mathf.Max(sideMargin, (viewport.X - width) * 0.5f),
            Mathf.Max(topMargin, (viewport.Y - height) * 0.5f));
        panel.Size = new Vector2(width, height);
        panel.SetDeferred(Control.PropertyName.Size, new Vector2(width, height));
        panel.CustomMinimumSize = Vector2.Zero;
        RequestRecursiveLayout(panel);
    }

    private static float ResolvePopupSideMargin(float width)
    {
        return Mathf.Clamp(width * 0.045f, 24f, 64f);
    }

    private static float ResolvePopupTopMargin(float height)
    {
        return Mathf.Clamp(height * 0.055f, 24f, 56f);
    }

    private void BeginTaskOverlay(string title)
    {
        _taskOverlayTitle.Text = title;
        _taskOverlayMessage.Text = "";
        _taskOverlayProgress.Value = 0;
        _taskOverlayError.Visible = false;
        _taskOverlayError.Text = "";
        _taskOverlayClose.Visible = false;
        ShowTaskOverlay();
    }

    private void ShowTaskOverlay()
    {
        var viewport = GetViewportRect().Size;
        var width = Math.Min(820f, Math.Max(560f, viewport.X - 96f));
        var height = Math.Min(260f, Math.Max(220f, viewport.Y - 120f));
        _taskOverlay.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _taskOverlay.Show();
        _taskOverlay.MoveToFront();
        _taskOverlay.GrabFocus();

        _taskOverlayPanel.SetAnchorsAndOffsetsPreset(LayoutPreset.TopLeft);
        _taskOverlayPanel.Scale = Vector2.One;
        _taskOverlayPanel.Rotation = 0f;
        _taskOverlayPanel.ResetSize();
        _taskOverlayPanel.Position = new Vector2(Mathf.Max(24f, (viewport.X - width) * 0.5f),
            Mathf.Max(24f, (viewport.Y - height) * 0.5f));
        _taskOverlayPanel.Size = new Vector2(width, height);
        _taskOverlayPanel.SetDeferred(Control.PropertyName.Size, new Vector2(width, height));
        _taskOverlayPanel.CustomMinimumSize = Vector2.Zero;
        LayoutTaskOverlay(width, height);
    }

    private void LayoutTaskOverlay(float width, float height)
    {
        const float margin = 16f;
        const float gap = 10f;
        var contentWidth = Math.Max(1f, width - margin * 2f);
        var titleHeight = 34f;
        var progressHeight = 22f;
        var closeHeight = _taskOverlayClose.Visible ? ButtonMinHeight : 0f;
        var closeGap = _taskOverlayClose.Visible ? gap : 0f;
        var errorHeight = _taskOverlayError.Visible ? 42f : 0f;
        var errorGap = _taskOverlayError.Visible ? gap : 0f;
        var messageTop = margin + titleHeight + gap;
        var progressTop = height - margin - closeHeight - closeGap - errorHeight - errorGap - progressHeight;
        var messageHeight = Math.Max(34f, progressTop - messageTop - gap);

        _taskOverlayTitle.Position = new Vector2(margin, margin);
        _taskOverlayTitle.Size = new Vector2(contentWidth, titleHeight);
        _taskOverlayMessage.Position = new Vector2(margin, messageTop);
        _taskOverlayMessage.Size = new Vector2(contentWidth, messageHeight);
        _taskOverlayProgress.Position = new Vector2(margin, progressTop);
        _taskOverlayProgress.Size = new Vector2(contentWidth, progressHeight);

        if (_taskOverlayError.Visible)
        {
            var errorTop = progressTop + progressHeight + gap;
            _taskOverlayError.Position = new Vector2(margin, errorTop);
            _taskOverlayError.Size = new Vector2(contentWidth, errorHeight);
        }

        if (_taskOverlayClose.Visible)
        {
            _taskOverlayClose.Position = new Vector2(width - margin - ButtonMinWidth, height - margin - closeHeight);
            _taskOverlayClose.Size = new Vector2(ButtonMinWidth, closeHeight);
        }
    }

    private void UpdateTaskOverlay(string message, string detail, double progress)
    {
        if (!_taskOverlay.Visible)
            return;

        _taskOverlayMessage.Text = string.IsNullOrWhiteSpace(detail) ? message : $"{message}\n{detail}";
        _taskOverlayProgress.Value = Math.Clamp(progress, 0, 100);
        LayoutTaskOverlay(_taskOverlayPanel.Size.X, _taskOverlayPanel.Size.Y);
    }

    private void UpdateSteamUploadProgress(WorkshopUploadProgress progress)
    {
        if (!_taskOverlay.Visible)
            return;

        var message = progress.Operation switch
        {
            WorkshopUploadProgressOperation.LocalizedUpdate when !string.IsNullOrWhiteSpace(progress.Language) =>
                string.Format(T("Submitting localized text to Steam Workshop ({0})..."), progress.Language),
            _ => _activeUploadMode == WorkshopUploadMode.MetadataOnly
                ? T("Submitting metadata to Steam Workshop...")
                : T("Submitting content and metadata to Steam Workshop...")
        };
        var detail = $"{T("Status")}: {FormatSteamUploadStatus(progress.Status)}\n{FormatSteamUploadBytes(progress)}";
        UpdateTaskOverlay(message, detail, ResolveSteamUploadPercent(progress));
    }

    private double ResolveSteamUploadPercent(WorkshopUploadProgress progress)
    {
        if (progress.BytesTotal == 0)
            return _taskOverlayProgress.Value;

        return Math.Clamp(progress.BytesProcessed * 100d / progress.BytesTotal, 0d, 100d);
    }

    private string FormatSteamUploadBytes(WorkshopUploadProgress progress)
    {
        if (progress.BytesTotal > 0)
        {
            var percent = ResolveSteamUploadPercent(progress);
            return string.Format(
                T("{0} / {1} ({2:0.#}%)"),
                FormatBytes(progress.BytesProcessed),
                FormatBytes(progress.BytesTotal),
                percent);
        }

        return progress.BytesProcessed > 0
            ? string.Format(T("Uploaded: {0}"), FormatBytes(progress.BytesProcessed))
            : T("Waiting for Steam progress data.");
    }

    private string FormatSteamUploadStatus(WorkshopItemUpdateProgressStatus status)
    {
        return status switch
        {
            WorkshopItemUpdateProgressStatus.PreparingConfig => T("Preparing update metadata"),
            WorkshopItemUpdateProgressStatus.PreparingContent => T("Preparing content files"),
            WorkshopItemUpdateProgressStatus.UploadingContent => T("Uploading content files"),
            WorkshopItemUpdateProgressStatus.UploadingPreviewFile => T("Uploading preview image"),
            WorkshopItemUpdateProgressStatus.CommittingChanges => T("Committing Steam Workshop changes"),
            _ => T("Waiting for Steam progress data")
        };
    }

    private void EndTaskOverlay()
    {
        _taskOverlay.Hide();
    }

    private void ShowTaskError(string title, Exception ex)
    {
        ShowTaskError(title, ex.Message);
    }

    private void ShowTaskError(string title, string message)
    {
        if (!_taskOverlay.Visible)
            BeginTaskOverlay(title);

        _taskOverlayTitle.Text = title;
        _taskOverlayMessage.Text = T("Operation failed.");
        _taskOverlayProgress.Value = 100;
        _taskOverlayError.Text = message;
        _taskOverlayError.Visible = true;
        _taskOverlayClose.Visible = true;
        LayoutTaskOverlay(_taskOverlayPanel.Size.X, _taskOverlayPanel.Size.Y);
        _taskOverlay.MoveToFront();
        _taskOverlayClose.GrabFocus();
    }

    private void ShowInfoToast(string body, string title, ulong? workshopItemId = null)
    {
        var displayBody = workshopItemId == null
            ? body
            : $"{body} {T("Click to open the Workshop page.")}";
        RitsuToastService.Show(new RitsuToastRequest(
            displayBody,
            title,
            null,
            RitsuToastLevel.Info,
            ToastDurationSeconds,
            workshopItemId == null ? null : () => OpenWorkshopPage(workshopItemId)));
    }

    private static void ShowErrorToast(string body, string title)
    {
        RitsuToastService.Show(new RitsuToastRequest(
            body,
            title,
            null,
            RitsuToastLevel.Error,
            ToastDurationSeconds));
    }

    private void OpenCurrentWorkshopPage()
    {
        OpenWorkshopPage(ResolveCurrentWorkshopItemId());
    }

    private Button CreateOpenWorkshopButton()
    {
        var button = CreateButton(T("Open Workshop Page"), OpenCurrentWorkshopPage);
        _openWorkshopButtons.Add(button);
        button.Disabled = ResolveCurrentWorkshopItemId() == null;
        return button;
    }

    private void UpdateWorkshopPageButtons()
    {
        var disabled = ResolveCurrentWorkshopItemId() == null;
        foreach (var button in _openWorkshopButtons)
            button.Disabled = disabled;
    }

    private void OpenWorkshopPage(ulong? itemId)
    {
        if (itemId == null || itemId.Value == 0)
        {
            ShowErrorToast(T("No Workshop item id is bound."), T("Open Workshop Page"));
            return;
        }

        OS.ShellOpen($"https://steamcommunity.com/sharedfiles/filedetails/?id={itemId.Value}");
    }

    private ulong? ResolveCurrentWorkshopItemId()
    {
        if (ulong.TryParse(_workshopId?.Text.Trim(), out var editId))
            return editId;

        if (_selected == null)
            return null;

        var metadata = WorkshopMetadataEditor.LoadOrCreate(_selected);
        if (metadata.WorkshopItemId != null)
            return metadata.WorkshopItemId;
        return WorkshopJson.Read<WorkshopUploadState>(WorkshopPaths.StateFile(_selected.Path))?.WorkshopItemId;
    }

    private static PanelContainer CreatePanel()
    {
        return CreatePanel(ModSettingsUiFactory.CreateListShellStyle());
    }

    private static PanelContainer CreatePanel(StyleBox style)
    {
        var panel = new PanelContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore
        };
        panel.AddThemeStyleboxOverride("panel", style);
        return panel;
    }

    private static StyleBoxFlat CreateModListHoverStyle(bool selected)
    {
        var style = RitsuShellPanelStyles.CreateSidebarModCardCompact(
            RitsuShellTheme.Current.Metric.Radius.Default,
            selected,
            10);
        style.BorderColor = RitsuShellTheme.Current.Text.HoverHighlight;
        style.ShadowColor = new Color(
            style.BorderColor.R,
            style.BorderColor.G,
            style.BorderColor.B,
            selected ? 0.38f : 0.24f);
        style.ShadowSize = selected ? 6 : 4;
        return style;
    }

    private static void ApplyModListItemStyle(PanelContainer card, bool selected, bool highlighted)
    {
        card.AddThemeStyleboxOverride("panel", highlighted
            ? CreateModListHoverStyle(selected)
            : RitsuShellPanelStyles.CreateSidebarModCardCompact(
                RitsuShellTheme.Current.Metric.Radius.Default,
                selected,
                10));
    }

    private static MarginContainer WrapWithMargin(Control child, int margin)
    {
        var wrap = new MarginContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore
        };
        wrap.AddThemeConstantOverride("margin_left", margin);
        wrap.AddThemeConstantOverride("margin_top", margin);
        wrap.AddThemeConstantOverride("margin_right", margin);
        wrap.AddThemeConstantOverride("margin_bottom", margin);
        wrap.AddChild(child);
        return wrap;
    }

    private static Button CreateButton(string text, Action action,
        ModSettingsButtonTone tone = ModSettingsButtonTone.Normal,
        bool listRow = false)
    {
        var button = new ModSettingsTextButton(text, tone, action)
        {
            CustomMinimumSize = listRow ? new Vector2(0f, 56f) : new Vector2(ButtonMinWidth, ButtonMinHeight),
            FocusMode = FocusModeEnum.All,
            SizeFlagsHorizontal = listRow ? SizeFlags.ExpandFill : SizeFlags.ShrinkEnd
        };
        if (listRow)
            button.Alignment = HorizontalAlignment.Left;
        return button;
    }

    private static Button CreateSmallButton(string text, Action action,
        ModSettingsButtonTone tone = ModSettingsButtonTone.Normal)
    {
        return new ModSettingsTextButton(text, tone, action)
        {
            CustomMinimumSize = new Vector2(108f, 38f),
            FocusMode = FocusModeEnum.All,
            SizeFlagsHorizontal = SizeFlags.ShrinkEnd
        };
    }

    private static Button CreateIconButton(string text, Action action)
    {
        return new ModSettingsTextButton(text, ModSettingsButtonTone.Normal, action)
        {
            CustomMinimumSize = new Vector2(44f, 38f),
            FocusMode = FocusModeEnum.All,
            SizeFlagsHorizontal = SizeFlags.ShrinkBegin
        };
    }

    private static LineEdit CreateLine(string value)
    {
        var line = new LineEdit
        {
            Text = value,
            SelectAllOnFocus = false,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0f, RitsuShellTheme.Current.Metric.Entry.ValueMinHeight)
        };
        line.CaretColumn = value.Length;
        ModSettingsUiControlTheming.ApplyEntryLineEditValueFieldTheme(line, RitsuShellTheme.Current.Font.Body);
        return line;
    }

    private static TextEdit CreateText(string value)
    {
        var text = new TextEdit
        {
            Text = value,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0f, 180f),
            WrapMode = TextEdit.LineWrappingMode.Boundary
        };
        ModSettingsUiControlTheming.ApplyEntryTextEditValueFieldTheme(text, RitsuShellTheme.Current.Font.Body);
        return text;
    }

    private string FormatWorkshopResult(WorkshopItemSummary result)
    {
        return $"{result.Title} ({result.Id})";
    }

    private string FormatDependency(ulong id)
    {
        return _dependencyTitles.TryGetValue(id, out var item)
            ? $"{item.Title} ({id})"
            : id.ToString();
    }

    private Control? FirstVisiblePopup()
    {
        if (_excludePopup?.Visible == true)
            return _excludePopup;
        if (_tagPopup?.Visible == true)
            return _tagPopup;
        if (_languagePopup?.Visible == true)
            return _languagePopup;
        if (_dependencyPopup?.Visible == true)
            return _dependencyPopup;
        if (_bindPopup?.Visible == true)
            return _bindPopup;
        return null;
    }

    private static Label CreateFormLabel(string text, bool expand = false)
    {
        var label = CreateMuted(text);
        label.CustomMinimumSize = expand ? Vector2.Zero : new Vector2(180f, 0f);
        label.SizeFlagsHorizontal = expand ? SizeFlags.ExpandFill : SizeFlags.ShrinkBegin;
        label.AutowrapMode = expand ? TextServer.AutowrapMode.WordSmart : TextServer.AutowrapMode.Off;
        label.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        return label;
    }

    private static Label CreateInlineLabel(string text)
    {
        var label = CreateMuted(text);
        label.SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
        label.AutowrapMode = TextServer.AutowrapMode.Off;
        label.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        return label;
    }

    private static Label CreateInlineStrong(string text)
    {
        var label = CreateMuted(text);
        label.AddThemeFontOverride("font", RitsuShellTheme.Current.Font.BodyBold);
        label.AutowrapMode = TextServer.AutowrapMode.Off;
        label.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        return label;
    }

    private static Label CreateTitle(string text, int size = 26)
    {
        var label = new Label
        {
            Text = text,
            AutowrapMode = TextServer.AutowrapMode.Off,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        label.AddThemeFontOverride("font", RitsuShellTheme.Current.Font.BodyBold);
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", RitsuShellTheme.Current.Text.LabelPrimary);
        return label;
    }

    private static Label CreateMuted(string text)
    {
        var label = new Label
        {
            Text = text,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        label.AddThemeFontOverride("font", RitsuShellTheme.Current.Font.Body);
        label.AddThemeFontSizeOverride("font_size", RitsuShellTheme.Current.Metric.FontSize.ValueLabel);
        label.AddThemeColorOverride("font_color", RitsuShellTheme.Current.Text.LabelPrimary);
        return label;
    }

    private static ModSettingsDropdownChoiceControl<string> CreateDropdown(
        IReadOnlyList<string> options,
        string value,
        Action<string> onChanged)
    {
        return CreateDropdown(options.Select(option => (option, option)).ToArray(), value, onChanged);
    }

    private static ModSettingsDropdownChoiceControl<string> CreateDropdown(
        IReadOnlyList<(string Value, string Label)> options,
        string value,
        Action<string> onChanged)
    {
        return new ModSettingsDropdownChoiceControl<string>(
            options.Select(option => (option.Value, option.Label)).ToArray(),
            value,
            onChanged)
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(260f, 42f)
        };
    }

    private static void ThemeScroll(ScrollContainer scroll)
    {
        ModSettingsUiControlTheming.ApplySettingsScrollContainerTheme(scroll);
    }

    private static void AddScrollableContent(ScrollContainer scroll, Control content)
    {
        var gutter = new MarginContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore
        };
        gutter.AddThemeConstantOverride("margin_right", ScrollbarGutter);
        content.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        gutter.AddChild(content);
        scroll.AddChild(gutter);
    }

    private static void ThemeContentTree(Tree tree)
    {
        tree.AddThemeFontOverride("font", RitsuShellTheme.Current.Font.Body);
        tree.AddThemeFontOverride("title_button_font", RitsuShellTheme.Current.Font.BodyBold);
        tree.AddThemeFontSizeOverride("font_size", RitsuShellTheme.Current.Metric.FontSize.ValueLabel);
        tree.AddThemeFontSizeOverride("title_button_font_size", RitsuShellTheme.Current.Metric.FontSize.ValueLabel);
        tree.AddThemeColorOverride("font_color", RitsuShellTheme.Current.Text.LabelPrimary);
        tree.AddThemeColorOverride("font_selected_color", RitsuShellTheme.Current.Text.LabelPrimary);
        tree.AddThemeColorOverride("title_button_color", RitsuShellTheme.Current.Text.LabelPrimary);
        tree.AddThemeColorOverride("children_hl_line_color", RitsuShellTheme.Current.Text.LabelSecondary);
        tree.AddThemeConstantOverride("v_separation", 8);
        tree.AddThemeConstantOverride("h_separation", 14);
        tree.AddThemeConstantOverride("item_margin", 8);
        tree.AddThemeConstantOverride("button_margin", 8);
    }

    private void SetActivity(string text)
    {
        if (_activity != null)
            _activity.Text = text;
    }

    private void Fail(string label, Exception ex)
    {
        SetActivity($"{label}: {ex.Message}");
        Main.Logger.Error($"[Audit] Operation failed. Error={ex}");
    }

    private static void Clear(Node node)
    {
        foreach (var child in node.GetChildren())
            child.QueueFree();
    }

    private static string ReadText(string path, string fallback)
    {
        return File.Exists(path) ? File.ReadAllText(path) : fallback;
    }

    private static string? BlankToNull(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string FormatKeys(IEnumerable<string> keys)
    {
        var values = keys
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return values.Length == 0 ? "<none>" : string.Join(",", values);
    }

    private static string T(string key)
    {
        return WorkshopUploaderText.Resolve(key, key);
    }

    private enum ContentFileStatus
    {
        New,
        Modified,
        Unchanged,
        Deleted
    }
}
