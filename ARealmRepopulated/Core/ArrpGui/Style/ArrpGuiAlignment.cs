using Dalamud.Bindings.ImGui;

namespace ARealmRepopulated.Core.ArrpGui.Style;

public static class ArrpGuiAlignment {

    public static void Center(bool verticalAlign = true, float frameHeight = -1) {

        if (frameHeight == -1) {
            frameHeight = ImGui.GetFrameHeight();
        }
        if (verticalAlign) {
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + ((frameHeight - ImGui.GetTextLineHeightWithSpacing()) * 0.5f));
        }

    }

}
