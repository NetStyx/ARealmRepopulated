using ARealmRepopulated.Core.ArrpGui.Style;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility.Raii;
using System.Numerics;

namespace ARealmRepopulated.Core.ArrpGui.Components;

public static class ArrpGuiLayout {
    
    public static void HeaderRow(string id, Action content, Action? trailing = null) {
        using (var padding = ImRaii.PushStyle(ImGuiStyleVar.CellPadding, ArrpGuiSpacing.HeaderCellPadding))
        using (var table = ImRaii.Table(id, 2, ImGuiTableFlags.NoSavedSettings | ImGuiTableFlags.NoBordersInBody)) {
            if (table.Success) {
                ImGui.TableSetupColumn($"{id}Lead", ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableSetupColumn($"{id}Trail", ImGuiTableColumnFlags.WidthFixed);

                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                content();

                ImGui.TableNextColumn();
                trailing?.Invoke();
            } else {
                content();
            }
        }

        ImGui.Separator();
        ImGui.Dummy(ArrpGuiSpacing.VerticalComponentSpacing);
    }

    public static void PanelHeader(string id, Action leading, string title, string? subtitle = null, Action? trailing = null)
        => HeaderRow(id, () => DrawPanelHeaderLine(leading, title, subtitle), trailing);

    public static void PanelHeader(string id, FontAwesomeIcon icon, string title, string? subtitle = null, Action? trailing = null, Action? titleSuffix = null)
        => HeaderRow(id, () => DrawPanelHeaderLine(icon, title, subtitle, titleSuffix), trailing);

    private static void DrawPanelHeaderLine(FontAwesomeIcon icon, string title, string? subtitle, Action? titleSuffix)
        => DrawPanelHeaderLine(() => {
            using (ImRaii.PushFont(UiBuilder.IconFont))
                ImGui.Text(icon.ToIconString());
        }, title, subtitle, titleSuffix);

    private static void DrawPanelHeaderLine(Action leading, string title, string? subtitle, Action? titleSuffix = null) {
        ImGui.AlignTextToFramePadding();
        leading();

        ImGui.SameLine(0, ArrpGuiSpacing.HeadingPartSpacing);
        ImGui.Text(title);
        titleSuffix?.Invoke();

        if (!string.IsNullOrWhiteSpace(subtitle)) {
            ImGui.SameLine(0, ArrpGuiSpacing.HeadingPartSpacing);
            ImGui.TextDisabled(subtitle);
        }
    }
    
    public static void SectionHeader(string title) {
        ImGui.Dummy(ArrpGuiSpacing.VerticalSectionSpacing);
        ImGui.TextDisabled(title);
        ImGui.Separator();
        ImGui.Dummy(ArrpGuiSpacing.VerticalHeaderSpacing);
    }
    
    public static void Badge(FontAwesomeIcon icon, Vector4? color = null, string? tooltip = null) {
        using (ImRaii.PushFont(UiBuilder.IconFont))
            ImGui.TextColored(color ?? ImGuiColors.DalamudWhite, icon.ToIconString());

        if (!string.IsNullOrEmpty(tooltip) && ImGui.IsItemHovered())
            ImGui.SetTooltip(tooltip);
    }
        
    /// <summary> Wrapped text led by an icon that is centred vertically on the whole text. </summary>
    public static void IconNote(FontAwesomeIcon icon, Vector4 color, string text) {
        var top = ImGui.GetCursorPosY();
        var glyph = icon.ToIconString();

        Vector2 iconSize;
        using (ImRaii.PushFont(UiBuilder.IconFont))
            iconSize = ImGui.CalcTextSize(glyph);

        var wrapWidth = ImGui.GetContentRegionAvail().X - iconSize.X - ArrpGuiSpacing.InlineIconSpacing;
        var textHeight = ImGui.CalcTextSize(text, false, wrapWidth).Y;

        ImGui.SetCursorPosY(top + Math.Max(0, (textHeight - iconSize.Y) / 2));
        using (ImRaii.PushFont(UiBuilder.IconFont))
            ImGui.TextColored(color, glyph);

        ImGui.SameLine(0, ArrpGuiSpacing.InlineIconSpacing);
        ImGui.SetCursorPosY(top);
        ImGui.TextWrapped(text);
    }

    public static void RightAlignedBadge(FontAwesomeIcon icon, Vector4? color = null, string? tooltip = null) {
        using (ImRaii.PushFont(UiBuilder.IconFont)) {
            var glyph = icon.ToIconString();
            ImGui.SameLine(ImGui.GetContentRegionMax().X - ImGui.CalcTextSize(glyph).X);
            ImGui.TextColored(color ?? ImGuiColors.DalamudWhite, glyph);
        }

        if (!string.IsNullOrEmpty(tooltip) && ImGui.IsItemHovered())
            ImGui.SetTooltip(tooltip);
    }
    
    public static void RightAlignedText(string text) {
        if (string.IsNullOrEmpty(text))
            return;

        ImGui.SameLine(ImGui.GetContentRegionMax().X - ImGui.CalcTextSize(text).X);
        ImGui.TextDisabled(text);
    }

    public static void Tooltip(string text) {
        if (!string.IsNullOrEmpty(text) && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip(text);
    }

}
