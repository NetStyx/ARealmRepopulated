using ARealmRepopulated.Core.ArrpGui.Components;
using ARealmRepopulated.Core.ArrpGui.Style;
using ARealmRepopulated.Data.Characters;
using ARealmRepopulated.Data.Scenarios;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility.Raii;

namespace ARealmRepopulated.Windows;

public partial class ScenarioEditorWindow {

    private void CharacterFileManager_CharacterChanged(Guid characterId) {
        if (ScenarioObject == null || characterFileManager.GetCharacter(characterId) is not { } character)
            return;

        ScenarioObject.Npcs.Where(n => n.CharacterId == characterId).ToList().ForEach(n => n.CopyCharacterFrom(character));
    }

    private void ScenarioFileManager_CharacterUnlinked(Guid characterId) {
        ScenarioObject?.Npcs.Where(n => n.CharacterId == characterId).ToList().ForEach(n => n.CharacterId = null);

        foreach (var npc in _storedLinks.Where(e => e.Value == characterId).Select(e => e.Key).ToList())
            _storedLinks[npc] = null;
    }

    private void RememberStoredLinks() {
        _storedLinks.Clear();
        ScenarioObject.Npcs.ForEach(n => _storedLinks[n] = n.CharacterId);
    }

    private ICharacterDefinition ResolveDefinition(ScenarioNpcData npc)
        => _unsavedCharacters.Any(c => c.Id == npc.CharacterId) ? npc : characterFileManager.ResolveDefinition(npc);

    private void DrawCharacterLinkSection(ScenarioNpcData npc) {
        var unsavedCharacter = _unsavedCharacters.FirstOrDefault(c => c.Id == npc.CharacterId);
        var linkedCharacter = npc.CharacterId is { } characterId ? characterFileManager.GetCharacter(characterId) ?? unsavedCharacter : null;

        ArrpGuiForm.Draw("##arrpActorCharacterForm", form => {
            form.StretchedRow(loc["ScenarioEditor_ActorData_Character"], () => DrawCharacterPicker(npc, linkedCharacter), loc["ScenarioEditor_ActorData_Character_Desc"]);

            form.Row(string.Empty, () => {
                if (npc.CharacterId == null) {
                    if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.UserPlus, loc["ScenarioEditor_ActorData_Character_Save"])) {
                        var character = new CharacterData();
                        character.CopyCharacterFrom(npc);
                        _unsavedCharacters.Add(character);
                        npc.CharacterId = character.Id;
                    }
                    ArrpGuiLayout.Tooltip(loc["ScenarioEditor_ActorData_Character_Save_Desc"]);
                } else {
                    using (ImRaii.Disabled(linkedCharacter == null || unsavedCharacter != null)) {
                        if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Edit, loc["ScenarioEditor_ActorData_Character_Edit"]) && linkedCharacter != null) {
                            CharacterEditorWindow.Open(windowSystem, serviceProvider, linkedCharacter.Id);
                        }
                    }

                    ImGui.SameLine(0, ArrpGuiSpacing.ButtonSpacing);
                    if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Unlink, loc["ScenarioEditor_ActorData_Character_Unlink"])) {
                        npc.CharacterId = null;
                    }
                    ArrpGuiLayout.Tooltip(loc["ScenarioEditor_ActorData_Character_Unlink_Desc"]);
                }

                if (npc.CharacterId != _storedLinks.GetValueOrDefault(npc)) {
                    ImGui.SameLine(0, ArrpGuiSpacing.ButtonSpacing);
                    ArrpGuiLayout.IconNote(FontAwesomeIcon.ExclamationCircle, ArrpGuiColors.ArrpYellow, loc["ScenarioEditor_ActorData_Character_Unsaved"]);
                }
            });
        });

        if (npc.CharacterId != null && linkedCharacter == null) {
            ImGui.Dummy(ArrpGuiSpacing.VerticalComponentSpacing);
            ArrpGuiLayout.IconNote(FontAwesomeIcon.ExclamationTriangle, ArrpGuiColors.ArrpRed, loc["ScenarioEditor_ActorData_Character_Missing_Desc"]);
        } else if (npc.CharacterId != null) {
            ImGui.Dummy(ArrpGuiSpacing.VerticalComponentSpacing);
            using (ImRaii.Disabled())
                ImGui.TextWrapped(loc["ScenarioEditor_ActorData_Character_Linked_Desc"]);
        }
    }

    private void DrawCharacterPicker(ScenarioNpcData npc, CharacterData? linkedCharacter) {
        var preview = npc.CharacterId == null ? loc["ScenarioEditor_ActorData_Character_None"]
            : linkedCharacter == null ? loc["ScenarioEditor_ActorData_Character_Missing"]
            : CharacterLabel(linkedCharacter);

        using var combo = ImRaii.Combo("##value", preview);
        if (!combo.Success)
            return;

        var characters = characterFileManager.GetCharacters().OrderBy(CharacterLabel, StringComparer.InvariantCultureIgnoreCase).ToList();
        if (characters.Count == 0) {
            ImGui.TextDisabled(loc["ScenarioEditor_ActorData_Character_Empty"]);
            return;
        }

        foreach (var character in characters) {
            if (ImGui.Selectable($"{CharacterLabel(character)}##{character.Id}", npc.CharacterId == character.Id)) {
                npc.CharacterId = character.Id;
                npc.CopyCharacterFrom(character);
            }
        }
    }

    private string CharacterLabel(CharacterData character)
        => string.IsNullOrWhiteSpace(character.Name) ? loc["CharacterEditor_UnnamedCharacter"] : character.Name;

    private void Import(ScenarioExportData importedScenario) {
        foreach (var character in importedScenario.Characters.Where(c => c.ReplacesLocal(characterFileManager.GetCharacter(c.Id))))
            characterFileManager.Adopt(character);

        characterFileManager.ApplyCharacters(importedScenario.Scenario, importedScenario.Scenario.Title);
        InitScenarioStructures(importedScenario.Scenario, _scenarioFilePath);
        _importState.SetResult(true);
    }
}