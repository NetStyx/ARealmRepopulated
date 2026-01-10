using ARealmRepopulated.Core.ArrpGui.Components;
using ARealmRepopulated.Core.ArrpGui.Style;
using ARealmRepopulated.Core.Services.Npcs;
using ARealmRepopulated.Core.Services.Scenarios;
using ARealmRepopulated.Core.Services.Windows;
using ARealmRepopulated.Data.Appearance;
using ARealmRepopulated.Data.Scenarios;
using ARealmRepopulated.Infrastructure;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using System.IO;
using System.Numerics;
using CsMaths = FFXIVClientStructs.FFXIV.Common.Math;
namespace ARealmRepopulated.Windows;

public partial class ScenarioEditorWindow(
    DebugOverlay debugOverlay,
    ScenarioFileManager scenarioFileManager,
    NpcAppearanceService appearanceService,
    NpcAppearanceDataParser appearanceDataParser,
    ArrpDataCache dataCache,
    ArrpEventService eventService,
    ArrpGuiEmotePicker emotePicker,
    FileDialogManager fileDialogManager,
    IObjectTable objectTable,
    ITargetManager _targetManager) : ADalamudWindow($"Scenario Editor###ARealmRepopulatedScenarioConfigWindow") {

    private string _scenarioFilePath = string.Empty;
    private readonly NpcActionUiRegistry _actionUiRegistry = new();
    public ScenarioData ScenarioObject { get; private set; } = null!;
    public ScenarioNpcData? SelectedScenarioNpc { get; private set; } = null;
    public ScenarioNpcAction? SelectedScenarioNpcAction { get; private set; } = null!;
    public PathMovementPoint? SelectedPathMovementPoint { get; private set; } = null;

    public Guid UniqueScenarioId { get; set; } = Guid.NewGuid();

    private readonly TransferState _importState = new() { DefaultIcon = FontAwesomeIcon.Download };
    private readonly TransferState _exportState = new() { DefaultIcon = FontAwesomeIcon.Upload };

    protected override void SetWindowOptions() {
        this.AllowPinning = false;
        this.AllowClickthrough = false;

        Flags = ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoCollapse;
        SizeConstraints = new WindowSizeConstraints {
            MinimumSize = new Vector2(400, 400),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
        this.Size = new Vector2(800, 900);
        this.SizeCondition = ImGuiCond.Appearing;

        this.TitleBarButtons.Add(new TitleBarButton { Icon = FontAwesomeIcon.InfoCircle, ShowTooltip = DrawFrameTooltip });
        this.OnWindowClosed += () => {
            debugOverlay.RemoveEditor(this);
        };

        _actionUiRegistry.Register<ScenarioNpcEmoteAction>(
            shortName: (a) => "Emote",
            help: (a) => "Plays an emote. If the emote supports looping and no timelimit is specified, it continues to do so.",
            draw: DrawEmoteAction
        );
        _actionUiRegistry.Register<ScenarioNpcIdleAction>(
            shortName: (a) => "Idle",
            help: (a) => "Returns the actor to the idle state.",
            draw: DrawIdleAction
        );
        _actionUiRegistry.Register<ScenarioNpcWaitingAction>(
            shortName: (a) => "Wait",
            help: (a) => "Simply waits at the current location for a specific time.",
            draw: DrawWaitingAction
        );
        _actionUiRegistry.Register<ScenarioNpcSpawnAction>(
            shortName: (a) => "Spawn",
            help: (a) => "Spawn the actor in if it was previously despawned.",
            draw: DrawSpawnAction
        );
        _actionUiRegistry.Register<ScenarioNpcDespawnAction>(
            shortName: (a) => "Despawn",
            help: (a) => "Despawnes the actor. This does not unload it but disables the drawing cycles.",
            draw: DrawDespawnAction
        );
        _actionUiRegistry.Register<ScenarioNpcMovementAction>(
            shortName: (a) => "Move",
            help: (a) => "Walks, or runs, the actor to a specific location. If the target is at a different angle than the actor is looking at, this action rotates the actor first in the correct direction.",
            draw: DrawMovementAction
        );
        _actionUiRegistry.Register<ScenarioNpcRotationAction>(
            shortName: (a) => "Rotate",
            help: (a) => "Rotates the actor to a given angle.",
            draw: DrawRotationAction
        );
        _actionUiRegistry.Register<ScenarioNpcSyncAction>(
            shortName: (a) => "Sync",
            help: (a) => "Pauses continuation of the action list until every scenario actor has reached the same sync action",
            draw: DrawSyncAction
        );
        _actionUiRegistry.Register<ScenarioNpcPathAction>(
            shortName: (a) => "Path",
            help: (a) => "Create a path based on a point lists. The actor walks or runs along this path more naturally as opposed to multiple move actions.",
            draw: DrawPathAction
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
        if (ScenarioObject != null) {
            ResetSelectedNpc(ScenarioObject.Npcs.FirstOrDefault());
            if (SelectedScenarioNpc != null) {
                ResetSelectedAction(SelectedScenarioNpc.Actions.FirstOrDefault());
            }
        }

        UpdateWindowTitle();
        debugOverlay.AddEditor(this);
        IsOpen = true;
    }

    private void UpdateWindowTitle() {
        this.WindowName = $"Scenario Editor - {ScenarioObject.Title}###{UniqueScenarioId}";
    }

    private void DrawFrameTooltip() {
        using var tooltip = ImRaii.Tooltip();
        using var table = ImRaii.Table("##ScenarioEditFrameTooltipTable", 1, ImGuiTableFlags.NoSavedSettings);

        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        ImGui.TextDisabled("Scenario File Name");

        var fileName = string.IsNullOrWhiteSpace(_scenarioFilePath) ? "not yet saved" : Path.GetFileName(_scenarioFilePath);
        ImGui.Text($"{fileName}");

        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        ImGui.TextDisabled("Scenario Location");
        ImGui.Text($"Server {ScenarioObject.Location.Server} / Territory {ScenarioObject.Location.Territory} / Division {ScenarioObject.Location.HousingDivision} / Ward {ScenarioObject.Location.HousingWard} / Plot {ScenarioObject.Location.HousingPlot}");

    }
    public override void Draw() {

        using (ImRaii.Child("##scenarioEditorMainArea", new Vector2(0, -50))) {

            ImGui.Dummy(ArrpGuiSpacing.VerticalHeaderSpacing);
            ImGui.Text("Scenario Meta Data");
            ImGui.TextDisabled("Edit the scenario information in this section. These are mostly meta data to identify the scenario on the user interface.");

            ImGui.Separator();
            ImGui.Dummy(ArrpGuiSpacing.VerticalComponentSpacing);

            DrawScenarioGeneralTable();

            ImGui.Dummy(ArrpGuiSpacing.VerticalSectionSpacing);
            ImGui.Text("Scenario Actor Data");
            ImGui.TextDisabled("Select the actor to edit here. You can define the actions for each actor individually.");

            ImGui.Separator();
            ImGui.Dummy(ArrpGuiSpacing.VerticalComponentSpacing);

            ArrpGuiAlignment.CenterText("Select Actor:");
            ImGui.SameLine();
            if (ImGui.BeginCombo("##scenarioEditorWindowNpcSelection", SelectedScenarioNpc == null ? "Select NPC ..." : SelectedScenarioNpc.Name, ImGuiComboFlags.None)) {
                var scenarioNpcs = ScenarioObject.Npcs.ToList();
                for (var npcIndex = 0; npcIndex < scenarioNpcs.Count; npcIndex++) {
                    var npc = scenarioNpcs[npcIndex];
                    var npcSelected = npc == SelectedScenarioNpc;

                    if (ImGui.Selectable($"{npc.Name}##scenarioEditorSelectedNpc{npcIndex}", npcSelected)) {
                        ResetSelectedNpc(npc);
                    }

                    if (npcSelected) {
                        ImGui.SetItemDefaultFocus();
                    }
                }
                ImGui.EndCombo();
            }

            ImGui.SameLine();
            using (ImRaii.PushId("##scenarioNpcControllAddNpc")) {
                if (ImGuiComponents.IconButtonWithText(Dalamud.Interface.FontAwesomeIcon.Plus, "Add NPC")) {
                    var newScenarioNpc = new ScenarioNpcData { Name = "New Actor", Position = objectTable.LocalPlayer?.Position ?? Vector3.Zero, Rotation = objectTable.LocalPlayer?.Rotation ?? 0f };
                    ScenarioObject.Npcs.Add(newScenarioNpc);
                    ResetSelectedNpc(newScenarioNpc);
                }
            }

            if (SelectedScenarioNpc != null) {
                ImGui.SameLine();
                using (ImRaii.PushId("##scenarioNpcControllDeleteNpc")) {
                    if (ImGuiComponents.IconButtonWithText(Dalamud.Interface.FontAwesomeIcon.Trash, "Remove NPC")) {
                        ScenarioObject.Npcs.Remove(SelectedScenarioNpc);
                        ResetSelectedNpc();
                    }
                }
            }

            if (SelectedScenarioNpc != null) {
                ImGui.Dummy(ArrpGuiSpacing.VerticalComponentSpacing);

                using (ImRaii.Child("##scenarioEditorNpcArea", new Vector2(0, 0), false, ImGuiWindowFlags.NoSavedSettings)) {
                    using (ImRaii.TabBar("##scenarioEditorNpcTabBar")) {

                        if (ImGui.BeginTabItem("General##scenarioEditorNpcTabGeneral", ImGuiTabItemFlags.NoTooltip)) {

                            if (SelectedScenarioNpcAction != null) {
                                ResetSelectedAction();
                            }

                            DrawNpcGeneralTab();
                            ImGui.EndTabItem();
                        }

                        if (ImGui.BeginTabItem("Actions##scenarioEditorNpcTabActions", ImGuiTabItemFlags.NoTooltip)) {
                            DrawNpcActionTab();
                            ImGui.EndTabItem();
                        }
                    }
                }
            }
        }

        ImGui.Separator();
        ImGui.Dummy(ArrpGuiSpacing.VerticalComponentSpacing);
        using (ImRaii.Table("##scenarioEditorWindowControlTable", 4, ImGuiTableFlags.NoSavedSettings)) {
            ImGui.TableSetupColumn("##scenarioEditorWindowControlImport", ImGuiTableColumnFlags.WidthFixed);
            ImGui.TableSetupColumn("##scenarioEditorWindowControlStrech", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("##scenarioEditorWindowControlClose", ImGuiTableColumnFlags.WidthFixed);
            ImGui.TableSetupColumn("##scenarioEditorWindowControlPadding", ImGuiTableColumnFlags.WidthFixed, ArrpGuiSpacing.WindowGripSpacing);
            ImGui.TableNextRow();
            ImGui.TableNextColumn();

            _importState.CheckState(out var importIcon, out var importColor);
            var importTooltip = "Import from clipboard";
            if (_importState.Result.HasValue) {
                importTooltip = (bool)_importState.Result ? "Import success!" : "Import failed!";
            }
            if (ImGuiComponents.IconButtonWithText(importIcon, importTooltip, defaultColor: importColor, hoveredColor: importColor)) {
                _importState.SetResult(false);
                var clipBoardText = ImGui.GetClipboardText();
                if (!string.IsNullOrWhiteSpace(clipBoardText)) {
                    var importedScenarioData = scenarioFileManager.ImportBase64Scenario(clipBoardText);
                    if (importedScenarioData != null) {
                        ScenarioObject = importedScenarioData;
                        _importState.SetResult(true);
                    }
                }
            }

            ImGui.SameLine(0, 5);
            _exportState.CheckState(out var exportIcon, out var exportColor);
            if (ImGuiComponents.IconButtonWithText(exportIcon, "Export to clipboard", defaultColor: exportColor, hoveredColor: exportColor)) {
                _exportState.SetResult(true);
                ImGui.SetClipboardText(ScenarioFileManager.ExportBase64Scenario(ScenarioObject));
            }

            ImGui.TableNextColumn();
            ImGui.TableNextColumn();

            if (ImGui.Button("Apply")) {
                SaveScenario();
            }

            using (ImRaii.PushColor(ImGuiCol.Button, ArrpGuiColors.ArrpGreen)) {
                ImGui.SameLine(0, 5);
                if (ImGui.Button("Save & Close")) {
                    SaveScenario();
                    IsOpen = false;
                }
            }

            using (ImRaii.PushColor(ImGuiCol.Button, ImGuiColors.DPSRed)) {
                ImGui.SameLine(0, 5);
                if (ImGui.Button("Discard & Close")) {
                    IsOpen = false;
                }
            }
        }

    }

    public void DrawScenarioGeneralTable() {

        var titleRef = ScenarioObject.Title;
        var descriptionRef = ScenarioObject.Description;
        var loopingRef = ScenarioObject.Looping;

        using var padding = ImRaii.PushStyle(ImGuiStyleVar.CellPadding, new Vector2(10, 5));
        using var table = ImRaii.Table("##generalEditTable", 2, ImGuiTableFlags.NoSavedSettings);

        ImGui.TableSetupColumn("##scenarioEditorGeneralOptionLabel", ImGuiTableColumnFlags.WidthFixed);
        ImGui.TableSetupColumn("##scenarioEditorGeneralOptionControls", ImGuiTableColumnFlags.WidthStretch);

        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        ImGui.Text("Location");

        ImGui.TableNextColumn();

        var territoryName = "Undefined";
        if (ScenarioObject.Location.Territory > 0) {
            territoryName = dataCache.GetTerritoryType((ushort)ScenarioObject.Location.Territory).PlaceName.Value.Name.ToString();
        }

        ImGui.Text($"{ScenarioObject.Location.Territory} - {territoryName}");
        ImGui.SameLine(0, 5);
        if (ImGui.SmallButton("Set to Current Location")) {
            var currentLocation = eventService.CurrentLocation;
            ScenarioObject.Location.Server = currentLocation.Server;
            ScenarioObject.Location.Territory = currentLocation.TerritoryType;
            ScenarioObject.Location.HousingDivision = currentLocation.HousingDivision;
            ScenarioObject.Location.HousingWard = currentLocation.HousingWard;
            ScenarioObject.Location.HousingPlot = currentLocation.HousingPlot;
            ScenarioObject.Location.Server = currentLocation.Server;
        }

        ImGui.TableNextRow();
        ImGui.TableNextColumn();

        ImGui.Text("Title:");
        ImGui.TableNextColumn();
        ImGui.SetNextItemWidth(-1);
        if (ImGui.InputText("##scenarioEditorTitleInput", ref titleRef)) {
            ScenarioObject.Title = titleRef;
            UpdateWindowTitle();
        }

        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        ImGui.Text("Description:");
        ImGui.TableNextColumn();
        ImGui.SetNextItemWidth(-1);
        if (ImGui.InputTextMultiline("##scenarioEditorDescriptionInput", ref descriptionRef, size: new Vector2(0, 50))) {
            ScenarioObject.Description = descriptionRef;
        }

        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        ImGui.Text("Looping:");
        ImGui.TableNextColumn();
        if (ImGui.Checkbox("##scenarioEditorLoopingInput", ref loopingRef)) {
            ScenarioObject.Looping = loopingRef;
        }

        using (ImRaii.Disabled(!ScenarioObject.Looping)) {
            ImGui.SameLine(0, 10);
            ImGui.Text("Delay between loops:");
            ImGui.SameLine(0, 5);
            using (ImRaii.ItemWidth(100)) {
                var duration = ScenarioObject.LoopDelay;
                if (ImGui.InputFloat("s.##scenarioEditorLoopDelayInput", ref duration, step: 0.1f)) {
                    ScenarioObject.LoopDelay = duration;
                }
            }
        }
    }

    private void DrawNpcGeneralTab() {
        if (SelectedScenarioNpc == null)
            return;

        using (var cellPaddingStyle = ImRaii.PushStyle(ImGuiStyleVar.CellPadding, new Vector2(10, 5))) {
            if (ImGui.BeginTable("##scenarioNpcEditorTableAction", 2, ImGuiTableFlags.NoBordersInBody | ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.NoSavedSettings)) {

                ImGui.TableSetupColumn("##scenarioNpcEditorTableActionList", ImGuiTableColumnFlags.WidthFixed);
                ImGui.TableSetupColumn("##scenarioNpcEditorTableActionContent", ImGuiTableColumnFlags.WidthStretch);

                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.Text("Name");
                ImGui.SameLine();
                ImGuiComponents.HelpMarker("The npc does not output a name at any point. Its only use is to make it easier identifyable when editing the scenario.");

                ImGui.TableNextColumn();
                var name = SelectedScenarioNpc.Name;
                if (ImGui.InputText("##scenarioNpcGeneralEditName", ref name)) {
                    SelectedScenarioNpc.Name = name;
                }

                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.Text("Position");

                ImGui.TableNextColumn();
                var position = new Vector3(SelectedScenarioNpc.Position.X, SelectedScenarioNpc.Position.Y, SelectedScenarioNpc.Position.Z);
                if (ImGui.InputFloat3("##scenarioNpcGeneralEditPosition", ref position)) {
                    SelectedScenarioNpc.Position = new CsMaths.Vector3(position.X, position.Y, position.Z);
                }
                ImGui.SameLine();
                if (ImGuiComponents.IconButton(FontAwesomeIcon.LocationCrosshairs) && objectTable.LocalPlayer != null) {
                    SelectedScenarioNpc.Position = new CsMaths.Vector3(objectTable.LocalPlayer.Position.X, objectTable.LocalPlayer.Position.Y, objectTable.LocalPlayer.Position.Z);
                }
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Set to current location");

                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.Text("Roation");

                ImGui.TableNextColumn();
                var rotation = SelectedScenarioNpc.Rotation;
                if (ImGui.InputFloat("##scenarioNpcGeneralEditRotation", ref rotation)) {
                    SelectedScenarioNpc.Rotation = rotation;
                }
                ImGui.SameLine();
                if (ImGuiComponents.IconButton(FontAwesomeIcon.ArrowsSpin) && objectTable.LocalPlayer != null) {
                    SelectedScenarioNpc.Rotation = objectTable.LocalPlayer.Rotation;
                }
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Set to current rotation");

                ImGui.EndTable();
            }
        }

        ImGui.Dummy(ArrpGuiSpacing.VerticalComponentSpacing);
        ImGui.Text("Appearance");
        ImGui.SameLine();
        ImGuiComponents.HelpMarker("NPC Appearance can be changed by choosing a template from your environment. It is currently not planned to include a character editor.");
        ImGui.Separator();
        ImGui.Dummy(ArrpGuiSpacing.VerticalComponentSpacing);
        DrawNpcAppearanceInfo();
    }

    private unsafe void DrawNpcPickerPopup() {
        using (ImRaii.Table("###ArrpNpcInspectorListTable", 1, ImGuiTableFlags.NoSavedSettings | ImGuiTableFlags.BordersOuter, new Vector2(200, -1))) {
            ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthStretch, -1);

            var selectionFound = false;
            var npcObjectList = objectTable.Where(o
                => (o.ObjectKind == ObjectKind.BattleNpc || o.ObjectKind == ObjectKind.EventNpc)).ToList();
            foreach (var npcObject in npcObjectList) {

                var characterRef = (Character*)npcObject.Address;
                if (characterRef->DrawObject == null) {
                    continue;
                }

                if (objectTable.LocalPlayer != null && Vector3.Distance(objectTable.LocalPlayer.Position, npcObject.Position) > 10) {
                    continue;
                }

                var npcName = npcObject.Name.ToString();
                if (string.IsNullOrWhiteSpace(npcName)) {
                    npcName = $"Unnamed NPC {npcObject.ObjectIndex}";
                }

                ImGui.TableNextRow();
                ImGui.TableNextColumn();

                if (ImGui.Selectable($"{npcName}###ArrpNpcInspectorSelectionName{npcObject.Address}", false, ImGuiSelectableFlags.None)) {
                    var currentTargetAppearance = ExportCurrentCharacter(characterRef);
                    if (currentTargetAppearance != null) {
                        SelectedScenarioNpc?.Appearance = currentTargetAppearance;
                    }
                } else if (ImGui.IsItemHovered()) {
                    selectionFound = true;
                    debugOverlay.SetNpcTrace(npcObject.Position);
                }

            }

            if (!selectionFound) {
                debugOverlay.ClearNpcTrace();
            }

        }
    }

    private void DrawNpcActionTab() {
        if (SelectedScenarioNpc == null)
            return;

        using var cellPaddingStyle = ImRaii.PushStyle(ImGuiStyleVar.CellPadding, new Vector2(10, 5));
        using (ImRaii.Table("##scenarioNpcEditorTableAction", 2, ImGuiTableFlags.NoBordersInBody | ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.NoSavedSettings)) {

            ImGui.TableSetupColumn("##scenarioNpcEditorTableActionList", ImGuiTableColumnFlags.WidthFixed);
            ImGui.TableSetupColumn("##scenarioNpcEditorTableActionContent", ImGuiTableColumnFlags.WidthStretch);

            ImGui.TableNextRow();
            ImGui.TableNextColumn();

            if (ImGuiComponents.IconButton(Dalamud.Interface.FontAwesomeIcon.Plus)) {
                ImGui.OpenPopup("AddActionSelection");
            }

            if (ImGui.BeginPopup("AddActionSelection")) {
                DrawActionSelection();
                ImGui.EndPopup();
            }

            if (SelectedScenarioNpcAction != null) {
                var isFirstActionSelected = SelectedScenarioNpc.Actions.IndexOf(SelectedScenarioNpcAction) == 0;
                var isLastActionSelected = SelectedScenarioNpc.Actions.IndexOf(SelectedScenarioNpcAction) == SelectedScenarioNpc.Actions.Count - 1;
                var isActionSelected = SelectedScenarioNpcAction != null;

                using (ImRaii.Disabled(!isActionSelected || isFirstActionSelected)) {
                    ImGui.SameLine(0, 5);
                    if (ImGuiComponents.IconButton(Dalamud.Interface.FontAwesomeIcon.ArrowUp)) {
                        MoveSelectedActionUp();
                    }
                }

                using (ImRaii.Disabled(!isActionSelected || isLastActionSelected)) {
                    ImGui.SameLine(0, 5);
                    if (ImGuiComponents.IconButton(Dalamud.Interface.FontAwesomeIcon.ArrowDown)) {
                        MoveSelectedActionDown();
                    }
                }

                using (ImRaii.Disabled(!isActionSelected)) {
                    ImGui.SameLine(0, 5);
                    if (ImGuiComponents.IconButton(Dalamud.Interface.FontAwesomeIcon.Trash)) {
                        RemoveSelectedAction();
                    }
                }
            }
            ImGui.TableNextColumn();
            DrawCurrentActionBar();

            ImGui.TableNextRow();
            ImGui.TableNextColumn();

            using (ImRaii.ListBox("##scenarioNpcEditorActionListBox", new Vector2(120, -10))) {
                var scenarioNpcActions = SelectedScenarioNpc.Actions.ToList();
                var syncActionCount = 0;
                for (var actionIndex = 0; actionIndex < scenarioNpcActions.Count; actionIndex++) {
                    var npcAction = scenarioNpcActions[actionIndex];
                    var npcActionSelected = npcAction == SelectedScenarioNpcAction;
                    var npcActionShortName = _actionUiRegistry.GetShortName(npcAction);

                    if (npcAction is ScenarioNpcSyncAction) {
                        syncActionCount++;
                        npcActionShortName += $" [{syncActionCount}]";
                    }

                    if (ImGui.Selectable($"{npcActionShortName}##scenarioEditorSelectedNpc{actionIndex}", npcActionSelected)) {
                        ResetSelectedAction(npcAction);
                    }

                    if (npcAction is ScenarioNpcSyncAction) {
                        ImGui.Separator();
                        ImGui.Dummy(ArrpGuiSpacing.VerticalHeaderSpacing);
                    }
                }
            }

            ImGui.TableNextColumn();
            DrawCurrentAction();

        }
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

        ResetSelectedAction(action);
    }

    private void MoveSelectedActionUp() {
        if (SelectedScenarioNpc == null)
            return;

        if (SelectedScenarioNpcAction == null)
            return;

        var currentIndex = SelectedScenarioNpc.Actions.IndexOf(SelectedScenarioNpcAction);

        SelectedScenarioNpc.Actions.Remove(SelectedScenarioNpcAction);
        SelectedScenarioNpc.Actions.Insert(currentIndex - 1, SelectedScenarioNpcAction);
    }

    private void MoveSelectedActionDown() {
        if (SelectedScenarioNpc == null)
            return;

        if (SelectedScenarioNpcAction == null)
            return;

        var currentIndex = SelectedScenarioNpc.Actions.IndexOf(SelectedScenarioNpcAction);

        SelectedScenarioNpc.Actions.Remove(SelectedScenarioNpcAction);
        SelectedScenarioNpc.Actions.Insert(currentIndex + 1, SelectedScenarioNpcAction);
    }

    private void RemoveSelectedAction() {
        if (SelectedScenarioNpc == null)
            return;

        if (SelectedScenarioNpcAction == null)
            return;

        var currentIndex = SelectedScenarioNpc.Actions.IndexOf(SelectedScenarioNpcAction);
        SelectedScenarioNpc.Actions.Remove(SelectedScenarioNpcAction);

        if (currentIndex <= SelectedScenarioNpc.Actions.Count - 1) {
            ResetSelectedAction(SelectedScenarioNpc.Actions[currentIndex]);
        } else {
            if (SelectedScenarioNpc.Actions.Count > 0) {
                ResetSelectedAction(SelectedScenarioNpc.Actions.Last());
            } else {
                ResetSelectedAction();
            }
        }
    }

    private void SaveScenario() {
        if (string.IsNullOrWhiteSpace(_scenarioFilePath)) {
            _scenarioFilePath = scenarioFileManager.StoreScenarioFile(ScenarioObject).FullName;
        } else {
            scenarioFileManager.StoreScenarioFile(ScenarioObject, _scenarioFilePath);
        }
    }

    private void ResetSelectedNpc(ScenarioNpcData? newNpc = null) {
        SelectedScenarioNpc = newNpc;
        ResetSelectedAction();
    }
    private void ResetSelectedAction(ScenarioNpcAction? newAction = null) {
        SelectedScenarioNpcAction = newAction;
        SelectedPathMovementPoint = null;
    }

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
        }

        stateIcon = Result == null ? FontAwesomeIcon.FileUpload : Result == true ? FontAwesomeIcon.CheckCircle : FontAwesomeIcon.ExclamationCircle;
        stateColor = Result == null ? null : Result == true ? ImGuiColors.HealerGreen : ImGuiColors.DPSRed;
    }

    public void SetResult(bool state) {
        Result = state;
        Timeout = DateTime.Now.AddSeconds(2);
    }
}
