using ARealmRepopulated.Core.ArrpGui.Components;
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

    private static bool IsRemoteAppearanceManagement(ScenarioNpcData npc)
        => npc.TryGetIntegrationProperty<bool>(IntegrationProvider.ExternalAppearanceConfigKey, out var isExternal) && isExternal;
    
    private static void SetStableActorName(ScenarioNpcData npc, string name) {
        npc.SetIntegrationProperty(IntegrationProvider.ActorNameConfigKey, name);
        if (string.IsNullOrWhiteSpace(name))
            npc.SetIntegrationProperty(IntegrationProvider.ExternalAppearanceConfigKey, "");
    }

    private void DrawNpcSetupTab() {
        DrawNpcBaseAppearanceInfo();

        if (SelectedScenarioNpc == null || !IsRemoteAppearanceManagement(SelectedScenarioNpc))
            return;

        ImGui.Dummy(ArrpGuiSpacing.VerticalSectionSpacing);
        ArrpGuiLayout.IconNote(FontAwesomeIcon.ExclamationTriangle, ArrpGuiColors.ArrpYellow, loc["ScenarioEditor_ActorData_Appearance_ManagedExternally"]);
    }
    
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

        var isExternal = IsRemoteAppearanceManagement(SelectedScenarioNpc);
        using (ImRaii.Disabled(isExternal))
            DrawNpcAppearanceSources();

        ImGui.Dummy(ArrpGuiSpacing.VerticalComponentSpacing);
        ImGui.Separator();
        ImGui.Dummy(ArrpGuiSpacing.VerticalComponentSpacing);

        var appearance = SelectedScenarioNpc.Appearance;
        ArrpGuiForm.Draw("##npcAppearanceEditorSetupForm", form => {            
            if (!isExternal) {
                DrawValueRow(form, loc["ScenarioEditor_ActorData_Appearance_CRaceTribeGender"], $"{appearance.Race} / {appearance.Tribe} / {appearance.Sex}");
                DrawValueRow(form, loc["ScenarioEditor_ActorData_Appearance_CModelSkeleton"], $"{appearance.ModelCharaId} / {appearance.ModelSkeletonId}");

                form.StretchedRow(loc["ScenarioEditor_ActorData_Appearance_CScale"], () => {
                    var scale = appearance.Scale ?? NpcAppearanceData.ScaleDefault;
                    if (ImGui.SliderFloat("##npcAppearanceEditorSetupScale", ref scale, NpcAppearanceData.ScaleSoftMin, NpcAppearanceData.ScaleSoftMax, "%.2f")) {
                        appearance.Scale = Math.Clamp(scale, NpcAppearanceData.ScaleMin, NpcAppearanceData.ScaleMax);
                    }
                }, loc["ScenarioEditor_ActorData_Appearance_CScale_Desc"]);

                form.StretchedRow(loc["ScenarioEditor_ActorData_Appearance_CHeightMultiplier"], () => {
                    var heightMultiplier = appearance.HeightMultiplier ?? NpcAppearanceData.HeightMultiplierDefault;
                    if (ImGui.SliderFloat("##npcAppearanceEditorSetupHeightMultiplier", ref heightMultiplier,
                            NpcAppearanceData.HeightMultiplierMin, NpcAppearanceData.HeightMultiplierMax, "%.2f")) {
                        appearance.HeightMultiplier = Math.Clamp(heightMultiplier,
                            NpcAppearanceData.HeightMultiplierMin, NpcAppearanceData.HeightMultiplierMax);
                    }
                }, loc["ScenarioEditor_ActorData_Appearance_CHeightMultiplier_Desc"]);
            }

            // other plugins do not seem to handle voices ... so we still have that responsibility
            DrawNpcVoiceRow(form, appearance);

            if (!isExternal) {
                form.CheckboxRow(loc["ScenarioEditor_ActorData_Appearance_CWeaponsHidden"], appearance.HideWeapons, hide => appearance.HideWeapons = hide);
                form.CheckboxRow(loc["ScenarioEditor_ActorData_Appearance_CHeadgearHidden"], appearance.HideHeadgear, hide => appearance.HideHeadgear = hide);
            }
        });
    }

    private void DrawNpcVoiceRow(ArrpGuiForm form, NpcAppearanceData appearance) {
        if (!dataCache.IsHumanModel(appearance.ModelCharaId))
            return;

        var voices = characterCreationData.GetVoices(appearance.Race, appearance.Tribe, appearance.Sex);
        if (voices.Length == 0 && appearance.Voice == null)
            return;

        form.StretchedRow(loc["ScenarioEditor_ActorData_Appearance_Voice"], () => {
            using var combo = ImRaii.Combo("##npcVoiceEditorVoice", DescribeVoice(voices, appearance.Voice));
            if (!combo.Success)
                return;

            if (ImGui.Selectable(loc["ScenarioEditor_ActorData_Appearance_Voice_None"], appearance.Voice == null)) {
                appearance.Voice = null;
            }

            for (var i = 0; i < voices.Length; i++) {
                if (ImGui.Selectable($"{loc["ScenarioEditor_ActorData_Appearance_Voice_Entry", i + 1]}##npcVoiceEditorVoice{voices[i]}", appearance.Voice == voices[i])) {
                    appearance.Voice = voices[i];
                }
            }
        }, loc["ScenarioEditor_ActorData_Appearance_Voice_Desc"]);
    }

    private string DescribeVoice(byte[] voices, byte? voice) {
        if (voice == null) {
            return loc["ScenarioEditor_ActorData_Appearance_Voice_None"];
        }
        
        var index = Array.IndexOf(voices, voice.Value);
        return index >= 0
            ? loc["ScenarioEditor_ActorData_Appearance_Voice_Entry", index + 1]
            : loc["ScenarioEditor_ActorData_Appearance_Voice_Other", voice.Value];
    }

    private void DrawNpcCustomizeAppearanceInfo() {

        if (SelectedScenarioNpc == null)
            return;

        using var cellPadding = ImRaii.PushStyle(ImGuiStyleVar.CellPadding, ArrpGuiSpacing.TableCellPadding);
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

        var appearance = SelectedScenarioNpc.Appearance;
        ArrpGuiForm.Draw("##npcAppearanceEditorEquipmentForm", form => {
            DrawValueRow(form, loc["ScenarioEditor_ActorData_Appearance_EMainHand"], DescribeWeapon(ItemSlots.MainHand, appearance.MainHand));
            DrawValueRow(form, loc["ScenarioEditor_ActorData_Appearance_EOffHand"], DescribeWeapon(ItemSlots.OffHand, appearance.OffHand));

            DrawValueRow(form, loc["ScenarioEditor_ActorData_Appearance_EHeadgear"], DescribeEquipment(ItemSlots.Head, appearance.HeadGear));
            DrawValueRow(form, loc["ScenarioEditor_ActorData_Appearance_EBody"], DescribeEquipment(ItemSlots.Body, appearance.Body));
            DrawValueRow(form, loc["ScenarioEditor_ActorData_Appearance_EHands"], DescribeEquipment(ItemSlots.Hands, appearance.Hands));
            DrawValueRow(form, loc["ScenarioEditor_ActorData_Appearance_ELegs"], DescribeEquipment(ItemSlots.Legs, appearance.Legs));
            DrawValueRow(form, loc["ScenarioEditor_ActorData_Appearance_EFeet"], DescribeEquipment(ItemSlots.Feet, appearance.Feet));
            DrawValueRow(form, loc["ScenarioEditor_ActorData_Appearance_EEars"], DescribeEquipment(ItemSlots.Ears, appearance.Ears));
            DrawValueRow(form, loc["ScenarioEditor_ActorData_Appearance_ENeck"], DescribeEquipment(ItemSlots.Neck, appearance.Neck));
            DrawValueRow(form, loc["ScenarioEditor_ActorData_Appearance_EWrist"], DescribeEquipment(ItemSlots.Wrists, appearance.Wrists));
            DrawValueRow(form, loc["ScenarioEditor_ActorData_Appearance_ERingLeft"], DescribeEquipment(ItemSlots.LeftRing, appearance.LeftRing));
            DrawValueRow(form, loc["ScenarioEditor_ActorData_Appearance_ERingRight"], DescribeEquipment(ItemSlots.RightRing, appearance.RightRing));
            DrawValueRow(form, loc["ScenarioEditor_ActorData_Appearance_EGlasses"], DescribeGlasses(appearance.Glasses));
        });
    }

    private void DrawNpcIntegrationInfo() {
        if (SelectedScenarioNpc == null)
            return;

        var npc = SelectedScenarioNpc;
        var currentName = npc.AdditionalData.GetValueOrDefault(IntegrationProvider.ActorNameConfigKey, "");
        if (!ReferenceEquals(_actorNameOwner, npc)) {
            _actorNameOwner = npc;
            _actorNameUsePrefix = currentName.Length == 0 || ActorName.HasPrefix(currentName);
        }

        ArrpGuiForm.Draw("##npcIntegrationEditorForm", form => {
            form.Row(loc["ScenarioEditor_ActorData_Appearance_Integration_Input_ActorName"], () => {
                var usePrefix = _actorNameUsePrefix;
                if (ImGui.Checkbox("##npcIntegrationEditorGeneralLinkPrefix", ref usePrefix)) {
                    _actorNameUsePrefix = usePrefix;

                    var toggledIdentifier = usePrefix ? ActorName.Filter(currentName) : ActorName.WithoutPrefix(currentName);
                    currentName = ComposeActorName(usePrefix, toggledIdentifier);
                    SetStableActorName(npc, currentName);
                }
                ImGui.SameLine();

                var currentIdenitfer = usePrefix ? ActorName.WithoutPrefix(currentName) : currentName;
                var nameFlags = usePrefix ? ImGuiInputTextFlags.CharsNoBlank : ImGuiInputTextFlags.None;
                var nameLimit = usePrefix ? ActorName.MaxPrefixedNameBytes : ActorName.MaxNameBytes;
                if (ImGui.InputTextEx("##npcIntegrationEditorGeneralLink", "", ref currentIdenitfer, maxLength: nameLimit, flags: nameFlags)) {
                    currentIdenitfer = ActorName.TruncateToBytes(usePrefix ? ActorName.Clean(currentIdenitfer) : currentIdenitfer.Trim(), nameLimit);
                    currentName = ComposeActorName(usePrefix, currentIdenitfer);
                    SetStableActorName(npc, currentName);
                }
                ImGui.SameLine();
                if (ImGuiComponents.IconButton(FontAwesomeIcon.TrowelBricks)) {
                    _actorNameUsePrefix = true;
                    currentName = ActorName.IntegrationPrefix + characterCreationData.GetRandomName();
                    SetStableActorName(npc, currentName);
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
            }, loc["ScenarioEditor_ActorData_Appearance_Integration_Input_ActorName_Desc"]);

            if (string.IsNullOrWhiteSpace(currentName))
                return;

            form.CheckboxRow(loc["ScenarioEditor_ActorData_Appearance_Integration_Input_ExternalAppearance"], IsRemoteAppearanceManagement(npc),
                isExternal => npc.SetIntegrationProperty(IntegrationProvider.ExternalAppearanceConfigKey, isExternal ? "true" : ""),
                loc["ScenarioEditor_ActorData_Appearance_Integration_Input_ExternalAppearance_Desc"]);
        });
    }

    private static void DrawNpcModelRow(string description, byte? val) {
        ImGui.TableNextColumn();
        ImGui.Text(description);
        ImGui.TextDisabled((val ?? 0).ToString());
    }
    private static void DrawValueRow(ArrpGuiForm form, string label, string value)
        => form.Row(label, () => {
            ImGui.AlignTextToFramePadding();
            ImGui.TextDisabled(value);
        });

    private string DescribeWeapon(ItemSlots slot, WeaponModel? model) {
        if (model == null)
            return "-";

        var weaponModel = dataCache.GetItemByModel(slot, model.ModelSetId, model.Base, model.Variant);
        if (weaponModel == ItemModelData.Empty || weaponModel.Item == 0)
            return "-";

        var weaponItem = dataCache.GetItem(weaponModel.Item);
        return weaponItem != null ? weaponItem.Value.Name.ToString() : $"Unknown Item {weaponModel.Item}";
    }

    private string DescribeEquipment(ItemSlots slot, EquipmentModel? model) {
        if (model == null)
            return "-";

        var equipModel = dataCache.GetItemByModel(slot, 0, model.ModelId, model.Variant);
        if (equipModel == ItemModelData.Empty || equipModel.Item == 0)
            return equipModel.ModelBase != 0 ? $"(?) {equipModel.ModelBase}" : "-";

        var equipItem = dataCache.GetItem(equipModel.Item);
        return equipItem != null ? equipItem.Value.Name.ToString() : $"Unknown Item {equipModel.Item}";
    }

    private string DescribeGlasses(ushort? glassesId) {
        if (glassesId is null or 0)
            return "-";

        var glassesName = dataCache.GetGlasses(glassesId.Value)?.Name.ToString() ?? "";
        return string.IsNullOrWhiteSpace(glassesName) ? $"(?) {glassesId}" : glassesName;
    }

}
