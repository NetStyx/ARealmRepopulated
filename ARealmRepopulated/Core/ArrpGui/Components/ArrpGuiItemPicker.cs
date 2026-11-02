using ARealmRepopulated.Core.ArrpGui.Style;
using ARealmRepopulated.Core.l10n;
using ARealmRepopulated.Data.Appearance;
using ARealmRepopulated.Infrastructure;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using System.Numerics;

namespace ARealmRepopulated.Core.ArrpGui.Components;

public readonly record struct ArrpGuiItemPick(ulong ModelKey, ulong OffHandModelKey);

public class ArrpGuiItemPicker(ArrpItemData itemData, ArrpGuiGameIcon gameIcon, ArrpTranslation loc) {

    private const string PopupName = "##ArrpItemPicker";

    private enum SourceFilter {
        All,
        Items,
        NpcGear
    }

    private readonly record struct Entry(string Name, string Source, uint Icon, ArrpGuiItemPick Pick);

    private string _search = string.Empty;
    private int _source = (int)SourceFilter.All;

    private List<Entry>? _filteredEntries;
    private string _filteredSearch = string.Empty;
    private ItemSlots _filteredSlot = ItemSlots.None;
    private int _filteredSource = 0;

    /// <summary>
    /// How a model without an item shows up in the editor.
    /// </summary>
    public static string DescribeModel(ItemSlots slot, ulong modelKey) {
        var parts = ItemModelData.DecodeModel(slot, modelKey);
        return slot.IsWeapon()
            ? $"(?) {parts.ModelSet} / {parts.ModelBase} / {parts.ModelVariant}"
            : $"(?) {parts.ModelBase} / {parts.ModelVariant}";
    }

    public void OpenPopup() {
        _search = string.Empty;
        _filteredEntries = null;
        ImGui.OpenPopup(PopupName);
    }

    public bool Popup(ItemSlots slot, ulong currentKey, out ArrpGuiItemPick? pick) {

        pick = null;

        using var popup = ArrpGuiLayout.Popup(PopupName);
        if (!popup.Success)
            return false;

        if (slot != ItemSlots.Glasses) {
            ImGui.SetNextItemWidth(ImGui.GetFrameHeight() * 6);
            ImGui.Combo("##ArrpItemPickerSource", ref _source, [loc["ArrpGuiPicker_Filter_All"], loc["ArrpGuiPicker_Item_Source_Items"], loc["ArrpGuiPicker_Item_Source_NpcGear"]], 3);
            ImGui.SameLine();
        }

        ImGui.SetNextItemWidth(-1);
        ImGui.InputTextWithHint("##ArrpItemPickerSearch", loc["ArrpGuiPicker_Search"], ref _search, 64);

        if (_filteredEntries == null || _filteredSearch != _search || _filteredSlot != slot || _filteredSource != _source) {
            _filteredSlot = slot;
            _filteredSearch = _search;
            _filteredSource = _source;
            _filteredEntries = [..
                GetEntries(slot).Where(e => string.IsNullOrEmpty(_filteredSearch)
                || e.Name.Contains(_filteredSearch, StringComparison.OrdinalIgnoreCase))
            ];
        }

        var iconSize = new Vector2(ImGui.GetFrameHeight() * 2);
        var rowHeight = iconSize.Y + (ImGui.GetStyle().CellPadding.Y * 2);
        var opening = ImGui.IsWindowAppearing();

        var chosen = false;

        using (var table = ImRaii.Table("##ArrpItemPickerTable", 2, ImGuiTableFlags.NoSavedSettings | ImGuiTableFlags.ScrollY, new Vector2(ImGui.GetFrameHeight() * 24, rowHeight * 8))) {
            if (!table.Success)
                return false;

            ImGui.TableSetupColumn("##itemPickerNameCol", ImGuiTableColumnFlags.WidthStretch, -1);
            ImGui.TableSetupColumn("##itemPickerSourceCol", ImGuiTableColumnFlags.WidthFixed, ImGui.GetFrameHeight() * 4);
            ImGui.TableSetupScrollFreeze(0, 1);

            if (DrawRow("##ArrpItemPickerNone", loc["ArrpGuiPicker_Item_None"], string.Empty, currentKey == 0, iconSize, () => DrawNoItemIcon(iconSize))) {
                ImGui.CloseCurrentPopup();
                chosen = true;
            }

            // items can share a model so we simply showing the first one
            var current = _filteredEntries.FindIndex(e => e.Pick.ModelKey == currentKey);
            if (opening && current >= 0) {
                ImGui.SetScrollY(current * rowHeight);
            }

            var clipper = new ImGuiListClipper();
            clipper.Begin(_filteredEntries.Count, rowHeight);
            while (clipper.Step()) {
                for (var i = clipper.DisplayStart; i < clipper.DisplayEnd; i++) {
                    var entry = _filteredEntries[i];

                    if (DrawRow($"##ArrpItemPickerEntry{i}", entry.Name, entry.Source, i == current, iconSize, () => gameIcon.Draw(entry.Icon, iconSize))) {
                        ImGui.CloseCurrentPopup();
                        pick = entry.Pick;
                        chosen = true;
                    }
                }
            }
            clipper.End();
        }

        using (ImRaii.PushColor(ImGuiCol.Text, ArrpGuiColors.NoteColor)) {
            ImGui.TextWrapped(loc["ArrpGuiPicker_Item_ModelDisclaimer"]);
        }

        return chosen;
    }

    private IEnumerable<Entry> GetEntries(ItemSlots slot) {
        if (slot == ItemSlots.Glasses) {
            foreach (var glasses in itemData.GetNamedGlasses()) {
                yield return new Entry(glasses.Name.ToString(), string.Empty, (uint)glasses.Icon, new ArrpGuiItemPick(glasses.RowId, 0));
            }
            yield break;
        }

        var source = (SourceFilter)_filteredSource;

        if (source != SourceFilter.NpcGear) {
            var itemSource = loc["ArrpGuiPicker_Item_Source_Items"];
            foreach (var item in itemData.GetEquippableItems(slot)) {
                yield return new Entry(item.Name.ToString(), itemSource, item.Icon, new ArrpGuiItemPick(item.ModelMain, item.ModelSub));
            }
        }

        if (source != SourceFilter.Items) {
            var npcSource = loc["ArrpGuiPicker_Item_Source_NpcGear"];
            foreach (var model in itemData.GetNpcOnlyModels(slot)) {
                yield return new Entry(DescribeModel(slot, model), npcSource, 0, new ArrpGuiItemPick(model, 0));
            }
        }
    }

    private static bool DrawRow(string id, string name, string source, bool selected, Vector2 iconSize, Action drawIcon) {
        ImGui.TableNextRow();
        ImGui.TableNextColumn();

        var start = ImGui.GetCursorPos();
        var clicked = ImGui.Selectable(id, selected, ImGuiSelectableFlags.SpanAllColumns, new Vector2(0, iconSize.Y));

        var textY = start.Y + ((iconSize.Y - ImGui.GetTextLineHeight()) / 2);
        ImGui.SetCursorPos(start);
        drawIcon();
        ImGui.SameLine();
        ImGui.SetCursorPosY(textY);
        ImGui.TextUnformatted(name);

        ImGui.TableNextColumn();
        ImGui.SetCursorPosY(textY);
        ImGui.TextDisabled(source);

        return clicked;
    }

    private void DrawNoItemIcon(Vector2 size) {
        gameIcon.Draw(0, size);
        gameIcon.DrawNoneMark(size);
    }
}
