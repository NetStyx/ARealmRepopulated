using Dalamud.Bindings.ImGui;
using System.Numerics;

namespace ARealmRepopulated.Core.ArrpGui.Components;

public static class ArrpGuiCheckbox {    
    public static bool LabelLeft(string id, string label, ref bool value) {
        var style = ImGui.GetStyle();
        var boxSize = ImGui.GetFrameHeight();
        var labelSize = ImGui.CalcTextSize(label);
        var innerSpacing = style.ItemInnerSpacing.X;
        var origin = ImGui.GetCursorScreenPos();
        
        var pressed = ImGui.InvisibleButton(id, new Vector2(labelSize.X + innerSpacing + boxSize, boxSize));
        var hovered = ImGui.IsItemHovered();
        var held = ImGui.IsItemActive();

        if (pressed) {
            value = !value;
        }

        var drawList = ImGui.GetWindowDrawList();
        drawList.AddText(new Vector2(origin.X, origin.Y + ((boxSize - labelSize.Y) * 0.5f)), ImGui.GetColorU32(ImGuiCol.Text), label);

        var boxMin = new Vector2(origin.X + labelSize.X + innerSpacing, origin.Y);
        var frameColor = held && hovered ? ImGuiCol.FrameBgActive
            : hovered ? ImGuiCol.FrameBgHovered
            : ImGuiCol.FrameBg;

        drawList.AddRectFilled(boxMin, boxMin + new Vector2(boxSize, boxSize), ImGui.GetColorU32(frameColor), style.FrameRounding);

        if (value) {
            DrawCheckMark(drawList, boxMin, boxSize);
        }

        return pressed;
    }
    
    private static void DrawCheckMark(ImDrawListPtr drawList, Vector2 boxMin, float boxSize) {
        var padding = Math.Max(1f, MathF.Floor(boxSize / 6f));
        var size = boxSize - (padding * 2f);
        var origin = boxMin + new Vector2(padding, padding);

        var thickness = Math.Max(size / 5f, 1f);
        size -= thickness * 0.5f;
        origin += new Vector2(thickness * 0.25f, thickness * 0.25f);

        var third = size / 3f;
        var bendX = origin.X + third;
        var bendY = origin.Y + size - (third * 0.5f);

        drawList.PathLineTo(new Vector2(bendX - third, bendY - third));
        drawList.PathLineTo(new Vector2(bendX, bendY));
        drawList.PathLineTo(new Vector2(bendX + (third * 2f), bendY - (third * 2f)));
        drawList.PathStroke(ImGui.GetColorU32(ImGuiCol.CheckMark), ImDrawFlags.None, thickness);
    }

}

// i hate imgui so much.