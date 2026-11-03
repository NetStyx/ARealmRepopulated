using ARealmRepopulated.Core.l10n;
using ARealmRepopulated.Infrastructure;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility.Raii;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;

namespace ARealmRepopulated.Core.ArrpGui.Components;

public class ArrpGuiNpcListPicker(ArrpNpcData npcData, ArrpTranslation loc) {

    private const string PopupName = "##ArrpNpcListPicker";

    private enum SourceFilter {
        All,
        Event,
        Battle
    }

    private string _search = string.Empty;
    private int _modelType = 1;
    private int _source = (int)SourceFilter.All;

    private List<NpcLookup>? _filteredNpcs;
    private string _filteredSearch = string.Empty;
    private int _filteredModelType = 0;
    private int _filteredSource = 0;

    public void OpenPopup() {
        ImGui.OpenPopup(PopupName);
    }

    public bool Popup([NotNullWhen(true)] out NpcLookup? npc) {

        npc = null;

        using var popup = ArrpGuiLayout.Popup(PopupName);
        if (!popup.Success)
            return false;

        ImGui.SetNextItemWidth(ImGui.GetFrameHeight() * 5);
        ImGui.Combo("##ArrpNpcListPickerType", ref _modelType, [loc["ArrpGuiPicker_Filter_All"], loc["ArrpGuiPicker_Npc_Type_Human"], loc["ArrpGuiPicker_Npc_Type_Demihuman"], loc["ArrpGuiPicker_Npc_Type_Monster"], loc["ArrpGuiPicker_Npc_Type_Statics"], loc["ArrpGuiPicker_Npc_Type_Parts"]], 6);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(ImGui.GetFrameHeight() * 6);
        ImGui.Combo("##ArrpNpcListPickerSource", ref _source, [loc["ArrpGuiPicker_Filter_All"], loc["ArrpGuiPicker_Npc_Source_Event"], loc["ArrpGuiPicker_Npc_Source_Battle"]], 3);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(-1);
        ImGui.InputTextWithHint("##ArrpNpcListPickerSearch", loc["ArrpGuiPicker_Search"], ref _search, 64);

        if (_filteredNpcs == null || _filteredSearch != _search || _filteredModelType != _modelType || _filteredSource != _source) {
            _filteredSearch = _search;
            _filteredModelType = _modelType;
            _filteredSource = _source;
            _filteredNpcs = [.. npcData.GetNpcs().Where(IsListed)];
        }

        {
            using var table = ImRaii.Table("##ArrpNpcListPickerTable", 4, ImGuiTableFlags.NoSavedSettings | ImGuiTableFlags.ScrollY | ImGuiTableFlags.PadOuterX, new Vector2(ImGui.GetFrameHeight() * 28, ImGui.GetFrameHeightWithSpacing() * 12));
            if (!table.Success)
                return false;

            ImGui.TableSetupColumn("##npcListPickerNameCol", ImGuiTableColumnFlags.WidthStretch, -1);
            ImGui.TableSetupColumn("##npcListPickerSourceCol", ImGuiTableColumnFlags.WidthFixed, ImGui.GetFrameHeight() * 4);
            ImGui.TableSetupColumn("##npcListPickerBaseIdCol", ImGuiTableColumnFlags.WidthFixed, ImGui.GetFrameHeight() * 4);
            ImGui.TableSetupColumn("##npcListPickerModelIdCol", ImGuiTableColumnFlags.WidthFixed, ImGui.GetFrameHeight() * 4);

            ImGui.TableSetupScrollFreeze(0, 1);
            ImGui.TableHeadersRow();

            ArrpGuiHelper.DrawCenteredHeaderCell(0, () => ImGui.Text(loc["ArrpGuiPicker_Npc_Column_Name"]));
            ArrpGuiHelper.DrawCenteredHeaderCell(1, () => ImGui.Text(loc["ArrpGuiPicker_Npc_Source"]));
            ArrpGuiHelper.DrawCenteredHeaderCell(2, () => ImGui.Text(loc["ArrpGuiPicker_Npc_Column_BaseId"]));
            ArrpGuiHelper.DrawCenteredHeaderCell(3, () => ImGui.Text(loc["ArrpGuiPicker_Npc_Column_ModelId"]));

            var clipper = new ImGuiListClipper();
            clipper.Begin(_filteredNpcs.Count);
            while (clipper.Step()) {
                for (var i = clipper.DisplayStart; i < clipper.DisplayEnd; i++) {
                    var entry = _filteredNpcs[i];

                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    if (ImGui.Selectable($"{entry.Name}###ArrpNpcListPickerEntry{i}", false, ImGuiSelectableFlags.SpanAllColumns)) {
                        ImGui.CloseCurrentPopup();
                        npc = entry;
                    }
                    ImGui.TableNextColumn();
                    ImGui.TextDisabled(loc[entry.BattleNpc != null ? "ArrpGuiPicker_Npc_Source_Battle" : "ArrpGuiPicker_Npc_Source_Event"]);
                    ImGui.TableNextColumn();
                    ImGui.Text(entry.BaseId.ToString());
                    ImGui.TableNextColumn();
                    ImGui.Text(entry.ModelId.ToString());
                }
            }
            clipper.End();
        }

        if (npc != null)
            return true;

        ImGui.TextDisabled(loc["ArrpGuiPicker_Npc_SourceDisclaimer"]);
        ImGui.SameLine();
        ImGuiComponents.HelpMarker(loc["ArrpGuiPicker_Npc_SourceDisclaimer_Source"]);

        return false;
    }

    private bool IsListed(NpcLookup npc) {
        if (_filteredModelType != 0 && _filteredModelType != npc.ModelType)
            return false;

        if ((SourceFilter)_filteredSource == SourceFilter.Event && npc.EventNpc == null)
            return false;

        if ((SourceFilter)_filteredSource == SourceFilter.Battle && npc.BattleNpc == null)
            return false;

        return string.IsNullOrEmpty(_filteredSearch) || npc.Name.Contains(_filteredSearch, StringComparison.OrdinalIgnoreCase);
    }
}
