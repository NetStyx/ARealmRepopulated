using ARealmRepopulated.Core.ArrpGui.Components;
using ARealmRepopulated.Core.ArrpGui.Style;
using ARealmRepopulated.Core.Services.Npcs;
using ARealmRepopulated.Core.SpatialMath;
using ARealmRepopulated.Data.Scenarios;
using ARealmRepopulated.Infrastructure;
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

        ImGui.TextDisabled(loc["ScenarioEditor_ActorData_Actions_Group_Timing"]);
        DrawActionSelectionEntry(new ScenarioNpcWaitingAction());
        DrawActionSelectionEntry(new ScenarioNpcSyncAction());

        ImGui.Separator();
        ImGui.TextDisabled(loc["ScenarioEditor_ActorData_Actions_Group_Pose"]);
        DrawActionSelectionEntry(new ScenarioNpcIdleAction());
        DrawActionSelectionEntry(new ScenarioNpcEmoteAction());
        DrawActionSelectionEntry(new ScenarioNpcTimelineAction());

        ImGui.Separator();
        ImGui.TextDisabled(loc["ScenarioEditor_ActorData_Actions_Group_Movement"]);
        DrawActionSelectionEntry(new ScenarioNpcMovementAction { TargetPosition = objectTable.LocalPlayer?.Position ?? new CsMaths.Vector3() });
        DrawActionSelectionEntry(new ScenarioNpcPathAction { Points = [new() { Point = objectTable.LocalPlayer?.Position.AsCsVector() ?? new CsMaths.Vector3(), Speed = NpcSpeed.Running }] });
        DrawActionSelectionEntry(new ScenarioNpcRotationAction { TargetRotation = objectTable.LocalPlayer?.Rotation ?? 0f });

        ImGui.Separator();
        ImGui.TextDisabled(loc["ScenarioEditor_ActorData_Actions_Group_Lifecycle"]);
        DrawActionSelectionEntry(new ScenarioNpcSpawnAction());
        DrawActionSelectionEntry(new ScenarioNpcDespawnAction());
    }
    
    private void DrawActionSelectionEntry(ScenarioNpcAction action) {
        if (ImGui.Selectable(_actionUiRegistry.GetShortName(action))) {
            AddAction(action);
        }
        ArrpGuiLayout.Tooltip(_actionUiRegistry.GetHelp(action));
    }

    private void DrawWaitingAction(ScenarioNpcWaitingAction waitingAction) {
        if (waitingAction.Duration > 0f)
            return;

        using (ImRaii.Disabled())
            ImGui.TextWrapped(loc["ScenarioEditor_ActorData_Actions_AWaiting_EndlessHint"]);
    }

    private void DrawIdleAction(ScenarioNpcIdleAction idleAction) {
        ArrpGuiForm.Draw("##arrpIdleActionForm", form
            => DrawPoseStateRow(form, PoseType.Idle,
                loc["ScenarioEditor_ActorData_Actions_AIdle_PoseState"],
                loc["ScenarioEditor_ActorData_Actions_AIdle_PoseStateHint"],
                idleAction.PoseState, poseState => idleAction.PoseState = poseState));

        if (idleAction.PoseState != 0 || idleAction.Duration != 0f)
            return;

        using (ImRaii.Disabled())
            ImGui.TextWrapped(loc["ScenarioEditor_ActorData_Actions_AIdle_ResetHint"]);
    }

    private void DrawRotationAction(ScenarioNpcRotationAction rotationAction) {
        ArrpGuiForm.Draw("##arrpRotationActionForm", form => form.Row(loc["ScenarioEditor_ActorData_Actions_ARotation_Caption"], () => {
            using (ImRaii.ItemWidth(ArrpGuiSpacing.NumericInputWidth)) {
                var rotation = rotationAction.TargetRotation;
                if (ImGui.InputFloat("##value", ref rotation)) {
                    rotationAction.TargetRotation = rotation;
                }
            }

            ImGui.SameLine(0, ArrpGuiSpacing.InlineIconSpacing);
            if (ImGui.SmallButton(loc["ScenarioEditor_ActorData_Actions_ARotation_SetCurrent"])) {
                rotationAction.TargetRotation = objectTable.LocalPlayer?.Rotation ?? 0f;
            }
        }));
    }

    private void DrawMovementAction(ScenarioNpcMovementAction moveAction) {
        ArrpGuiForm.Draw("##arrpMovementActionForm", form => {
            DrawPositionRow(form, moveAction.TargetPosition, position => moveAction.TargetPosition = position,
                loc["ScenarioEditor_ActorData_Actions_AMove_Position_CurrentLocationHint"]);
            DrawSpeedRow(form, moveAction);
        });
    }

    private void DrawEmoteAction(ScenarioNpcEmoteAction emoteAction) {
        var emoteRow = dataCache.GetEmote(emoteAction.Emote);

        ArrpGuiForm.Draw("##arrpEmoteActionForm", form => {

            form.Row(loc["ScenarioEditor_ActorData_Actions_AEmote_Select"], () => {
                var emoteName = $"{emoteAction.Emote} - {emoteRow.Name}";
                using (ImRaii.Disabled())
                using (ImRaii.ItemWidth(-(ImGui.GetFrameHeight() + ImGui.GetTextLineHeight())))
                    ImGui.InputText("##value", ref emoteName);

                ImGui.SameLine();
                if (ImGuiComponents.IconButton(FontAwesomeIcon.WandMagicSparkles)) {
                    emotePicker.OpenPopup();
                }
                ArrpGuiLayout.Tooltip(loc["ScenarioEditor_ActorData_Actions_AEmote_SelectHint"]);

                if (emotePicker.Popup(out var emoteId)) {
                    emoteAction.Emote = emoteId;
                }
            });

            if (emoteRow.TryGetPoseType(out var emotePoseType)) {
                DrawPoseStateRow(form, emotePoseType,
                    loc["ScenarioEditor_ActorData_Actions_AEmote_PoseState"],
                    loc["ScenarioEditor_ActorData_Actions_AEmote_PoseStateHint"],
                    emoteAction.PoseState, poseState => emoteAction.PoseState = poseState);
            }

            if (emoteAction.Emote == 0 || !emoteRow.EmoteMode.IsValid)
                return;

            var emoteCondition = (CharacterModes)emoteRow.EmoteMode.Value.ConditionMode;

            if (emoteCondition != CharacterModes.EmoteLoop && emoteCondition != CharacterModes.InPositionLoop) {
                form.CheckboxRow(loc["ScenarioEditor_ActorData_Actions_AEmote_Loop"], emoteAction.Loop, value => emoteAction.Loop = value);
            }

            if (emoteCondition == CharacterModes.InPositionLoop) {
                form.CheckboxRow(loc["ScenarioEditor_ActorData_Actions_AEmote_StayInPos"], emoteAction.StayInEmotePose, value => emoteAction.StayInEmotePose = value);
            }

            if (emoteRow.InteractsWithLayout()) {
                form.CheckboxRow(loc["ScenarioEditor_ActorData_Actions_AEmote_InteractWithLayout"], emoteAction.InteractWithLayout, value => emoteAction.InteractWithLayout = value);
            }
        });
    }

    private void DrawPathAction(ScenarioNpcPathAction pathAction) {
        ArrpGuiForm.Draw("##arrpPathActionForm", form => form.Row(loc["ScenarioEditor_ActorData_Actions_APath_Tension"], () => {
            ImGui.SetNextItemWidth(-1);
            var tension = pathAction.Tension;
            if (ImGui.SliderFloat("##value", ref tension, 0f, 1f)) {
                pathAction.Tension = tension;
            }
        }, loc["ScenarioEditor_ActorData_Actions_APath_TensionHint"]));

        ArrpGuiLayout.SectionHeader(loc["ScenarioEditor_ActorData_Actions_APath_PointsTitle"]);

        if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Plus, loc["ScenarioEditor_ActorData_Actions_APath_AddPoint"])) {
            AddPathPoint(pathAction);
        }
        ArrpGuiLayout.Tooltip(loc["ScenarioEditor_ActorData_Actions_APath_AddPointHint"]);

        ImGui.Dummy(ArrpGuiSpacing.VerticalHeaderSpacing);

        if (pathAction.Points.Count == 0)
            return;
        
        const int editorRows = 5;
        var style = ImGui.GetStyle();
        using (ImRaii.PushColor(ImGuiCol.ChildBg, style.Colors[(int)ImGuiCol.FrameBg]))
        using (ImRaii.PushStyle(ImGuiStyleVar.ChildRounding, style.FrameRounding))
        using (var list = ImRaii.Child("##arrpPathActionPoints", new Vector2(0, -ImGui.GetFrameHeightWithSpacing() * editorRows), true, ImGuiWindowFlags.AlwaysVerticalScrollbar)) {
            if (list.Success) {
                DrawPathPointList(pathAction);                
            }
        }

        using var editor = ImRaii.Child("##arrpPathPointEditor", Vector2.Zero, false, ImGuiWindowFlags.NoScrollbar);
        if (!editor.Success)
            return;

        ImGui.Dummy(ArrpGuiSpacing.VerticalSectionSpacing);

        var selectedIndex = SelectedPathMovementPoint == null ? -1 : pathAction.Points.IndexOf(SelectedPathMovementPoint);
        if (selectedIndex < 0) {
            using (ImRaii.Disabled())
                ImGui.TextWrapped(loc["ScenarioEditor_ActorData_Actions_APath_NoPointSelected"]);
        } else {
            DrawPathPointDetail(pathAction, selectedIndex);
        }
    }

    private void AddPathPoint(ScenarioNpcPathAction pathAction) {
        var point = new PathMovementPoint {
            Speed = SelectedPathMovementPoint?.Speed ?? NpcSpeed.Running,
            CustomSpeed = SelectedPathMovementPoint?.CustomSpeed ?? 0f,
            Point = objectTable.LocalPlayer?.Position.AsCsVector() ?? CsMaths.Vector3.Zero
        };

        var index = SelectedPathMovementPoint == null ? -1 : pathAction.Points.IndexOf(SelectedPathMovementPoint);
        if (index >= 0) {
            pathAction.Points.Insert(index + 1, point);
        } else {
            pathAction.Points.Add(point);
        }

        SelectedPathMovementPoint = point;        
    }

    private void DrawPathPointList(ScenarioNpcPathAction pathAction) {
        using var padding = ImRaii.PushStyle(ImGuiStyleVar.CellPadding, ArrpGuiSpacing.TableCellPadding);
        using var table = ImRaii.Table("##arrpPathActionPointsTable", 3, ImGuiTableFlags.NoSavedSettings | ImGuiTableFlags.NoBordersInBody | ImGuiTableFlags.RowBg);
        if (!table.Success)
            return;

        ImGui.TableSetupColumn("##arrpPathPointIndex", ImGuiTableColumnFlags.WidthFixed);
        ImGui.TableSetupColumn("##arrpPathPointSpeed", ImGuiTableColumnFlags.WidthFixed);
        ImGui.TableSetupColumn("##arrpPathPointPosition", ImGuiTableColumnFlags.WidthStretch);

        for (var i = 0; i < pathAction.Points.Count; i++) {
            var point = pathAction.Points[i];
            var isSelected = SelectedPathMovementPoint == point;

            using var id = ImRaii.PushId($"arrpPathPoint{i}");

            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            if (ImGui.Selectable($"{i + 1}", isSelected, ImGuiSelectableFlags.SpanAllColumns)) {
                SelectedPathMovementPoint = isSelected ? null : point;
            }
            ArrpGuiLayout.Tooltip(loc["ScenarioEditor_ActorData_Actions_APath_SelectPointHint"]);

            ImGui.TableNextColumn();
            ImGui.Text(DescribeSpeed(point));

            ImGui.TableNextColumn();
            var position = $"{point.Point.X:0.0}  {point.Point.Y:0.0}  {point.Point.Z:0.0}";
            // keep the same gap to the scrollbar as between the columns
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetContentRegionAvail().X - ImGui.CalcTextSize(position).X - ArrpGuiSpacing.TableCellPadding.X);
            ImGui.TextDisabled(position);
        }
    }

    private void DrawPathPointDetail(ScenarioNpcPathAction pathAction, int index) {
        var point = pathAction.Points[index];

        ArrpGuiLayout.HeaderRow("##arrpPathPointHeader", () => {
            ImGui.AlignTextToFramePadding();
            ImGui.Text(loc["ScenarioEditor_ActorData_Actions_APath_PointTitle", index + 1]);
        }, () => {
            using (ImRaii.Disabled(index == 0)) {
                if (ImGuiComponents.IconButton("##moveUp", FontAwesomeIcon.ArrowUp)) {
                    (pathAction.Points[index - 1], pathAction.Points[index]) = (pathAction.Points[index], pathAction.Points[index - 1]);                    
                }
            }
            ArrpGuiLayout.Tooltip(loc["ScenarioEditor_ActorData_Actions_APath_MoveUpHint"]);

            ImGui.SameLine(0, ArrpGuiSpacing.ButtonSpacing);
            using (ImRaii.Disabled(index == pathAction.Points.Count - 1)) {
                if (ImGuiComponents.IconButton("##moveDown", FontAwesomeIcon.ArrowDown)) {
                    (pathAction.Points[index + 1], pathAction.Points[index]) = (pathAction.Points[index], pathAction.Points[index + 1]);                    
                }
            }
            ArrpGuiLayout.Tooltip(loc["ScenarioEditor_ActorData_Actions_APath_MoveDownHint"]);

            ImGui.SameLine(0, ArrpGuiSpacing.ButtonSpacing);
            if (ImGuiComponents.IconButton("##remove", FontAwesomeIcon.Trash)) {
                pathAction.Points.RemoveAt(index);                
                SelectedPathMovementPoint = pathAction.Points.Count == 0 ? null : pathAction.Points[Math.Min(index, pathAction.Points.Count - 1)];                
            }
            ArrpGuiLayout.Tooltip(loc["ScenarioEditor_ActorData_Actions_APath_RemovePointHint"]);
        });

        ArrpGuiForm.Draw("##arrpPathPointForm", form => {
            DrawPositionRow(form, point.Point, position => point.Point = position,
                loc["ScenarioEditor_ActorData_Actions_APath_Position_CurrentLocationHint"]);
            DrawSpeedRow(form, point);
        });
    }

    private void DrawPositionRow(ArrpGuiForm form, CsMaths.Vector3 position, Action<CsMaths.Vector3> onChange, string currentLocationHint)
        => form.Row(loc["ScenarioEditor_ActorData_Actions_AMove_Position"], () => {
            var value = position.AsVector();
            if (ImGui.InputFloat3("##value", ref value)) {
                onChange(value.AsCsVector());
            }

            ImGui.SameLine();
            if (ImGuiComponents.IconButton(FontAwesomeIcon.LocationCrosshairs) && objectTable.LocalPlayer != null) {
                onChange(objectTable.LocalPlayer.Position.AsCsVector());
            }
            ArrpGuiLayout.Tooltip(currentLocationHint);
        });

    private void DrawSpeedRow(ArrpGuiForm form, INpcSpeedSelection selection)
        => form.Row(loc["ScenarioEditor_ActorData_Actions_AMove_Speed"], ()
            => DrawSpeedSelector("##value", selection, 180));

    private unsafe void DrawTimelineAction(ScenarioNpcTimelineAction timelineAction) {
        ArrpGuiLayout.SectionHeader(loc["ScenarioEditor_ActorData_Actions_ATimeline_SlotsTitle"]);

        if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Plus, loc["ScenarioEditor_ActorData_Actions_ATimeline_AddTimelineSlot"])) {
            timelineAction.ActionSlots.Add(new TimelineActionSlot());
        }
        ArrpGuiLayout.Tooltip(loc["ScenarioEditor_ActorData_Actions_ATimeline_AddTimelineSlot_Desc"]);

        ImGui.SameLine(0, ArrpGuiSpacing.InlineIconSpacing);
        if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.WandMagicSparkles, loc["ScenarioEditor_ActorData_Actions_ATimeline_AddTimelineSlot_CopyNpc"])) {
            npcPicker.OpenPopup();
        }
        ArrpGuiLayout.Tooltip(loc["ScenarioEditor_ActorData_Actions_ATimeline_AddTimelineSlot_CopyNpc_Desc"]);

        if (npcPicker.Popup(out var npc)) {
            timelineAction.ActionSlots.Clear();
            var timelineSequencer = npc->Timeline.TimelineSequencer;
            foreach (var timelineId in timelineSequencer.TimelineIds) {
                if (timelineId == 0)
                    continue;

                timelineAction.ActionSlots.Add(new TimelineActionSlot { TimelineId = timelineId });
            }
        }

        ImGui.Dummy(ArrpGuiSpacing.VerticalHeaderSpacing);

        using var child = ImRaii.Child("##arrpTimelineActionSlots", new Vector2(0, 0), false);
        if (!child.Success)
            return;

        using var padding = ImRaii.PushStyle(ImGuiStyleVar.CellPadding, ArrpGuiSpacing.TableCellPadding);
        using var table = ImRaii.Table("##arrpTimelineActionSlotTable", 3, ImGuiTableFlags.NoSavedSettings | ImGuiTableFlags.NoBordersInBody | ImGuiTableFlags.RowBg);
        if (!table.Success)
            return;

        ImGui.TableSetupColumn("##arrpTimelineSlotControls", ImGuiTableColumnFlags.WidthFixed);
        ImGui.TableSetupColumn(loc["ScenarioEditor_ActorData_Actions_ATimeline_Table_TimelineId"], ImGuiTableColumnFlags.WidthFixed, 140);
        ImGui.TableSetupColumn(loc["ScenarioEditor_ActorData_Actions_ATimeline_Table_TimelineKey"], ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupScrollFreeze(0, 1);
        ImGui.TableHeadersRow();

        TimelineActionSlot? slotToRemove = null;

        for (var i = 0; i < timelineAction.ActionSlots.Count; i++) {
            var slot = timelineAction.ActionSlots[i];

            using var id = ImRaii.PushId($"arrpTimelineSlot{i}");

            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            if (ImGuiComponents.IconButton("##remove", FontAwesomeIcon.Trash)) {
                slotToRemove = slot;
            }
            ArrpGuiLayout.Tooltip(loc["ScenarioEditor_ActorData_Actions_ATimeline_Hint_RemoveSlot"]);

            ImGui.TableNextColumn();
            using (ImRaii.ItemWidth(-(ImGui.GetFrameHeight() + ImGui.GetTextLineHeight()))) {
                var slotTimeline = slot.TimelineId;
                if (ImGui.InputUShort("##timelineId", ref slotTimeline, 0, 0)) {
                    slot.TimelineId = slotTimeline;
                }
            }

            ImGui.SameLine();
            timelinePicker.SetPopupName($"TimelinePicker{i}");
            if (ImGuiComponents.IconButton("##pick", FontAwesomeIcon.List)) {
                timelinePicker.OpenPopup();
            }

            if (timelinePicker.Popup(out var pickedTimeline)) {
                slot.TimelineId = (ushort)pickedTimeline.Value.RowId;
            }

            ImGui.TableNextColumn();
            ImGui.AlignTextToFramePadding();
            ImGui.TextDisabled($"{dataCache.GetActionTimeline(slot.TimelineId).Key}");
        }

        if (slotToRemove != null)
            timelineAction.ActionSlots.Remove(slotToRemove);
    }
    
    private void DrawSpeedSelector(string id, INpcSpeedSelection selection, float comboWidth) {
        using var pushedId = ImRaii.PushId(id);

        using (ImRaii.ItemWidth(comboWidth)) {
            DrawSpeedCombo("##speed", selection.Speed, speed => {                
                if (speed == NpcSpeed.Custom && selection.CustomSpeed <= 0f)
                    selection.CustomSpeed = MovementMotion.DefaultCustomSpeed;

                selection.Speed = speed;
            });
        }

        if (selection.Speed != NpcSpeed.Custom)
            return;

        ImGui.SameLine(0, ArrpGuiSpacing.ButtonSpacing);
        using (ImRaii.ItemWidth(-1)) {
            var customSpeed = selection.CustomSpeed;
            if (ImGui.DragFloat("##customSpeed", ref customSpeed, 0.05f, MovementMotion.MinCustomSpeed, MovementMotion.MaxCustomSpeed,
                $"%.2f {loc["ScenarioEditor_ActorData_Actions_WalkSpeed_Unit"]}"))
                selection.CustomSpeed = MovementMotion.ClampSpeed(customSpeed);
        }
        ArrpGuiLayout.Tooltip(loc["ScenarioEditor_ActorData_Actions_WalkSpeed_CustomHint"]);
    }

    private void DrawSpeedCombo(string id, NpcSpeed speed, Action<NpcSpeed> onChange) {
        using var combo = ImRaii.Combo(id, DescribeSpeed(speed));
        if (!combo.Success)
            return;

        foreach (var option in Enum.GetValues<NpcSpeed>()) {
            if (ImGui.Selectable(DescribeSpeed(option), speed == option)) {
                onChange(option);
            }
        }
    }

    private void DrawPoseStateRow(ArrpGuiForm form, PoseType poseType, string label, string hint, byte value, Action<byte> onChange) {
        var poseStateCount = dataCache.GetPoseStateCount(poseType);
        if (poseStateCount <= 1)
            return;

        form.Row(label, () => {
            ImGui.SetNextItemWidth(-1);
            var poseState = (int)value;
            if (ImGui.SliderInt("##value", ref poseState, 0, poseStateCount - 1)) {
                onChange((byte)poseState);
            }
        }, hint);
    }

}

public sealed class NpcActionUiRegistry {
    public class ActionTypeDisplayObject {
        public Action<ScenarioNpcAction> Draw { get; set; } = (_) => { };
        public Func<ScenarioNpcAction, string> NameResolver { get; set; } = (_) => string.Empty;
        public Func<ScenarioNpcAction, string> HelpResolver { get; set; } = (_) => string.Empty;
        public Func<ScenarioNpcAction, string> SummaryResolver { get; set; } = (_) => string.Empty;
    }

    private readonly Dictionary<Type, ActionTypeDisplayObject> _handlers = [];

    public void Register<T>(Func<T, string>? shortName = null, Func<T, string>? help = null, Func<T, string>? summary = null, Action<T>? draw = null) where T : ScenarioNpcAction {
        _handlers[typeof(T)] = new ActionTypeDisplayObject {
            HelpResolver = a => help != null ? help((T)a) : string.Empty,
            NameResolver = a => shortName != null ? shortName((T)a) : string.Empty,
            SummaryResolver = a => summary != null ? summary((T)a) : string.Empty,
            Draw = a => draw?.Invoke((T)a)
        };
    }

    public string GetShortName(ScenarioNpcAction action)
        => _handlers[action.GetType()].NameResolver(action);

    public string GetHelp(ScenarioNpcAction action)
        => _handlers[action.GetType()].HelpResolver(action);
    
    public string GetSummary(ScenarioNpcAction action)
        => _handlers[action.GetType()].SummaryResolver(action);

    public void Draw(ScenarioNpcAction action)
        => _handlers[action.GetType()].Draw(action);
}
