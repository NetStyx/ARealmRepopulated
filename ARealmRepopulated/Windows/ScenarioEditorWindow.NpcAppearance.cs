using ARealmRepopulated.Core.ArrpGui.Style;
using ARealmRepopulated.Data.Appearance;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility.Raii;
using System.Numerics;

namespace ARealmRepopulated.Windows;

public partial class ScenarioEditorWindow {

    private enum SelectedNpcAppearanceEditorTab {
        NpcBase,
        NpcCustomize,
        NpcEquipment,
    }

    private SelectedNpcAppearanceEditorTab _selectedNpcAppearanceEditorTab = SelectedNpcAppearanceEditorTab.NpcBase;

    private readonly TransferState _appearanceDataImportState = new() { DefaultIcon = FontAwesomeIcon.ClipboardCheck };
    private readonly TransferState _appearanceFileImportState = new() { DefaultIcon = FontAwesomeIcon.FileImport };

    private void DrawNpcAppearanceInfo() {

        using var table = ImRaii.Table("##ArrpNpcAppearanceEditorTable", 2, ImGuiTableFlags.NoSavedSettings);
        ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 130);
        ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthStretch, -1);

        ImGui.TableNextRow();
        ImGui.TableNextColumn();

        using (ImRaii.ListBox("##scenarioNpcAppearanceEditorListBox", new System.Numerics.Vector2(120, -10))) {

            if (ImGui.Selectable($"Setup##scenarioNpcAppearanceEditorListBoxGeneral", _selectedNpcAppearanceEditorTab == SelectedNpcAppearanceEditorTab.NpcBase)) {
                _selectedNpcAppearanceEditorTab = SelectedNpcAppearanceEditorTab.NpcBase;
            }

            if (ImGui.Selectable($"NPC Model##scenarioNpcAppearanceEditorListBoxCustomize", _selectedNpcAppearanceEditorTab == SelectedNpcAppearanceEditorTab.NpcCustomize)) {
                _selectedNpcAppearanceEditorTab = SelectedNpcAppearanceEditorTab.NpcCustomize;
            }

            if (ImGui.Selectable($"NPC Equipment##scenarioNpcAppearanceEditorListBoxGeneral", _selectedNpcAppearanceEditorTab == SelectedNpcAppearanceEditorTab.NpcEquipment)) {
                _selectedNpcAppearanceEditorTab = SelectedNpcAppearanceEditorTab.NpcEquipment;
            }
        }

        ImGui.TableNextColumn();
        //ImGui.TableSetBgColor(ImGuiTableBgTarget.CellBg, ImGui.GetColorU32(Core.ArrpGui.Style.ArrpGuiColors.ArrpRed));

        switch (_selectedNpcAppearanceEditorTab) {
            case SelectedNpcAppearanceEditorTab.NpcBase:
                DrawNpcBaseAppearanceInfo();
                break;
            case SelectedNpcAppearanceEditorTab.NpcCustomize:
                DrawNpcCustomizeAppearanceInfo();
                break;
            case SelectedNpcAppearanceEditorTab.NpcEquipment:
                DrawNpcEquipmentAppearanceInfo();
                break;
        }
    }

    private void DrawNpcBaseAppearanceInfo() {
        if (SelectedScenarioNpc == null)
            return;

        ImGui.TextDisabled("Pick the appearance of the npc from an existing source.");

        if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.PeoplePulling, "Pick from nearby NPC", new Vector2(200, 0))) {
            ImGui.OpenPopup("ArrpScenarioNpcGeneralEditAppearanceNpcPickerPopup");
        }
        if (ImGui.BeginPopup("ArrpScenarioNpcGeneralEditAppearanceNpcPickerPopup")) {
            DrawNpcPickerPopup();
            ImGui.EndPopup();
        }

        ImGui.SameLine();
        if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.PersonWalkingArrowLoopLeft, "Pick from yourself", new Vector2(200, 0))) {
            var currentTargetAppearance = ExportCurrentCharacter();
            if (currentTargetAppearance != null) {
                SelectedScenarioNpc.Appearance = currentTargetAppearance;
            }
        }
        ImGui.SameLine();
        using (ImRaii.Disabled(_targetManager.Target == null)) {
            if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.PersonRays, "Pick from target", new Vector2(200, 0))) {
                var currentTargetAppearance = ExportCurrentTarget();
                if (currentTargetAppearance != null) {
                    SelectedScenarioNpc.Appearance = currentTargetAppearance;
                }
            }
        }

        _appearanceFileImportState.CheckState(out var fileImportIcon, out var fileImportColor);
        var importFileTooltip = "Import from file";
        if (_appearanceFileImportState.Result.HasValue) {
            importFileTooltip = (bool)_appearanceFileImportState.Result ? "Import success!" : "Import failed!";
        }

        if (ImGuiComponents.IconButtonWithText(fileImportIcon, importFileTooltip, size: new Vector2(200, 0), defaultColor: fileImportColor)) {
            fileDialogManager.OpenFileDialog("Select file##arrpAppearanceFileSelector", "Character Files (.chara){.chara},All Files{.*}", (b, s) => {
                if (b && s.Count > 0) {
                    var appearanceData = appearanceDataParser.TryParseAppearanceFile(s[0]);
                    if (appearanceData != null) {
                        SelectedScenarioNpc.Appearance = appearanceData;
                        _appearanceFileImportState.SetResult(true);
                    } else {
                        _appearanceFileImportState.SetResult(false);
                    }
                }

            }, 1, isModal: true, startPath: null);
        }

        _appearanceDataImportState.CheckState(out var dataImportIcon, out var dataImportColor);
        var importDataTooltip = "Import from clipboard";
        if (_appearanceDataImportState.Result.HasValue) {
            importDataTooltip = (bool)_appearanceDataImportState.Result ? "Import success!" : "Import failed!";
        }

        ImGui.SameLine();
        if (ImGuiComponents.IconButtonWithText(dataImportIcon, importDataTooltip, size: new Vector2(200, 0), defaultColor: dataImportColor)) {
            var clipboardContents = ImGui.GetClipboardText();
            if (!string.IsNullOrWhiteSpace(clipboardContents)) {
                var appearanceData = appearanceDataParser.TryParseAppearanceData(clipboardContents);
                if (appearanceData != null) {
                    SelectedScenarioNpc.Appearance = appearanceData;
                    _appearanceDataImportState.SetResult(true);
                } else {
                    _appearanceDataImportState.SetResult(false);
                }
            }
        }

        ImGui.Dummy(ArrpGuiSpacing.VerticalComponentSpacing);
        ImGui.Separator();
        ImGui.Dummy(ArrpGuiSpacing.VerticalComponentSpacing);

        if (ImGui.BeginTable("##npcAppearanceEditorRaceTribeGenderTable", 3, ImGuiTableFlags.NoSavedSettings)) {

            ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthStretch);

            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.Text("Race");
            ImGui.TextDisabled(SelectedScenarioNpc.Appearance.Race.ToString());

            ImGui.TableNextColumn();
            ImGui.Text("Tribe");
            ImGui.TextDisabled(SelectedScenarioNpc.Appearance.Tribe.ToString());

            ImGui.TableNextColumn();
            ImGui.Text("Gender");
            ImGui.TextDisabled(SelectedScenarioNpc.Appearance.Sex.ToString());

            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.Text("Base ID");
            ImGui.TextDisabled(SelectedScenarioNpc.Appearance.ModelCharaId.ToString());

            ImGui.TableNextColumn();
            ImGui.Text("Base Skeleton ID");
            ImGui.TextDisabled(SelectedScenarioNpc.Appearance.ModelSkeletonId.ToString());

            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.Dummy(new Vector2(0, 50));

            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            var hideWeapons = SelectedScenarioNpc.Appearance.HideWeapons;
            if (ImGui.Checkbox("Hide Weapons##npcAppearanceEditorSetupHideWeapons", ref hideWeapons)) {
                SelectedScenarioNpc.Appearance.HideWeapons = hideWeapons;
            }

            var hideHeadgear = SelectedScenarioNpc.Appearance.HideHeadgear;
            ImGui.TableNextColumn();
            if (ImGui.Checkbox("Hide Headgear##npcAppearanceEditorSetupHideHeadgear", ref hideHeadgear)) {
                SelectedScenarioNpc.Appearance.HideHeadgear = hideHeadgear;
            }

            ImGui.EndTable();
        }
    }

    private void DrawNpcCustomizeAppearanceInfo() {

        if (SelectedScenarioNpc == null)
            return;

        using var child = ImRaii.Child("##npcAppearanceInfoChild", new System.Numerics.Vector2(0, -10), false);

        var characterEditorData = dataCache.GetCharacterEditorData();
        var selectedNpcTribe = SelectedScenarioNpc.Appearance.Tribe;
        var selectedNpcRace = SelectedScenarioNpc.Appearance.Race;
        var selectedNpcGender = SelectedScenarioNpc.Appearance.Sex;

        if (ImGui.BeginTable("##npcAppearanceEditorNpcValues", 3, ImGuiTableFlags.NoSavedSettings)) {

            ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthStretch);

            ImGui.TableNextRow();
            DrawNpcModelRow("Height", SelectedScenarioNpc.Appearance.Height);
            DrawNpcModelRow("Face", SelectedScenarioNpc.Appearance.Face);
            DrawNpcModelRow("Hairstyle", SelectedScenarioNpc.Appearance.HairStyle);

            ImGui.TableNextRow();
            DrawNpcModelRow("Highlights", SelectedScenarioNpc.Appearance.Highlights);
            DrawNpcModelRow("SkinColor", SelectedScenarioNpc.Appearance.SkinColor);
            DrawNpcModelRow("EyeColorRight", SelectedScenarioNpc.Appearance.EyeColorRight);

            ImGui.TableNextRow();
            DrawNpcModelRow("HairColor", SelectedScenarioNpc.Appearance.HairColor);
            DrawNpcModelRow("HighlightsColor", SelectedScenarioNpc.Appearance.HighlightsColor);
            DrawNpcModelRow("FacialFeatures", SelectedScenarioNpc.Appearance.FacialFeatures);

            ImGui.TableNextRow();
            DrawNpcModelRow("TattooColor", SelectedScenarioNpc.Appearance.TattooColor);
            DrawNpcModelRow("Eyebrows", SelectedScenarioNpc.Appearance.Eyebrows);
            DrawNpcModelRow("EyeColorLeft", SelectedScenarioNpc.Appearance.EyeColorLeft);

            ImGui.TableNextRow();
            DrawNpcModelRow("EyeShape", SelectedScenarioNpc.Appearance.EyeShape);
            DrawNpcModelRow("Nose", SelectedScenarioNpc.Appearance.Nose);
            DrawNpcModelRow("Jaw", SelectedScenarioNpc.Appearance.Jaw);

            ImGui.TableNextRow();
            DrawNpcModelRow("Lipstick", SelectedScenarioNpc.Appearance.Lipstick);
            DrawNpcModelRow("LipColorFurPattern", SelectedScenarioNpc.Appearance.LipColorFurPattern);
            DrawNpcModelRow("MuscleMass", SelectedScenarioNpc.Appearance.MuscleMass);

            ImGui.TableNextRow();
            DrawNpcModelRow("TailShape", SelectedScenarioNpc.Appearance.TailShape);
            DrawNpcModelRow("BustSize", SelectedScenarioNpc.Appearance.BustSize);

            ImGui.EndTable();
        }

    }
    private void DrawNpcEquipmentAppearanceInfo() {

        if (SelectedScenarioNpc == null)
            return;

        if (ImGui.BeginTable("##npcAppearanceEditorNpcValues", 3, ImGuiTableFlags.NoSavedSettings)) {

            ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthStretch);

            DrawNpcEquipmentRow("MainHand", ItemSlots.MainHand, SelectedScenarioNpc.Appearance.MainHand);
            DrawNpcEquipmentRow("OffHand", ItemSlots.OffHand, SelectedScenarioNpc.Appearance.OffHand);

            DrawNpcEquipmentRow("HeadGear", ItemSlots.Head, SelectedScenarioNpc.Appearance.HeadGear);
            DrawNpcEquipmentRow("Body", ItemSlots.Body, SelectedScenarioNpc.Appearance.Body);
            DrawNpcEquipmentRow("Hands", ItemSlots.Hands, SelectedScenarioNpc.Appearance.Hands);
            DrawNpcEquipmentRow("Legs", ItemSlots.Legs, SelectedScenarioNpc.Appearance.Legs);
            DrawNpcEquipmentRow("Feet", ItemSlots.Feet, SelectedScenarioNpc.Appearance.Feet);
            DrawNpcEquipmentRow("Ears", ItemSlots.Ears, SelectedScenarioNpc.Appearance.Ears);
            DrawNpcEquipmentRow("Neck", ItemSlots.Neck, SelectedScenarioNpc.Appearance.Neck);
            DrawNpcEquipmentRow("Wrists", ItemSlots.Wrists, SelectedScenarioNpc.Appearance.Wrists);
            DrawNpcEquipmentRow("LeftRing", ItemSlots.LeftRing, SelectedScenarioNpc.Appearance.LeftRing);
            DrawNpcEquipmentRow("RightRing", ItemSlots.RightRing, SelectedScenarioNpc.Appearance.RightRing);
            DrawNpcEquipmentRow("Glasses", SelectedScenarioNpc.Appearance.Glasses);

            ImGui.EndTable();
        }

    }

    private static void DrawNpcModelRow(string description, byte? val) {
        ImGui.TableNextColumn();
        ImGui.Text(description);
        ImGui.TextDisabled((val ?? 0).ToString());
    }
    private void DrawNpcEquipmentRow(string description, ItemSlots slot, WeaponModel? model) {
        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        ImGui.Text(description);

        ImGui.TableNextColumn();
        if (model == null) {
            ImGui.TextDisabled("-");
            return;
        }

        var weaponModel = dataCache.GetItemByModel(slot, model.ModelSetId, model.Base, model.Variant);
        if (weaponModel == ItemModelData.Empty || weaponModel.Item == 0) {
            ImGui.TextDisabled($"-");
            return;
        }

        var weaponItem = dataCache.GetItem(weaponModel.Item);
        ImGui.TextDisabled(weaponItem != null ? weaponItem.Value.Name.ToString() : $"Unknown Item {weaponModel.Item}");

    }

    private void DrawNpcEquipmentRow(string description, ItemSlots slot, EquipmentModel? model) {
        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        ImGui.Text(description);

        ImGui.TableNextColumn();
        if (model == null) {
            ImGui.TextDisabled("-");
            return;
        }
        var equipModel = dataCache.GetItemByModel(slot, 0, model.ModelId, model.Variant);
        if (equipModel == ItemModelData.Empty || equipModel.Item == 0) {
            ImGui.TextDisabled($"-");
            return;
        }

        var equipItem = dataCache.GetItem(equipModel.Item);
        ImGui.TextDisabled(equipItem != null ? equipItem.Value.Name.ToString() : $"Unknown Item {equipModel.Item}");
    }

    private void DrawNpcEquipmentRow(string description, ushort? item) {
        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        ImGui.Text(description);

        ImGui.TableNextColumn();
        if (item == null) {
            ImGui.TextDisabled("-");
            return;
        }

        var itemModel = dataCache.GetItem((uint)item);
        if (itemModel == null) {
            ImGui.TextDisabled(item.ToString());
            return;
        }

        ImGui.TextDisabled(itemModel.Value.Name.ToString());
    }

}

/*
if (ImGui.BeginTable("##npcAppearanceEditorRaceTribeGenderTable", 3, ImGuiTableFlags.NoSavedSettings)) {
ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthStretch);
ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthStretch);
ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthStretch);

ImGui.TableNextRow();
ImGui.TableNextColumn();

if (ImGui.BeginCombo("##npcAppearanceEditorRaceComboBox", selectedNpcRace != NpcRace.Unknown ? selectedNpcRace.ToString() : "Select Race ...")) {
    foreach (var race in characterEditorData.Races.Select(x => x.Race).Distinct()) {
        if (ImGui.Selectable(race.ToString(), false)) {
            var updatedRace = UpdateRace(race, NpcTribe.Unknown, NpcSex.Male);
            selectedNpcRace = updatedRace.Race;
            selectedNpcTribe = updatedRace.Tribe;
            selectedNpcGender = updatedRace.Sex;
        }
    }
    ImGui.EndCombo();
}

ImGui.TableNextColumn();
if (ImGui.BeginCombo("##npcAppearanceEditorTribeComboBox", selectedNpcTribe != NpcTribe.Unknown ? selectedNpcTribe.ToString() : "Select Tribe")) {
    foreach (var tribe in characterEditorData.Races.Where(x => x.Race == selectedNpcRace).Select(x => x.Tribe).Distinct()) {
        if (ImGui.Selectable(tribe.ToString(), false)) {
            var updatedRace = UpdateRace(selectedNpcRace, tribe, NpcSex.Male);
            selectedNpcTribe = updatedRace.Tribe;
            selectedNpcGender = updatedRace.Sex;
        }
    }
    ImGui.EndCombo();
}

ImGui.TableNextColumn();
if (ImGui.BeginCombo("##npcAppearanceEditorGenderComboBox", selectedNpcGender.ToString())) {
    foreach (var gender in characterEditorData.Races.Where(x => x.Race == selectedNpcRace && x.Tribe == selectedNpcTribe).Select(x => x.Gender).Distinct()) {
        if (ImGui.Selectable(gender.ToString(), false)) {
            selectedNpcGender = UpdateRace(selectedNpcRace, selectedNpcTribe, gender).Sex;
        }
    }
    ImGui.EndCombo();
}

ImGui.EndTable();
}

private (NpcRace Race, NpcTribe Tribe, NpcSex Sex) UpdateRace(NpcRace race, NpcTribe tribe, NpcSex sex) {
    SelectedScenarioNpc.Appearance?.Race = race;
    SelectedScenarioNpc.Appearance?.Tribe = dataCache.GetCharacterEditorData().Races.FirstOrDefault(x => x.Race == race && x.Tribe == tribe)?.Tribe ?? NpcTribe.Unknown;
    SelectedScenarioNpc.Appearance?.Sex = dataCache.GetCharacterEditorData().Races.FirstOrDefault(x => x.Race == race && x.Tribe == tribe && x.Gender == sex)?.Gender ?? NpcSex.Male;

    return (
        SelectedScenarioNpc.Appearance?.Race ?? NpcRace.Unknown,
        SelectedScenarioNpc.Appearance?.Tribe ?? NpcTribe.Unknown,
        SelectedScenarioNpc.Appearance?.Sex ?? NpcSex.Male
        );
}
*/
