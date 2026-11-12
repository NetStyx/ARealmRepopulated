using ARealmRepopulated.Core.ArrpGui.Components;
using ARealmRepopulated.Core.ArrpGui.Style;
using ARealmRepopulated.Core.IPC;
using ARealmRepopulated.Core.Services.Scenarios;
using ARealmRepopulated.Data.Appearance;
using ARealmRepopulated.Data.Characters;
using ARealmRepopulated.Infrastructure;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility.Raii;
using System.Diagnostics;
using System.IO;
using System.Numerics;

namespace ARealmRepopulated.Windows;

public partial class ConfigWindow {

    private string _searchCharacterText = string.Empty;

    private void CharactersTab() {
        ImGui.Dummy(ArrpGuiSpacing.VerticalHeaderSpacing);
        using (ImRaii.Disabled())
            ImGui.TextWrapped(loc["ListWnd_Characters_Desc"]);
        ImGui.Dummy(ArrpGuiSpacing.VerticalComponentSpacing);
        ImGui.Separator();

        using (var t = ImRaii.Table("##CharacterSearchTable", 1)) {
            if (!t.Success)
                return;

            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.SetNextItemWidth(-1);
            ImGui.InputTextWithHint("##SearchCharacters", loc["ListWnd_Characters_Search_Placeholder"], ref _searchCharacterText);
        }

        using var cellPadding = ImRaii.PushStyle(ImGuiStyleVar.CellPadding, new Vector2(10, 5));
        using var characterTable = ImRaii.Table("##CharacterTable", 4, ImGuiTableFlags.Borders | ImGuiTableFlags.ScrollY);
        if (!characterTable.Success)
            return;

        ImGui.TableSetupColumn("##characterNameHeader", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("##characterAppearanceHeader", ImGuiTableColumnFlags.WidthFixed);
        ImGui.TableSetupColumn("##characterUsageHeader", ImGuiTableColumnFlags.WidthFixed);
        ImGui.TableSetupColumn("##characterToolbarHeader", ImGuiTableColumnFlags.WidthFixed);

        ImGui.TableSetupScrollFreeze(0, 1);
        ImGui.TableNextRow();
        ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg0, ImGui.GetColorU32(ImGuiCol.TableHeaderBg));

        ArrpGuiHelper.DrawCenteredHeaderCell(0, () => {
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetTreeNodeToLabelSpacing());
            ImGui.Text(loc["ListWnd_Characters_Header_Name"]);
        });
        ArrpGuiHelper.DrawCenteredHeaderCell(1, () => ImGui.Text(loc["ListWnd_Characters_Header_Appearance"]));
        ArrpGuiHelper.DrawCenteredHeaderCell(2, () => ImGui.Text(loc["ListWnd_Characters_Header_Usage"]));
        ArrpGuiHelper.DrawCenteredHeaderCell(3, () => {
            using (ImRaii.PushColor(ImGuiCol.Button, ArrpGuiColors.ArrpGreen)) {
                if (ImGuiComponents.IconButton("##characterAddButton", FontAwesomeIcon.Plus)) {
                    serviceProvider.GetRequiredService<CharacterEditorWindow>().CreateCharacter();
                }
            }
            ArrpGuiLayout.Tooltip(loc["ListWnd_Characters_Action_Add_Desc"]);

            ImGui.SameLine(0, 5);
            if (ImGuiComponents.IconButton("##characterFolderButton", FontAwesomeIcon.FolderOpen)) {
                var targetPath = _characterFileManager.CharacterPath;
                if (!Directory.Exists(targetPath))
                    Directory.CreateDirectory(targetPath);

                Process.Start(new ProcessStartInfo { FileName = targetPath, UseShellExecute = true });
            }
            ArrpGuiLayout.Tooltip(loc["ListWnd_Characters_Action_OpenFolder_Desc"]);
        });

        var scenarioFiles = _fileManager.GetScenarioFiles().Where(f => !f.IsBroken).ToList();
        var characters = _characterFileManager.GetCharacters()
            .Where(c => CharacterLabel(c).Contains(_searchCharacterText, StringComparison.InvariantCultureIgnoreCase)
                || c.Group.Contains(_searchCharacterText, StringComparison.InvariantCultureIgnoreCase))
            .OrderBy(CharacterLabel, StringComparer.InvariantCultureIgnoreCase)
            .ToList();

        DrawCharacterGroupLevel(characters, 0, scenarioFiles);
    }

    private void DrawCharacterGroupLevel(List<CharacterData> characters, int depth, List<ScenarioFileData> scenarioFiles) {
        var groups = characters
            .Where(c => ArrpEntityExtensions.Segments(c.Group).Length > depth)
            .GroupBy(c => ArrpEntityExtensions.Segments(c.Group)[depth], StringComparer.InvariantCultureIgnoreCase)
            .OrderBy(g => g.Key, StringComparer.InvariantCultureIgnoreCase);

        foreach (var group in groups) {
            var groupCharacters = group.ToList();
            using var id = ImRaii.PushId(group.Key);
            if (!DrawCharacterGroupRow(group.Key, groupCharacters.Count))
                continue;

            using var indent = ImRaii.PushIndent();
            DrawCharacterGroupLevel(groupCharacters, depth + 1, scenarioFiles);
        }

        foreach (var character in characters.Where(c => ArrpEntityExtensions.Segments(c.Group).Length == depth)) {
            DrawCharacterRow(character, [.. scenarioFiles.Where(f => f.MetaData.CharacterIds.Contains(character.Id))]);
        }
    }

    private bool DrawCharacterGroupRow(string name, int count) {
        ImGui.TableNextRow();
        ImGui.TableNextColumn();

        var storage = ImGui.GetStateStorage();
        var openKey = ImGui.GetID("##open");
        if (DrawRowHighlight())
            storage.SetBool(openKey, !storage.GetBool(openKey));

        var isOpen = !string.IsNullOrEmpty(_searchCharacterText) || storage.GetBool(openKey);
        var startX = ImGui.GetCursorPosX();
        ImGui.AlignTextToFramePadding();
        using (ImRaii.PushFont(UiBuilder.IconFont)) {
            ImGui.TextDisabled((isOpen ? FontAwesomeIcon.CaretDown : FontAwesomeIcon.CaretRight).ToIconString());
        }
        ImGui.SameLine();
        ImGui.SetCursorPosX(startX + ImGui.GetTreeNodeToLabelSpacing());
        ImGui.Text(name);
        ImGui.SameLine();
        ImGui.TextDisabled($"({count})");

        return isOpen;
    }

    private void DrawCharacterRow(CharacterData character, List<ScenarioFileData> usedBy) {
        using var id = ImRaii.PushId(character.Id.ToString());
        var usedByTitles = usedBy.Select(f => f.MetaData.Title).Order(StringComparer.InvariantCultureIgnoreCase).ToList();

        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        DrawRowHighlight();
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenOverlapped) && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
            CharacterEditorWindow.Open(windowSystem, serviceProvider, character.Id);

        var startX = ImGui.GetCursorPosX();
        ImGui.AlignTextToFramePadding();
        using (ImRaii.PushFont(UiBuilder.IconFont)) {
            ImGui.TextDisabled(FontAwesomeIcon.User.ToIconString());
        }
        ImGui.SameLine();
        ImGui.SetCursorPosX(startX + ImGui.GetTreeNodeToLabelSpacing());
        if (character.TryGetIntegrationProperty(IntegrationProvider.ActorNameConfigKey, out var integrationName)) {
            ArrpGuiLayout.Badge(FontAwesomeIcon.InfoCircle, ArrpGuiColors.ArrpGreen, DescribeIntegration(character, integrationName));
            ImGui.SameLine(0, ArrpGuiSpacing.InlineIconSpacing);
        }
        ImGui.Text(CharacterLabel(character));

        ImGui.TableNextColumn();
        ImGui.AlignTextToFramePadding();
        ImGui.TextDisabled(DescribeAppearance(character.Appearance));

        ImGui.TableNextColumn();
        ImGui.AlignTextToFramePadding();
        ImGui.TextDisabled(usedBy.Count.ToString());
        if (usedBy.Count > 0 && ImGui.IsItemHovered())
            ImGui.SetTooltip(string.Join(Environment.NewLine, usedByTitles));

        ImGui.TableNextColumn();
        if (ImGuiComponents.IconButton("##characterEditButton", FontAwesomeIcon.Wrench)) {
            CharacterEditorWindow.Open(windowSystem, serviceProvider, character.Id);
        }
        ArrpGuiLayout.Tooltip(loc["ListWnd_Characters_Action_Edit_Desc"]);

        ImGui.SameLine(0, 5);
        var deletePopupId = $"{loc["ListWnd_Scenario_Popup_DeleteScenario_Title"]}##ConfirmCharacterDelete";
        if (ImGuiComponents.IconButton("##characterDeleteButton", FontAwesomeIcon.Trash)) {
            ImGui.OpenPopup(deletePopupId);
        }
        ArrpGuiLayout.Tooltip(loc["ListWnd_Characters_Action_Delete_Desc"]);

        ImGui.SetNextWindowPos(ImGui.GetMainViewport().GetCenter(), ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));
        using var modal = ArrpGuiLayout.PopupModal(deletePopupId, ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoSavedSettings);
        if (!modal.Success)
            return;

        ImGui.Dummy(ArrpGuiSpacing.VerticalHeaderSpacing);
        ImGui.Text(loc["ListWnd_Characters_Popup_Delete_Desc", CharacterLabel(character)]);
        if (usedByTitles.Count > 0) {
            ImGui.Dummy(ArrpGuiSpacing.VerticalComponentSpacing);
            ImGui.Text(loc["ListWnd_Characters_Popup_Delete_Used"]);
            foreach (var title in usedByTitles) {
                ImGui.Bullet();
                ImGui.Text(title);
            }
        }
        ImGui.Dummy(ArrpGuiSpacing.VerticalComponentSpacing);
        ImGui.Separator();
        ImGui.Dummy(ArrpGuiSpacing.VerticalComponentSpacing);

        if (ImGui.Button(loc["ListWnd_Characters_Popup_Delete_Accept"], new Vector2(120, 0))) {
            // the scenarios are written while the character still exists, so their actors keep its look
            _fileManager.UnlinkCharacter(character.Id);
            windowSystem.Windows.OfType<CharacterEditorWindow>().Where(w => w.IsEditing(character.Id)).ToList().ForEach(w => w.IsOpen = false);
            _characterFileManager.Remove(character.Id);
            ImGui.CloseCurrentPopup();
        }
        ImGui.SetItemDefaultFocus();
        ImGui.SameLine();
        if (ImGui.Button(loc["ListWnd_Characters_Popup_Delete_Decline"], new Vector2(120, 0))) {
            ImGui.CloseCurrentPopup();
        }
    }

    private string CharacterLabel(CharacterData character)
        => string.IsNullOrWhiteSpace(character.Name) ? loc["CharacterEditor_UnnamedCharacter"] : character.Name;

    private string DescribeIntegration(CharacterData character, string integrationName)
        => ArrpGuiCharacterEditor.IsExternallyManaged(character)
            ? $"{loc["ListWnd_Characters_IntegrationName", integrationName]}\n\n{loc["ListWnd_Characters_IntegrationName_External"]}"
            : loc["ListWnd_Characters_IntegrationName", integrationName];

    private string DescribeAppearance(NpcAppearanceData appearance) {
        if (!modelData.IsHumanModel(appearance.ModelCharaId))
            return loc["ListWnd_Characters_Appearance_Model", appearance.ModelCharaId];

        List<string> parts = [
            characterCreationData.GetClan(appearance.Tribe, appearance.Sex)?.Name ?? appearance.Tribe.ToString(),
            loc[$"ScenarioEditor_ActorData_Appearance_Gender_{appearance.Sex}"]
        ];
        if (appearance.BodyType is NpcBodyType.Old or NpcBodyType.Young)
            parts.Add(loc[$"ScenarioEditor_ActorData_Appearance_BodyType_{appearance.BodyType}"]);

        return string.Join(", ", parts);
    }
}
