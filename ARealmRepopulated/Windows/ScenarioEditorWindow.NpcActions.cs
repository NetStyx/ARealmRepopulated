using ARealmRepopulated.Core.ArrpGui.Style;
using ARealmRepopulated.Core.SpatialMath;
using ARealmRepopulated.Data.Scenarios;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility.Raii;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using System.Numerics;
using CsMaths = FFXIVClientStructs.FFXIV.Common.Math;

namespace ARealmRepopulated.Windows;

public partial class ScenarioEditorWindow {

    private void DrawActionSelection() {

        if (ImGui.Selectable(loc["ScenarioEditor_ActorData_Actions_AWaiting_Short"])) {
            AddAction(new ScenarioNpcWaitingAction());
        }

        if (ImGui.Selectable(loc["ScenarioEditor_ActorData_Actions_AIdle_Short"])) {
            AddAction(new ScenarioNpcIdleAction());
        }

        if (ImGui.Selectable(loc["ScenarioEditor_ActorData_Actions_AEmote_Short"])) {
            AddAction(new ScenarioNpcEmoteAction());
        }

        if (ImGui.Selectable(loc["ScenarioEditor_ActorData_Actions_ASpawn_Short"])) {
            AddAction(new ScenarioNpcSpawnAction());
        }

        if (ImGui.Selectable(loc["ScenarioEditor_ActorData_Actions_ADespawn_Short"])) {
            AddAction(new ScenarioNpcDespawnAction());
        }

        if (ImGui.Selectable(loc["ScenarioEditor_ActorData_Actions_AMove_Short"])) {
            AddAction(new ScenarioNpcMovementAction { TargetPosition = objectTable.LocalPlayer?.Position ?? new CsMaths.Vector3() });
        }

        if (ImGui.Selectable(loc["ScenarioEditor_ActorData_Actions_APath_Short"])) {
            AddAction(new ScenarioNpcPathAction { Points = [new() { Point = objectTable.LocalPlayer?.Position.AsCsVector() ?? new CsMaths.Vector3(), Speed = NpcSpeed.Running }] });
        }

        if (ImGui.Selectable(loc["ScenarioEditor_ActorData_Actions_ARotation_Short"])) {
            AddAction(new ScenarioNpcRotationAction { TargetRotation = objectTable.LocalPlayer?.Rotation ?? 0f });
        }

        if (ImGui.Selectable(loc["ScenarioEditor_ActorData_Actions_ASync_Short"])) {
            AddAction(new ScenarioNpcSyncAction());
        }
    }

    private void DrawCurrentActionBar() {
        if (SelectedScenarioNpcAction == null) {
            return;
        }

    }

    private void DrawCurrentAction() {
        if (SelectedScenarioNpcAction == null) {
            return;
        }

        ImGui.Text(_actionUiRegistry.GetShortName(SelectedScenarioNpcAction));
        using (ImRaii.Disabled()) {
            ImGui.TextWrapped(_actionUiRegistry.GetHelp(SelectedScenarioNpcAction));
        }
        ImGui.Separator();

        using (ImRaii.Table("##scenarioNpcEditorActionTable", 2, ImGuiTableFlags.NoBordersInBody | ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.NoSavedSettings)) {

            ImGui.TableSetupColumn("##scenarioNpcEditorActionTableCap", ImGuiTableColumnFlags.WidthFixed);
            ImGui.TableSetupColumn("##scenarioNpcEditorActionTableValue", ImGuiTableColumnFlags.WidthStretch);

            if (SelectedScenarioNpcAction.CanHaveTalk) {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();

                ImGui.Text(loc["ScenarioEditor_ActorData_Actions_ATalk_Short"]);

                ImGui.TableNextColumn();
                var talk = SelectedScenarioNpcAction.NpcTalk;
                if (ImGui.InputText("##scenarioNpcGeneralActionTalk", ref talk)) {
                    SelectedScenarioNpcAction.NpcTalk = talk;
                }
            }

            if (SelectedScenarioNpcAction.CanHaveDuration) {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.Text(loc["ScenarioEditor_ActorData_Actions_ADuration_Short"]);

                ImGui.TableNextColumn();
                var duration = SelectedScenarioNpcAction.Duration;
                if (ImGui.InputFloat("s.##scenarioNpcGeneralActionDuration", ref duration, step: 0.1f, stepFast: 0.1f, format: "%.2f")) {
                    if (duration < 0.1f)
                        duration = 0f;
                    SelectedScenarioNpcAction.Duration = Math.Clamp(duration, 0f, float.MaxValue);

                }
            }

            _actionUiRegistry.Draw(SelectedScenarioNpcAction);
        }
    }

    private void DrawSyncAction(ScenarioNpcSyncAction syncAction) {

    }

    private void DrawRotationAction(ScenarioNpcRotationAction rotationAction) {
        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        ImGui.Text("Rotation");
        ImGui.TableNextColumn();
        var rotation = rotationAction.TargetRotation;
        if (ImGui.InputFloat("##scenarioNpcRotateActionRotation", ref rotation)) {
            rotationAction.TargetRotation = rotation;
        }
        ImGui.SameLine();
        if (ImGui.SmallButton($"{loc["ScenarioEditor_ActorData_Actions_ARotation_SetCurrent"]}##scenarioNpcRotateActionCurrentRotation")) {
            rotationAction.TargetRotation = objectTable.LocalPlayer?.Rotation ?? 0f;
        }
    }

    private void DrawMovementAction(ScenarioNpcMovementAction moveAction) {
        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        ImGui.Text(loc["ScenarioEditor_ActorData_Actions_AMove_Position"]);

        ImGui.TableNextColumn();
        var position = new Vector3(moveAction.TargetPosition.X, moveAction.TargetPosition.Y, moveAction.TargetPosition.Z);
        if (ImGui.InputFloat3("##scenarioNpcMoveActionPosition", ref position)) {
            moveAction.TargetPosition = new CsMaths.Vector3(position.X, position.Y, position.Z);
        }
        ImGui.SameLine();
        if (ImGuiComponents.IconButton(FontAwesomeIcon.LocationCrosshairs) && objectTable.LocalPlayer != null) {
            moveAction.TargetPosition = new CsMaths.Vector3(objectTable.LocalPlayer.Position.X, objectTable.LocalPlayer.Position.Y, objectTable.LocalPlayer.Position.Z);
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(loc["ScenarioEditor_ActorData_Actions_AMove_Position_CurrentLocationHint"]);

        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        ImGui.Text(loc["ScenarioEditor_ActorData_Actions_AMove_Speed"]);
        ImGui.TableNextColumn();

        if (ImGui.BeginCombo($"##scenarioNpcMoveActionPositionSpeedSelection", moveAction.Speed.ToString())) {
            if (ImGui.Selectable($"{loc["ScenarioEditor_ActorData_Actions_WalkSpeed_Walking"]}##scenarioNpcMoveActionPositionSpeedSelectionWalking", moveAction.Speed == NpcSpeed.Walking)) {
                moveAction.Speed = NpcSpeed.Walking;
            }
            if (ImGui.Selectable($"{loc["ScenarioEditor_ActorData_Actions_WalkSpeed_Running"]}##scenarioNpcMoveActionPositionSpeedSelectionRunning", moveAction.Speed == NpcSpeed.Running)) {
                moveAction.Speed = NpcSpeed.Running;
            }
            ImGui.EndCombo();
        }

    }

    private void DrawPathAction(ScenarioNpcPathAction moveAction) {

        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        ImGui.Text(loc["ScenarioEditor_ActorData_Actions_APath_Tension"]);
        ImGui.SameLine();
        ImGuiComponents.HelpMarker(loc["ScenarioEditor_ActorData_Actions_APath_TensionHint"]);
        ImGui.TableNextColumn();
        var tensionRef = moveAction.Tension;
        if (ImGui.SliderFloat("##scenarioNpcPathInputTension", ref tensionRef, 0f, 1f)) {
            moveAction.Tension = tensionRef;
        }

        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        ImGui.Separator();
        ImGui.Dummy(ArrpGuiSpacing.VerticalComponentSpacing);
        if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Plus, $"{loc["ScenarioEditor_ActorData_Actions_APath_AddPoint"]}##scenarioNpcPathActionPointsTableAddEntry")) {

            var point = new PathMovementPoint { Speed = SelectedPathMovementPoint != null ? SelectedPathMovementPoint.Speed : NpcSpeed.Running, Point = objectTable.LocalPlayer?.Position.AsCsVector() ?? Vector3.Zero };

            if (SelectedPathMovementPoint != null) {
                var index = moveAction.Points.IndexOf(SelectedPathMovementPoint);
                if (index >= 0) {
                    moveAction.Points.Insert(index + 1, point);
                } else {
                    moveAction.Points.Add(point);
                }
            } else {
                moveAction.Points.Add(point);
            }

            SelectedPathMovementPoint = point;

        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(loc["ScenarioEditor_ActorData_Actions_APath_AddPoint"]);

        ImGui.TableNextColumn();
        ImGui.Separator();

        ImGui.Dummy(ArrpGuiSpacing.VerticalHeaderSpacing);
        using var child = ImRaii.Child("##scenarioNpcPathActionPointsChild", new Vector2(0, -20));
        using var table = ImRaii.Table("##scenarioNpcPathActionPointsTable", 3, ImGuiTableFlags.NoSavedSettings | ImGuiTableFlags.NoBordersInBody);

        ImGui.TableSetupColumn("##scenarioNpcPathActionPointsTableActionCol", ImGuiTableColumnFlags.WidthFixed, 50);
        ImGui.TableSetupColumn("##scenarioNpcPathActionPointsTableSpeedCol", ImGuiTableColumnFlags.WidthFixed, 100);
        ImGui.TableSetupColumn("##scenarioNpcPathActionPointsTablePointCol", ImGuiTableColumnFlags.WidthStretch);

        for (var i = 0; i < moveAction.Points.Count; i++) {
            var point = moveAction.Points[i];

            ImGui.TableNextRow();
            ImGui.TableNextColumn();

            if (ImGuiComponents.IconButton($"##scenarioNpcPathActionPointsTableRemoveEntry{i}", FontAwesomeIcon.Trash)) {
                moveAction.Points.Remove(point);
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(loc["ScenarioEditor_ActorData_Actions_APath_RemovePointHint"]);

            ImGui.SameLine();
            if (ImGuiComponents.IconButton($"##scenarioNpcPathActionPointsTableSelectRowEntry{i}", FontAwesomeIcon.LocationArrow)) {
                SelectedPathMovementPoint = point;
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(loc["ScenarioEditor_ActorData_Actions_APath_SelectPointHint"]);

            ImGui.TableNextColumn();
            ImGui.SetNextItemWidth(-1);
            if (ImGui.BeginCombo($"##scenarioNpcPathActionPointsTableSpeedSelection{i}", point.Speed.ToString())) {
                if (ImGui.Selectable($"{loc["ScenarioEditor_ActorData_Actions_WalkSpeed_Walking"]}##scenarioNpcPathActionPointsTableSpeedSelectionWalking{i}", point.Speed == NpcSpeed.Walking)) {
                    point.Speed = NpcSpeed.Walking;
                }
                if (ImGui.Selectable($"{loc["ScenarioEditor_ActorData_Actions_WalkSpeed_Running"]}##scenarioNpcPathActionPointsTableSpeedSelectionRunning{i}", point.Speed == NpcSpeed.Running)) {
                    point.Speed = NpcSpeed.Running;
                }
                ImGui.EndCombo();
            }

            ImGui.TableNextColumn();

            var pointPos = point.Point.AsVector();
            if (ImGui.InputFloat3($"##scenarioNpcPathActionPointsTablePoint{i}", ref pointPos)) {
                point.Point = pointPos;
            }

            ImGui.SameLine();
            if (ImGuiComponents.IconButton(FontAwesomeIcon.LocationCrosshairs) && objectTable.LocalPlayer != null) {
                point.Point = objectTable.LocalPlayer.Position.AsCsVector();
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(loc["ScenarioEditor_ActorData_Actions_APath_Position_CurrentLocationHint"]);

            ImGui.SameLine();
            ImGui.Selectable($"##scenarioNpcPathActionPointsTableSelectable{i}", SelectedPathMovementPoint == point, ImGuiSelectableFlags.SpanAllColumns | ImGuiSelectableFlags.Disabled);

        }

    }

    private void DrawDespawnAction(ScenarioNpcDespawnAction despawnAction) {

    }

    private void DrawSpawnAction(ScenarioNpcSpawnAction spawnAction) {

    }

    private void DrawWaitingAction(ScenarioNpcWaitingAction waitingAction) {

    }

    private void DrawIdleAction(ScenarioNpcIdleAction idleAction) {

    }

    private void DrawEmoteAction(ScenarioNpcEmoteAction emoteAction) {
        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        ImGui.Text(loc["ScenarioEditor_ActorData_Actions_AEmote_Select"]);

        ImGui.TableNextColumn();
        var emoteid = emoteAction.Emote;
        var emoteRow = dataCache.GetEmote(emoteid);
        var emoteName = $"{emoteid} - " + emoteRow.Name.ToString();

        ImGui.BeginDisabled(true);
        ImGui.InputText("##scenarioNpcEmoteActionEmote", ref emoteName);
        ImGui.EndDisabled();

        ImGui.SameLine();
        if (ImGuiComponents.IconButton(Dalamud.Interface.FontAwesomeIcon.WandMagicSparkles)) {
            ImGui.OpenPopup("EmotePicker");
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(loc["ScenarioEditor_ActorData_Actions_AEmote_SelectHint"]);

        if (ImGui.BeginPopup("EmotePicker")) {
            if (emotePicker.Open(out var emoteId)) {
                emoteAction.Emote = emoteId;
            }
            ImGui.EndPopup();
        }

        if (emoteid == 0 || !emoteRow.EmoteMode.IsValid)
            return;

        var emoteCondition = (CharacterModes)emoteRow.EmoteMode.Value.ConditionMode;

        if (emoteCondition != CharacterModes.EmoteLoop && emoteCondition != CharacterModes.InPositionLoop) {
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.TableNextColumn();
            var loop = emoteAction.Loop;
            if (ImGui.Checkbox($"{loc["ScenarioEditor_ActorData_Actions_AEmote_Loop"]}##scenarioNpcEmoteActionLoop", ref loop)) {
                emoteAction.Loop = loop;
            }
        }
        if (emoteCondition == CharacterModes.InPositionLoop) {
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.TableNextColumn();
            var endloop = emoteAction.StayInEmotePose;
            if (ImGui.Checkbox($"{loc["ScenarioEditor_ActorData_Actions_AEmote_StayInPos"]}##scenarioNpcEmoteActionStayInPoseAfterEnd", ref endloop)) {
                emoteAction.StayInEmotePose = endloop;
            }
        }

    }
}

public sealed class NpcActionUiRegistry {
    public class ActionTypeDisplayObject {
        public Action<ScenarioNpcAction> Draw { get; set; } = (_) => { };
        public Func<ScenarioNpcAction, string> NameResolver { get; set; } = (_) => string.Empty;
        public Func<ScenarioNpcAction, string> HelpResolver { get; set; } = (_) => string.Empty;
    }

    private readonly Dictionary<Type, ActionTypeDisplayObject> _handlers = [];

    public void Register<T>(Func<T, string>? shortName = null, Func<T, string>? help = null, Action<T>? draw = null) where T : ScenarioNpcAction {
        _handlers[typeof(T)] = new ActionTypeDisplayObject {
            HelpResolver = a => help != null ? help((T)a) : string.Empty,
            NameResolver = a => shortName != null ? shortName((T)a) : string.Empty,
            Draw = a => draw?.Invoke((T)a)
        };
    }

    public string GetShortName(ScenarioNpcAction action)
        => _handlers[action.GetType()].NameResolver(action);

    public string GetHelp(ScenarioNpcAction action)
        => _handlers[action.GetType()].HelpResolver(action);

    public void Draw(ScenarioNpcAction action)
        => _handlers[action.GetType()].Draw(action);
}
