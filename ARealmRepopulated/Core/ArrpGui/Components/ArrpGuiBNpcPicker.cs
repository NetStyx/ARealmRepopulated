using ARealmRepopulated.Core.l10n;
using ARealmRepopulated.Infrastructure;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility.Raii;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;

namespace ARealmRepopulated.Core.ArrpGui.Components;

// Preperation for the time i figure out how bnpc names and bnpc bases are linked together, so i can make a proper bnpc picker
public class ArrpGuiBNpcPicker(ArrpDataCache dataCache, ArrpTranslation loc) {
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

        {
            using var pickerTable = ImRaii.Table("##ArrpBNpcPickerListTable", 3, ImGuiTableFlags.NoSavedSettings | ImGuiTableFlags.ScrollY | ImGuiTableFlags.PadOuterX, size ?? new Vector2(500, 300));
            if (!pickerTable.Success)
                return false;

            ImGui.TableSetupColumn("##bnpcPickerNameIdCol", ImGuiTableColumnFlags.WidthFixed, 80);
            ImGui.TableSetupColumn("##bnpcPickerNameCol", ImGuiTableColumnFlags.WidthStretch, -1);
            ImGui.TableSetupColumn("##bnpcPickerBaseIdCol", ImGuiTableColumnFlags.WidthFixed, 80);

            ImGui.TableSetupScrollFreeze(0, 1);
            ImGui.TableHeadersRow();

            ArrpGuiHelper.DrawCenteredHeaderCell(0, () => ImGui.Text("Name ID"));
            ArrpGuiHelper.DrawCenteredHeaderCell(1, () => ImGui.Text("Name"));
            ArrpGuiHelper.DrawCenteredHeaderCell(2, () => ImGui.Text("Base ID"));

            foreach (var baseEntry in dataCache.GetBNpcBases((b) => !string.IsNullOrWhiteSpace(b) && (string.IsNullOrEmpty(_pickerSearch) || b.Contains(_pickerSearch, StringComparison.OrdinalIgnoreCase)))) {

                var baseId = baseEntry.Base.RowId;
                var nameId = baseEntry.Name.RowId;

                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.Text(baseEntry.Name.RowId.ToString());
                ImGui.TableNextColumn();
                if (ImGui.Selectable($"{baseEntry.Name.Singular}###ArrpTimelinePickerSelection{nameId}-{baseId}", false, ImGuiSelectableFlags.SpanAllColumns)) {
                    ImGui.CloseCurrentPopup();
                    preset = baseEntry;
                    return true;
                }
                ImGui.TableNextColumn();
                ImGui.Text(baseEntry.Base.RowId.ToString());

                ImGui.TableNextColumn();
            }
        }

        ImGui.TextDisabled(loc["ArrpGuiPicker_BNpcPreset_SourceDisclaimer"]);
        ImGui.SameLine();
        ImGuiComponents.HelpMarker(loc["ArrpGuiPicker_BNpcPreset_SourceDisclaimer_Source"]);

        return false;
    }

}
