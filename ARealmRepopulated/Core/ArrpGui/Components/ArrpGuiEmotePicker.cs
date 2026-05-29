using ARealmRepopulated.Infrastructure;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin.Services;
using System.Numerics;

namespace ARealmRepopulated.Core.ArrpGui.Components;

public class ArrpGuiEmotePicker(ArrpDataCache dataCache, ITextureProvider textureProvider) {

    private readonly string _emotePickerName = "EmotePicker";

    private string _emoteSearch = string.Empty;

    public void OpenPopup() {
        ImGui.OpenPopup(_emotePickerName);
    }

    public bool Popup(out ushort emoteId, Vector2? size = null) {

        emoteId = 0;

        using var emotePickerPopup = ImRaii.Popup(_emotePickerName);
        if (!emotePickerPopup.Success)
            return false;

        ImGui.SetNextItemWidth(-1);
        ImGui.InputTextWithHint("###ArrpEmoteSearch", "Search...", ref _emoteSearch, 64);

        using var cellPadding = ImRaii.PushStyle(ImGuiStyleVar.CellPadding, new Vector2(10, 5));
        using var table = ImRaii.Table("###ArrpEmoteInspectorListTable", 4, ImGuiTableFlags.NoSavedSettings | ImGuiTableFlags.ScrollY | ImGuiTableFlags.PadOuterX, size ?? new Vector2(500, 300));
        if (!table.Success)
            return false;

        ImGui.TableSetupColumn("##emotePickerIconCol", ImGuiTableColumnFlags.WidthFixed, 40);
        ImGui.TableSetupColumn("##emotePickerCategoryCol", ImGuiTableColumnFlags.WidthFixed, 80);
        ImGui.TableSetupColumn("##emotePickerNameCol", ImGuiTableColumnFlags.WidthStretch, -1);
        ImGui.TableSetupColumn("##emotePickerFlagsCol", ImGuiTableColumnFlags.WidthFixed, 100);

        ImGui.TableSetupScrollFreeze(0, 1);
        ImGui.TableHeadersRow();

        ArrpGuiHelper.DrawCenteredHeaderCell(0, () => ImGui.Text("Icon"));
        ArrpGuiHelper.DrawCenteredHeaderCell(1, () => ImGui.Text("Category"));
        ArrpGuiHelper.DrawCenteredHeaderCell(2, () => ImGui.Text("Name"));
        ArrpGuiHelper.DrawCenteredHeaderCell(3, () => ImGui.Text("Flags"));

        var emoteSheet = dataCache.GetEmotes();
        var emoteFilter = string.IsNullOrWhiteSpace(_emoteSearch) ? emoteSheet : emoteSheet.Where(e =>
            e.Name.ToString().Contains(_emoteSearch, StringComparison.OrdinalIgnoreCase)
            || e.RowId.ToString().Contains(_emoteSearch, StringComparison.OrdinalIgnoreCase)
            || (e.TextCommand.ToString()?.Contains(_emoteSearch, StringComparison.OrdinalIgnoreCase) ?? false)
        );

        emoteFilter = emoteFilter.OrderBy(e => e.EmoteCategory.RowId);

        foreach (var emoteRow in emoteFilter) {

            var emoteCommand = emoteRow.TextCommand.IsValid ? emoteRow.TextCommand.Value.Command.ToString() : string.Empty;
            var emoteCategory = emoteRow.EmoteCategory.IsValid ? emoteRow.EmoteCategory.Value.Name.ToString() : string.Empty;
            var emoteName = !emoteRow.Name.IsEmpty ? emoteRow.Name.ToString() : string.Empty;

            if (string.IsNullOrWhiteSpace(emoteName))
                continue;

            var iconId = emoteRow.Icon;
            if (iconId == 0) {
                var alternateEmote = dataCache.GetEmotes().FirstOrDefault(e => e.Icon > 0 && e.Name == emoteName);
                if (alternateEmote.RowId != 0 && alternateEmote.Icon != 0) {
                    iconId = alternateEmote.Icon;
                    emoteName += " (Alt.)";
                }
            }

            ImGui.TableNextRow(32f);
            ImGui.TableNextColumn();

            if (textureProvider.TryGetFromGameIcon(new Dalamud.Interface.Textures.GameIconLookup(iconId), out var iconTexture)) {
                var huh = iconTexture.GetWrapOrDefault();
                if (huh != null) {
                    ImGui.Image(huh.Handle, new Vector2(32, 32));
                }
            }

            ImGui.TableNextColumn();
            ImGui.Text($"{emoteCategory}\n#{emoteRow.RowId}");

            ImGui.TableNextColumn();
            if (ImGui.Selectable($"{emoteName}\n{emoteCommand}##emotePicker{emoteRow.RowId}", false, ImGuiSelectableFlags.SpanAllColumns, new Vector2(0, 32))) {
                emoteId = (ushort)emoteRow.RowId;
                ImGui.CloseCurrentPopup();
                return true;
            }

            ImGui.TableNextColumn();
            if (emoteRow.IsLooping()) {
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
