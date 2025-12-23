using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

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

    public static void CenterText(string text = "", bool verticalAlign = true, bool horizontalAlign = false, float frameHeight = -1) {

        Center(verticalAlign, frameHeight);

        if (horizontalAlign) {
            ImGuiHelpers.CenterCursorForText(text);
        }

        if (!string.IsNullOrWhiteSpace(text)) {
            ImGui.Text(text);
        }
    }

}
