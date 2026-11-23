using ARealmRepopulated.Core.l10n;
using ARealmRepopulated.Data.Appearance;
using ARealmRepopulated.Infrastructure;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Lumina.Excel.Sheets;
using System.Numerics;

namespace ARealmRepopulated.Core.ArrpGui.Components;

/// <summary>
/// Provides the UI for editing the equipment of our actors.
/// </summary>
public class ArrpGuiEquipmentEditor(
    ArrpItemData itemData,
    ArrpGuiItemPicker itemPicker,
    ArrpGuiDyePicker dyePicker,
    ArrpGuiGameIcon gameIcon,
    ArrpTranslation loc) {

    private const int DyeChannels = 2;

    public void Draw(NpcAppearanceData appearance) {
        ArrpGuiForm.Draw("##arrpAppearanceEquipmentForm", form => {
            DrawWeaponRow(form, loc["ScenarioEditor_ActorData_Appearance_EMainHand"], ItemSlots.MainHand, appearance, () => appearance.MainHand, model => appearance.MainHand = model);
            DrawWeaponRow(form, loc["ScenarioEditor_ActorData_Appearance_EOffHand"], ItemSlots.OffHand, appearance, () => appearance.OffHand, model => appearance.OffHand = model);

            DrawEquipmentRow(form, loc["ScenarioEditor_ActorData_Appearance_EHeadgear"], ItemSlots.Head, appearance, () => appearance.HeadGear, model => appearance.HeadGear = model);
            DrawEquipmentRow(form, loc["ScenarioEditor_ActorData_Appearance_EBody"], ItemSlots.Body, appearance, () => appearance.Body, model => appearance.Body = model);
            DrawEquipmentRow(form, loc["ScenarioEditor_ActorData_Appearance_EHands"], ItemSlots.Hands, appearance, () => appearance.Hands, model => appearance.Hands = model);
            DrawEquipmentRow(form, loc["ScenarioEditor_ActorData_Appearance_ELegs"], ItemSlots.Legs, appearance, () => appearance.Legs, model => appearance.Legs = model);
            DrawEquipmentRow(form, loc["ScenarioEditor_ActorData_Appearance_EFeet"], ItemSlots.Feet, appearance, () => appearance.Feet, model => appearance.Feet = model);
            DrawEquipmentRow(form, loc["ScenarioEditor_ActorData_Appearance_EEars"], ItemSlots.Ears, appearance, () => appearance.Ears, model => appearance.Ears = model);
            DrawEquipmentRow(form, loc["ScenarioEditor_ActorData_Appearance_ENeck"], ItemSlots.Neck, appearance, () => appearance.Neck, model => appearance.Neck = model);
            DrawEquipmentRow(form, loc["ScenarioEditor_ActorData_Appearance_EWrist"], ItemSlots.Wrists, appearance, () => appearance.Wrists, model => appearance.Wrists = model);
            DrawEquipmentRow(form, loc["ScenarioEditor_ActorData_Appearance_ERingLeft"], ItemSlots.LeftRing, appearance, () => appearance.LeftRing, model => appearance.LeftRing = model);
            DrawEquipmentRow(form, loc["ScenarioEditor_ActorData_Appearance_ERingRight"], ItemSlots.RightRing, appearance, () => appearance.RightRing, model => appearance.RightRing = model);

            DrawGlassesRow(form, appearance);

            form.Row(string.Empty, () => {
                using var table = ImRaii.Table("##equipmentToggles", 2, ImGuiTableFlags.NoSavedSettings | ImGuiTableFlags.SizingStretchSame | ImGuiTableFlags.NoPadOuterX);
                if (!table.Success)
                    return;

                var hideWeapons = appearance.HideWeapons;
                ImGui.TableNextColumn();
                if (ImGui.Checkbox(loc["ScenarioEditor_ActorData_Appearance_CWeaponsHidden"], ref hideWeapons))
                    appearance.HideWeapons = hideWeapons;

                var hideHeadgear = appearance.HideHeadgear;
                ImGui.TableNextColumn();
                if (ImGui.Checkbox(loc["ScenarioEditor_ActorData_Appearance_CHeadgearHidden"], ref hideHeadgear))
                    appearance.HideHeadgear = hideHeadgear;

                var closeVisor = appearance.CloseVisor;
                ImGui.TableNextColumn();
                if (ImGui.Checkbox(loc["ScenarioEditor_ActorData_Appearance_CloseVisor"], ref closeVisor))
                    appearance.CloseVisor = closeVisor;

                if (appearance.Race != NpcRace.Viera)
                    return;

                var hideEars = appearance.HideVieraEars;
                ImGui.TableNextColumn();
                if (ImGui.Checkbox(loc["ScenarioEditor_ActorData_Appearance_HideVieraEars"], ref hideEars))
                    appearance.HideVieraEars = hideEars;
                ArrpGuiLayout.Tooltip(loc["ScenarioEditor_ActorData_Appearance_HideVieraEars_Desc"]);
            });
        });
    }

    private void DrawWeaponRow(ArrpGuiForm form, string label, ItemSlots slot, NpcAppearanceData appearance, Func<WeaponModel?> getSelectedWeaponModel, Action<WeaponModel?> setSelectedWeaponModel) {
        form.Row(label, () => {
            var model = getSelectedWeaponModel();
            var item = itemData.FindItem(slot, model?.ModelSetId ?? 0, model?.Base ?? 0, model?.Variant ?? 0);

            var modelKey = model?.ToModelKey() ?? 0;
            if (DrawItemButton(item?.Icon ?? 0, DescribeItem(slot, item, modelKey), DyeChannels)) {
                itemPicker.OpenPopup();
            }
            if (itemPicker.Popup(slot, modelKey, out var picked)) {
                setSelectedWeaponModel(picked is { } weapon ? new WeaponModel(weapon.ModelKey) : null);
                appearance.HideWeapons = appearance.HideWeapons && picked == null;
                if (slot == ItemSlots.MainHand) {
                    if (picked is { OffHandModelKey: not 0 } pair) {
                        appearance.OffHand = new WeaponModel(pair.OffHandModelKey);
                    } else if (item is { ModelSub: not 0 }) {
                        appearance.OffHand = null;
                    }
                }
            }

            using var disabled = ImRaii.Disabled(model == null || model.ModelSetId == 0);
            var stain0 = model?.Stain0 ?? 0;
            var stain1 = model?.Stain1 ?? 0;
            if (DrawDyes(ref stain0, ref stain1) && model != null) {
                setSelectedWeaponModel(model with { Stain0 = stain0, Stain1 = stain1 });
            }
        });
    }

    private void DrawEquipmentRow(ArrpGuiForm form, string label, ItemSlots slot, NpcAppearanceData appearance, Func<EquipmentModel?> getSelectedEquipmentModel, Action<EquipmentModel?> setSelectedEquipmentModel) {
        form.Row(label, () => {
            var model = getSelectedEquipmentModel();
            var item = itemData.FindItem(slot, 0, model?.ModelId ?? 0, model?.Variant ?? 0);

            var modelKey = model?.ToModelKey() ?? 0;
            if (DrawItemButton(item?.Icon ?? 0, DescribeItem(slot, item, modelKey), DyeChannels)) {
                itemPicker.OpenPopup();
            }
            if (itemPicker.Popup(slot, modelKey, out var picked)) {
                setSelectedEquipmentModel(picked is { } equipment ? new EquipmentModel(equipment.ModelKey) : new EquipmentModel());
                if (slot == ItemSlots.Head) {
                    appearance.HideHeadgear = appearance.HideHeadgear && picked == null;
                }
            }

            using var disabled = ImRaii.Disabled(model == null || model.ModelId == 0);
            var stain0 = model?.Stain0 ?? 0;
            var stain1 = model?.Stain1 ?? 0;
            if (DrawDyes(ref stain0, ref stain1) && model != null) {
                setSelectedEquipmentModel(model with { Stain0 = stain0, Stain1 = stain1 });
            }
        });
    }

    private void DrawGlassesRow(ArrpGuiForm form, NpcAppearanceData appearance) {
        form.Row(loc["ScenarioEditor_ActorData_Appearance_EGlasses"], () => {
            var current = appearance.Glasses ?? 0;

            if (DrawItemButton((uint)(itemData.GetGlasses(current)?.Icon ?? 0), DescribeGlasses(appearance.Glasses), 0)) {
                itemPicker.OpenPopup();
            }
            if (itemPicker.Popup(ItemSlots.Glasses, current, out var picked)) {
                appearance.Glasses = (ushort)(picked?.ModelKey ?? 0);
            }
        });
    }

    private bool DrawItemButton(uint icon, string label, int dyeButtons) {
        var iconSize = new Vector2(ImGui.GetFrameHeight());
        if (icon != 0) {
            gameIcon.Draw(icon, iconSize);
        } else {
            ImGui.Dummy(iconSize);
        }
        ImGui.SameLine();

        var dyeWidth = dyeButtons * (ImGui.GetFrameHeight() + ImGui.GetStyle().ItemSpacing.X);
        return ImGui.Button($"{label}##item", new Vector2(ImGui.GetContentRegionAvail().X - dyeWidth, 0));
    }

    private bool DrawDyes(ref byte stain0, ref byte stain1) {
        var changed = DrawDye("##dye0", ref stain0);
        changed |= DrawDye("##dye1", ref stain1);
        return changed;
    }

    private bool DrawDye(string id, ref byte stain) {
        ImGui.SameLine();
        using var scope = ImRaii.PushId(id);

        var stainRow = itemData.GetStain(stain);
        if (ImGui.ColorButton("##dye", ArrpGuiDyePicker.GetColor(stainRow), ImGuiColorEditFlags.NoTooltip)) {
            dyePicker.OpenPopup();
        }
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled)) {
            ImGui.SetTooltip(stain == 0 || stainRow == null ? loc["ArrpGuiPicker_Dye_None"] : stainRow.Value.Name.ToString());
        }

        if (!dyePicker.Popup(out var picked))
            return false;

        stain = picked;
        return true;
    }

    private string DescribeItem(ItemSlots slot, Item? item, ulong modelKey) {
        if (item is { } known)
            return known.Name.ToString();

        return modelKey != 0 ? ArrpGuiItemPicker.DescribeModel(slot, modelKey) : loc["ArrpGuiPicker_Item_None"];
    }

    private string DescribeGlasses(ushort? glassesId) {
        if (glassesId is null or 0)
            return loc["ArrpGuiPicker_Item_None"];

        var glassesName = itemData.GetGlasses(glassesId.Value)?.Name.ToString() ?? "";
        return string.IsNullOrWhiteSpace(glassesName) ? $"(?) {glassesId}" : glassesName;
    }
}
