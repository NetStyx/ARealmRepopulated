using ARealmRepopulated.Core.ArrpGui.Style;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility.Raii;

namespace ARealmRepopulated.Core.ArrpGui.Components;

public sealed class ArrpGuiForm {
    private int _row;

    private ArrpGuiForm() { }

    public static void Draw(string id, Action<ArrpGuiForm> content) {
        using var padding = ImRaii.PushStyle(ImGuiStyleVar.CellPadding, ArrpGuiSpacing.TableCellPadding);
        using var table = ImRaii.Table(id, 2, ImGuiTableFlags.NoSavedSettings | ImGuiTableFlags.NoBordersInBody);
        if (!table.Success)
            return;

        ImGui.TableSetupColumn($"{id}Label", ImGuiTableColumnFlags.WidthFixed, ArrpGuiSpacing.FormLabelWidth);
        ImGui.TableSetupColumn($"{id}Value", ImGuiTableColumnFlags.WidthStretch);

        content(new ArrpGuiForm());
    }

    private static void BeginRow(string label, string? help = null) {
        ImGui.TableNextRow();
        ImGui.TableNextColumn();

        if (!string.IsNullOrEmpty(label)) {
            ImGui.AlignTextToFramePadding();
            ImGui.Text(label);
        }

        if (!string.IsNullOrEmpty(help)) {
            if (!string.IsNullOrEmpty(label))
                ImGui.SameLine();
            ImGuiComponents.HelpMarker(help);
        }

        ImGui.TableNextColumn();
    }

    public void Row(string label, Action draw, string? help = null) {
        BeginRow(label, help);
        using var id = ImRaii.PushId(_row++);
        draw();
    }

    public void StretchedRow(string label, Action draw, string? help = null) {
        Row(label, () => {
            ImGui.SetNextItemWidth(-1);
            draw();
        }, help);
    }

    public void CheckboxRow(string label, bool value, Action<bool> onChange, string? help = null) {
        BeginRow(string.Empty);
        using var id = ImRaii.PushId(_row++);

        var current = value;
        if (ImGui.Checkbox(label, ref current)) {
            onChange(current);
        }

        if (string.IsNullOrEmpty(help))
            return;

        ImGui.SameLine();
        ImGuiComponents.HelpMarker(help);
    }
    
}
