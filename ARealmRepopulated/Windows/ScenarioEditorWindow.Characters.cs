using ARealmRepopulated.Core.ArrpGui.Components;
using ARealmRepopulated.Core.ArrpGui.Style;
using ARealmRepopulated.Data.Characters;
using ARealmRepopulated.Data.Scenarios;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility.Raii;
using System.Numerics;

namespace ARealmRepopulated.Windows;

public partial class ScenarioEditorWindow {

    private readonly record struct PendingCharacterImport(CharacterData Character, CharacterImportMatch Match);

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

    private void StartImport(ScenarioExportData importedScenario) {
        _pendingImport = importedScenario;
        _pendingCharacters.Clear();

        foreach (var character in importedScenario.Characters) {
            var match = character.MatchImport(characterFileManager.GetCharacter(character.Id));
            if (match == CharacterImportMatch.New) {
                characterFileManager.Adopt(character);
            } else if (match is CharacterImportMatch.Newer or CharacterImportMatch.Conflicting) {
                _pendingCharacters.Enqueue(new PendingCharacterImport(character, match));
            }
        }

        if (_pendingCharacters.Count == 0)
            FinishImport();
    }

    private void FinishImport() {
        if (_pendingImport is not { } importedScenario)
            return;

        _pendingImport = null;
        characterFileManager.ApplyCharacters(importedScenario.Scenario, importedScenario.Scenario.Title);
        InitScenarioStructures(importedScenario.Scenario, _scenarioFilePath);
        _importState.SetResult(true);
    }

    private void DrawImportQuestion() {
        if (_pendingImport == null || !_pendingCharacters.TryPeek(out var pending))
            return;

        if (!ImGui.IsPopupOpen(ImportQuestionPopupId))
            ImGui.OpenPopup(ImportQuestionPopupId);

        ImGui.SetNextWindowPos(ImGui.GetMainViewport().GetCenter(), ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));
        using var modal = ArrpGuiLayout.PopupModal(ImportQuestionPopupId, ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoTitleBar);
        if (!modal.Success)
            return;

        ImGui.Dummy(ArrpGuiSpacing.VerticalHeaderSpacing);
        ImGui.TextColored(ArrpGuiColors.ArrpYellow, CharacterLabel(pending.Character));
        ImGui.Dummy(ArrpGuiSpacing.VerticalComponentSpacing);

        ImGui.PushTextWrapPos(ImGui.GetFontSize() * 30);
        ImGui.TextUnformatted(loc[pending.Match == CharacterImportMatch.Newer
            ? "ScenarioEditor_Import_Character_Newer"
            : "ScenarioEditor_Import_Character_Conflicting"]);
        ImGui.PopTextWrapPos();
        ImGui.Dummy(ArrpGuiSpacing.VerticalComponentSpacing);
        ImGui.Separator();
        ImGui.Dummy(ArrpGuiSpacing.VerticalComponentSpacing);

        var answered = false;
        if (ImGui.Button(loc["ScenarioEditor_Import_Character_UseImported"])) {
            pending.Character.Group = characterFileManager.GetCharacter(pending.Character.Id)?.Group ?? "";
            characterFileManager.Adopt(pending.Character);
            answered = true;
        }

        ImGui.SameLine();
        if (ImGui.Button(loc["ScenarioEditor_Import_Character_KeepMine"])) {
            answered = true;
        }
        ImGui.SetItemDefaultFocus();

        ImGui.SameLine();
        if (ImGui.Button(loc["ScenarioEditor_Import_Character_ImportAsNew"])) {
            var newCharacter = pending.Character.Copy();
            newCharacter.Id = Guid.NewGuid();
            characterFileManager.Adopt(newCharacter);
            _pendingImport.Scenario.Npcs.Where(n => n.CharacterId == pending.Character.Id).ToList().ForEach(n => n.CharacterId = newCharacter.Id);
            answered = true;
        }

        if (!answered)
            return;

        _pendingCharacters.Dequeue();
        ImGui.CloseCurrentPopup();
        if (_pendingCharacters.Count == 0)
            FinishImport();
    }
}
