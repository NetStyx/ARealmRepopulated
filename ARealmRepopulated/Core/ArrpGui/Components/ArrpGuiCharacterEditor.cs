using ARealmRepopulated.Configuration;
using ARealmRepopulated.Core.ArrpGui.Style;
using ARealmRepopulated.Core.IPC;
using ARealmRepopulated.Core.l10n;
using ARealmRepopulated.Core.Services.Npcs;
using ARealmRepopulated.Data.Appearance;
using ARealmRepopulated.Data.Characters;
using ARealmRepopulated.Infrastructure;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using System.Numerics;

namespace ARealmRepopulated.Core.ArrpGui.Components;

public class ArrpGuiCharacterEditor(
    PluginConfig config,
    IDalamudPluginInterface pluginInterface,
    IObjectTable objectTable,
    ITargetManager targetManager,
    NpcAppearanceService appearanceService,
    NpcAppearanceRandomizer appearanceRandomizer,
    NpcAppearanceDataParser appearanceDataParser,
    ArrpModelData modelData,
    ArrpTerritoryData territoryData,
    ArrpCharacterCreationData characterCreationData,
    ArrpEventService eventService,
    ArrpGuiEquipmentEditor equipmentEditor,
    ArrpGuiModelEditor modelEditor,
    ArrpGuiNpcListPicker npcListPicker,
    ArrpGuiNpcPicker npcPicker,
    FileDialogManager fileDialogManager,
    ArrpTranslation loc) {

    private readonly TransferState _appearanceDataImportState = new() { DefaultIcon = FontAwesomeIcon.ClipboardCheck };
    private readonly TransferState _appearanceFileImportState = new() { DefaultIcon = FontAwesomeIcon.FileImport };

    private ICharacterDefinition? _actorNameOwner = null;
    private bool _actorNameUsePrefix = true;
    private AppearanceMixLocks _mixLocks = AppearanceMixLocks.None;
    private bool _mixFromResidents = true;

    public bool ShowPreview { get; private set; } = false;

    public static bool IsExternallyManaged(ICharacterDefinition character)
        => character.TryGetIntegrationProperty<bool>(IntegrationProvider.ExternalAppearanceConfigKey, out var isExternal) && isExternal;

    public void DrawTabs(ICharacterDefinition character) {
        using (var setup = ImRaii.TabItem($"{loc["ScenarioEditor_ActorData_Appearance_Setup"]}##arrpCharacterTabSetup", ImGuiTabItemFlags.NoTooltip)) {
            if (setup.Success) {
                ArrpGuiLayout.TabBody("##arrpCharacterTabSetupBody", () => DrawSetupTab(character));
            }
        }

        if (!IsExternallyManaged(character)) {
            using (var model = ImRaii.TabItem($"{loc["ScenarioEditor_ActorData_Appearance_Model"]}##arrpCharacterTabModel", ImGuiTabItemFlags.NoTooltip)) {
                if (model.Success)
                    ArrpGuiLayout.TabBody("##arrpCharacterTabModelBody", () => modelEditor.Draw(character.Appearance));
            }

            using (var equipment = ImRaii.TabItem($"{loc["ScenarioEditor_ActorData_Appearance_Equip"]}##arrpCharacterTabEquipment", ImGuiTabItemFlags.NoTooltip)) {
                if (equipment.Success)
                    ArrpGuiLayout.TabBody("##arrpCharacterTabEquipmentBody", () => equipmentEditor.Draw(character.Appearance));
            }
        }

        if (!config.RuntimeConfig.ModdingToolsInstalled)
            return;

        using var integration = ImRaii.TabItem($"{loc["ScenarioEditor_ActorData_Appearance_Integration"]}##arrpCharacterTabIntegration", ImGuiTabItemFlags.NoTooltip);
        if (integration.Success)
            ArrpGuiLayout.TabBody("##arrpCharacterTabIntegrationBody", () => DrawIntegrationTab(character));
    }

    public void DrawPreviewToggle(ICharacterDefinition character) {
        if (IsExternallyManaged(character))
            return;

        if (ImGuiComponents.IconButton("##arrpCharacterPreview", FontAwesomeIcon.Eye, ShowPreview ? ArrpGuiColors.ArrpGreen : null)) {
            ShowPreview = !ShowPreview;
        }
        ArrpGuiLayout.Tooltip($"{loc["ScenarioEditor_ActorData_Appearance_ShowPreview"]}\n\n{loc["ScenarioEditor_ActorData_Appearance_ShowPreview_Desc"]}");
        ImGui.SameLine();
    }

    private void DrawSetupTab(ICharacterDefinition character) {
        ArrpGuiLayout.SectionHeader(loc["ScenarioEditor_ActorData_Appearance_Setup_Source"]);
        using (ImRaii.Disabled())
            ImGui.TextWrapped(loc["ScenarioEditor_ActorData_Appearance_Setup_Desc"]);

        ImGui.Dummy(ArrpGuiSpacing.VerticalComponentSpacing);

        var isExternal = IsExternallyManaged(character);
        using (ImRaii.Disabled(isExternal))
            DrawAppearanceSources(character);

        if (!isExternal) {
            ArrpGuiLayout.SectionHeader(loc["ScenarioEditor_ActorData_Appearance_Setup_Generate"]);
            DrawAppearanceRandomizer(character);
        }

        ArrpGuiLayout.SectionHeader(loc["ScenarioEditor_ActorData_Appearance_Setup_Summary"]);

        var appearance = character.Appearance;
        ArrpGuiForm.Draw("##npcAppearanceEditorSetupForm", form => {
            if (!isExternal) {
                DrawValueRow(form, loc["ScenarioEditor_ActorData_Appearance_CRaceTribeGender"], $"{appearance.Race} / {appearance.Tribe} / {appearance.Sex}");
                DrawValueRow(form, loc["ScenarioEditor_ActorData_Appearance_CModelSkeleton"], $"{appearance.ModelCharaId} / {appearance.ModelSkeletonId}");
            }

            // other plugins do not seem to handle voices ... so we still have that responsibility
            DrawVoiceRow(form, appearance);
        });

        if (!isExternal)
            return;

        ImGui.Dummy(ArrpGuiSpacing.VerticalSectionSpacing);
        ArrpGuiLayout.IconNote(FontAwesomeIcon.ExclamationTriangle, ArrpGuiColors.ArrpYellow, loc["ScenarioEditor_ActorData_Appearance_ManagedExternally"]);
    }

    private unsafe void DrawAppearanceSources(ICharacterDefinition character) {
        using var cellPadding = ImRaii.PushStyle(ImGuiStyleVar.CellPadding, ArrpGuiSpacing.TableCellPadding);
        using var table = ImRaii.Table("##npcAppearanceEditorSourceTable", 2, ImGuiTableFlags.NoSavedSettings);
        if (!table.Success)
            return;

        ImGui.TableSetupColumn("##npcAppearanceSourceLeft", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("##npcAppearanceSourceRight", ImGuiTableColumnFlags.WidthStretch);

        ImGui.TableNextRow();

        ImGui.TableNextColumn();
        if (DrawAppearanceSourceButton(FontAwesomeIcon.PeoplePulling, loc["ScenarioEditor_ActorData_Appearance_Setup_PickNpc"])) {
            npcPicker.OpenPopup();
        }
        if (npcPicker.Popup(out var npc)) {
            character.Appearance = ReadAppearance(npc);
        }

        ImGui.TableNextColumn();
        if (DrawAppearanceSourceButton(FontAwesomeIcon.PersonWalkingArrowLoopLeft, loc["ScenarioEditor_ActorData_Appearance_Setup_PickSelf"]) && objectTable.LocalPlayer != null) {
            character.Appearance = ReadAppearance((Character*)objectTable.LocalPlayer.Address);
        }

        ImGui.TableNextRow();

        ImGui.TableNextColumn();
        using (ImRaii.Disabled(targetManager.Target == null)) {
            if (DrawAppearanceSourceButton(FontAwesomeIcon.PersonRays, loc["ScenarioEditor_ActorData_Appearance_Setup_PickTarget"]) && targetManager.Target is ICharacter target) {
                character.Appearance = ReadAppearance((Character*)target.Address);
            }
        }

        ImGui.TableNextColumn();
        if (DrawAppearanceSourceButton(FontAwesomeIcon.SearchLocation, loc["ScenarioEditor_ActorData_Appearance_Setup_PickAllNpcs"])) {
            npcListPicker.OpenPopup();
        }
        if (npcListPicker.Popup(out var listedNpc)) {
            var newAppearance = new NpcAppearanceData();
            appearanceService.Read(listedNpc, newAppearance);

            character.Appearance = newAppearance;
        }

        ImGui.TableNextRow();

        _appearanceFileImportState.CheckState(out var fileImportIcon, out var fileImportColor);
        var importFileTooltip = loc["ScenarioEditor_ActorData_Appearance_Setup_ImportFile"];
        if (_appearanceFileImportState.Result.HasValue) {
            importFileTooltip = (bool)_appearanceFileImportState.Result ? loc["ScenarioEditor_ActorData_Appearance_Setup_ImportFile_Success"] : loc["ScenarioEditor_ActorData_Appearance_Setup_ImportFile_Failure"];
        }

        ImGui.TableNextColumn();
        if (DrawAppearanceSourceButton(fileImportIcon, importFileTooltip, fileImportColor)) {
            fileDialogManager.OpenFileDialog($"{loc["ScenarioEditor_ActorData_Appearance_Setup_ImportFile_Select"]}##arrpAppearanceFileSelector", "Character Files (.chara){.chara},All Files{.*}", (b, s) => {
                if (b && s.Count > 0) {
                    var appearanceData = appearanceDataParser.TryParseAppearanceFile(s[0]);
                    if (appearanceData != null) {
                        character.Appearance = appearanceData;
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
        if (DrawAppearanceSourceButton(dataImportIcon, importDataTooltip, dataImportColor)) {
            var clipboardContents = ImGui.GetClipboardText();
            if (!string.IsNullOrWhiteSpace(clipboardContents)) {
                var appearanceData = appearanceDataParser.TryParseAppearanceData(clipboardContents);
                if (appearanceData != null) {
                    character.Appearance = appearanceData;
                    _appearanceDataImportState.SetResult(true);
                } else {
                    _appearanceDataImportState.SetResult(false);
                }
            }
        }
    }

    private unsafe NpcAppearanceData ReadAppearance(Character* source) {
        var appearance = new NpcAppearanceData();
        appearanceService.Read(source, appearance);
        return appearance;
    }

    private static bool DrawAppearanceSourceButton(FontAwesomeIcon icon, string label, Vector4? color = null)
        => ImGuiComponents.IconButtonWithText(icon, label, size: new Vector2(ImGui.GetContentRegionAvail().X, 0), defaultColor: color);

    private void DrawVoiceRow(ArrpGuiForm form, NpcAppearanceData appearance) {
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

    private void DrawAppearanceRandomizer(ICharacterDefinition character) {
        ArrpGuiForm.Draw("##npcAppearanceEditorRandomizeForm", form => {
            form.StretchedRow(loc["ScenarioEditor_ActorData_Appearance_Randomize_Source"], () => {
                var locationName = territoryData.GetPlaceName(eventService.CurrentLocation.TerritoryType);
                var allNpcs = loc["ScenarioEditor_ActorData_Appearance_Randomize_Source_All"];

                using var combo = ImRaii.Combo("##npcAppearanceEditorRandomizeSource", _mixFromResidents ? locationName : allNpcs);
                if (!combo.Success)
                    return;

                if (ImGui.Selectable(locationName, _mixFromResidents)) {
                    _mixFromResidents = true;
                }

                if (ImGui.Selectable(allNpcs, !_mixFromResidents)) {
                    _mixFromResidents = false;
                }
            }, loc["ScenarioEditor_ActorData_Appearance_Randomize_Source_Desc"]);

            if (characterCreationData.IsCustomizable(character.Appearance)) {
                form.Row(loc["ScenarioEditor_ActorData_Appearance_Randomize_Lock"], () => {
                    var clanFollowsLocks = NpcAppearanceMixer.KeepsClan(_mixLocks & ~AppearanceMixLocks.Clan);
                    using (ImRaii.Disabled(clanFollowsLocks))
                        DrawMixLock(AppearanceMixLocks.Clan, clanFollowsLocks);

                    foreach (var mixLock in (AppearanceMixLocks[])[AppearanceMixLocks.Hair, AppearanceMixLocks.Face, AppearanceMixLocks.Body, AppearanceMixLocks.Outfit]) {
                        ImGui.SameLine();
                        DrawMixLock(mixLock);
                    }
                });
            }

            form.Row(string.Empty, () => {
                if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Dice, loc["ScenarioEditor_ActorData_Appearance_Randomize"])) {
                    var options = new AppearanceRandomizationOptions(_mixLocks, _mixFromResidents ? eventService.CurrentLocation.TerritoryType : null);
                    if (appearanceRandomizer.Randomize(character.Appearance, options) is { } mixed) {
                        character.Appearance = mixed;
                    }
                }
            });
        });
    }

    private void DrawMixLock(AppearanceMixLocks mixLock, bool forced = false) {
        var isLocked = forced || _mixLocks.HasFlag(mixLock);
        if (ImGui.Checkbox(loc[$"ScenarioEditor_ActorData_Appearance_Randomize_Lock_{mixLock}"], ref isLocked)) {
            _mixLocks = isLocked ? _mixLocks | mixLock : _mixLocks & ~mixLock;
        }
    }

    private static string ComposeActorName(bool usePrefix, string identifier)
        => string.IsNullOrWhiteSpace(identifier) ? ""
        : usePrefix ? ActorName.IntegrationPrefix + identifier
        : identifier;

    private static void SetStableActorName(ICharacterDefinition character, string name) {
        character.SetIntegrationProperty(IntegrationProvider.ActorNameConfigKey, name);
        if (string.IsNullOrWhiteSpace(name))
            character.SetIntegrationProperty(IntegrationProvider.ExternalAppearanceConfigKey, "");
    }

    private void DrawIntegrationTab(ICharacterDefinition character) {
        var currentName = character.AdditionalData.GetValueOrDefault(IntegrationProvider.ActorNameConfigKey, "");
        if (!ReferenceEquals(_actorNameOwner, character)) {
            _actorNameOwner = character;
            _actorNameUsePrefix = currentName.Length == 0 || ActorName.HasPrefix(currentName);
        }

        ArrpGuiForm.Draw("##npcIntegrationEditorForm", form => {
            form.Row(loc["ScenarioEditor_ActorData_Appearance_Integration_Input_ActorName"], () => {
                var usePrefix = _actorNameUsePrefix;
                if (ImGui.Checkbox("##npcIntegrationEditorGeneralLinkPrefix", ref usePrefix)) {
                    _actorNameUsePrefix = usePrefix;

                    var toggledIdentifier = usePrefix ? ActorName.Filter(currentName) : ActorName.WithoutPrefix(currentName);
                    currentName = ComposeActorName(usePrefix, toggledIdentifier);
                    SetStableActorName(character, currentName);
                }
                ImGui.SameLine();

                var currentIdenitfer = usePrefix ? ActorName.WithoutPrefix(currentName) : currentName;
                var nameFlags = usePrefix ? ImGuiInputTextFlags.CharsNoBlank : ImGuiInputTextFlags.None;
                var nameLimit = usePrefix ? ActorName.MaxPrefixedNameBytes : ActorName.MaxNameBytes;
                if (ImGui.InputTextEx("##npcIntegrationEditorGeneralLink", "", ref currentIdenitfer, maxLength: nameLimit, flags: nameFlags)) {
                    currentIdenitfer = ActorName.TruncateToBytes(usePrefix ? ActorName.Clean(currentIdenitfer) : currentIdenitfer.Trim(), nameLimit);
                    currentName = ComposeActorName(usePrefix, currentIdenitfer);
                    SetStableActorName(character, currentName);
                }
                ImGui.SameLine();
                if (ImGuiComponents.IconButton(FontAwesomeIcon.TrowelBricks)) {
                    _actorNameUsePrefix = true;
                    currentName = ActorName.IntegrationPrefix + characterCreationData.GetRandomName();
                    SetStableActorName(character, currentName);
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

            form.CheckboxRow(loc["ScenarioEditor_ActorData_Appearance_Integration_Input_ExternalAppearance"], IsExternallyManaged(character),
                isExternal => character.SetIntegrationProperty(IntegrationProvider.ExternalAppearanceConfigKey, isExternal ? "true" : ""),
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
