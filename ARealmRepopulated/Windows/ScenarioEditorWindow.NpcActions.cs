using ARealmRepopulated.Data.Scenarios;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using CsMaths = FFXIVClientStructs.FFXIV.Common.Math;

namespace ARealmRepopulated.Windows;

public partial class ScenarioEditorWindow
{

    private void DrawActionSelection()
    {
        if (ImGui.Selectable("Waiting"))
        {
            AddAction(new ScenarioNpcWaitingAction());
        }

        if (ImGui.Selectable("Emote"))
        {
            AddAction(new ScenarioNpcEmoteAction());
        }

        if (ImGui.Selectable("Spawn"))
        {
            AddAction(new ScenarioNpcSpawnAction());
        }

        if (ImGui.Selectable("Despawn"))
        {
            AddAction(new ScenarioNpcDespawnAction());
        }

        if (ImGui.Selectable("Move"))
        {
            AddAction(new ScenarioNpcMovementAction { TargetPosition = _state.LocalPlayer?.Position ?? new CsMaths.Vector3() });
        }

        if (ImGui.Selectable("Rotation"))
        {
            AddAction(new ScenarioNpcRotationAction { TargetRotation = _state.LocalPlayer?.Rotation ?? 0f });
        }

        if (ImGui.Selectable("Sync"))
        {
            AddAction(new ScenarioNpcSyncAction());
        }
    }

    private void DrawCurrentAction()
    {
        if (SelectedScenarioNpcAction == null)
        {
            return;
        }

        using (ImRaii.Table("##scenarioNpcEditorActionTable", 2, ImGuiTableFlags.NoBordersInBody | ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.NoSavedSettings))
        {

            ImGui.TableSetupColumn("##scenarioNpcEditorActionTableCap", ImGuiTableColumnFlags.WidthFixed);
            ImGui.TableSetupColumn("##scenarioNpcEditorActionTableValue", ImGuiTableColumnFlags.WidthStretch);


            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.Text("Npc Talk");

            ImGui.TableNextColumn();
            var talk = SelectedScenarioNpcAction.NpcTalk;
            if (ImGui.InputText("##scenarioNpcEmoteActionTalk", ref talk))
            {
                SelectedScenarioNpcAction.NpcTalk = talk;
            }

            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.Text("Duration");

            ImGui.TableNextColumn();
            var duration = SelectedScenarioNpcAction.Duration;
            if (ImGui.InputFloat("s.##scenarioNpcEmoteActionDuration", ref duration, step: 0.1f))
            {
                SelectedScenarioNpcAction.Duration = duration;
            }


            switch (SelectedScenarioNpcAction)
            {
                case ScenarioNpcEmoteAction emoteAction:
                    DrawEmoteAction(emoteAction);
                    break;

                case ScenarioNpcWaitingAction waitingAction:
                    DrawWaitingAction(waitingAction);
                    break;

                case ScenarioNpcSpawnAction spawnAction:
                    DrawSpawnAction(spawnAction);
                    break;

                case ScenarioNpcDespawnAction despawnAction:
                    DrawDespawnAction(despawnAction);
                    break;

                case ScenarioNpcMovementAction moveAction:
                    DrawMovementAction(moveAction);
                    break;

                case ScenarioNpcRotationAction rotationAction:
                    DrawRotationAction(rotationAction);
                    break;

                case ScenarioNpcSyncAction syncAction:
                    DrawSyncAction(syncAction);
                    break;
            }
        }
    }

    private static string GetReadableActionName(ScenarioNpcAction action) => action switch
    {
        ScenarioNpcEmoteAction => "Emote",
        ScenarioNpcWaitingAction => "Wait",
        ScenarioNpcSpawnAction => "Spawn",
        ScenarioNpcDespawnAction => "Despawn",
        ScenarioNpcMovementAction => "Move",
        ScenarioNpcRotationAction => "Rotate",
        ScenarioNpcSyncAction => "Sync",
        _ => "Unknown",
    };

    private void DrawSyncAction(ScenarioNpcSyncAction syncAction)
    {

    }

    private void DrawRotationAction(ScenarioNpcRotationAction rotationAction)
    {
        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        ImGui.Text("Rotation");
        ImGui.TableNextColumn();
        var rotation = rotationAction.TargetRotation;
        if (ImGui.InputFloat("##scenarioNpcRotateActionRotation", ref rotation))
        {
            rotationAction.TargetRotation = rotation;
        }
        ImGui.SameLine();
        if (ImGui.SmallButton("Set Current Rotation##scenarioNpcRotateActionCurrentRotation"))
        {
            rotationAction.TargetRotation = _state.LocalPlayer?.Rotation ?? 0f;
        }
    }

    private void DrawMovementAction(ScenarioNpcMovementAction moveAction)
    {
        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        ImGui.Text("Position");

        ImGui.TableNextColumn();
        var position = new Vector3(moveAction.TargetPosition.X, moveAction.TargetPosition.Y, moveAction.TargetPosition.Z);
        if (ImGui.InputFloat3("##scenarioNpcMoveActionPosition", ref position))
        {
            moveAction.TargetPosition = new CsMaths.Vector3(position.X, position.Y, position.Z);
        }
        ImGui.SameLine();
        if (ImGui.SmallButton("Set Current Position##scenarioNpcMoveActionCurrentPosition") && _state.LocalPlayer != null)
        {
            moveAction.TargetPosition = new CsMaths.Vector3(_state.LocalPlayer.Position.X, _state.LocalPlayer.Position.Y, _state.LocalPlayer.Position.Z);
        }

        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        ImGui.TableNextColumn();
        
        var isRunning = moveAction.IsRunning;
        if (ImGui.Checkbox("Running? ##scenarioNpcMoveActionIsRunning", ref isRunning))
        {
            moveAction.IsRunning = isRunning;
        }
    }

    private void DrawDespawnAction(ScenarioNpcDespawnAction despawnAction)
    {

    }

    private void DrawSpawnAction(ScenarioNpcSpawnAction spawnAction)
    {

    }

    private void DrawWaitingAction(ScenarioNpcWaitingAction waitingAction)
    {

    }

    private void DrawEmoteAction(ScenarioNpcEmoteAction emoteAction)
    {
        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        ImGui.Text("Emote ID");

        ImGui.TableNextColumn();
        var emoteid = emoteAction.Emote;
        if (ImGui.InputUShort("##scenarioNpcEmoteActionEmote", ref emoteid))
        {
            emoteAction.Emote = emoteid;
        }

        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        ImGui.Text("Loop");

        ImGui.TableNextColumn();
        var loop = emoteAction.Loop;
        if (ImGui.Checkbox("##scenarioNpcEmoteActionLoop", ref loop))
        {
            emoteAction.Loop = loop;
        }        
    }
}
