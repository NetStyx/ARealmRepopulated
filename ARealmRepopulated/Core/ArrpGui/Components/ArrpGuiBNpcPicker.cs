using ARealmRepopulated.Infrastructure;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;

namespace ARealmRepopulated.Core.ArrpGui.Components;

// Preperation for the time i figure out how bnpc names and bnpc bases are linked together, so i can make a proper bnpc picker
public class ArrpGuiBNpcPicker(ArrpDataCache dataCache) {
    private string _pickerName = "TimelinePicker";
    private string _pickerSearch = string.Empty;

    public void SetPopupName(string name)
        => _pickerName = name;

    public void OpenPopup() {
        ImGui.OpenPopup($"{_pickerName}");
    }

    public bool Popup([NotNullWhen(true)] out BNpcLookup? preset, Vector2? size = null) {

        preset = null;

        using var pickerPopup = ImRaii.Popup($"{_pickerName}");
        if (!pickerPopup.Success)
            return false;

        ImGui.SetNextItemWidth(-1);
        ImGui.InputTextWithHint("##ArrpBNpcPickerListSearch", "Search...", ref _pickerSearch, 64);

        using var pickerTable = ImRaii.Table("##ArrpBNpcPickerListTable", 4, ImGuiTableFlags.NoSavedSettings | ImGuiTableFlags.ScrollY | ImGuiTableFlags.PadOuterX, size ?? new Vector2(500, 300));
        if (!pickerTable.Success)
            return false;

        ImGui.TableSetupColumn("##bnpcPickerIdCol", ImGuiTableColumnFlags.WidthFixed, 80);
        ImGui.TableSetupColumn("##bnpcPickerNameCol", ImGuiTableColumnFlags.WidthStretch, -1);
        ImGui.TableSetupColumn("##bnpcPickerSlotCol", ImGuiTableColumnFlags.WidthFixed, 40);
        ImGui.TableSetupColumn("##bnpcPickerFlagsCol", ImGuiTableColumnFlags.WidthFixed, 100);

        ImGui.TableSetupScrollFreeze(0, 1);
        ImGui.TableHeadersRow();

        ArrpGuiHelper.DrawCenteredHeaderCell(0, () => ImGui.Text("ID"));
        ArrpGuiHelper.DrawCenteredHeaderCell(1, () => ImGui.Text("Name"));
        ArrpGuiHelper.DrawCenteredHeaderCell(2, () => ImGui.Text("Slot"));
        ArrpGuiHelper.DrawCenteredHeaderCell(3, () => ImGui.Text("Flags"));

        foreach (var baseEntry in dataCache.GetBNpcBases((b) => string.IsNullOrEmpty(_pickerSearch) || b.Contains(_pickerSearch))) {
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.Text(baseEntry.BNpcBaseId.ToString());
            ImGui.TableNextColumn();
            if (ImGui.Selectable($"{baseEntry.Name.GetValueOrDefault().Singular}###ArrpTimelinePickerSelection{baseEntry.BNpcBaseId}", false, ImGuiSelectableFlags.SpanAllColumns)) {
                ImGui.CloseCurrentPopup();
                preset = baseEntry;
                return true;
            }
            ImGui.TableNextColumn();
            ImGui.Text(baseEntry.Base.HasValue ? baseEntry.Base.Value.NpcEquip.RowId.ToString() : "N/A");

            ImGui.TableNextColumn();
        }

        return false;
    }

}
