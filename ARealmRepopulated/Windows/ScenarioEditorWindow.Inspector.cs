using ARealmRepopulated.Core.ArrpGui.Components;
using ARealmRepopulated.Core.ArrpGui.Style;
using ARealmRepopulated.Core.IPC;
using ARealmRepopulated.Data.Location;
using ARealmRepopulated.Data.Scenarios;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility.Raii;
using System.Numerics;
using CsMaths = FFXIVClientStructs.FFXIV.Common.Math;

namespace ARealmRepopulated.Windows;

public partial class ScenarioEditorWindow {

    private void DrawInspectorPane() {
        if (SelectedScenarioNpc == null) {
            DrawScenarioInspector();
            return;
        }

        if (SelectedScenarioNpcAction != null) {
            DrawActionInspector(SelectedScenarioNpcAction);
            return;
        }

        DrawActorInspector(SelectedScenarioNpc);
    }

    private void DrawScenarioInspector() {
        ArrpGuiLayout.PanelHeader("##arrpScenarioPanelHeader", FontAwesomeIcon.Film, ScenarioTitle(), loc["ScenarioEditor_BaseData_Title"]);

        using var tabBar = ImRaii.TabBar("##arrpScenarioInspectorTabs");
        if (!tabBar.Success)
            return;

        using (var general = ImRaii.TabItem($"{loc["ScenarioEditor_BaseData_General_Title"]}##arrpScenarioTabGeneral", ImGuiTabItemFlags.NoTooltip)) {
            if (general.Success)
                DrawTabBody("##arrpScenarioTabGeneralBody", DrawScenarioGeneralTab);
        }

        using (var playback = ImRaii.TabItem($"{loc["ScenarioEditor_Playback_Title"]}##arrpScenarioTabPlayback", ImGuiTabItemFlags.NoTooltip)) {
            if (playback.Success)
                DrawTabBody("##arrpScenarioTabPlaybackBody", DrawScenarioPlaybackTab);
        }

        using var conditions = ImRaii.TabItem($"{loc["ScenarioEditor_Conditions_Title"]}##arrpScenarioTabConditions", ImGuiTabItemFlags.NoTooltip);
        if (conditions.Success)
            DrawScenarioConditionsTab();
    }

    private void DrawScenarioGeneralTab() {
        using (ImRaii.Disabled())
            ImGui.TextWrapped(loc["ScenarioEditor_BaseData_Desc"]);

        ImGui.Dummy(ArrpGuiSpacing.VerticalComponentSpacing);

        ArrpGuiForm.Draw("##arrpScenarioInspectorForm", form => {

            form.Row(loc["ScenarioEditor_BaseData_Input_Location"], () => {
                ImGui.AlignTextToFramePadding();
                ImGui.Text($"{ScenarioObject.Location.Territory} - {DescribeTerritory()}");
                ImGui.SameLine(0, ArrpGuiSpacing.InlineIconSpacing);
                if (ImGui.SmallButton(loc["ScenarioEditor_BaseData_Input_LocationCurrent"])) {
                    eventService.CurrentLocation.UpdateScenarioLocation(ScenarioObject.Location);
                }
            });

            form.StretchedRow(loc["ScenarioEditor_BaseData_Input_Title"], () => {
                var title = ScenarioObject.Title;
                if (ImGui.InputText("##value", ref title)) {
                    ScenarioObject.Title = title;
                }
            });

            form.StretchedRow(loc["ScenarioEditor_BaseData_Input_Description"], () => {
                var description = ScenarioObject.Description;
                if (ImGui.InputTextMultiline("##value", ref description, size: new Vector2(0, 80))) {
                    ScenarioObject.Description = description;
                }
            });

            form.Row(loc["ScenarioEditor_BaseData_Input_Group"], () => {
                var innerSpacing = ImGui.GetStyle().ItemInnerSpacing.X;
                var group = ScenarioObject.Group;
                ImGui.SetNextItemWidth(-(ImGui.GetFrameHeight() + innerSpacing));
                if (ImGui.InputTextWithHint("##value", loc["ScenarioEditor_BaseData_Input_Group_Hint"], ref group)) {
                    ScenarioObject.Group = group;
                }

                ImGui.SameLine(0, innerSpacing);
                using var combo = ImRaii.Combo("##groupSuggestions", string.Empty, ImGuiComboFlags.NoPreview | ImGuiComboFlags.PopupAlignLeft);
                if (!combo.Success)
                    return;

                var existingGroups = scenarioFileManager.GetScenarioFiles()
                    .Select(f => string.Join('/', f.MetaData.GroupSegments()))
                    .Where(g => g.Length > 0)
                    .Distinct(StringComparer.InvariantCultureIgnoreCase)
                    .Order(StringComparer.InvariantCultureIgnoreCase)
                    .ToList();

                if (existingGroups.Count == 0)
                    ImGui.TextDisabled(loc["ScenarioEditor_BaseData_Input_Group_None"]);

                foreach (var existing in existingGroups) {
                    if (ImGui.Selectable(existing, existing == ScenarioObject.Group)) {
                        ScenarioObject.Group = existing;
                    }
                }
            }, loc["ScenarioEditor_BaseData_Input_Group_Help"]);
        });
    }

    private void DrawScenarioPlaybackTab() {
        using (ImRaii.Disabled())
            ImGui.TextWrapped(loc["ScenarioEditor_Playback_Desc"]);

        ImGui.Dummy(ArrpGuiSpacing.VerticalComponentSpacing);

        ArrpGuiForm.Draw("##arrpScenarioPlaybackForm", form => {
            form.Row(loc["ScenarioEditor_BaseData_Input_Looping"], () => {
                var looping = ScenarioObject.Looping;
                if (ImGui.Checkbox("##looping", ref looping)) {
                    ScenarioObject.Looping = looping;
                }
                ImGui.SameLine();

                using var disabled = ImRaii.Disabled(!ScenarioObject.Looping);
                using var width = ImRaii.ItemWidth(ArrpGuiSpacing.NumericInputWidth);
                var delay = ScenarioObject.LoopDelay;
                if (ImGui.InputFloat("s.##delay", ref delay, step: 0.1f, format: "%.1f")) {
                    ScenarioObject.LoopDelay = Math.Max(delay, 0f);
                }
            }, loc["ScenarioEditor_BaseData_Input_LoopingDelay"]);

            form.Row(loc["ScenarioEditor_BaseData_Input_EndBehavior"], () => {
                using var width = ImRaii.ItemWidth(ArrpGuiSpacing.NumericInputWidth * 2);
                using var combo = ImRaii.Combo("##value", DescribeEndBehavior(ScenarioObject.EndBehavior));
                if (!combo.Success)
                    return;

                foreach (var option in Enum.GetValues<ScenarioEndBehavior>()) {
                    if (ImGui.Selectable(DescribeEndBehavior(option), ScenarioObject.EndBehavior == option)) {
                        ScenarioObject.EndBehavior = option;
                    }
                }
            }, loc["ScenarioEditor_BaseData_Input_EndBehavior_Desc"]);
        });
    }

    private void DrawActorInspector(ScenarioNpcData npc) {        
        ArrpGuiLayout.PanelHeader("##arrpActorPanelHeader", FontAwesomeIcon.User, ActorLabel(npc),
            Pluralize(npc.Actions.Count, "ScenarioEditor_Inspector_Actor_SubtitleOne", "ScenarioEditor_Inspector_Actor_Subtitle"),
            trailing: () => DrawDeleteAction("##arrpActorDelete", "ScenarioEditor_Inspector_Actor_Delete"),
            titleSuffix: npc.TryGetIntegrationProperty(IntegrationProvider.ActorNameConfigKey, out var actorName) ? () => DrawActorNameSuffix(actorName) : null);

        using var tabBar = ImRaii.TabBar("##arrpActorInspectorTabs");
        if (!tabBar.Success)
            return;

        using (var placement = ImRaii.TabItem($"{loc["ScenarioEditor_ActorData_General_Title"]}##arrpActorTabPlacement", ImGuiTabItemFlags.NoTooltip)) {
            if (placement.Success)
                DrawActorPlacementTab(npc);
        }

        using (var setup = ImRaii.TabItem($"{loc["ScenarioEditor_ActorData_Appearance_Setup"]}##arrpActorTabSetup", ImGuiTabItemFlags.NoTooltip)) {
            if (setup.Success)
                DrawTabBody("##arrpActorTabSetupBody", DrawNpcSetupTab);
        }

        if (!IsRemoteAppearanceManagement(npc)) {
            using (var model = ImRaii.TabItem($"{loc["ScenarioEditor_ActorData_Appearance_Model"]}##arrpActorTabModel", ImGuiTabItemFlags.NoTooltip)) {
                if (model.Success)
                    DrawTabBody("##arrpActorTabModelBody", DrawNpcCustomizeAppearanceInfo);
            }

            using (var equipment = ImRaii.TabItem($"{loc["ScenarioEditor_ActorData_Appearance_Equip"]}##arrpActorTabEquipment", ImGuiTabItemFlags.NoTooltip)) {
                if (equipment.Success)
                    DrawTabBody("##arrpActorTabEquipmentBody", DrawNpcEquipmentAppearanceInfo);
            }
        }

        if (!config.RuntimeConfig.ModdingToolsInstalled)
            return;

        using var integration = ImRaii.TabItem($"{loc["ScenarioEditor_ActorData_Appearance_Integration"]}##arrpActorTabIntegration", ImGuiTabItemFlags.NoTooltip);
        if (integration.Success)
            DrawTabBody("##arrpActorTabIntegrationBody", DrawNpcIntegrationInfo);
    }
    
    private static void DrawActorNameSuffix(string actorName) {
        ImGui.SameLine(0, 0);
        ImGui.Text(" [");
        ImGui.SameLine(0, 0);
        ImGui.TextColored(ArrpGuiColors.ArrpGreen, actorName);
        ImGui.SameLine(0, 0);
        ImGui.Text("]");
    }

    private static void DrawTabBody(string id, Action draw) {
        ImGui.Dummy(ArrpGuiSpacing.VerticalHeaderSpacing);

        using var body = ImRaii.Child(id, new Vector2(0, 0), false);
        if (!body.Success)
            return;

        draw();
    }

    private void DrawActorPlacementTab(ScenarioNpcData npc) {
        DrawTabBody("##arrpActorTabPlacementBody", () => {
            DrawActorNameForm(npc);
            DrawActorPlacementSection(npc);
            DrawActorBehaviorSection(npc);
        });
    }

    private void DrawActorNameForm(ScenarioNpcData npc) {
        ArrpGuiForm.Draw("##arrpActorNameForm", form => {
            form.Row(loc["ScenarioEditor_ActorData_General_Input_Name"], () => {
                using (ImRaii.ItemWidth(-(ImGui.GetFrameHeight() + ImGui.GetTextLineHeight()))) {
                    var name = npc.Name;
                    if (ImGui.InputText("##value", ref name)) {
                        npc.Name = name;
                    }
                }

                ImGui.SameLine();
                if (ImGuiComponents.IconButton(FontAwesomeIcon.Random)) {
                    var newActorName = characterCreationData.GenerateRandomName(npc.Appearance.Race, npc.Appearance.Tribe, npc.Appearance.Sex);
                    npc.Name = $"{newActorName.FirstName} {newActorName.LastName}";
                }
                ArrpGuiLayout.Tooltip(loc["ScenarioEditor_ActorData_General_Input_NameRandom"]);
            });

            form.StretchedRow(loc["ScenarioEditor_ActorData_General_Input_NameDisplay"], () => {
                using var combo = ImRaii.Combo("##value", DescribeNameDisplay(npc.DisplayNameSetting));
                if (!combo.Success)
                    return;

                foreach (var option in Enum.GetValues<NpcNameDisplay>()) {
                    if (ImGui.Selectable(DescribeNameDisplay(option), npc.DisplayNameSetting == option)) {
                        npc.DisplayNameSetting = option;
                    }
                }
            }, loc["ScenarioEditor_ActorData_General_Input_NameDisplay_Desc"]);
        });
    }

    private string DescribeEndBehavior(ScenarioEndBehavior endBehavior) => endBehavior switch {
        ScenarioEndBehavior.AfterRun => loc["ScenarioEditor_BaseData_Input_EndBehavior_AfterRun"],
        ScenarioEndBehavior.Immediately => loc["ScenarioEditor_BaseData_Input_EndBehavior_Immediately"],
        ScenarioEndBehavior.Never => loc["ScenarioEditor_BaseData_Input_EndBehavior_Never"],
        _ => endBehavior.ToString()
    };

    private string DescribeNameDisplay(NpcNameDisplay display) => display switch {
        NpcNameDisplay.Never => loc["ScenarioEditor_ActorData_General_Input_NameDisplay_Never"],
        NpcNameDisplay.GameDefault => loc["ScenarioEditor_ActorData_General_Input_NameDisplay_GameDefault"],
        NpcNameDisplay.Always => loc["ScenarioEditor_ActorData_General_Input_NameDisplay_Always"],
        _ => display.ToString()
    };

    private void DrawActorPlacementSection(ScenarioNpcData npc) {
        ArrpGuiLayout.SectionHeader(loc["ScenarioEditor_ActorData_Placement_Title"]);

        ArrpGuiForm.Draw("##arrpActorPlacementForm", form => {
            form.Row(loc["ScenarioEditor_ActorData_General_Input_Position"], () => {
                var position = new Vector3(npc.Position.X, npc.Position.Y, npc.Position.Z);
                if (ImGui.InputFloat3("##value", ref position)) {
                    npc.Position = new CsMaths.Vector3(position.X, position.Y, position.Z);
                }

                ImGui.SameLine();
                if (ImGuiComponents.IconButton(FontAwesomeIcon.LocationCrosshairs) && objectTable.LocalPlayer != null) {
                    npc.Position = new CsMaths.Vector3(objectTable.LocalPlayer.Position.X, objectTable.LocalPlayer.Position.Y, objectTable.LocalPlayer.Position.Z);
                }
                ArrpGuiLayout.Tooltip(loc["ScenarioEditor_ActorData_General_Input_PositionCurrent"]);

                DrawGizmoTargetToggle("##gizmoPosition", ScenarioEditorGizmoTarget.Position);
            });

            form.LabeledCheckboxRow(loc["ScenarioEditor_ActorData_General_Input_SnapToSurface"], npc.SnapToSurface, value => npc.SnapToSurface = value, loc["ScenarioEditor_ActorData_General_Input_SnapToSurface_Desc"]);

            form.Row(loc["ScenarioEditor_ActorData_General_Input_Rotation"], () => {
                var rotation = npc.Rotation;
                if (ImGui.InputFloat("##value", ref rotation)) {
                    npc.Rotation = rotation;
                }

                ImGui.SameLine();
                if (ImGuiComponents.IconButton(FontAwesomeIcon.ArrowsSpin) && objectTable.LocalPlayer != null) {
                    npc.Rotation = objectTable.LocalPlayer.Rotation;
                }
                ArrpGuiLayout.Tooltip(loc["ScenarioEditor_ActorData_General_Input_RotationCurrent"]);
            });

            form.Row(loc["ScenarioEditor_ActorData_General_Input_DrawOffset"], () => {
                var drawOffset = new Vector3(npc.DrawOffset.X, npc.DrawOffset.Y, npc.DrawOffset.Z);
                if (ImGui.InputFloat3("##value", ref drawOffset)) {
                    npc.DrawOffset = new CsMaths.Vector3(drawOffset.X, drawOffset.Y, drawOffset.Z);
                }

                ImGui.SameLine();
                if (ImGuiComponents.IconButton(FontAwesomeIcon.Undo)) {
                    npc.DrawOffset = CsMaths.Vector3.Zero;
                }
                ArrpGuiLayout.Tooltip(loc["ScenarioEditor_ActorData_General_Input_DrawOffsetReset"]);

                DrawGizmoTargetToggle("##gizmoDrawOffset", ScenarioEditorGizmoTarget.DrawOffset);
            }, loc["ScenarioEditor_ActorData_General_Input_DrawOffset_Desc"]);
        });
    }

    private void DrawActorBehaviorSection(ScenarioNpcData npc) {
        ArrpGuiLayout.SectionHeader(loc["ScenarioEditor_ActorData_Behavior_Title"]);

        ArrpGuiForm.Draw("##arrpActorBehaviorForm", form => {
            form.LabeledCheckboxRow(loc["ScenarioEditor_ActorData_Behavior_TrackPlayer"], npc.Behavior.TrackPlayer, value => npc.Behavior.TrackPlayer = value);
        });
    }

    private void DrawGizmoTargetToggle(string id, ScenarioEditorGizmoTarget target) {

        var isOverlayEnabled = config.EnableScenarioDebugOverlay;
        var isSelected = SelectedGizmoTarget == target;

        ImGui.SameLine();
        using (ImRaii.Disabled(!isOverlayEnabled)) {
            if (ImGuiComponents.IconButton(id, FontAwesomeIcon.ArrowsUpDownLeftRight, isSelected ? ArrpGuiColors.ArrpGreen : null)) {
                SelectedGizmoTarget = target;
            }
        }

        ArrpGuiLayout.Tooltip(loc[isOverlayEnabled
            ? "ScenarioEditor_ActorData_General_Input_GizmoTarget"
            : "ScenarioEditor_ActorData_General_Input_GizmoTargetNoOverlay"]);
    }

    private void DrawActionInspector(ScenarioNpcAction action) {
        ArrpGuiLayout.PanelHeader("##arrpActionPanelHeader", () => DrawActionEnabledToggle(action),
            _actionUiRegistry.GetShortName(action), ActorLabel(SelectedScenarioNpc!),
            trailing: () => DrawDeleteAction("##arrpActionDelete", "ScenarioEditor_Inspector_Action_Delete"));

        using (ImRaii.Disabled())
            ImGui.TextWrapped(_actionUiRegistry.GetHelp(action));

        ImGui.Dummy(ArrpGuiSpacing.VerticalComponentSpacing);

        using var body = ImRaii.Child("##arrpActionInspectorBody", new Vector2(0, 0), false);
        if (!body.Success)
            return;
        
        if (action.CanHaveTalk || action.CanHaveDuration) {
            DrawActionCommonForm(action);
        }

        _actionUiRegistry.Draw(action);
    }
    
    private void DrawActionEnabledToggle(ScenarioNpcAction action) {
        var enabled = action.Enabled;
        if (ImGui.Checkbox("##arrpActionEnabledToggle", ref enabled)) {
            action.Enabled = enabled;
        }

        ArrpGuiLayout.Tooltip(loc["ScenarioEditor_Inspector_Action_EnabledHint"]);
    }

    private void DrawDeleteAction(string id, string labelKey) {
        var armed = ImGui.GetIO().KeyShift;

        using (ImRaii.Disabled(!armed)) {
            if (ImGuiComponents.IconButton(id, FontAwesomeIcon.Trash, ImGuiColors.DalamudRed)) {
                DeleteSelection();
            }
        }

        ArrpGuiLayout.Tooltip(armed
            ? loc[labelKey]
            : $"{loc[labelKey]}\n\n{loc["ScenarioEditor_Inspector_DeleteShiftHint"]}");
    }

    private void DrawActionCommonForm(ScenarioNpcAction action) {
        ArrpGuiForm.Draw("##arrpActionCommonForm", form => {

            if (action.CanHaveTalk) {
                form.StretchedRow(loc["ScenarioEditor_ActorData_Actions_ATalk_Short"], () => {
                    var talk = action.NpcTalk;
                    if (ImGui.InputText("##value", ref talk)) {
                        action.NpcTalk = talk;
                    }
                });
            }

            if (action.CanHaveDuration) {
                form.Row(loc["ScenarioEditor_ActorData_Actions_ADuration_Short"], () => {
                    using var width = ImRaii.ItemWidth(ArrpGuiSpacing.NumericInputWidth);
                    var duration = action.Duration;
                    if (ImGui.InputFloat("s.##value", ref duration, step: 0.1f, stepFast: 0.1f, format: "%.2f")) {
                        if (duration < 0.1f)
                            duration = 0f;
                        action.Duration = Math.Clamp(duration, 0f, float.MaxValue);
                    }
                });
            }
        });
    }

}
