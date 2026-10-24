using ARealmRepopulated.Core.l10n;
using ARealmRepopulated.Infrastructure;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using Lumina.Excel.Sheets;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;

namespace ARealmRepopulated.Core.ArrpGui.Components;

public class ArrpGuiTimelinePicker(ArrpDataCache dataCache, ArrpTranslation loc) {

    private string _timelinePickerName = "TimelinePicker";
    private string _timelineSearch = string.Empty;
    private int _slotFilter = 0;
    private byte[]? _slots;

    private List<ActionTimeline>? _filteredTimelines;
    private string _filteredSearch = string.Empty;
    private int _filteredSlot = 0;

    public void SetPopupName(string name)
        => _timelinePickerName = name;

    public string GetSlotName(byte slot)
        => loc[$"ArrpGuiPicker_Timeline_Slot{slot}"];

    public void OpenPopup() {
        ImGui.OpenPopup($"{_timelinePickerName}");
    }

    public bool Popup([NotNullWhen(true)] out ActionTimeline? timeline, Vector2? size = null, IReadOnlySet<byte>? takenSlots = null) {

        timeline = null;

        using var timelinePickerPopup = ArrpGuiLayout.Popup($"{_timelinePickerName}");
        if (!timelinePickerPopup.Success)
            return false;

        _slots ??= [.. dataCache.GetActionTimelines().Where(t => !t.Key.IsEmpty).Select(t => t.GetSlot()).Distinct().Order()];
        string[] slotOptions = [loc["ArrpGuiPicker_Timeline_AllSlots"], .. _slots.Select(GetSlotName)];

        ImGui.SetNextItemWidth(120);
        ImGui.Combo("##ArrpTimelinePickerSlot", ref _slotFilter, slotOptions, slotOptions.Length);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(-1);
        ImGui.InputTextWithHint("###ArrpTimelinePickerListSearch", "Search...", ref _timelineSearch, 64);

        using var timelinePickerTable = ImRaii.Table("###ArrpTimelinePickerListTable", 4, ImGuiTableFlags.NoSavedSettings | ImGuiTableFlags.ScrollY | ImGuiTableFlags.PadOuterX, size ?? new Vector2(500, 300));
        if (!timelinePickerTable.Success)
            return false;

        ImGui.TableSetupColumn("##timelinePickerIdCol", ImGuiTableColumnFlags.WidthFixed, 80);
        ImGui.TableSetupColumn("##timelinePickerSlotCol", ImGuiTableColumnFlags.WidthFixed, 100);
        ImGui.TableSetupColumn("##timelinePickerKeyCol", ImGuiTableColumnFlags.WidthStretch, -1);
        ImGui.TableSetupColumn("##timelinePickerFlagsCol", ImGuiTableColumnFlags.WidthFixed, 100);

        ImGui.TableSetupScrollFreeze(0, 1);
        ImGui.TableHeadersRow();

        ArrpGuiHelper.DrawCenteredHeaderCell(0, () => ImGui.Text("ID"));
        ArrpGuiHelper.DrawCenteredHeaderCell(1, () => ImGui.Text("Slot"));
        ArrpGuiHelper.DrawCenteredHeaderCell(2, () => ImGui.Text("Key"));
        ArrpGuiHelper.DrawCenteredHeaderCell(3, () => ImGui.Text("Flags"));

        if (_filteredTimelines == null || _filteredSearch != _timelineSearch || _filteredSlot != _slotFilter) {
            _filteredSearch = _timelineSearch;
            _filteredSlot = _slotFilter;

            var timelineList = dataCache.GetActionTimelines().Where(t => !t.Key.IsEmpty);
            if (_filteredSlot > 0)
                timelineList = timelineList.Where(t => t.GetSlot() == _slots[_filteredSlot - 1]);

            if (!string.IsNullOrWhiteSpace(_filteredSearch)) {
                timelineList = timelineList.Where(t =>
                    t.Key.ToString().Contains(_filteredSearch, StringComparison.OrdinalIgnoreCase)
                    || t.RowId.ToString().Contains(_filteredSearch, StringComparison.OrdinalIgnoreCase));
            }

            _filteredTimelines = [.. timelineList.OrderBy(e => e.RowId)];
        }

        // yay for vitualization! big lists without stuttering wohoo
        var clipper = new ImGuiListClipper();
        clipper.Begin(_filteredTimelines.Count);
        while (clipper.Step()) {
            for (var i = clipper.DisplayStart; i < clipper.DisplayEnd; i++) {
                var timelineEntry = _filteredTimelines[i];

                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.Text(timelineEntry.RowId.ToString());
                ImGui.TableNextColumn();
                ImGui.Text(GetSlotName(timelineEntry.GetSlot()));

                ImGui.TableNextColumn();
                var isSlotTaken = takenSlots?.Contains(timelineEntry.GetSlot()) ?? false;
                using (ImRaii.Disabled(isSlotTaken)) {
                    if (ImGui.Selectable($"{timelineEntry.Key}###ArrpTimelinePickerSelection{timelineEntry.RowId}", false, ImGuiSelectableFlags.SpanAllColumns)) {
                        ImGui.CloseCurrentPopup();
                        timeline = timelineEntry;
                    }
                }
                if (isSlotTaken)
                    ArrpGuiLayout.Tooltip(loc["ArrpGuiPicker_Timeline_SlotTaken"]);

                ImGui.TableNextColumn();
                if (timelineEntry.IsLooping()) {
                    using (ImRaii.PushFont(UiBuilder.IconFont)) {
                        ImGui.Text(FontAwesomeIcon.Recycle.ToIconString());
                    }
                    ImGui.SameLine(0, 4);
                    ImGui.Text("Looping");
                }
            }
        }
        clipper.End();

        return timeline != null;
    }
}
