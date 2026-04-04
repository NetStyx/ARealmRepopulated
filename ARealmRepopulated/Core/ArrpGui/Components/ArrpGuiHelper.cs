using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using System.Numerics;

namespace ARealmRepopulated.Core.ArrpGui.Components;

public static class ArrpGuiHelper {

    public static void InlineIcon(FontAwesomeIcon icon, Vector4? uiColor = null, float? spacing = null, string? tooltip = null) {

        uiColor ??= ImGuiColors.DalamudWhite;
        spacing ??= 0f;

        ImGui.PushFont(UiBuilder.IconFont);
        ImGui.TextColored(uiColor.Value, icon.ToIconString());
        ImGui.PopFont();

        if (tooltip != null && ImGui.IsItemHovered())
            ImGui.SetTooltip(tooltip);

        ImGui.SameLine(0, spacing.Value);
    }
}
