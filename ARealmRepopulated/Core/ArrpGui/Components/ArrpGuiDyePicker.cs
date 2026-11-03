using ARealmRepopulated.Core.ArrpGui.Style;
using ARealmRepopulated.Core.l10n;
using ARealmRepopulated.Infrastructure;
using Dalamud.Bindings.ImGui;
using Lumina.Excel.Sheets;
using System.Numerics;

namespace ARealmRepopulated.Core.ArrpGui.Components;

public class ArrpGuiDyePicker(ArrpItemData itemData, ArrpGuiGameIcon gameIcon, ArrpTranslation loc) {

    private const string PopupName = "##ArrpDyePicker";

    public void OpenPopup() {
        ImGui.OpenPopup(PopupName);
    }

    public bool Popup(out byte stain) {

        stain = 0;

        using var popup = ArrpGuiLayout.Popup(PopupName);
        if (!popup.Success)
            return false;

        var swatchSize = new Vector2(ImGui.GetFrameHeight());
        var changed = false;

        if (ImGui.Button("##stainNone", swatchSize)) {
            changed = true;
            ImGui.CloseCurrentPopup();
        }
        gameIcon.DrawNoneMark(swatchSize);
        if (ImGui.IsItemHovered()) {
            ImGui.SetTooltip(loc["ArrpGuiPicker_Dye_None"]);
        }
        ImGui.SameLine();

        var stains = itemData.GetStains();
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
            // The no dye swatch takes the first cell of the grid.
            if ((i + 2) % 10 != 0) {
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
