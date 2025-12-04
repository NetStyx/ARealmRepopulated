using ARealmRepopulated.Core.ArrpGui.Style;
using ARealmRepopulated.Infrastructure;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin.Services;
using System.Numerics;

namespace ARealmRepopulated.Core.ArrpGui.Components;

public class ArrpGuiEmotePicker(ArrpDataCache dataCache, ITextureProvider textureProvider) {

    private string _emoteSearch = string.Empty;

    public bool Open(out ushort emoteId, Vector2? size = null) {
        emoteId = 0;
        using var padding = ImRaii.PushStyle(ImGuiStyleVar.CellPadding, new Vector2(5, 5));
        using var table = ImRaii.Table("###ArrpEmoteInspectorListTable", 1, ImGuiTableFlags.NoSavedSettings | ImGuiTableFlags.ScrollY, size ?? new Vector2(200, 300));

        ImGui.TableSetupScrollFreeze(0, 1);
        ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthStretch, -1);

        ImGui.TableNextRow(ImGuiTableRowFlags.Headers);
        ImGui.TableNextColumn();

        ImGui.SetNextItemWidth(-1);
        ImGui.InputTextWithHint("###ArrpEmoteSearch", "Search...", ref _emoteSearch, 64);

        var emoteSheet = dataCache.GetEmotes();
        var emoteFilter = string.IsNullOrWhiteSpace(_emoteSearch) ? emoteSheet : emoteSheet.Where(e =>
            e.Name.ToString().Contains(_emoteSearch, StringComparison.InvariantCultureIgnoreCase)
            || (e.TextCommand.ToString()?.Contains(_emoteSearch, StringComparison.InvariantCultureIgnoreCase) ?? false)
        );

        emoteFilter = emoteFilter.OrderBy(e => e.EmoteCategory.RowId);

        foreach (var emoteRow in emoteFilter) {

            var emoteCommand = emoteRow.TextCommand.IsValid ? emoteRow.TextCommand.Value.Command.ToString() : string.Empty;
            var emoteName = !emoteRow.Name.IsEmpty ? emoteRow.Name.ToString() : string.Empty;

            if (string.IsNullOrWhiteSpace(emoteName))
                continue;

            ImGui.TableNextRow();
            ImGui.TableNextColumn();

            var cursor = ImGui.GetCursorPos();
            var selectableSize = new Vector2(ImGui.GetContentRegionAvail().X, 32.0f);
            if (ImGui.Selectable($"##emotePicker{emoteRow.RowId}", false, ImGuiSelectableFlags.SpanAllColumns, selectableSize)) {
                emoteId = (ushort)emoteRow.RowId;
                ImGui.CloseCurrentPopup();
                return true;
            }

            if (ImGui.IsItemHovered() && !string.IsNullOrWhiteSpace(emoteCommand)) {
                ImGui.SetTooltip(emoteCommand);
            }

            ImGui.SetCursorPos(cursor);

            uint iconId = (uint)emoteRow.Icon;
            if (iconId == 0) {
                iconId = emoteFilter.Where(e => e.Icon > 0 && e.Name == emoteRow.Name).FirstOrDefault().Icon;
                if (iconId != 0) {
                    emoteName += " (Alt.)";
                }
            }

            if (textureProvider.TryGetFromGameIcon(new Dalamud.Interface.Textures.GameIconLookup(iconId), out var iconTexture)) {
                var huh = iconTexture.GetWrapOrDefault();
                if (huh != null) {
                    ImGui.Image(huh.Handle, new Vector2(32, 32));
                }
            }
            ImGui.SameLine(0, 5);

            ArrpGuiAlignment.CenterText(emoteName, frameHeight: 32, horizontalAlign: true);



        }


        return false;
    }


}
