using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Colors;
using System.Numerics;

namespace ARealmRepopulated.Core.ArrpGui.Style;

public static class ArrpGuiColors {

    public static Vector4 ArrpGreen => new Vector4(0, 150, 0, 255).FromRgb();
    public static Vector4 ArrpRed => new Vector4(192, 0, 0, 255).FromRgb();
    public static Vector4 ArrpYellow => ImGuiColors.DalamudYellow;
    
    public static Vector4 TextColor => ImGui.GetStyle().Colors[(int)ImGuiCol.Text];    
    public static Vector4 NoteColor => ImGui.GetStyle().Colors[(int)ImGuiCol.TextDisabled];

    /// <summary>Scrollbar grab dimmed the way ImGui dims disabled controls, for a scrollbar with nothing to scroll.</summary>
    public static Vector4 ScrollbarGrabDisabledColor {
        get {
            var style = ImGui.GetStyle();
            var grabColor = style.Colors[(int)ImGuiCol.ScrollbarGrab];
            return grabColor with { W = grabColor.W * style.DisabledAlpha };
        }
    }

}

public static class ArrpGuiColorConverter {
    public static Vector4 FromRgb(this Vector4 rgba)
        => new(rgba.X / 255f, rgba.Y / 255f, rgba.Z / 255f, rgba.W / 255f);
}
