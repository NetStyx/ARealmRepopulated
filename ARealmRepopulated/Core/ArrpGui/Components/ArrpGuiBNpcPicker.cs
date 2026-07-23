using ARealmRepopulated.Core.l10n;
using ARealmRepopulated.Infrastructure;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility.Raii;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;

namespace ARealmRepopulated.Core.ArrpGui.Components;

public class ArrpGuiBNpcPicker(ArrpDataCache dataCache, ArrpTranslation loc) {
    private string _pickerName = "TimelinePicker";
    private string _pickerSearch = string.Empty;
    private int _pickerSelectedType = 1;

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

        ImGui.SetNextItemWidth(100);
        ImGui.Combo("##ArrpBNpcPickerType", ref _pickerSelectedType, ["All", "Human", "Demihuman", "Monster", "Statics", "Parts"], 6);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(-1);
        ImGui.InputTextWithHint("##ArrpBNpcPickerListSearch", "Search...", ref _pickerSearch, 64);

        {
            using var pickerTable = ImRaii.Table("##ArrpBNpcPickerListTable", 3, ImGuiTableFlags.NoSavedSettings | ImGuiTableFlags.ScrollY | ImGuiTableFlags.PadOuterX, size ?? new Vector2(550, 300));
            if (!pickerTable.Success)
                return false;

            ImGui.TableSetupColumn("##bnpcPickerNameCol", ImGuiTableColumnFlags.WidthStretch, -1);
            ImGui.TableSetupColumn("##bnpcPickerBaseIdCol", ImGuiTableColumnFlags.WidthFixed, 80);
            ImGui.TableSetupColumn("##bnpcPickerModelIdCol", ImGuiTableColumnFlags.WidthFixed, 80);

            ImGui.TableSetupScrollFreeze(0, 1);
            ImGui.TableHeadersRow();

            ArrpGuiHelper.DrawCenteredHeaderCell(0, () => ImGui.Text("Name"));
            ArrpGuiHelper.DrawCenteredHeaderCell(1, () => ImGui.Text("Base ID"));
            ArrpGuiHelper.DrawCenteredHeaderCell(2, () => ImGui.Text("Model ID"));

            foreach (var baseEntry in dataCache.GetBNpcBases(_pickerSelectedType, (b) => !string.IsNullOrWhiteSpace(b) && (string.IsNullOrEmpty(_pickerSearch) || b.Contains(_pickerSearch, StringComparison.OrdinalIgnoreCase)))) {

                var baseId = baseEntry.Base.RowId;
                var nameId = baseEntry.Name.RowId;
                var modelId = baseEntry.Base.ModelChara.IsValid ? baseEntry.Base.ModelChara.RowId : 0;

                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                if (ImGui.Selectable($"[{nameId}] {baseEntry.Name.Singular}###ArrpTimelinePickerSelection{nameId}-{baseId}", false, ImGuiSelectableFlags.SpanAllColumns)) {
                    ImGui.CloseCurrentPopup();
                    preset = baseEntry;
                    return true;
                }
                ImGui.TableNextColumn();
                ImGui.Text(baseId.ToString());
                ImGui.TableNextColumn();
                ImGui.Text(modelId.ToString());

                ImGui.TableNextColumn();
            }
        }

        ImGui.TextDisabled(loc["ArrpGuiPicker_BNpcPreset_SourceDisclaimer"]);
        ImGui.SameLine();
        ImGuiComponents.HelpMarker(loc["ArrpGuiPicker_BNpcPreset_SourceDisclaimer_Source"]);

        return false;
    }

}
