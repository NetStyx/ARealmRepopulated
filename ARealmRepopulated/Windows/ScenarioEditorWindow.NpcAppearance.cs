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
using Dalamud.Utility;
using System.Numerics;

namespace ARealmRepopulated.Windows;

public partial class ScenarioEditorWindow {

    private readonly TransferState _appearanceDataImportState = new() { DefaultIcon = FontAwesomeIcon.ClipboardCheck };
    private readonly TransferState _appearanceFileImportState = new() { DefaultIcon = FontAwesomeIcon.FileImport };

    private ScenarioNpcData? _actorNameOwner = null;
    private bool _actorNameUsePrefix = true;
    private bool _showAppearancePreview = false;
    private AppearanceRollLocks _rollLocks = AppearanceRollLocks.None;
    private bool _rollFromResidents = true;

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
        if (DrawNpcAppearanceSourceButton(FontAwesomeIcon.SearchLocation, loc["ScenarioEditor_ActorData_Appearance_Setup_PickAllNpcs"])) {
            npcListPicker.OpenPopup();
        }
        if (npcListPicker.Popup(out var listedNpc)) {
            var newAppearance = new NpcAppearanceData();
            appearanceService.Read(listedNpc, newAppearance);

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

        if (!isExternal) {
            ArrpGuiLayout.SectionHeader(loc["ScenarioEditor_ActorData_Appearance_Setup_Generate"]);
            DrawAppearanceRoll(SelectedScenarioNpc);
        }

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
            }

            // other plugins do not seem to handle voices ... so we still have that responsibility
            DrawNpcVoiceRow(form, appearance);
        });
    }

    private void DrawNpcVoiceRow(ArrpGuiForm form, NpcAppearanceData appearance) {
        if (!modelData.IsHumanModel(appearance.ModelCharaId))
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

        modelEditor.Draw(SelectedScenarioNpc.Appearance);
    }

    private void DrawNpcEquipmentAppearanceInfo() {
        if (SelectedScenarioNpc == null)
            return;

        equipmentEditor.Draw(SelectedScenarioNpc.Appearance);
    }

    private void DrawAppearanceRoll(ScenarioNpcData npc) {
        ArrpGuiForm.Draw("##npcAppearanceEditorRandomizeForm", form => {
            form.StretchedRow(loc["ScenarioEditor_ActorData_Appearance_Randomize_Source"], () => {
                var locationName = territoryData.GetPlaceName(eventService.CurrentLocation.TerritoryType);
                var allNpcs = loc["ScenarioEditor_ActorData_Appearance_Randomize_Source_All"];

                using var combo = ImRaii.Combo("##npcAppearanceEditorRandomizeSource", _rollFromResidents ? locationName : allNpcs);
                if (!combo.Success)
                    return;

                if (ImGui.Selectable(locationName, _rollFromResidents)) {
                    _rollFromResidents = true;
                }

                if (ImGui.Selectable(allNpcs, !_rollFromResidents)) {
                    _rollFromResidents = false;
                }
            }, loc["ScenarioEditor_ActorData_Appearance_Randomize_Source_Desc"]);

            if (characterCreationData.IsCustomizable(npc.Appearance)) {
                form.Row(loc["ScenarioEditor_ActorData_Appearance_Randomize_Lock"], () => {
                    var clanFollowsLocks = AppearanceRoller.KeepsClan(_rollLocks & ~AppearanceRollLocks.Clan);
                    using (ImRaii.Disabled(clanFollowsLocks))
                        DrawRollLock(AppearanceRollLocks.Clan, clanFollowsLocks);

                    foreach (var rollLock in (AppearanceRollLocks[])[AppearanceRollLocks.Hair, AppearanceRollLocks.Face, AppearanceRollLocks.Body, AppearanceRollLocks.Outfit]) {
                        ImGui.SameLine();
                        DrawRollLock(rollLock);
                    }
                });
            }

            form.Row(string.Empty, () => {
                if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Dice, loc["ScenarioEditor_ActorData_Appearance_Randomize"])) {
                    var options = new AppearanceRandomizationOptions(_rollLocks, _rollFromResidents ? eventService.CurrentLocation.TerritoryType : null);
                    if (appearanceRandomizer.Randomize(npc.Appearance, options) is { } rolled) {
                        npc.Appearance = rolled;
                    }
                }
            });
        });
    }

    private void DrawRollLock(AppearanceRollLocks rollLock, bool forced = false) {
        var isLocked = forced || _rollLocks.HasFlag(rollLock);
        if (ImGui.Checkbox(loc[$"ScenarioEditor_ActorData_Appearance_Randomize_Lock_{rollLock}"], ref isLocked)) {
            _rollLocks = isLocked ? _rollLocks | rollLock : _rollLocks & ~rollLock;
        }
    }

    private void DrawAppearancePreviewToggle(ScenarioNpcData npc) {
        if (IsRemoteAppearanceManagement(npc))
            return;

        if (ImGuiComponents.IconButton("##arrpActorPreview", FontAwesomeIcon.Eye, _showAppearancePreview ? ArrpGuiColors.ArrpGreen : null)) {
            _showAppearancePreview = !_showAppearancePreview;
        }
        ArrpGuiLayout.Tooltip($"{loc["ScenarioEditor_ActorData_Appearance_ShowPreview"]}\n\n{loc["ScenarioEditor_ActorData_Appearance_ShowPreview_Desc"]}");
        ImGui.SameLine();
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

        if (pluginInterface.Manifest.RepoUrl is { Length: > 0 } repoUrl) {
            ImGui.Dummy(ArrpGuiSpacing.VerticalComponentSpacing);
            ImGui.Separator();
            ImGui.Dummy(ArrpGuiSpacing.VerticalComponentSpacing);
            using (ImRaii.Disabled())
                ImGui.TextWrapped(loc["ScenarioEditor_ActorData_Appearance_Integration_OpenGuide_Desc"]);
            ImGui.Dummy(ArrpGuiSpacing.VerticalComponentSpacing);
            if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Book, loc["ScenarioEditor_ActorData_Appearance_Integration_OpenGuide"])) {
                Util.OpenLink($"{repoUrl.TrimEnd('/')}/wiki/Plugin-Integrations");
            }
        }
    }

    private static void DrawValueRow(ArrpGuiForm form, string label, string value)
        => form.Row(label, () => {
            ImGui.AlignTextToFramePadding();
            ImGui.TextDisabled(value);
        });

}
