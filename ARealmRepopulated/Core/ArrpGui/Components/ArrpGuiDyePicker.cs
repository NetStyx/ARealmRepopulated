using ARealmRepopulated.Core.ArrpGui.Style;
using ARealmRepopulated.Core.l10n;
using ARealmRepopulated.Infrastructure;
using Dalamud.Bindings.ImGui;
using Lumina.Excel.Sheets;
using System.Numerics;

namespace ARealmRepopulated.Core.ArrpGui.Components;

public class ArrpGuiDyePicker(ArrpItemData itemData, ArrpTranslation loc) {

    private const string PopupName = "##ArrpDyePicker";

    public void OpenPopup() {
        ImGui.OpenPopup(PopupName);
    }

    public bool Popup(out byte stain) {

        stain = 0;

        using var popup = ArrpGuiLayout.Popup(PopupName);
        if (!popup.Success)
            return false;

        if (ImGui.Selectable(loc["ArrpGuiPicker_Dye_None"])) {
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

    public static Vector4 GetColor(Stain? stain) {
        if (stain is not { } row || row.RowId == 0)
            return Vector4.Zero;

        return row.Color.FromRgb();
    }
}
