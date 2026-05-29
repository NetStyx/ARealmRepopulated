using ARealmRepopulated.Infrastructure;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using Lumina.Excel.Sheets;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;

namespace ARealmRepopulated.Core.ArrpGui.Components;

public class ArrpGuiTimelinePicker(ArrpDataCache dataCache) {

    private string _timelinePickerName = "TimelinePicker";
    private string _timelineSearch = string.Empty;

    public void SetPopupName(string name)
        => _timelinePickerName = name;

    public void OpenPopup() {
        ImGui.OpenPopup($"{_timelinePickerName}");
    }

    public bool Popup([NotNullWhen(true)] out ActionTimeline? timeline, Vector2? size = null) {

        timeline = null;

        using var timelinePickerPopup = ImRaii.Popup($"{_timelinePickerName}");
        if (!timelinePickerPopup.Success)
            return false;

        ImGui.SetNextItemWidth(-1);
        ImGui.InputTextWithHint("###ArrpTimelinePickerListSearch", "Search...", ref _timelineSearch, 64);

        using var timelinePickerTable = ImRaii.Table("###ArrpTimelinePickerListTable", 4, ImGuiTableFlags.NoSavedSettings | ImGuiTableFlags.ScrollY | ImGuiTableFlags.PadOuterX, size ?? new Vector2(500, 300));
        if (!timelinePickerTable.Success)
            return false;

        ImGui.TableSetupColumn("##timelinePickerIdCol", ImGuiTableColumnFlags.WidthFixed, 80);
        ImGui.TableSetupColumn("##timelinePickerSlotCol", ImGuiTableColumnFlags.WidthFixed, 40);
        ImGui.TableSetupColumn("##timelinePickerKeyCol", ImGuiTableColumnFlags.WidthStretch, -1);
        ImGui.TableSetupColumn("##timelinePickerFlagsCol", ImGuiTableColumnFlags.WidthFixed, 100);

        ImGui.TableSetupScrollFreeze(0, 1);
        ImGui.TableHeadersRow();

        ArrpGuiHelper.DrawCenteredHeaderCell(0, () => ImGui.Text("ID"));
        ArrpGuiHelper.DrawCenteredHeaderCell(1, () => ImGui.Text("Slot"));
        ArrpGuiHelper.DrawCenteredHeaderCell(2, () => ImGui.Text("Key"));
        ArrpGuiHelper.DrawCenteredHeaderCell(3, () => ImGui.Text("Flags"));

        var timelineList = dataCache.GetActionTimelines();
        var timelineFilter = string.IsNullOrWhiteSpace(_timelineSearch) ? timelineList : timelineList.Where(t =>
            t.Key.ToString().Contains(_timelineSearch, StringComparison.OrdinalIgnoreCase)
            || t.RowId.ToString().Contains(_timelineSearch, StringComparison.OrdinalIgnoreCase)
        );

        timelineFilter = timelineFilter.OrderBy(e => e.RowId);

        foreach (var timelineEntry in timelineFilter) {
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.Text(timelineEntry.RowId.ToString());
            ImGui.TableNextColumn();
            ImGui.Text(timelineEntry.Slot.ToString());

            ImGui.TableNextColumn();
            if (ImGui.Selectable($"{timelineEntry.Key}###ArrpTimelinePickerSelection{timelineEntry.RowId}", false, ImGuiSelectableFlags.SpanAllColumns)) {
                ImGui.CloseCurrentPopup();
                timeline = timelineEntry;
                return true;
            }

            ImGui.TableNextColumn();
            if (timelineEntry.IsLoop) {
                using (ImRaii.PushFont(UiBuilder.IconFont)) {
                    ImGui.Text(FontAwesomeIcon.Recycle.ToIconString());
                }
                ImGui.SameLine(0, 4);
                ImGui.Text("Looping");
            }
        }

        return false;
    }
}
