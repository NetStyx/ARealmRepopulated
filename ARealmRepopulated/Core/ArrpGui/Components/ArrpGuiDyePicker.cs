using ARealmRepopulated.Core.ArrpGui.Style;
using ARealmRepopulated.Core.l10n;
using ARealmRepopulated.Infrastructure;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Lumina.Excel.Sheets;
using System.Numerics;

namespace ARealmRepopulated.Core.ArrpGui.Components;

public class ArrpGuiDyePicker(ArrpItemData itemData, ArrpTranslation loc) {

    private const string PopupName = "##ArrpDyePicker";

    public bool Draw(string id, ref byte stain) {
        using var scope = ImRaii.PushId(id);

        var stainRow = itemData.GetStain(stain);
        if (ImGui.ColorButton("##dye", GetColor(stainRow), ImGuiColorEditFlags.NoTooltip)) {
            ImGui.OpenPopup(PopupName);
        }
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled)) {
            ImGui.SetTooltip(stain == 0 || stainRow == null ? loc["ArrpGuiPicker_Dye_None"] : stainRow.Value.Name.ToString());
        }

        using var popup = ArrpGuiLayout.Popup(PopupName);
        if (!popup.Success)
            return false;

        if (ImGui.Selectable(loc["ArrpGuiPicker_Dye_None"])) {
            stain = 0;
            ImGui.CloseCurrentPopup();
            return true;
        }

        var stains = itemData.GetStains();
        var changed = false;
        for (var i = 0; i < stains.Count; i++) {
            var entry = stains[i];
            if (ImGui.ColorButton($"##stain{entry.RowId}", GetColor(entry), ImGuiColorEditFlags.NoTooltip)) {
                stain = (byte)entry.RowId;
                changed = true;
                ImGui.CloseCurrentPopup();
            }
            if (ImGui.IsItemHovered()) {
                ImGui.SetTooltip(entry.Name.ToString());
            }
            if ((i + 1) % 10 != 0) {
                ImGui.SameLine();
            }
        }

        return changed;
    }

    private static Vector4 GetColor(Stain? stain) {
        if (stain is not { } row || row.RowId == 0)
            return Vector4.Zero;

        return row.Color.FromRgb();
    }
}
