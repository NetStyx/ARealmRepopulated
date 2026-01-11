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

            if (ImGui.Selectable($"{loc["ScenarioEditor_ActorData_Appearance_Setup"]}##scenarioNpcAppearanceEditorListBoxGeneral", _selectedNpcAppearanceEditorTab == SelectedNpcAppearanceEditorTab.NpcBase)) {
                _selectedNpcAppearanceEditorTab = SelectedNpcAppearanceEditorTab.NpcBase;
            }

            if (ImGui.Selectable($"{loc["ScenarioEditor_ActorData_Appearance_Model"]}##scenarioNpcAppearanceEditorListBoxCustomize", _selectedNpcAppearanceEditorTab == SelectedNpcAppearanceEditorTab.NpcCustomize)) {
                _selectedNpcAppearanceEditorTab = SelectedNpcAppearanceEditorTab.NpcCustomize;
            }

            if (ImGui.Selectable($"{loc["ScenarioEditor_ActorData_Appearance_Equip"]}##scenarioNpcAppearanceEditorListBoxGeneral", _selectedNpcAppearanceEditorTab == SelectedNpcAppearanceEditorTab.NpcEquipment)) {
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

        ImGui.TextDisabled(loc["ScenarioEditor_ActorData_Appearance_Setup_Desc"]);

        if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.PeoplePulling, loc["ScenarioEditor_ActorData_Appearance_Setup_PickNpc"], new Vector2(200, 0))) {
            ImGui.OpenPopup("ArrpScenarioNpcGeneralEditAppearanceNpcPickerPopup");
        }
        if (ImGui.BeginPopup("ArrpScenarioNpcGeneralEditAppearanceNpcPickerPopup")) {
            DrawNpcPickerPopup();
            ImGui.EndPopup();
        }

        ImGui.SameLine();
        if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.PersonWalkingArrowLoopLeft, loc["ScenarioEditor_ActorData_Appearance_Setup_PickSelf"], new Vector2(200, 0))) {
            var currentTargetAppearance = ExportCurrentCharacter();
            if (currentTargetAppearance != null) {
                SelectedScenarioNpc.Appearance = currentTargetAppearance;
            }
        }
        ImGui.SameLine();
        using (ImRaii.Disabled(_targetManager.Target == null)) {
            if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.PersonRays, loc["ScenarioEditor_ActorData_Appearance_Setup_PickTarget"], new Vector2(200, 0))) {
                var currentTargetAppearance = ExportCurrentTarget();
                if (currentTargetAppearance != null) {
                    SelectedScenarioNpc.Appearance = currentTargetAppearance;
                }
            }
        }

        _appearanceFileImportState.CheckState(out var fileImportIcon, out var fileImportColor);
        var importFileTooltip = loc["ScenarioEditor_ActorData_Appearance_Setup_ImportFile"];
        if (_appearanceFileImportState.Result.HasValue) {
            importFileTooltip = (bool)_appearanceFileImportState.Result ? loc["ScenarioEditor_ActorData_Appearance_Setup_ImportFile_Success"] : loc["ScenarioEditor_ActorData_Appearance_Setup_ImportFile_Failure"];
        }

        if (ImGuiComponents.IconButtonWithText(fileImportIcon, importFileTooltip, size: new Vector2(200, 0), defaultColor: fileImportColor)) {
            fileDialogManager.OpenFileDialog($"{loc["ScenarioEditor_ActorData_Appearance_Setup_ImportFile_Select"]}##arrpAppearanceFileSelector", "Character Files (.chara){.chara},All Files{.*}", (b, s) => {
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
        var importDataTooltip = loc["ScenarioEditor_ActorData_Appearance_Setup_ImportClipboard"];
        if (_appearanceDataImportState.Result.HasValue) {
            importDataTooltip = (bool)_appearanceDataImportState.Result ? loc["ScenarioEditor_ActorData_Appearance_Setup_ImportClipboard_Success"] : loc["ScenarioEditor_ActorData_Appearance_Setup_ImportClipboard_Failure"];
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
            ImGui.Text(loc["ScenarioEditor_ActorData_Appearance_CRace"]);
            ImGui.TextDisabled(SelectedScenarioNpc.Appearance.Race.ToString());

            ImGui.TableNextColumn();
            ImGui.Text(loc["ScenarioEditor_ActorData_Appearance_CTribe"]);
            ImGui.TextDisabled(SelectedScenarioNpc.Appearance.Tribe.ToString());

            ImGui.TableNextColumn();
            ImGui.Text(loc["ScenarioEditor_ActorData_Appearance_CGender"]);
            ImGui.TextDisabled(SelectedScenarioNpc.Appearance.Sex.ToString());

            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.Text(loc["ScenarioEditor_ActorData_Appearance_CBase"]);
            ImGui.TextDisabled(SelectedScenarioNpc.Appearance.ModelCharaId.ToString());

            ImGui.TableNextColumn();
            ImGui.Text(loc["ScenarioEditor_ActorData_Appearance_CSkeleton"]);
            ImGui.TextDisabled(SelectedScenarioNpc.Appearance.ModelSkeletonId.ToString());

            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.Dummy(new Vector2(0, 50));

            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            var hideWeapons = SelectedScenarioNpc.Appearance.HideWeapons;
            if (ImGui.Checkbox($"{loc["ScenarioEditor_ActorData_Appearance_CWeaponsHidden"]}##npcAppearanceEditorSetupHideWeapons", ref hideWeapons)) {
                SelectedScenarioNpc.Appearance.HideWeapons = hideWeapons;
            }

            var hideHeadgear = SelectedScenarioNpc.Appearance.HideHeadgear;
            ImGui.TableNextColumn();
            if (ImGui.Checkbox($"{loc["ScenarioEditor_ActorData_Appearance_CHeadgearHidden"]}##npcAppearanceEditorSetupHideHeadgear", ref hideHeadgear)) {
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
            DrawNpcModelRow(loc["ScenarioEditor_ActorData_Appearance_CHeight"], SelectedScenarioNpc.Appearance.Height);
            DrawNpcModelRow(loc["ScenarioEditor_ActorData_Appearance_CHairstyle"], SelectedScenarioNpc.Appearance.HairStyle);
            DrawNpcModelRow(loc["ScenarioEditor_ActorData_Appearance_CHairColor"], SelectedScenarioNpc.Appearance.HairColor);

            ImGui.TableNextRow();
            DrawNpcModelRow(loc["ScenarioEditor_ActorData_Appearance_CHighlights"], SelectedScenarioNpc.Appearance.Highlights);
            DrawNpcModelRow(loc["ScenarioEditor_ActorData_Appearance_CHighlightsColor"], SelectedScenarioNpc.Appearance.HighlightsColor);
            DrawNpcModelRow(loc["ScenarioEditor_ActorData_Appearance_CSkinColor"], SelectedScenarioNpc.Appearance.SkinColor);

            ImGui.TableNextRow();
            DrawNpcModelRow(loc["ScenarioEditor_ActorData_Appearance_CFace"], SelectedScenarioNpc.Appearance.Face);
            DrawNpcModelRow(loc["ScenarioEditor_ActorData_Appearance_CNose"], SelectedScenarioNpc.Appearance.Nose);
            DrawNpcModelRow(loc["ScenarioEditor_ActorData_Appearance_CJaw"], SelectedScenarioNpc.Appearance.Jaw);

            ImGui.TableNextRow();
            DrawNpcModelRow(loc["ScenarioEditor_ActorData_Appearance_CFacialFeatures"], SelectedScenarioNpc.Appearance.FacialFeatures);
            DrawNpcModelRow(loc["ScenarioEditor_ActorData_Appearance_CFacialFeaturesColor"], SelectedScenarioNpc.Appearance.TattooColor);
            DrawNpcModelRow(loc["ScenarioEditor_ActorData_Appearance_CEyebrows"], SelectedScenarioNpc.Appearance.Eyebrows);

            ImGui.TableNextRow();
            DrawNpcModelRow(loc["ScenarioEditor_ActorData_Appearance_CEyeShape"], SelectedScenarioNpc.Appearance.EyeShape);
            DrawNpcModelRow(loc["ScenarioEditor_ActorData_Appearance_CEyeColorLeft"], SelectedScenarioNpc.Appearance.EyeColorLeft);
            DrawNpcModelRow(loc["ScenarioEditor_ActorData_Appearance_CEyeColorRight"], SelectedScenarioNpc.Appearance.EyeColorRight);

            ImGui.TableNextRow();
            DrawNpcModelRow(loc["ScenarioEditor_ActorData_Appearance_CLipstick"], SelectedScenarioNpc.Appearance.Lipstick);
            DrawNpcModelRow(loc["ScenarioEditor_ActorData_Appearance_CLipColorFurPattern"], SelectedScenarioNpc.Appearance.LipColorFurPattern);
            DrawNpcModelRow(loc["ScenarioEditor_ActorData_Appearance_CMuscleMass"], SelectedScenarioNpc.Appearance.MuscleMass);

            ImGui.TableNextRow();
            DrawNpcModelRow(loc["ScenarioEditor_ActorData_Appearance_CTailShape"], SelectedScenarioNpc.Appearance.TailShape);
            DrawNpcModelRow(loc["ScenarioEditor_ActorData_Appearance_CBustSize"], SelectedScenarioNpc.Appearance.BustSize);

            ImGui.EndTable();
        }

    }
    private void DrawNpcEquipmentAppearanceInfo() {

        if (SelectedScenarioNpc == null)
            return;

        if (ImGui.BeginTable("##npcAppearanceEditorNpcValues", 3, ImGuiTableFlags.NoSavedSettings)) {

            ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthStretch);

            DrawNpcEquipmentRow(loc["ScenarioEditor_ActorData_Appearance_EMainHand"], ItemSlots.MainHand, SelectedScenarioNpc.Appearance.MainHand);
            DrawNpcEquipmentRow(loc["ScenarioEditor_ActorData_Appearance_EOffHand"], ItemSlots.OffHand, SelectedScenarioNpc.Appearance.OffHand);

            DrawNpcEquipmentRow(loc["ScenarioEditor_ActorData_Appearance_EHeadgear"], ItemSlots.Head, SelectedScenarioNpc.Appearance.HeadGear);
            DrawNpcEquipmentRow(loc["ScenarioEditor_ActorData_Appearance_EBody"], ItemSlots.Body, SelectedScenarioNpc.Appearance.Body);
            DrawNpcEquipmentRow(loc["ScenarioEditor_ActorData_Appearance_EHands"], ItemSlots.Hands, SelectedScenarioNpc.Appearance.Hands);
            DrawNpcEquipmentRow(loc["ScenarioEditor_ActorData_Appearance_ELegs"], ItemSlots.Legs, SelectedScenarioNpc.Appearance.Legs);
            DrawNpcEquipmentRow(loc["ScenarioEditor_ActorData_Appearance_EFeet"], ItemSlots.Feet, SelectedScenarioNpc.Appearance.Feet);
            DrawNpcEquipmentRow(loc["ScenarioEditor_ActorData_Appearance_EEars"], ItemSlots.Ears, SelectedScenarioNpc.Appearance.Ears);
            DrawNpcEquipmentRow(loc["ScenarioEditor_ActorData_Appearance_ENeck"], ItemSlots.Neck, SelectedScenarioNpc.Appearance.Neck);
            DrawNpcEquipmentRow(loc["ScenarioEditor_ActorData_Appearance_EWrist"], ItemSlots.Wrists, SelectedScenarioNpc.Appearance.Wrists);
            DrawNpcEquipmentRow(loc["ScenarioEditor_ActorData_Appearance_ERingLeft"], ItemSlots.LeftRing, SelectedScenarioNpc.Appearance.LeftRing);
            DrawNpcEquipmentRow(loc["ScenarioEditor_ActorData_Appearance_ERingRight"], ItemSlots.RightRing, SelectedScenarioNpc.Appearance.RightRing);
            DrawNpcEquipmentRow(loc["ScenarioEditor_ActorData_Appearance_EGlasses"], SelectedScenarioNpc.Appearance.Glasses);

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
