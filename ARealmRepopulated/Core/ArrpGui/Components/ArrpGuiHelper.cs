using ARealmRepopulated.Core.ArrpGui.Style;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace ARealmRepopulated.Core.ArrpGui.Components;

public static class ArrpGuiHelper {

    public static void DrawCenteredHeaderCell(int column, Action draw) {
        ImGui.TableSetColumnIndex(column);
        using var id = ImRaii.PushId(column);
        ArrpGuiAlignment.Center();
        draw();
    }

    public static IServiceCollection AddGuiComponents(this IServiceCollection services) {
        services
            .AddSingleton<ArrpGuiColorPicker>()
            .AddSingleton<ArrpGuiDyePicker>()
            .AddSingleton<ArrpGuiEmotePicker>()
            .AddSingleton<ArrpGuiEquipmentEditor>()
            .AddSingleton<ArrpGuiGameIcon>()
            .AddSingleton<ArrpGuiItemPicker>()
            .AddSingleton<ArrpGuiModelEditor>()
            .AddSingleton<ArrpGuiNpcListPicker>()
            .AddSingleton<ArrpGuiNpcPicker>()
            .AddSingleton<ArrpGuiTimelinePicker>();
        return services;
    }

}
