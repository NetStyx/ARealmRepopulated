using ARealmRepopulated.Core.ArrpGui.Style;
using ARealmRepopulated.Core.IPC;
using ARealmRepopulated.Core.Services.Npcs;
using ARealmRepopulated.Data.Appearance;
using ARealmRepopulated.Data.Scenarios;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility.Raii;
using System.Numerics;

namespace ARealmRepopulated.Windows;

public partial class ScenarioEditorWindow {

    private readonly TransferState _appearanceDataImportState = new() { DefaultIcon = FontAwesomeIcon.ClipboardCheck };
    private readonly TransferState _appearanceFileImportState = new() { DefaultIcon = FontAwesomeIcon.FileImport };

    private ScenarioNpcData? _actorNameOwner = null;
    private bool _actorNameUsePrefix = true;

    private static string ComposeActorName(bool usePrefix, string identifier)
        => string.IsNullOrWhiteSpace(identifier) ? ""
        : usePrefix ? ActorName.IntegrationPrefix + identifier
        : identifier;
    
    private unsafe void DrawNpcAppearanceSources() {
        if (SelectedScenarioNpc == null)
            return;

        using var cellPadding = ImRaii.PushStyle(ImGuiStyleVar.CellPadding, ArrpGuiSpacing.TableCellPadding);
        using var table = ImRaii.Table("##npcAppearanceEditorSourceTable", 2, ImGuiTableFlags.NoSavedSettings);
        if (!table.Success)
            return;

        ImGui.TableSetupColumn("##npcAppearanceSourceLeft", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("##npcAppearanceSourceRight", ImGuiTableColumnFlags.WidthStretch);

        ImGui.TableNextRow();

        ImGui.TableNextColumn();
        if (DrawNpcAppearanceSourceButton(FontAwesomeIcon.PeoplePulling, loc["ScenarioEditor_ActorData_Appearance_Setup_PickNpc"])) {
            npcPicker.OpenPopup();
        }
        if (npcPicker.Popup(out var npc)) {
            var currentTargetAppearance = ExportCurrentCharacter(npc);
            if (currentTargetAppearance != null) {
                SelectedScenarioNpc.Appearance = currentTargetAppearance;
            }
        }

        ImGui.TableNextColumn();
        if (DrawNpcAppearanceSourceButton(FontAwesomeIcon.PersonWalkingArrowLoopLeft, loc["ScenarioEditor_ActorData_Appearance_Setup_PickSelf"])) {
            var currentTargetAppearance = ExportCurrentCharacter();
            if (currentTargetAppearance != null) {
                SelectedScenarioNpc.Appearance = currentTargetAppearance;
            }
        }

        ImGui.TableNextRow();

        ImGui.TableNextColumn();
        using (ImRaii.Disabled(_targetManager.Target == null)) {
            if (DrawNpcAppearanceSourceButton(FontAwesomeIcon.PersonRays, loc["ScenarioEditor_ActorData_Appearance_Setup_PickTarget"])) {
                var currentTargetAppearance = ExportCurrentTarget();
                if (currentTargetAppearance != null) {
                    SelectedScenarioNpc.Appearance = currentTargetAppearance;
                }
            }
        }

        ImGui.TableNextColumn();
        if (DrawNpcAppearanceSourceButton(FontAwesomeIcon.SearchLocation, loc["ScenarioEditor_ActorData_Appearance_Setup_PickPreset"])) {
            bnpcPresetPicker.OpenPopup();
        }
        if (bnpcPresetPicker.Popup(out var bnpcPicker) && bnpcPicker.Base is var npcBase) {
            var newAppearance = new NpcAppearanceData();
            appearanceService.Read(npcBase, newAppearance);

            SelectedScenarioNpc.Appearance = newAppearance;
        }

        ImGui.TableNextRow();

        _appearanceFileImportState.CheckState(out var fileImportIcon, out var fileImportColor);
        var importFileTooltip = loc["ScenarioEditor_ActorData_Appearance_Setup_ImportFile"];
        if (_appearanceFileImportState.Result.HasValue) {
            importFileTooltip = (bool)_appearanceFileImportState.Result ? loc["ScenarioEditor_ActorData_Appearance_Setup_ImportFile_Success"] : loc["ScenarioEditor_ActorData_Appearance_Setup_ImportFile_Failure"];
        }

        ImGui.TableNextColumn();
        if (DrawNpcAppearanceSourceButton(fileImportIcon, importFileTooltip, fileImportColor)) {
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

        ImGui.TableNextColumn();
        if (DrawNpcAppearanceSourceButton(dataImportIcon, importDataTooltip, dataImportColor)) {
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
    }

    private static bool DrawNpcAppearanceSourceButton(FontAwesomeIcon icon, string label, Vector4? color = null)
        => ImGuiComponents.IconButtonWithText(icon, label, size: new Vector2(ImGui.GetContentRegionAvail().X, 0), defaultColor: color);

    private unsafe void DrawNpcBaseAppearanceInfo() {
        if (SelectedScenarioNpc == null)
            return;

        using (ImRaii.Disabled())
            ImGui.TextWrapped(loc["ScenarioEditor_ActorData_Appearance_Setup_Desc"]);

        ImGui.Dummy(ArrpGuiSpacing.VerticalComponentSpacing);

        DrawNpcAppearanceSources();

        ImGui.Dummy(ArrpGuiSpacing.VerticalComponentSpacing);
        ImGui.Separator();
        ImGui.Dummy(ArrpGuiSpacing.VerticalComponentSpacing);

        using var cellPadding = ImRaii.PushStyle(ImGuiStyleVar.CellPadding, ArrpGuiSpacing.TableCellPadding);
        using var t = ImRaii.Table("##npcAppearanceEditorRaceTribeGenderTable", 3, ImGuiTableFlags.NoSavedSettings);
        if (!t.Success)
            return;

        ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthStretch);

        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        ImGui.Text(loc["ScenarioEditor_ActorData_Appearance_CRaceTribeGender"]);
        ImGui.TextDisabled($"{SelectedScenarioNpc.Appearance.Race} / {SelectedScenarioNpc.Appearance.Tribe} / {SelectedScenarioNpc.Appearance.Sex}");

        ImGui.TableNextColumn();
        ImGui.Text(loc["ScenarioEditor_ActorData_Appearance_CBase"]);
        ImGui.TextDisabled(SelectedScenarioNpc.Appearance.ModelCharaId.ToString());

        ImGui.TableNextColumn();
        ImGui.Text(loc["ScenarioEditor_ActorData_Appearance_CSkeleton"]);
        ImGui.TextDisabled(SelectedScenarioNpc.Appearance.ModelSkeletonId.ToString());

        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        ImGui.Text(loc["ScenarioEditor_ActorData_Appearance_CScale"]);
        ImGui.SameLine();
        ImGuiComponents.HelpMarker(loc["ScenarioEditor_ActorData_Appearance_CScale_Desc"]);
        var scale = SelectedScenarioNpc.Appearance.Scale ?? NpcAppearanceData.ScaleDefault;
        ImGui.SetNextItemWidth(-1);
        if (ImGui.SliderFloat("##npcAppearanceEditorSetupScale", ref scale, NpcAppearanceData.ScaleSoftMin, NpcAppearanceData.ScaleSoftMax, "%.2f")) {
            SelectedScenarioNpc.Appearance.Scale = Math.Clamp(scale, NpcAppearanceData.ScaleMin, NpcAppearanceData.ScaleMax);
        }

        ImGui.TableNextColumn();
        ImGui.Text(loc["ScenarioEditor_ActorData_Appearance_CHeightMultiplier"]);
        ImGui.SameLine();
        ImGuiComponents.HelpMarker(loc["ScenarioEditor_ActorData_Appearance_CHeightMultiplier_Desc"]);
        var heightMultiplier = SelectedScenarioNpc.Appearance.HeightMultiplier ?? NpcAppearanceData.HeightMultiplierDefault;
        ImGui.SetNextItemWidth(-1);
        if (ImGui.SliderFloat("##npcAppearanceEditorSetupHeightMultiplier", ref heightMultiplier,
                NpcAppearanceData.HeightMultiplierMin, NpcAppearanceData.HeightMultiplierMax, "%.2f")) {
            SelectedScenarioNpc.Appearance.HeightMultiplier = Math.Clamp(heightMultiplier,
                NpcAppearanceData.HeightMultiplierMin, NpcAppearanceData.HeightMultiplierMax);
        }

        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        ImGui.Dummy(ArrpGuiSpacing.VerticalSectionSpacing);

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
    }

    private void DrawNpcCustomizeAppearanceInfo() {

        if (SelectedScenarioNpc == null)
            return;

        using var table = ImRaii.Table("##npcAppearanceEditorNpcValues", 3, ImGuiTableFlags.NoSavedSettings);
        if (!table.Success)
            return;

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
                
        ImGui.TableNextColumn();
        ImGui.Text(loc["ScenarioEditor_ActorData_Appearance_EExtended"]);
        ImGui.TextDisabled(SelectedScenarioNpc.Appearance.ExtendedAppearance?.HasAnyValue == true
            ? loc["ScenarioEditor_ActorData_Appearance_EExtended_Present"]
            : loc["ScenarioEditor_ActorData_Appearance_EExtended_Absent"]);

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(loc["ScenarioEditor_ActorData_Appearance_EExtended_Hint"]);        
    }
        
    private void DrawNpcEquipmentAppearanceInfo() {

        if (SelectedScenarioNpc == null)
            return;

        using var table = ImRaii.Table("##npcAppearanceEditorEquipmentValues", 2, ImGuiTableFlags.NoSavedSettings);
        if (!table.Success)
            return;

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
        DrawNpcGlassesRow(loc["ScenarioEditor_ActorData_Appearance_EGlasses"], SelectedScenarioNpc.Appearance.Glasses);

    }

    private void DrawNpcIntegrationInfo() {
        if (SelectedScenarioNpc == null)
            return;

        ImGui.TextDisabled(loc["ScenarioEditor_ActorData_Appearance_Integration_Desc"]);
        ImGui.Separator();

        using var table = ImRaii.Table("##npcIntegrationEditorValues", 2, ImGuiTableFlags.NoSavedSettings);
        if (!table.Success)
            return;

        ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 300);
        ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthStretch);

        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        ImGui.Text(loc["ScenarioEditor_ActorData_Appearance_Integration_Input_ActorName"]);
        ImGui.SameLine();
        ImGuiComponents.HelpMarker(loc["ScenarioEditor_ActorData_Appearance_Integration_Input_ActorName_Desc"]);

        ImGui.TableNextColumn();
        var currentName = SelectedScenarioNpc.AdditionalData.GetValueOrDefault(IntegrationProvider.ActorNameConfigKey, "");

        if (!ReferenceEquals(_actorNameOwner, SelectedScenarioNpc)) {
            _actorNameOwner = SelectedScenarioNpc;
            _actorNameUsePrefix = currentName.Length == 0 || ActorName.HasPrefix(currentName);
        }

        var usePrefix = _actorNameUsePrefix;
        if (ImGui.Checkbox("##npcIntegrationEditorGeneralLinkPrefix", ref usePrefix)) {
            _actorNameUsePrefix = usePrefix;

            var toggledIdentifier = usePrefix ? ActorName.Filter(currentName) : ActorName.WithoutPrefix(currentName);
            currentName = ComposeActorName(usePrefix, toggledIdentifier);
            SelectedScenarioNpc.SetIntegrationProperty(IntegrationProvider.ActorNameConfigKey, currentName);
        }
        ImGui.SameLine();

        var currentIdenitfer = usePrefix ? ActorName.WithoutPrefix(currentName) : currentName;
        var nameFlags = usePrefix ? ImGuiInputTextFlags.CharsNoBlank : ImGuiInputTextFlags.None;
        var nameLimit = usePrefix ? ActorName.MaxPrefixedNameBytes : ActorName.MaxNameBytes;
        if (ImGui.InputTextEx("##npcIntegrationEditorGeneralLink", "", ref currentIdenitfer, maxLength: nameLimit, flags: nameFlags)) {
            currentIdenitfer = ActorName.TruncateToBytes(usePrefix ? ActorName.Clean(currentIdenitfer) : currentIdenitfer.Trim(), nameLimit);
            currentName = ComposeActorName(usePrefix, currentIdenitfer);
            SelectedScenarioNpc.SetIntegrationProperty(IntegrationProvider.ActorNameConfigKey, currentName);
        }
        ImGui.SameLine();
        if (ImGuiComponents.IconButton(FontAwesomeIcon.TrowelBricks)) {            
            _actorNameUsePrefix = true;
            SelectedScenarioNpc.SetIntegrationProperty(IntegrationProvider.ActorNameConfigKey, ActorName.IntegrationPrefix + characterCreationData.GetRandomName());
        }
        if (ImGui.IsItemHovered()) {
            ImGui.SetTooltip(loc["ScenarioEditor_ActorData_Appearance_Integration_Input_ActorName_Random_Hint"]);
        }

        if (!string.IsNullOrWhiteSpace(currentName)) {
            ImGui.SameLine();
            if (ImGuiComponents.IconButton("##scenarioNpcAppearanceEditorIntegrationCopyInternalName", FontAwesomeIcon.Copy)) {
                ImGui.SetClipboardText(currentName);
            }
            if (ImGui.IsItemHovered()) {
                ImGui.SetTooltip(loc["ScenarioEditor_ActorData_Appearance_Integration_Input_ActorName_Clipboard_Hint"]);
            }
        }

        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        ImGui.Text(loc["ScenarioEditor_ActorData_Appearance_Integration_Input_ActorName_Result"]);

        ImGui.TableNextColumn();
        ImGui.TextDisabled(string.IsNullOrWhiteSpace(currentName)
            ? loc["ScenarioEditor_ActorData_Appearance_Integration_Input_ActorName_Result_Unset"]
            : string.Format(loc["ScenarioEditor_ActorData_Appearance_Integration_Input_ActorName_Result_Value"], currentName, ActorName.ByteLength(currentName), ActorName.MaxNameBytes));
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
            if (equipModel.ModelBase != 0) {
                ImGui.TextDisabled($"(?) " + equipModel.ModelBase);
            } else {
                ImGui.TextDisabled($"-");
            }
            return;
        }

        var equipItem = dataCache.GetItem(equipModel.Item);
        ImGui.TextDisabled(equipItem != null ? equipItem.Value.Name.ToString() : $"Unknown Item {equipModel.Item}");
    }

    private void DrawNpcGlassesRow(string description, ushort? glassesId) {
        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        ImGui.Text(description);

        ImGui.TableNextColumn();
        if (glassesId is null or 0) {
            ImGui.TextDisabled("-");
            return;
        }

        var glasses = dataCache.GetGlasses(glassesId.Value);
        var glassesName = glasses?.Name.ToString() ?? "";
        if (string.IsNullOrWhiteSpace(glassesName)) {
            ImGui.TextDisabled($"(?) {glassesId}");
            return;
        }

        ImGui.TextDisabled(glassesName);
    }

}
