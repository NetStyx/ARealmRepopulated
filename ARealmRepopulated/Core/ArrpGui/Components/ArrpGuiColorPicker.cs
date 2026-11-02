using ARealmRepopulated.Core.ArrpGui.Style;
using ARealmRepopulated.Core.l10n;
using ARealmRepopulated.Data.Appearance;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using System.Numerics;

namespace ARealmRepopulated.Core.ArrpGui.Components;

public readonly record struct ArrpGuiColorPick(byte? PaletteValue, Vector3? ExactColor);

/// <summary>
/// Control to handle the games palettes and optionally a color picker.
/// </summary>
public class ArrpGuiColorPicker(ArrpTranslation loc) {

    private const string PopupName = "##ArrpColorPicker";

    private static float ExactColorWidth => ImGui.GetFrameHeight() * 10;

    public void OpenPopup() {
        ImGui.OpenPopup(PopupName);
    }

    public bool Popup(Vector3 exactColor, byte currentPaletteValue, CharacterEditorColorPalette[] palettes, string[]? paletteLabels, bool allowsExactColor, out ArrpGuiColorPick pick, Action? additionalControls = null) {

        pick = default;

        using var popup = ArrpGuiLayout.Popup(PopupName);
        if (!popup.Success)
            return false;

        byte? paletteValue = null;
        using (ImRaii.Group()) {
            ArrpGuiLayout.SectionHeader(loc["ScenarioEditor_ActorData_Appearance_ExactColor_PaletteHeader"]);

            if (palettes.Length > 1) {
                var tabs = palettes.Select((palette, i) => new ArrpGuiTab(
                    paletteLabels?.ElementAtOrDefault(i) ?? loc["ScenarioEditor_ActorData_Appearance_Palette_Numbered", i + 1],
                    () => paletteValue = DrawSwatches(palette.Colors)));

                ArrpGuiLayout.Tabs("##paletteTabs", [.. tabs], Array.FindIndex(palettes, p => p.Colors.Any(c => c.Value == currentPaletteValue)));
            } else if (palettes.Length == 1) {
                paletteValue = DrawSwatches(palettes[0].Colors);
            }
        }

        if (paletteValue.HasValue) {
            pick = new ArrpGuiColorPick(paletteValue, null);
            ImGui.CloseCurrentPopup();
            return true;
        }

        if (!allowsExactColor)
            return false;

        ImGui.SameLine(0f, ArrpGuiSpacing.ButtonGroupSpacing);
        using var group = ImRaii.Group();
        ArrpGuiLayout.SectionHeader(loc["ScenarioEditor_ActorData_Appearance_ExactColor_PickerHeader"]);
        if (palettes.Length > 1) {
            ImGui.Dummy(new Vector2(0, ImGui.GetFrameHeight()));
        }

        ImGui.SetNextItemWidth(ExactColorWidth);

        var picked = exactColor;
        var changed = ImGui.ColorPicker3("##exact", ref picked, ImGuiColorEditFlags.NoSidePreview | ImGuiColorEditFlags.NoSmallPreview | ImGuiColorEditFlags.NoAlpha);

        using (ImRaii.ItemWidth(ExactColorWidth)) {
            additionalControls?.Invoke();
        }

        if (changed) {
            pick = new ArrpGuiColorPick(null, picked);
        }

        return changed;
    }

    private static byte? DrawSwatches(CharacterEditorColor[] colors) {
        using var spacing = ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, Vector2.One);

        byte? picked = null;
        for (var i = 0; i < colors.Length; i++) {
            var entry = colors[i];
            if (ImGui.ColorButton($"##swatch{entry.Value}", entry.Swatch, ImGuiColorEditFlags.NoTooltip | ImGuiColorEditFlags.NoAlpha)) {
                picked = entry.Value;
            }
            if ((i + 1) % 8 != 0) {
                ImGui.SameLine();
            }
        }

        return picked;
    }
}
