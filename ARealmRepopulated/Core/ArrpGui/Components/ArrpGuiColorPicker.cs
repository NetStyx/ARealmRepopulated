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

    public ArrpGuiColorPick? Draw(string tooltip, Vector4 shownColor, Vector3 exactColor, byte currentPaletteValue, CharacterEditorColorPalette[] palettes, string[]? paletteLabels, bool allowsExactColor, Action? additionalControls = null) {
        if (ImGui.ColorButton("##color", shownColor, ImGuiColorEditFlags.NoTooltip | ImGuiColorEditFlags.NoAlpha)) {
            ImGui.OpenPopup(PopupName);
        }
        if (ImGui.IsItemHovered()) {
            ImGui.SetTooltip(tooltip);
        }

        using var popup = ArrpGuiLayout.Popup(PopupName);
        if (!popup.Success)
            return null;

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
            ImGui.CloseCurrentPopup();
            return new ArrpGuiColorPick(paletteValue, null);
        }

        if (!allowsExactColor)
            return null;

        ImGui.SameLine(0f, ArrpGuiSpacing.ButtonGroupSpacing);
        using var group = ImRaii.Group();
        ArrpGuiLayout.SectionHeader(loc["ScenarioEditor_ActorData_Appearance_ExactColor_PickerHeader"]);
        if (palettes.Length > 1) {
            ImGui.Dummy(new Vector2(0, ImGui.GetFrameHeight()));
        }

        var picked = exactColor;
        ImGui.SetNextItemWidth(ExactColorWidth);
        var changed = ImGui.ColorPicker3("##exact", ref picked, ImGuiColorEditFlags.NoSidePreview | ImGuiColorEditFlags.NoSmallPreview | ImGuiColorEditFlags.NoAlpha);

        additionalControls?.Invoke();

        return changed ? new ArrpGuiColorPick(null, picked) : null;
    }

    /// <summary>
    /// A slider for <c>drawExtras</c>, as wide as the color picker above it.
    /// </summary>
    public static bool DrawExtraSlider(string label, ref float value, float max) {
        ImGui.TextUnformatted(label);
        ImGui.SetNextItemWidth(ExactColorWidth);
        return ImGui.SliderFloat($"##{label}", ref value, 0f, max, "%.2f");
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
