using ARealmRepopulated.Configuration;
using ARealmRepopulated.Core.ArrpGui.Components;
using ARealmRepopulated.Core.ArrpGui.Style;
using ARealmRepopulated.Core.l10n;
using ARealmRepopulated.Core.Native;
using ARealmRepopulated.Core.Services.Npcs;
using ARealmRepopulated.Core.Services.Scenarios;
using ARealmRepopulated.Core.Services.Windows;
using ARealmRepopulated.Data.Appearance;
using ARealmRepopulated.Data.Location;
using ARealmRepopulated.Data.Scenarios;
using ARealmRepopulated.Infrastructure;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using System.IO;
using System.Numerics;
using System.Text.Json;
namespace ARealmRepopulated.Windows;

public partial class ScenarioEditorWindow(
    PluginConfig config,
    DebugOverlay debugOverlay,
    ScenarioFileManager scenarioFileManager,
    NpcAppearanceService appearanceService,
    NpcAppearanceDataParser appearanceDataParser,
    ArrpDataCache dataCache,
    ArrpCharacterCreationData characterCreationData,
    ArrpEventService eventService,
    ArrpGuiBNpcPicker bnpcPresetPicker,
    ArrpGuiEmotePicker emotePicker,
    ArrpGuiNpcPicker npcPicker,
    ArrpGuiTimelinePicker timelinePicker,
    ArrpTranslation loc,
    FileDialogManager fileDialogManager,
    IObjectTable objectTable,
    ITargetManager _targetManager) : ADalamudWindow($"Scenario Editor###ARealmRepopulatedScenarioConfigWindow") {

    private const string AddActionPopupId = "##arrpScenarioEditorAddAction";    
    private const float OutlineDefaultWidth = 250f;

    private const ImGuiTreeNodeFlags OutlineLeafFlags = ImGuiTreeNodeFlags.Leaf
        | ImGuiTreeNodeFlags.Bullet
        | ImGuiTreeNodeFlags.NoTreePushOnOpen
        | ImGuiTreeNodeFlags.SpanAvailWidth;

    private string _scenarioFilePath = string.Empty;
    private readonly NpcActionUiRegistry _actionUiRegistry = new();    
    private readonly HashSet<string> _collapsedActors = [];
            
    private static float FooterHeight
        => ImGui.GetStyle().ItemSpacing.Y + 1f + ImGui.GetFrameHeightWithSpacing() + (ArrpGuiSpacing.FooterCellPadding.Y * 2f);

    public ScenarioData ScenarioObject { get; private set; } = null!;
    public ScenarioNpcData? SelectedScenarioNpc { get; private set; } = null;
    public ScenarioNpcAction? SelectedScenarioNpcAction { get; private set; } = null!;
    public PathMovementPoint? SelectedPathMovementPoint { get; private set; } = null;
    public ScenarioEditorGizmoTarget SelectedGizmoTarget { get; private set; } = ScenarioEditorGizmoTarget.Position;

    public Guid UniqueScenarioId { get; set; } = Guid.NewGuid();

    private readonly TransferState _importState = new() { DefaultIcon = FontAwesomeIcon.Download };
    private readonly TransferState _exportState = new() { DefaultIcon = FontAwesomeIcon.Upload };

    protected override void SetWindowOptions() {
        this.AllowPinning = false;
        this.AllowClickthrough = false;

        Flags = ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoCollapse;
        SizeConstraints = new WindowSizeConstraints {
            MinimumSize = new Vector2(640, 420),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
        this.Size = new Vector2(900, 760);
        this.SizeCondition = ImGuiCond.Appearing;

        this.WindowName = $"Scenario Editor###{UniqueScenarioId}";

        this.TitleBarButtons.Add(new TitleBarButton { Icon = FontAwesomeIcon.InfoCircle, ShowTooltip = DrawFrameTooltip });
        this.OnWindowClosed += () => {
            debugOverlay.RemoveEditor(this);
        };

        _actionUiRegistry.Register<ScenarioNpcWaitingAction>(
            shortName: (a) => loc["ScenarioEditor_ActorData_Actions_AWaiting_Short"],
            help: (a) => loc["ScenarioEditor_ActorData_Actions_AWaiting_Desc"],
            summary: (a) => a.Duration > 0f ? FormatSeconds(a.Duration) : loc["ScenarioEditor_Summary_Endless"],
            draw: DrawWaitingAction
        );
        _actionUiRegistry.Register<ScenarioNpcIdleAction>(
            shortName: (a) => loc["ScenarioEditor_ActorData_Actions_AIdle_Short"],
            help: (a) => loc["ScenarioEditor_ActorData_Actions_AIdle_Desc"],
            summary: (a) => a.Duration > 0f ? FormatSeconds(a.Duration) : string.Empty,
            draw: DrawIdleAction
        );
        _actionUiRegistry.Register<ScenarioNpcEmoteAction>(
            shortName: (a) => loc["ScenarioEditor_ActorData_Actions_AEmote_Short"],
            help: (a) => loc["ScenarioEditor_ActorData_Actions_AEmote_Desc"],
            summary: (a) => a.Emote == 0 ? string.Empty : dataCache.GetEmote(a.Emote).Name.ToString(),
            draw: DrawEmoteAction
        );
        _actionUiRegistry.Register<ScenarioNpcSpawnAction>(
            shortName: (a) => loc["ScenarioEditor_ActorData_Actions_ASpawn_Short"],
            help: (a) => loc["ScenarioEditor_ActorData_Actions_ASpawn_Desc"]
        );
        _actionUiRegistry.Register<ScenarioNpcDespawnAction>(
            shortName: (a) => loc["ScenarioEditor_ActorData_Actions_ADespawn_Short"],
            help: (a) => loc["ScenarioEditor_ActorData_Actions_ADespawn_Desc"]
        );
        _actionUiRegistry.Register<ScenarioNpcMovementAction>(
            shortName: (a) => loc["ScenarioEditor_ActorData_Actions_AMove_Short"],
            help: (a) => loc["ScenarioEditor_ActorData_Actions_AMove_Desc"],
            summary: (a) => DescribeSpeed(a),
            draw: DrawMovementAction
        );
        _actionUiRegistry.Register<ScenarioNpcPathAction>(
            shortName: (a) => loc["ScenarioEditor_ActorData_Actions_APath_Short"],
            help: (a) => loc["ScenarioEditor_ActorData_Actions_APath_Desc"],
            summary: (a) => loc["ScenarioEditor_Summary_Points", a.Points.Count],
            draw: DrawPathAction
        );
        _actionUiRegistry.Register<ScenarioNpcRotationAction>(
            shortName: (a) => loc["ScenarioEditor_ActorData_Actions_ARotation_Short"],
            help: (a) => loc["ScenarioEditor_ActorData_Actions_ARotation_Desc"],
            summary: (a) => a.TargetRotation.ToString("0.00"),
            draw: DrawRotationAction
        );
        _actionUiRegistry.Register<ScenarioNpcSyncAction>(
            shortName: (a) => loc["ScenarioEditor_ActorData_Actions_ASync_Short"],
            help: (a) => loc["ScenarioEditor_ActorData_Actions_ASync_Desc"]
        );
        _actionUiRegistry.Register<ScenarioNpcTimelineAction>(
            shortName: (a) => loc["ScenarioEditor_ActorData_Actions_ATimeline_Short"],
            help: (a) => loc["ScenarioEditor_ActorData_Actions_ATimeline_Desc"],
            summary: (a) => loc["ScenarioEditor_Summary_Slots", a.ActionSlots.Count],
            draw: DrawTimelineAction
        );

    }

    public void CreateScenario() {
        var location = eventService.CurrentLocation;
        InitScenarioStructures(new ScenarioData {
            Title = "New Scenario", Location = new ScenarioLocation {
                Territory = location.TerritoryType,
                Server = location.Server,
                HousingDivision = location.HousingDivision,
                HousingPlot = location.HousingPlot,
                HousingWard = location.HousingWard
            }
        }, string.Empty);
    }

    public void EditScenario(string filePath) {
        if (string.IsNullOrWhiteSpace(filePath)) {
            return;
        }

        var loadedScenarioObject = scenarioFileManager.LoadScenarioFile(filePath);
        if (loadedScenarioObject == null) {
            return;
        }

        InitScenarioStructures(loadedScenarioObject, filePath);
    }

    private void InitScenarioStructures(ScenarioData scenarioData, string filePath) {
        _scenarioFilePath = filePath;
        ScenarioObject = scenarioData;
        _collapsedActors.Clear();

        ResetSelectedNpc(ScenarioObject.Npcs.FirstOrDefault());
        
        debugOverlay.AddEditor(this);
        IsOpen = true;
    }
    

    private void DrawFrameTooltip() {
        using var tooltip = ImRaii.Tooltip();
        using var table = ImRaii.Table("##ScenarioEditFrameTooltipTable", 1, ImGuiTableFlags.NoSavedSettings);
        if (!table.Success)
            return;

        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        ImGui.TextDisabled(loc["ScenarioEditor_FileInfo_Name"]);

        var fileName = string.IsNullOrWhiteSpace(_scenarioFilePath) ? loc["ScenarioEditor_FileInfo_Unsaved"] : Path.GetFileName(_scenarioFilePath);
        ImGui.Text($"{fileName}");

        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        ImGui.TextDisabled(loc["ScenarioEditor_FileInfo_Location"]);
        ImGui.Text(loc[
            "ScenarioEditor_FileInfo_LocationDetails",
            ScenarioObject.Location.Server,
            ScenarioObject.Location.Territory,
            ScenarioObject.Location.HousingDivision,
            ScenarioObject.Location.HousingWard,
            ScenarioObject.Location.HousingPlot
            ]);

    }

    public override void Draw() {
        if (ScenarioObject == null)
            return;

        DrawHeaderBar();
        DrawBody();
        DrawFooter();
    }
    
    private void DrawHeaderBar() {
        ImGui.Dummy(ArrpGuiSpacing.VerticalComponentSpacing);

        ArrpGuiLayout.Badge(FontAwesomeIcon.Film);
        ImGui.SameLine(0, ArrpGuiSpacing.SegmentSpacing);
        ImGui.Text(ScenarioTitle());

        var isForeign = eventService.CurrentLocation.IsForeignLocation(ScenarioObject.Location);
        DrawHeaderSegment(DescribeTerritory(),
            icon: isForeign ? FontAwesomeIcon.EyeSlash : FontAwesomeIcon.Eye,
            muted: isForeign,
            iconTooltip: loc[isForeign ? "ScenarioEditor_Header_ForeignTerritory" : "ScenarioEditor_Header_CurrentTerritory"]);

        DrawHeaderSegment(Pluralize(ScenarioObject.Npcs.Count, "ScenarioEditor_Outline_ActorCountOne", "ScenarioEditor_Outline_ActorCount"));
        DrawHeaderSegment(ScenarioObject.Looping
            ? loc["ScenarioEditor_Header_Loop", FormatSeconds(ScenarioObject.LoopDelay)]
            : loc["ScenarioEditor_Header_NoLoop"]);

        ImGui.Dummy(ArrpGuiSpacing.VerticalComponentSpacing);
        ImGui.Separator();
        ImGui.Dummy(ArrpGuiSpacing.VerticalComponentSpacing);
    }
    
    private static void DrawHeaderSegment(string text, FontAwesomeIcon? icon = null, bool muted = true, string? iconTooltip = null) {
        ImGui.SameLine(0, ArrpGuiSpacing.SegmentSpacing);
        ImGui.TextDisabled("|");
        ImGui.SameLine(0, ArrpGuiSpacing.SegmentSpacing);

        if (icon.HasValue) {
            ArrpGuiLayout.Badge(icon.Value, muted ? ArrpGuiColors.NoteColor : ArrpGuiColors.TextColor, iconTooltip);
            ImGui.SameLine(0, ArrpGuiSpacing.InlineIconSpacing);
        }

        if (muted) {
            ImGui.TextDisabled(text);
        } else {
            ImGui.Text(text);
        }
    }

    private void DrawBody() {        
        using var body = ImRaii.Child("##arrpScenarioEditorBody", new Vector2(0, -FooterHeight), true);
        if (!body.Success)
            return;

        using var padding = ImRaii.PushStyle(ImGuiStyleVar.CellPadding, ArrpGuiSpacing.BodyCellPadding);
        using var table = ImRaii.Table("##arrpScenarioEditorBodyTable", 2,
            ImGuiTableFlags.Resizable | ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.NoSavedSettings);
        if (!table.Success)
            return;

        ImGui.TableSetupColumn("##arrpBodyOutline", ImGuiTableColumnFlags.WidthFixed, OutlineDefaultWidth);
        ImGui.TableSetupColumn("##arrpBodyInspector", ImGuiTableColumnFlags.WidthStretch);

        ImGui.TableNextRow();        
        ImGui.TableNextColumn();
        using (var outline = ImRaii.Child("##arrpScenarioEditorOutline", Vector2.Zero, false)) {
            if (outline.Success)
                DrawOutlinePane();
        }

        ImGui.TableNextColumn();
        using (var inspector = ImRaii.Child("##arrpScenarioEditorInspector", Vector2.Zero, false)) {
            if (inspector.Success)
                DrawInspectorPane();
        }
    }

    private void DrawFooter() {
        ImGui.Separator();        
        using var padding = ImRaii.PushStyle(ImGuiStyleVar.CellPadding, ArrpGuiSpacing.FooterCellPadding);
        using var table = ImRaii.Table("##arrpScenarioEditorFooter", 4, ImGuiTableFlags.NoSavedSettings);
        if (!table.Success)
            return;

        ImGui.TableSetupColumn("##arrpFooterTransfer", ImGuiTableColumnFlags.WidthFixed);
        ImGui.TableSetupColumn("##arrpFooterSpacer", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("##arrpFooterCommit", ImGuiTableColumnFlags.WidthFixed);
        ImGui.TableSetupColumn("##arrpFooterGrip", ImGuiTableColumnFlags.WidthFixed, ArrpGuiSpacing.WindowGripSpacing);

        ImGui.TableNextRow();
        ImGui.TableNextColumn();

        _importState.CheckState(out var importIcon, out var importColor);
        var importTooltip = loc["ScenarioEditor_BaseData_Action_Import_Desc"];
        if (_importState.Result.HasValue) {
            importTooltip = (bool)_importState.Result ? loc["ScenarioEditor_BaseData_Action_Import_Success"] : loc["ScenarioEditor_BaseData_Action_Import_Failed"];
        }
        if (ImGuiComponents.IconButton("##arrpScenarioEditorImport", importIcon, importColor)) {
            _importState.SetResult(false);
            var clipBoardText = ImGui.GetClipboardText();
            if (!string.IsNullOrWhiteSpace(clipBoardText)) {
                var importedScenarioData = scenarioFileManager.ImportBase64Scenario(clipBoardText);
                if (importedScenarioData != null) {
                    InitScenarioStructures(importedScenarioData, _scenarioFilePath);
                    _importState.SetResult(true);
                }
            }
        }
        ArrpGuiLayout.Tooltip(importTooltip);

        ImGui.SameLine(0, 5);
        _exportState.CheckState(out var exportIcon, out var exportColor);
        if (ImGuiComponents.IconButton("##arrpScenarioEditorExport", exportIcon, exportColor)) {
            _exportState.SetResult(true);
            ImGui.SetClipboardText(ScenarioFileManager.ExportBase64Scenario(ScenarioObject));
        }
        ArrpGuiLayout.Tooltip(loc["ScenarioEditor_BaseData_Action_Export_Desc"]);

        ImGui.TableNextColumn();
        ImGui.TableNextColumn();

        if (ImGui.Button(loc["ScenarioEditor_BaseData_Action_Apply"])) {
            SaveScenario();
        }

        using (ImRaii.PushColor(ImGuiCol.Button, ArrpGuiColors.ArrpGreen)) {
            ImGui.SameLine(0, 5);
            if (ImGui.Button(loc["ScenarioEditor_BaseData_Action_SaveClose"])) {
                SaveScenario();
                IsOpen = false;
            }
        }

        using (ImRaii.PushColor(ImGuiCol.Button, ImGuiColors.DPSRed)) {
            ImGui.SameLine(0, 5);
            if (ImGui.Button(loc["ScenarioEditor_BaseData_Action_DiscardClose"])) {
                IsOpen = false;
            }
        }
    }

    private void DrawOutlinePane() {
        DrawOutlineToolbar();

        using var tree = ImRaii.Child("##arrpScenarioEditorOutlineTree", new Vector2(0, 0), false);
        if (!tree.Success)
            return;

        DrawOutlineScenarioNode();
        
        if (ScenarioObject.Npcs.Count == 0) {
            ImGui.Dummy(ArrpGuiSpacing.VerticalComponentSpacing);
            using (ImRaii.Disabled())
                ImGui.TextWrapped(loc["ScenarioEditor_Outline_Empty"]);
            return;
        }
        
        foreach (var actor in ScenarioObject.Npcs.ToList()) {
            DrawOutlineActorNode(actor);
        }
    }
    
    private void DrawOutlineToolbar()
        => ArrpGuiLayout.HeaderRow("##arrpOutlineToolbar", DrawOutlineToolbarActions);

    private void DrawOutlineToolbarActions() {
        if (ImGuiComponents.IconButton("##arrpOutlineAddActor", FontAwesomeIcon.UserPlus)) {
            AddActor();
        }
        ArrpGuiLayout.Tooltip(loc["ScenarioEditor_ActorData_Manage_AddActor"]);

        ImGui.SameLine(0, ArrpGuiSpacing.ButtonSpacing);
        using (ImRaii.Disabled(SelectedScenarioNpc == null)) {
            if (ImGuiComponents.IconButton("##arrpOutlineAddAction", FontAwesomeIcon.Plus)) {
                ImGui.OpenPopup(AddActionPopupId);
            }
        }
        ArrpGuiLayout.Tooltip(loc["ScenarioEditor_Outline_AddAction"]);

        using (var popup = ImRaii.Popup(AddActionPopupId)) {
            if (popup.Success)
                DrawActionSelection();
        }

        ImGui.SameLine(0, ArrpGuiSpacing.ButtonGroupSpacing);
        using (ImRaii.Disabled(!CanMoveSelection(-1))) {
            if (ImGuiComponents.IconButton("##arrpOutlineMoveUp", FontAwesomeIcon.ArrowUp))
                MoveSelection(-1);
        }
        ArrpGuiLayout.Tooltip(loc["ScenarioEditor_Outline_MoveUp"]);

        ImGui.SameLine(0, ArrpGuiSpacing.ButtonSpacing);
        using (ImRaii.Disabled(!CanMoveSelection(1))) {
            if (ImGuiComponents.IconButton("##arrpOutlineMoveDown", FontAwesomeIcon.ArrowDown))
                MoveSelection(1);
        }
        ArrpGuiLayout.Tooltip(loc["ScenarioEditor_Outline_MoveDown"]);

        ImGui.SameLine(0, ArrpGuiSpacing.ButtonSpacing);
        using (ImRaii.Disabled(SelectedScenarioNpc == null)) {
            if (ImGuiComponents.IconButton("##arrpOutlineDuplicate", FontAwesomeIcon.Clone))
                DuplicateSelection();
        }
        ArrpGuiLayout.Tooltip(loc["ScenarioEditor_Outline_Duplicate"]);
    }

    private void DrawOutlineScenarioNode() {
        using var id = ImRaii.PushId("arrpOutlineScenarioRoot");
        
        if (ImGui.Selectable($"{ScenarioTitle()}##outlineNode", SelectedScenarioNpc == null)) {
            ResetSelectedNpc();
        }
        ArrpGuiLayout.Tooltip(loc["ScenarioEditor_Outline_ScenarioNodeHint"]);
    }

    private void DrawOutlineActorNode(ScenarioNpcData npc) {        
        using var id = ImRaii.PushId(npc.Identifier);

        var flags = ImGuiTreeNodeFlags.OpenOnArrow | ImGuiTreeNodeFlags.OpenOnDoubleClick | ImGuiTreeNodeFlags.SpanAvailWidth;
        if (!_collapsedActors.Contains(npc.Identifier))
            flags |= ImGuiTreeNodeFlags.DefaultOpen;
        if (SelectedScenarioNpc == npc && SelectedScenarioNpcAction == null)
            flags |= ImGuiTreeNodeFlags.Selected;

        using var node = ImRaii.TreeNode(ActorLabel(npc), flags);

        if (ImGui.IsItemToggledOpen()) {
            if (node.Success) {
                _collapsedActors.Remove(npc.Identifier);
            } else {
                _collapsedActors.Add(npc.Identifier);
            }
        } else if (ImGui.IsItemClicked()) {
            ResetSelectedNpc(npc);
        }

        DrawOutlineContextMenu(npc, null);

        if (npc.Actions.Count == 0) {
            ArrpGuiLayout.RightAlignedBadge(FontAwesomeIcon.ExclamationTriangle, ImGuiColors.DalamudYellow, loc["ScenarioEditor_ActorData_Manage_SelectIssue"]);
        }

        if (!node.Success)
            return;

        var actions = npc.Actions.ToList();
        var syncActionCount = 0;
        for (var actionIndex = 0; actionIndex < actions.Count; actionIndex++) {
            DrawOutlineActionNode(npc, actions[actionIndex], actionIndex, ref syncActionCount);
        }
    }

    private void DrawOutlineActionNode(ScenarioNpcData npc, ScenarioNpcAction action, int actionIndex, ref int syncActionCount) {
        using var id = ImRaii.PushId($"arrpOutlineAction{actionIndex}");

        var isSync = action is ScenarioNpcSyncAction;
        var label = _actionUiRegistry.GetShortName(action);
        if (isSync) {
            syncActionCount++;
            label += $" [{syncActionCount}]";
        }

        var color = !action.Enabled ? ImGuiColors.DalamudGrey2
            : isSync ? ImGuiColors.TankBlue
            : ImGuiColors.DalamudWhite;

        var flags = OutlineLeafFlags;
        if (SelectedScenarioNpcAction == action)
            flags |= ImGuiTreeNodeFlags.Selected;
        
        using (ImRaii.PushColor(ImGuiCol.Text, color)) {
            ImGui.TreeNodeEx("##outlineNode", flags, label);
        }

        if (ImGui.IsItemClicked()) {
            if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left)) {
                action.Enabled = !action.Enabled;
            }
            ResetSelectedAction(npc, action);
        }

        DrawOutlineContextMenu(npc, action);
        ArrpGuiLayout.RightAlignedText(_actionUiRegistry.GetSummary(action));

        if (isSync) {
            ImGui.Separator();
        }
    }

    private void DrawOutlineContextMenu(ScenarioNpcData npc, ScenarioNpcAction? action) {
        using var popup = ImRaii.ContextPopup("##arrpOutlineContext");
        if (!popup.Success)
            return;
        
        if (action == null) {
            if (SelectedScenarioNpc != npc || SelectedScenarioNpcAction != null)
                ResetSelectedNpc(npc);
        } else if (SelectedScenarioNpcAction != action) {
            ResetSelectedAction(npc, action);
        }

        if (action != null) {
            var enabled = action.Enabled;
            if (ImGui.Checkbox(loc["ScenarioEditor_Inspector_Action_Enabled"], ref enabled)) {
                action.Enabled = enabled;
            }
            ImGui.Separator();
        }

        using (ImRaii.Disabled(!CanMoveSelection(-1))) {
            if (ImGui.Selectable(loc["ScenarioEditor_Outline_MoveUp"]))
                MoveSelection(-1);
        }

        using (ImRaii.Disabled(!CanMoveSelection(1))) {
            if (ImGui.Selectable(loc["ScenarioEditor_Outline_MoveDown"]))
                MoveSelection(1);
        }

        if (ImGui.Selectable(loc["ScenarioEditor_Outline_Duplicate"])) {
            DuplicateSelection();
        }

        ImGui.Separator();
        using (ImRaii.PushColor(ImGuiCol.Text, ImGuiColors.DalamudRed)) {
            if (ImGui.Selectable(loc["ScenarioEditor_Outline_Delete"]))
                DeleteSelection();
        }
    }

    private void ResetSelectedNpc(ScenarioNpcData? newNpc = null) {
        SelectedScenarioNpc = newNpc;
        SelectedScenarioNpcAction = null;
        SelectedPathMovementPoint = null;
    }

    private void ResetSelectedAction(ScenarioNpcData npc, ScenarioNpcAction? newAction = null) {
        SelectedScenarioNpc = npc;
        SelectedScenarioNpcAction = newAction;        
        SelectedPathMovementPoint = (newAction as ScenarioNpcPathAction)?.Points.LastOrDefault();        
    }

    private void AddActor() {
        var newScenarioNpc = new ScenarioNpcData {
            Position = objectTable.LocalPlayer?.Position ?? Vector3.Zero,
            Rotation = objectTable.LocalPlayer?.Rotation ?? 0f
        };
        var newActorName = characterCreationData.GenerateRandomName(newScenarioNpc.Appearance.Race, newScenarioNpc.Appearance.Tribe, newScenarioNpc.Appearance.Sex);

        newScenarioNpc.Actions.Add(new ScenarioNpcWaitingAction());
        newScenarioNpc.Name = $"{newActorName.FirstName} {newActorName.LastName}";

        ScenarioObject.Npcs.Add(newScenarioNpc);
        ResetSelectedNpc(newScenarioNpc);
    }

    private void AddAction(ScenarioNpcAction action) {
        if (SelectedScenarioNpc == null)
            return;

        if (SelectedScenarioNpcAction != null) {
            var currentIndex = SelectedScenarioNpc.Actions.IndexOf(SelectedScenarioNpcAction);
            SelectedScenarioNpc.Actions.Insert(currentIndex + 1, action);
        } else {
            SelectedScenarioNpc.Actions.Add(action);
        }

        ResetSelectedAction(SelectedScenarioNpc, action);
    }

    private bool CanMoveSelection(int offset) {
        if (SelectedScenarioNpc == null)
            return false;

        if (SelectedScenarioNpcAction == null) {
            var actorIndex = ScenarioObject.Npcs.IndexOf(SelectedScenarioNpc) + offset;
            return actorIndex >= 0 && actorIndex < ScenarioObject.Npcs.Count;
        }

        var index = SelectedScenarioNpc.Actions.IndexOf(SelectedScenarioNpcAction) + offset;
        return index >= 0 && index < SelectedScenarioNpc.Actions.Count;
    }

    private void MoveSelection(int offset) {
        if (!CanMoveSelection(offset) || SelectedScenarioNpc == null)
            return;

        if (SelectedScenarioNpcAction == null) {
            MoveListEntry(ScenarioObject.Npcs, SelectedScenarioNpc, offset);
            return;
        }

        MoveListEntry(SelectedScenarioNpc.Actions, SelectedScenarioNpcAction, offset);
    }

    private static void MoveListEntry<T>(List<T> items, T item, int offset) {
        var index = items.IndexOf(item);
        items.RemoveAt(index);
        items.Insert(index + offset, item);
    }

    private void DuplicateSelection() {
        if (SelectedScenarioNpc == null)
            return;

        if (SelectedScenarioNpcAction == null) {
            var clonedActor = CloneScenarioObject(SelectedScenarioNpc);
            if (clonedActor == null)
                return;

            clonedActor.Identifier = Guid.NewGuid().AsHexString();
            ScenarioObject.Npcs.Insert(ScenarioObject.Npcs.IndexOf(SelectedScenarioNpc) + 1, clonedActor);
            ResetSelectedNpc(clonedActor);
            return;
        }

        var clonedAction = CloneScenarioObject(SelectedScenarioNpcAction);
        if (clonedAction == null)
            return;

        SelectedScenarioNpc.Actions.Insert(SelectedScenarioNpc.Actions.IndexOf(SelectedScenarioNpcAction) + 1, clonedAction);
        ResetSelectedAction(SelectedScenarioNpc, clonedAction);
    }

    private void DeleteSelection() {
        if (SelectedScenarioNpc == null)
            return;

        if (SelectedScenarioNpcAction == null) {
            var actorIndex = ScenarioObject.Npcs.IndexOf(SelectedScenarioNpc);
            ScenarioObject.Npcs.Remove(SelectedScenarioNpc);
            ResetSelectedNpc(NeighbourOf(ScenarioObject.Npcs, actorIndex));
            return;
        }

        var actions = SelectedScenarioNpc.Actions;
        var index = actions.IndexOf(SelectedScenarioNpcAction);
        actions.Remove(SelectedScenarioNpcAction);
        ResetSelectedAction(SelectedScenarioNpc, NeighbourOf(actions, index));
    }
    
    private static T? NeighbourOf<T>(List<T> items, int removedIndex) where T : class
        => items.Count == 0 ? null : items[Math.Min(removedIndex, items.Count - 1)];

    private static T? CloneScenarioObject<T>(T source) where T : class
        => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(source, ScenarioFileManager.ScenarioLoadSerializerOptions), ScenarioFileManager.ScenarioLoadSerializerOptions);

    private void SaveScenario() {
        if (string.IsNullOrWhiteSpace(_scenarioFilePath)) {
            _scenarioFilePath = scenarioFileManager.StoreScenarioFile(ScenarioObject).FullName;
        } else {
            scenarioFileManager.StoreScenarioFile(ScenarioObject, _scenarioFilePath);
        }
    }

    private string ScenarioTitle()
        => string.IsNullOrWhiteSpace(ScenarioObject.Title) ? loc["ScenarioEditor_Outline_UntitledScenario"] : ScenarioObject.Title;

    private string ActorLabel(ScenarioNpcData npc)
        => string.IsNullOrWhiteSpace(npc.Name) ? loc["ScenarioEditor_Outline_UnnamedActor"] : npc.Name;

    private string DescribeTerritory() {
        if (ScenarioObject.Location.Territory == 0)
            return loc["ScenarioEditor_Header_NoTerritory"];

        var territoryName = dataCache.GetTerritoryType((ushort)ScenarioObject.Location.Territory).PlaceName.Value.Name.ToString();
        return string.IsNullOrWhiteSpace(territoryName) ? ScenarioObject.Location.Territory.ToString() : territoryName;
    }

    private string DescribeSpeed(NpcSpeed speed) => speed switch {
        NpcSpeed.Walking => loc["ScenarioEditor_ActorData_Actions_WalkSpeed_Walking"],
        NpcSpeed.Running => loc["ScenarioEditor_ActorData_Actions_WalkSpeed_Running"],
        NpcSpeed.Sprinting => loc["ScenarioEditor_ActorData_Actions_WalkSpeed_Sprinting"],
        NpcSpeed.Custom => loc["ScenarioEditor_ActorData_Actions_WalkSpeed_Custom"],
        _ => speed.ToString()
    };

    private string DescribeSpeed(INpcSpeedSelection selection) => selection.Speed == NpcSpeed.Custom
        ? loc["ScenarioEditor_ActorData_Actions_WalkSpeed_CustomValue", MovementMotion.ClampSpeed(selection.CustomSpeed)]
        : DescribeSpeed(selection.Speed);

    private static string FormatSeconds(float seconds)
        => $"{seconds:0.0}s";
    
    private string Pluralize(int count, string singularKey, string pluralKey)
        => count == 1 ? loc[singularKey] : loc[pluralKey, count];

    private unsafe NpcAppearanceData ExportCurrentCharacter() {
        if (objectTable.LocalPlayer == null) {
            throw new InvalidOperationException("No local player found");
        }
        return ExportCurrentCharacter((Character*)objectTable.LocalPlayer.Address);
    }

    private unsafe NpcAppearanceData ExportCurrentCharacter(Character* character) {
        var appearanceFile = new NpcAppearanceData();
        appearanceService.Read(character, appearanceFile);
        return appearanceFile;
    }

    private unsafe NpcAppearanceData? ExportCurrentTarget() {
        if (_targetManager.Target != null && _targetManager.Target is ICharacter c) {
            return ExportCurrentCharacter((Character*)c.Address);
        }
        return null;
    }
}

public enum ScenarioEditorGizmoTarget {
    Position,
    DrawOffset
}

public class TransferState {
    public bool? Result { get; private set; } = null;
    public DateTime Timeout { get; private set; } = DateTime.MinValue;
    public FontAwesomeIcon DefaultIcon { get; set; } = FontAwesomeIcon.Question;

    public void CheckState(out FontAwesomeIcon stateIcon, out Vector4? stateColor) {
        if (Result == null) {
            stateIcon = DefaultIcon;
            stateColor = null;
            return;
        }

        if (DateTime.Now > Timeout) {
            Result = null;
            Timeout = DateTime.MinValue;
            stateIcon = DefaultIcon;
            stateColor = null;
            return;
        }

        stateIcon = Result == true ? FontAwesomeIcon.CheckCircle : FontAwesomeIcon.ExclamationCircle;
        stateColor = Result == true ? ImGuiColors.HealerGreen : ImGuiColors.DPSRed;
    }

    public void SetResult(bool state) {
        Result = state;
        Timeout = DateTime.Now.AddSeconds(2);
    }
}
