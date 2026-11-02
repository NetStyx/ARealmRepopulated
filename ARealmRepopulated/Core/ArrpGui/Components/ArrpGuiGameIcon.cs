using ARealmRepopulated.Data.Appearance;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin.Services;
using System.Numerics;

namespace ARealmRepopulated.Core.ArrpGui.Components;

/// <summary>
/// Ingame icons as buttons
/// </summary>
public class ArrpGuiGameIcon(ITextureProvider textureProvider) {

    public void Draw(uint icon, Vector2 size) {
        if (icon != 0) {
            ImGui.Image(GetIconTexture(icon), size);
            return;
        }

        ImGui.Dummy(size);
        ImGui.GetWindowDrawList().AddRect(ImGui.GetItemRectMin(), ImGui.GetItemRectMax(), ImGui.GetColorU32(ImGuiCol.Border));
    }

    public bool DrawButton(string id, uint icon, string label, Vector2 size) {
        using var scope = ImRaii.PushId(id);

        if (icon != 0) {
            return ImGui.ImageButton(GetIconTexture(icon), size);
        }

        return ImGui.Button($"{label}##emptySlot", size + (ImGui.GetStyle().FramePadding * 2));
    }

    /// <summary>
    /// An option as seen in the character creator. The value 0 without an icon is its "none" choice, like "no face paint".
    /// </summary>
    public bool DrawOption(string id, CharacterEditorOption option, Vector2 size) {
        if (option is not { Value: 0, Icon: 0 })
            return DrawButton(id, option.Icon, option.Value.ToString(), size);

        var clicked = DrawButton(id, 0, string.Empty, size);
        DrawNoneMark(size);
        return clicked;
    }

    /// <summary>
    /// Puts an empty ring on top of the widget drawn right before this call, marking it as the "none" choice.    
    /// </summary>
    public void DrawNoneMark(Vector2 size) {
        var center = (ImGui.GetItemRectMin() + ImGui.GetItemRectMax()) / 2;
        var extent = size * 0.3f;
        ImGui.GetWindowDrawList().AddImage(GetIconTexture(240026), center - extent, center + extent);
    }

    public bool DrawToggle(string id, uint icon, string label, Vector2 size, bool isOn) {
        var clicked = DrawButton(id, icon, label, size);
        if (isOn) {
            var min = ImGui.GetItemRectMin();
            var max = ImGui.GetItemRectMax();
            var drawList = ImGui.GetWindowDrawList();
            for (var glow = 3; glow > 0; glow--) {
                drawList.AddRect(min - new Vector2(glow), max + new Vector2(glow), ImGui.GetColorU32(ImGuiCol.CheckMark, 0.5f / glow), 0f, ImDrawFlags.None, 2f);
            }
            drawList.AddRect(min, max, ImGui.GetColorU32(ImGuiCol.CheckMark), 0f, ImDrawFlags.None, 2f);
        }

        return clicked;
    }

    /// <summary>
    /// Returns the value of the option that was clicked.
    /// </summary>
    public byte? DrawGrid(CharacterEditorOption[] options, int columns, Vector2 size) {
        byte? picked = null;
        for (var i = 0; i < options.Length; i++) {
            var option = options[i];
            if (DrawOption($"##option{option.Value}", option, size)) {
                picked = option.Value;
            }
            if ((i + 1) % columns != 0 && i + 1 < options.Length) {
                ImGui.SameLine();
            }
        }

        return picked;
    }

    private ImTextureID GetIconTexture(uint icon)
        => textureProvider.GetFromGameIcon(new GameIconLookup(icon)).GetWrapOrEmpty().Handle;
}
