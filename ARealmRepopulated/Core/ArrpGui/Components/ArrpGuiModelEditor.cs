using ARealmRepopulated.Core.ArrpGui.Style;
using ARealmRepopulated.Core.l10n;
using ARealmRepopulated.Data.Appearance;
using ARealmRepopulated.Infrastructure;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using System.Numerics;

namespace ARealmRepopulated.Core.ArrpGui.Components;

/// <summary>
/// Edits what the games character creator offers. Aligned to the games own character editor.
/// </summary>
public class ArrpGuiModelEditor(
    ArrpNpcData npcData,
    ArrpCharacterCreationData creationData,
    ArrpHumanCmpFile humanCmp,
    ArrpGuiColorPicker colorPicker,
    ArrpGuiGameIcon gameIcon,
    ArrpTranslation loc) {

    private const string IconPopup = "##ArrpAppearanceIcons";

    private static readonly NpcBodyType[] BodyTypes = [NpcBodyType.Normal, NpcBodyType.Old, NpcBodyType.Young];

    private static Vector2 IconSize => new(ImGui.GetFrameHeight() * 2);

    public void Draw(NpcAppearanceData appearance) {
        var clan = creationData.GetClan(appearance.Tribe, appearance.Sex);
        if (clan == null || !creationData.IsCustomizable(appearance)) {
            using (ImRaii.Disabled())
                ImGui.TextWrapped(loc["ScenarioEditor_ActorData_Appearance_NoCustomize"]);
            return;
        }

        ArrpGuiLayout.SectionHeader(loc["ScenarioEditor_ActorData_Appearance_Section_Identity"]);
        ArrpGuiForm.Draw("##arrpAppearanceModelIdentity", form => {
            DrawClanRow(form, appearance, clan);
            DrawGenderRow(form, appearance);
            DrawBodyTypeRow(form, appearance, clan);
        });

        foreach (var section in Enum.GetValues<ModelSection>()) {
            var menus = clan.Menus.Where(m => GetSection(m) == section).ToList();
            if (menus.Count == 0)
                continue;

            ArrpGuiLayout.SectionHeader(loc[$"ScenarioEditor_ActorData_Appearance_Section_{section}"]);
            ArrpGuiForm.Draw($"##arrpAppearanceModel{section}", form => {
                var attachedColors = menus.Where(m => GetColorTarget(m, menus) != null).ToList();
                foreach (var menu in menus.Except(attachedColors)) {
                    DrawMenuRow(form, appearance, clan, menu, attachedColors.FirstOrDefault(c => GetColorTarget(c, menus) == menu));
                }
            });
        }
    }

    private static CharacterEditorMenu? GetColorTarget(CharacterEditorMenu color, List<CharacterEditorMenu> menus) {
        CustomizeIndex? owner = color.Customize switch {
            CustomizeIndex.HairColor => CustomizeIndex.HairStyle,
            CustomizeIndex.LipColorFurPattern when color.Kind == CharacterEditorMenuKind.Color => CustomizeIndex.Mouth,
            CustomizeIndex.FacialFeaturesColor => CustomizeIndex.FacialFeatures,
            CustomizeIndex.FacePaintColor => CustomizeIndex.FacePaint,
            _ => null
        };
        return menus.FirstOrDefault(m => m.Customize == owner);
    }

    private enum ModelSection {
        Body,
        Hair,
        Face,
        Eyes,
        Mouth,
        Markings
    }

    private static ModelSection GetSection(CharacterEditorMenu menu) => menu.Customize switch {
        CustomizeIndex.HairStyle or CustomizeIndex.HairColor => ModelSection.Hair,
        CustomizeIndex.Face or CustomizeIndex.Jaw or CustomizeIndex.Eyebrows or CustomizeIndex.Nose => ModelSection.Face,
        CustomizeIndex.EyeShape or CustomizeIndex.EyeColorRight => ModelSection.Eyes,
        CustomizeIndex.Mouth => ModelSection.Mouth,

        // the same byte is the lip color for most clans ... and the fur pattern for Hrothgar -.-
        CustomizeIndex.LipColorFurPattern => menu.Kind == CharacterEditorMenuKind.Color ? ModelSection.Mouth : ModelSection.Body,
        CustomizeIndex.FacialFeatures => menu.FirstFeatureBit == 0 ? ModelSection.Face : ModelSection.Markings,
        CustomizeIndex.FacialFeaturesColor or CustomizeIndex.FacePaint or CustomizeIndex.FacePaintColor => ModelSection.Markings,
        _ => ModelSection.Body
    };

    private void DrawClanRow(ArrpGuiForm form, NpcAppearanceData appearance, CharacterEditorClan clan) {
        form.StretchedRow(loc["ScenarioEditor_ActorData_Appearance_Clan"], () => {
            using var combo = ImRaii.Combo("##clan", clan.Name);
            if (!combo.Success)
                return;

            foreach (var option in creationData.GetClans().Where(r => r.Gender == appearance.Sex)) {
                if (ImGui.Selectable(option.Name, option.Tribe == appearance.Tribe) && option.Tribe != appearance.Tribe) {
                    option.ApplyDefaults(appearance);
                }
            }
        }, loc["ScenarioEditor_ActorData_Appearance_Clan_Desc"]);
    }

    private void DrawGenderRow(ArrpGuiForm form, NpcAppearanceData appearance) {
        form.Row(loc["ScenarioEditor_ActorData_Appearance_Gender"], () => {
            foreach (var gender in Enum.GetValues<NpcSex>()) {
                if (ImGui.RadioButton(loc[$"ScenarioEditor_ActorData_Appearance_Gender_{gender}"], appearance.Sex == gender) && appearance.Sex != gender) {
                    creationData.GetClan(appearance.Tribe, gender)?.ApplyDefaults(appearance);
                }
                ImGui.SameLine();
            }
            ImGui.NewLine();
        });
    }

    private void DrawBodyTypeRow(ArrpGuiForm form, NpcAppearanceData appearance, CharacterEditorClan clan) {
        form.Row(loc["ScenarioEditor_ActorData_Appearance_BodyType"], () => {
            foreach (var bodyType in BodyTypes) {
                using (ImRaii.Disabled(!npcData.IsBodyTypeAvailable(appearance.Tribe, appearance.Sex, bodyType))) {
                    if (ImGui.RadioButton(loc[$"ScenarioEditor_ActorData_Appearance_BodyType_{bodyType}"], appearance.BodyType == bodyType) && appearance.BodyType != bodyType) {
                        SetBodyType(appearance, clan, bodyType);
                    }
                }
                ImGui.SameLine();
            }
            ImGui.NewLine();
        }, loc["ScenarioEditor_ActorData_Appearance_BodyType_Desc"]);
    }

    private void SetBodyType(NpcAppearanceData appearance, CharacterEditorClan clan, NpcBodyType bodyType) {
        appearance.ResetModel();
        appearance.BodyType = bodyType;
        clan.FitOptions(appearance, npcData.GetBodyTypeLooks(appearance));
    }

    private void DrawMenuRow(ArrpGuiForm form, NpcAppearanceData appearance, CharacterEditorClan clan, CharacterEditorMenu menu, CharacterEditorMenu? attachedColor) {
        void DrawAttachedColor() {
            if (attachedColor != null) {
                ImGui.SameLine();
                DrawColors(appearance, clan, attachedColor);
            }
        }

        switch (menu.Kind) {
            case CharacterEditorMenuKind.Slider:
                form.StretchedRow(menu.Name, () => DrawSlider(appearance, menu));
                break;
            case CharacterEditorMenuKind.Options:
                form.Row(menu.Name, () => {
                    DrawOptions(appearance, menu);
                    DrawAttachedColor();
                    DrawRowOptionCheckbox(appearance, menu);
                });
                break;
            case CharacterEditorMenuKind.Icons:
                form.Row(menu.Name, () => {
                    DrawIcons(appearance, menu);
                    DrawAttachedColor();
                    DrawRowOptionCheckbox(appearance, menu);
                });
                break;
            case CharacterEditorMenuKind.Color:
            case CharacterEditorMenuKind.EyeColor:
                form.Row(menu.Name, () => DrawColors(appearance, clan, menu));
                break;
            case CharacterEditorMenuKind.FacialFeatures:
                form.Row(menu.Name, () => {
                    DrawFacialFeatures(appearance, clan, menu);
                    DrawAttachedColor();
                    DrawRowOptionCheckbox(appearance, menu);
                });
                break;
        }
    }

    private void DrawRowOptionCheckbox(NpcAppearanceData appearance, CharacterEditorMenu menu) {
        if (menu.Customize == CustomizeIndex.FacePaint) {
            DrawOptionCheckbox(appearance, CustomizeIndex.FacePaint, loc["ScenarioEditor_ActorData_Appearance_FacePaintFlip"]);
        } else if (menu.Customize == CustomizeIndex.EyeShape) {
            DrawOptionCheckbox(appearance, CustomizeIndex.EyeShape, loc["ScenarioEditor_ActorData_Appearance_SmallIris"]);
        } else if (menu.Customize == CustomizeIndex.FacialFeatures && menu.FirstFeatureBit > 0) {
            DrawOptionCheckbox(appearance, CustomizeIndex.FacialFeatures, loc["ScenarioEditor_ActorData_Appearance_LegacyTattoo"]);
        }
    }

    private void DrawColors(NpcAppearanceData appearance, CharacterEditorClan clan, CharacterEditorMenu menu) {
        if (menu.Kind == CharacterEditorMenuKind.EyeColor) {
            DrawEyeColor(appearance, CustomizeIndex.EyeColorLeft, menu, loc["ScenarioEditor_ActorData_Appearance_EyeColor_Left"]);
            ImGui.SameLine(0f, ArrpGuiSpacing.ButtonGroupSpacing);
            DrawEyeColor(appearance, CustomizeIndex.EyeColorRight, menu, loc["ScenarioEditor_ActorData_Appearance_EyeColor_Right"]);
            return;
        }

        DrawColor(appearance, menu.Customize, menu.Palettes, menu.Name);
        if (menu.Customize == CustomizeIndex.LipColorFurPattern) {
            DrawOptionCheckbox(appearance, CustomizeIndex.Mouth, loc["ScenarioEditor_ActorData_Appearance_Lipstick"]);
        }
        if (menu.Customize == CustomizeIndex.HairColor) {
            var highlights = loc["ScenarioEditor_ActorData_Appearance_Highlights"];
            ImGui.SameLine();
            DrawColor(appearance, CustomizeIndex.HighlightsColor, clan.HighlightPalettes, highlights);
            DrawOptionCheckbox(appearance, CustomizeIndex.Highlights, highlights);
        }
    }

    private static void DrawSlider(NpcAppearanceData appearance, CharacterEditorMenu menu) {
        var value = (int)appearance.GetOption(menu.Customize);
        if (ImGui.SliderInt("##value", ref value, 0, 100)) {
            appearance.SetOption(menu.Customize, (byte)value);
        }
    }

    private void DrawOptions(NpcAppearanceData appearance, CharacterEditorMenu menu) {
        var current = appearance.GetOption(menu.Customize);
        var options = menu.GetBodyTypes(npcData.GetBodyTypeLooks(appearance));
        for (var i = 0; i < options.Length; i++) {
            var option = options[i];
            if (ImGui.RadioButton($"{i + 1}##option{option.Value}", current == option.Value)) {
                appearance.SetOption(menu.Customize, option.Value);
            }
            if (i + 1 < options.Length) {
                ImGui.SameLine();
            }
        }
    }

    private void DrawIcons(NpcAppearanceData appearance, CharacterEditorMenu menu) {
        var current = appearance.GetOption(menu.Customize);
        var size = IconSize;

        var looks = npcData.GetBodyTypeLooks(appearance);
        var options = menu.GetBodyTypes(looks);
        var selected = options.FirstOrDefault(o => o.Value == current);
        if (gameIcon.DrawOption("##current", selected with { Value = current }, size)) {
            ImGui.OpenPopup(IconPopup);
        }

        using var popup = ArrpGuiLayout.Popup(IconPopup);
        if (!popup.Success)
            return;

        var specialOptions = menu.GetSpecialBodyTypes(looks, current);
        if (specialOptions.Length == 0) {
            DrawIconOptions(appearance, menu, options, size);
            return;
        }

        ArrpGuiLayout.Tabs(
            "##optionTabs",
            [
                new(loc["ScenarioEditor_ActorData_Appearance_Options_Standard"], () => DrawIconOptions(appearance, menu, options, size)),
                new(loc["ScenarioEditor_ActorData_Appearance_Options_Special"], () => DrawIconOptions(appearance, menu, specialOptions, size))
            ],
            specialOptions.Any(o => o.Value == current) ? 1 : 0);
    }

    private void DrawIconOptions(NpcAppearanceData appearance, CharacterEditorMenu menu, CharacterEditorOption[] options, Vector2 size) {
        if (gameIcon.DrawGrid(options, 6, size) is { } picked) {
            appearance.SetOption(menu.Customize, picked);
            ImGui.CloseCurrentPopup();
        }
    }

    private static void DrawOptionCheckbox(NpcAppearanceData appearance, CustomizeIndex index, string label) {
        ImGui.SameLine();

        var enabled = appearance.IsOptionEnabled(index);
        if (ImGui.Checkbox(label, ref enabled)) {
            appearance.SetOptionEnabled(index, enabled);
        }
    }

    private void DrawEyeColor(NpcAppearanceData appearance, CustomizeIndex index, CharacterEditorMenu menu, string label) {
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(label);
        ImGui.SameLine();
        DrawColor(appearance, index, menu.Palettes, label);
    }

    private void DrawColor(NpcAppearanceData appearance, CustomizeIndex index, CharacterEditorColorPalette[] palettes, string name) {
        using var id = ImRaii.PushId((int)index);

        var value = appearance.GetOption(index);
        var paletteColor = humanCmp.Colors?.GetColor(index, appearance.Tribe, appearance.Sex, value) ?? default;
        var exact = appearance.ExtendedAppearance?.GetColor(index);
        var color = exact is { } exactColor ? new Vector4(exactColor.X, exactColor.Y, exactColor.Z, 1f) : paletteColor.Color;

        var tooltip = exact.HasValue ? $"{name}\n{loc["ScenarioEditor_ActorData_Appearance_ExactColor"]}" : name;
        string[]? paletteLabels = index is CustomizeIndex.LipColorFurPattern or CustomizeIndex.FacePaintColor
            ? [loc["ScenarioEditor_ActorData_Appearance_Palette_Strong"], loc["ScenarioEditor_ActorData_Appearance_Palette_Light"]]
            : null;
        var pick = colorPicker.Draw(tooltip, exact.HasValue ? color : paletteColor.Swatch, new Vector3(color.X, color.Y, color.Z), value, palettes, paletteLabels, NpcExtendedAppearance.SupportsColor(index), () => DrawColorExtras(appearance, index));

        if (pick?.PaletteValue is { } paletteValue) {
            appearance.SetOption(index, paletteValue);
        } else if (pick?.ExactColor is { } pickedColor) {

            // without the file there is no opacity to start from
            appearance.SetExactColor(index, pickedColor, paletteColor.Color.W > 0f ? paletteColor.Color.W : 1f);
        }
    }

    private void DrawColorExtras(NpcAppearanceData appearance, CustomizeIndex index) {
        var extended = appearance.ExtendedAppearance;
        if (index == CustomizeIndex.LipColorFurPattern && extended?.MouthColor is { } mouth) {
            var opacity = mouth.W;
            if (ArrpGuiColorPicker.DrawExtraSlider(loc["ScenarioEditor_ActorData_Appearance_ExactColor_LipOpacity"], ref opacity, 1f)) {
                mouth.W = opacity;
                extended.MouthColor = mouth;
            }
        }

        if (index == CustomizeIndex.SkinColor) {
            // the shader keeps it in the same value as the skin color
            var muscleTone = extended?.MuscleTone
                ?? humanCmp.Heights?.GetMuscleTone(appearance.Race, appearance.Tribe, appearance.Sex, appearance.BodyType, appearance.GetOption(CustomizeIndex.MuscleMass))
                ?? 1f;
            if (ArrpGuiColorPicker.DrawExtraSlider(loc["ScenarioEditor_ActorData_Appearance_ExactColor_MuscleTone"], ref muscleTone, 2.5f)) {
                appearance.ExtendedAppearance ??= new NpcExtendedAppearance();
                appearance.ExtendedAppearance.MuscleTone = muscleTone;
            }
        }
    }

    private void DrawFacialFeatures(NpcAppearanceData appearance, CharacterEditorClan clan, CharacterEditorMenu menu) {

        // faces count from 1, the icons from 0
        var faceIndex = appearance.GetOption(CustomizeIndex.Face) - 1;
        var icons = faceIndex >= 0 && faceIndex < clan.FacialFeatureIcons.Length ? clan.FacialFeatureIcons[faceIndex] : [];
        var size = IconSize;

        for (var bit = menu.FirstFeatureBit; bit < menu.FirstFeatureBit + menu.FeatureCount; bit++) {
            var isOn = appearance.HasFacialFeature(bit);
            var icon = bit < icons.Length ? icons[bit] : 0;
            if (gameIcon.DrawToggle($"##feature{bit}", icon, (bit - menu.FirstFeatureBit + 1).ToString(), size, isOn)) {
                appearance.SetFacialFeature(bit, !isOn);
            }

            if (bit + 1 < menu.FirstFeatureBit + menu.FeatureCount) {
                ImGui.SameLine();
            }
        }
    }
}
