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

    public static IServiceCollection AddGuiPickers(this IServiceCollection services) {
        services
            .AddSingleton<ArrpGuiBNpcPicker>()
            .AddSingleton<ArrpGuiEmotePicker>()
            .AddSingleton<ArrpGuiNpcPicker>()
            .AddSingleton<ArrpGuiTimelinePicker>();
        return services;
    }

}
