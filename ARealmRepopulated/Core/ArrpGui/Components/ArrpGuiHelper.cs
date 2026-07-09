using ARealmRepopulated.Core.ArrpGui.Style;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility.Raii;
using System.Numerics;

namespace ARealmRepopulated.Core.ArrpGui.Components;

public static class ArrpGuiHelper {

    public static void InlineIcon(FontAwesomeIcon icon, Vector4? uiColor = null, float? spacing = null, string? tooltip = null) {

        uiColor ??= ImGuiColors.DalamudWhite;
        spacing ??= 0f;

        using (ImRaii.PushFont(UiBuilder.IconFont)) {
            ImGui.TextColored(uiColor.Value, icon.ToIconString());
        }

        if (tooltip != null && ImGui.IsItemHovered())
            ImGui.SetTooltip(tooltip);

        ImGui.SameLine(0, spacing.Value);
    }

    public static void DrawCenteredHeaderCell(int column, Action draw) {
        ImGui.TableSetColumnIndex(column);
        using var id = ImRaii.PushId(column);
        ArrpGuiAlignment.Center();
        draw();
    }

    public static IServiceCollection AddGuiPickers(this IServiceCollection services) {
        services
            .AddSingleton<ArrpGuiBNpcPicker>()
            .AddSingleton<ArrpGuiEmotePicker>()
            .AddSingleton<ArrpGuiNpcPicker>()
            .AddSingleton<ArrpGuiTimelinePicker>();
        return services;
    }

}
