using ARealmRepopulated.Windows;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using System.Numerics;

namespace ARealmRepopulated.Core.ArrpGui.Components;

public unsafe class ArrpGuiNpcPicker(IObjectTable objectTable, DebugOverlay? debugOverlay = null) {

    private readonly string _npcPickerName = "NpcPicker";

    private string _npcSearch = string.Empty;

    public void OpenPopup() {
        ImGui.OpenPopup(_npcPickerName);
    }

    public bool Popup(out Character* character) {

        character = null;

        using var npcPickerPopup = ImRaii.Popup(_npcPickerName);
        if (!npcPickerPopup.Success)
            return false;

        ImGui.SetNextItemWidth(-1);
        ImGui.InputTextWithHint("###ArrpNpcInspectorListSearch", "Search...", ref _npcSearch, 64);

        using var npcPickerTable = ImRaii.Table("###ArrpNpcInspectorListTable", 2, ImGuiTableFlags.NoSavedSettings | ImGuiTableFlags.BordersOuter, new Vector2(300, -1));
        if (!npcPickerTable.Success)
            return false;

        ImGui.TableSetupScrollFreeze(0, 1);
        ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 100);
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

            if (!string.IsNullOrWhiteSpace(_npcSearch) && !npcName.Contains(_npcSearch, StringComparison.OrdinalIgnoreCase)) {
                continue;
            }

            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.Text(npcObject.ObjectKind.ToString());

            ImGui.TableNextColumn();
            if (ImGui.Selectable($"{npcName}###ArrpNpcInspectorSelectionName{npcObject.Address}", false, ImGuiSelectableFlags.SpanAllColumns)) {
                debugOverlay?.ClearNpcTrace();
                character = characterRef;
                return true;
            } else if (ImGui.IsItemHovered()) {
                selectionFound = true;
                debugOverlay?.SetNpcTrace(npcObject.Position);
            }
        }

        if (!selectionFound) {
            debugOverlay?.ClearNpcTrace();
        }

        return false;
    }

}
