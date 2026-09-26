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

            form.CheckboxRow(loc["ScenarioEditor_BaseData_Input_Looping"], ScenarioObject.Looping, value => ScenarioObject.Looping = value);

            using (ImRaii.Disabled(!ScenarioObject.Looping)) {
                form.Row(loc["ScenarioEditor_BaseData_Input_LoopingDelay"], () => {
                    using var width = ImRaii.ItemWidth(ArrpGuiSpacing.NumericInputWidth);
                    var delay = ScenarioObject.LoopDelay;
                    if (ImGui.InputFloat("s.##value", ref delay, step: 0.1f)) {
                        ScenarioObject.LoopDelay = Math.Max(delay, 0f);
                    }
                });
            }
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
        DrawTabBody("##arrpActorTabPlacementBody", () => ArrpGuiForm.Draw("##arrpActorPlacementForm", form => {

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
            
            form.CheckboxRow(loc["ScenarioEditor_ActorData_Behavior_TrackPlayer"], npc.Behavior.TrackPlayer, value => npc.Behavior.TrackPlayer = value);
        }));
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
