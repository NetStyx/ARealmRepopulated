using ARealmRepopulated.Data.Appearance;
using ARealmRepopulated.Infrastructure;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using Lumina.Excel.Sheets;
using static FFXIVClientStructs.FFXIV.Client.Game.Character.DrawDataContainer;
using static FFXIVClientStructs.FFXIV.Client.Game.Control.EmoteController;

namespace ARealmRepopulated.Core.Services.Npcs;

public unsafe class NpcAppearanceService(IObjectTable objectTable, IPluginLog log, ArrpDataCache dataCache) {

    public enum Animations : ushort {
        None = 0,
        Idle = 3,
        IdleArmed = 34,
        Walking = 13,
        WalkingArmed = 41,
        Running = 22,
        RunningArmed = 50
    }

    public void Apply(Character* chara, NpcAppearanceData file) {

        chara->ModelContainer.ModelCharaId = file.ModelCharaId;
        chara->ModelContainer.ModelSkeletonId = file.ModelSkeletonId;

        chara->DrawData.CustomizeData.Data[(int)CustomizeIndex.Race] = (byte)file.Race;
        chara->DrawData.CustomizeData.Data[(int)CustomizeIndex.Sex] = (byte)file.Sex;
        chara->DrawData.CustomizeData.Data[(int)CustomizeIndex.BodyType] = (byte)file.BodyType;
        chara->DrawData.CustomizeData.Data[(int)CustomizeIndex.Height] = file.Height.GetValueOrDefault();
        chara->DrawData.CustomizeData.Data[(int)CustomizeIndex.Tribe] = (byte)file.Tribe;
        chara->DrawData.CustomizeData.Data[(int)CustomizeIndex.Face] = file.Face.GetValueOrDefault();
        chara->DrawData.CustomizeData.Data[(int)CustomizeIndex.HairStyle] = file.HairStyle.GetValueOrDefault();
        chara->DrawData.CustomizeData.Data[(int)CustomizeIndex.Highlights] = file.Highlights.GetValueOrDefault();
        chara->DrawData.CustomizeData.Data[(int)CustomizeIndex.SkinColor] = file.SkinColor.GetValueOrDefault();
        chara->DrawData.CustomizeData.Data[(int)CustomizeIndex.EyeColorRight] = file.EyeColorRight.GetValueOrDefault();
        chara->DrawData.CustomizeData.Data[(int)CustomizeIndex.HairColor] = file.HairColor.GetValueOrDefault();
        chara->DrawData.CustomizeData.Data[(int)CustomizeIndex.HighlightsColor] = file.HighlightsColor.GetValueOrDefault();
        chara->DrawData.CustomizeData.Data[(int)CustomizeIndex.FacialFeatures] = file.FacialFeatures.GetValueOrDefault();
        chara->DrawData.CustomizeData.Data[(int)CustomizeIndex.FacialFeaturesColor] = file.TattooColor.GetValueOrDefault();
        chara->DrawData.CustomizeData.Data[(int)CustomizeIndex.Eyebrows] = file.Eyebrows.GetValueOrDefault();
        chara->DrawData.CustomizeData.Data[(int)CustomizeIndex.EyeColorLeft] = file.EyeColorLeft.GetValueOrDefault();
        chara->DrawData.CustomizeData.Data[(int)CustomizeIndex.EyeShape] = file.EyeShape.GetValueOrDefault();
        chara->DrawData.CustomizeData.Data[(int)CustomizeIndex.Nose] = file.Nose.GetValueOrDefault();
        chara->DrawData.CustomizeData.Data[(int)CustomizeIndex.Jaw] = file.Jaw.GetValueOrDefault();
        chara->DrawData.CustomizeData.Data[(int)CustomizeIndex.Lipstick] = file.Lipstick.GetValueOrDefault();
        chara->DrawData.CustomizeData.Data[(int)CustomizeIndex.LipColorFurPattern] = file.LipColorFurPattern.GetValueOrDefault();
        chara->DrawData.CustomizeData.Data[(int)CustomizeIndex.MuscleMass] = file.MuscleMass.GetValueOrDefault();
        chara->DrawData.CustomizeData.Data[(int)CustomizeIndex.TailShape] = file.TailShape.GetValueOrDefault();
        chara->DrawData.CustomizeData.Data[(int)CustomizeIndex.BustSize] = file.BustSize.GetValueOrDefault();
        chara->DrawData.CustomizeData.Data[(int)CustomizeIndex.FacePaint] = file.FacePaint.GetValueOrDefault();
        chara->DrawData.CustomizeData.Data[(int)CustomizeIndex.FacePaintColor] = file.FacePaintColor.GetValueOrDefault();

        file.MainHand?.Apply(chara, isMainHand: true);
        file.OffHand?.Apply(chara, isMainHand: false);

        file.HeadGear?.Apply(chara, EquipmentSlot.Head);
        file.Body?.Apply(chara, EquipmentSlot.Body);
        file.Hands?.Apply(chara, EquipmentSlot.Hands);
        file.Legs?.Apply(chara, EquipmentSlot.Legs);
        file.Feet?.Apply(chara, EquipmentSlot.Feet);
        file.Ears?.Apply(chara, EquipmentSlot.Ears);
        file.Neck?.Apply(chara, EquipmentSlot.Neck);
        file.Wrists?.Apply(chara, EquipmentSlot.Wrists);
        file.LeftRing?.Apply(chara, EquipmentSlot.LFinger);
        file.RightRing?.Apply(chara, EquipmentSlot.RFinger);

        chara->Scale = file.Scale ?? 1f;

        chara->DrawData.HideWeapons(file.HideWeapons);
        chara->DrawData.HideHeadgear(0, file.HideHeadgear);
        /*
        if (!(&chara->DrawData.CustomizeData)->NormalizeCustomizeData(&chara->DrawData.CustomizeData))
        {
            chara->DrawData.CustomizeData = new CustomizeData();
        }*/
    }

    public void Read(Character* chara, NpcAppearanceData file) {

        file.AppearanceId = Guid.NewGuid();

        file.ModelCharaId = chara->ModelContainer.ModelCharaId;
        file.ModelSkeletonId = chara->ModelContainer.ModelSkeletonId;

        file.Race = (NpcRace)chara->DrawData.CustomizeData.Data[(int)CustomizeIndex.Race];
        file.Sex = (NpcSex)chara->DrawData.CustomizeData.Data[(int)CustomizeIndex.Sex];
        file.BodyType = (NpcBodyType)chara->DrawData.CustomizeData.Data[(int)CustomizeIndex.BodyType];
        file.Height = chara->DrawData.CustomizeData.Data[(int)CustomizeIndex.Height];
        file.Tribe = (NpcTribe)chara->DrawData.CustomizeData.Data[(int)CustomizeIndex.Tribe];
        file.Face = chara->DrawData.CustomizeData.Data[(int)CustomizeIndex.Face];
        file.HairStyle = chara->DrawData.CustomizeData.Data[(int)CustomizeIndex.HairStyle];
        file.Highlights = chara->DrawData.CustomizeData.Data[(int)CustomizeIndex.Highlights];
        file.SkinColor = chara->DrawData.CustomizeData.Data[(int)CustomizeIndex.SkinColor];
        file.EyeColorRight = chara->DrawData.CustomizeData.Data[(int)CustomizeIndex.EyeColorRight];
        file.HairColor = chara->DrawData.CustomizeData.Data[(int)CustomizeIndex.HairColor];
        file.HighlightsColor = chara->DrawData.CustomizeData.Data[(int)CustomizeIndex.HighlightsColor];
        file.FacialFeatures = chara->DrawData.CustomizeData.Data[(int)CustomizeIndex.FacialFeatures];
        file.TattooColor = chara->DrawData.CustomizeData.Data[(int)CustomizeIndex.FacialFeaturesColor];
        file.Eyebrows = chara->DrawData.CustomizeData.Data[(int)CustomizeIndex.Eyebrows];
        file.EyeColorLeft = chara->DrawData.CustomizeData.Data[(int)CustomizeIndex.EyeColorLeft];
        file.EyeShape = chara->DrawData.CustomizeData.Data[(int)CustomizeIndex.EyeShape];
        file.Nose = chara->DrawData.CustomizeData.Data[(int)CustomizeIndex.Nose];
        file.Jaw = chara->DrawData.CustomizeData.Data[(int)CustomizeIndex.Jaw];
        file.Lipstick = chara->DrawData.CustomizeData.Data[(int)CustomizeIndex.Lipstick];
        file.LipColorFurPattern = chara->DrawData.CustomizeData.Data[(int)CustomizeIndex.LipColorFurPattern];
        file.MuscleMass = chara->DrawData.CustomizeData.Data[(int)CustomizeIndex.MuscleMass];
        file.TailShape = chara->DrawData.CustomizeData.Data[(int)CustomizeIndex.TailShape];
        file.BustSize = chara->DrawData.CustomizeData.Data[(int)CustomizeIndex.BustSize];
        file.FacePaint = chara->DrawData.CustomizeData.Data[(int)CustomizeIndex.FacePaint];
        file.FacePaintColor = chara->DrawData.CustomizeData.Data[(int)CustomizeIndex.FacePaintColor];

        file.HideWeapons = chara->DrawData.IsWeaponHidden;
        file.HideHeadgear = chara->DrawData.IsHatHidden;

        file.MainHand = WeaponModel.Read(chara, WeaponSlot.MainHand);
        file.OffHand = WeaponModel.Read(chara, WeaponSlot.OffHand);

        file.HeadGear = EquipmentModel.Read(chara, EquipmentSlot.Head);
        file.Body = EquipmentModel.Read(chara, EquipmentSlot.Body);
        file.Hands = EquipmentModel.Read(chara, EquipmentSlot.Hands);
        file.Legs = EquipmentModel.Read(chara, EquipmentSlot.Legs);
        file.Feet = EquipmentModel.Read(chara, EquipmentSlot.Feet);
        file.Ears = EquipmentModel.Read(chara, EquipmentSlot.Ears);
        file.Neck = EquipmentModel.Read(chara, EquipmentSlot.Neck);
        file.Wrists = EquipmentModel.Read(chara, EquipmentSlot.Wrists);
        file.LeftRing = EquipmentModel.Read(chara, EquipmentSlot.LFinger);
        file.RightRing = EquipmentModel.Read(chara, EquipmentSlot.RFinger);

        file.Scale = chara->Scale;
    }

    public void Read(BNpcBase npcBase, NpcAppearanceData file) {

        file.Scale = npcBase.Scale;

        // need to figure out how the modelchara type relates to .. everything ... and where i get the skeleton from
        if (npcBase.ModelChara.IsValid && npcBase.ModelChara.RowId != 0 && npcBase.ModelChara.Value is var modelChara) {
            file.ModelCharaId = (int)modelChara.RowId;
            file.ModelSkeletonId = modelChara.Model;
        }

        if (npcBase.BNpcCustomize.IsValid && npcBase.BNpcCustomize.RowId != 0 && npcBase.BNpcCustomize.Value is var customize) {
            file.Race = (NpcRace)customize.Race.RowId;
            file.Sex = (NpcSex)customize.Gender;
            file.BodyType = (NpcBodyType)customize.BodyType;
            file.Height = customize.Height;
            file.Tribe = (NpcTribe)customize.Tribe.RowId;
            file.Face = customize.Face;
            file.HairStyle = customize.HairStyle;
            file.Highlights = customize.HairHighlight;
            file.SkinColor = customize.SkinColor;
            file.EyeColorRight = customize.EyeColor;
            file.HairColor = customize.HairColor;
            file.HighlightsColor = customize.HairHighlightColor;
            file.FacialFeatures = customize.FacialFeature;
            file.TattooColor = customize.FacialFeatureColor;
            file.Eyebrows = customize.Eyebrows;
            file.EyeColorLeft = customize.EyeHeterochromia;
            file.EyeShape = customize.EyeShape;
            file.Nose = customize.Nose;
            file.Jaw = customize.Jaw;
            file.Lipstick = customize.LipColor;
            file.LipColorFurPattern = customize.FacePaintColor;
            file.MuscleMass = customize.BustOrTone1;
            file.TailShape = customize.ExtraFeature1;
            file.BustSize = customize.ExtraFeature2OrBust;
            file.FacePaint = customize.FacePaint;
            file.FacePaintColor = customize.FacePaintColor;
        }

        if (npcBase.NpcEquip.IsValid && npcBase.NpcEquip.RowId != 0 && npcBase.NpcEquip.Value is var equip) {
            file.MainHand = new WeaponModel(equip.ModelMainHand) { Stain0 = (byte)equip.DyeMainHand.RowId, Stain1 = (byte)equip.Dye2MainHand.RowId };
            if (equip.ModelOffHand > 0) {
                file.OffHand = new WeaponModel(equip.ModelOffHand) { Stain0 = (byte)equip.DyeOffHand.RowId, Stain1 = (byte)equip.Dye2OffHand.RowId };
            }

            file.HeadGear = new EquipmentModel(equip.ModelHead) { Stain0 = (byte)equip.DyeHead.RowId, Stain1 = (byte)equip.Dye2Head.RowId };
            file.Body = new EquipmentModel(equip.ModelBody) { Stain0 = (byte)equip.DyeBody.RowId, Stain1 = (byte)equip.Dye2Body.RowId };
            file.Hands = new EquipmentModel(equip.ModelHands) { Stain0 = (byte)equip.DyeHands.RowId, Stain1 = (byte)equip.Dye2Hands.RowId };
            file.Legs = new EquipmentModel(equip.ModelLegs) { Stain0 = (byte)equip.DyeLegs.RowId, Stain1 = (byte)equip.Dye2Legs.RowId };
            file.Feet = new EquipmentModel(equip.ModelFeet) { Stain0 = (byte)equip.DyeFeet.RowId, Stain1 = (byte)equip.Dye2Feet.RowId };
            file.Ears = new EquipmentModel(equip.ModelEars) { Stain0 = (byte)equip.DyeEars.RowId, Stain1 = (byte)equip.Dye2Ears.RowId };
            file.Neck = new EquipmentModel(equip.ModelNeck) { Stain0 = (byte)equip.DyeNeck.RowId, Stain1 = (byte)equip.Dye2Neck.RowId };
            file.Wrists = new EquipmentModel(equip.ModelWrists) { Stain0 = (byte)equip.DyeWrists.RowId, Stain1 = (byte)equip.Dye2Wrists.RowId };
            file.LeftRing = new EquipmentModel(equip.ModelLeftRing) { Stain0 = (byte)equip.DyeLeftRing.RowId, Stain1 = (byte)equip.Dye2LeftRing.RowId };
            file.RightRing = new EquipmentModel(equip.ModelRightRing) { Stain0 = (byte)equip.DyeRightRing.RowId, Stain1 = (byte)equip.Dye2RightRing.RowId };

            if (equip.ModelHead > 0) {
                file.HideHeadgear = false;
            }

            if (equip.ModelMainHand > 0) {
                file.HideWeapons = false;
            }
        }
    }

    public void PlayEmote(BattleChara* character, Emote emoteEntry) {

        var emoteOption = new PlayEmoteOption { TargetId = 0, Flags = 1 };

        if (character->EmoteController.IsEmoting()) {
            var currentEmote = dataCache.GetEmote(character->EmoteController.EmoteId);
            if (emoteEntry.RowId != currentEmote.RowId) {
                character->EmoteController.PlayEmote(emoteEntry.RowId, &emoteOption);
            }
        } else {
            character->EmoteController.PlayEmote(emoteEntry.RowId, &emoteOption);
        }

        character->EmoteController.CurrentPoseType = emoteEntry.GetPoseType();
        character->Timeline.IsWeaponDrawn = emoteEntry.DrawsWeapon;
    }

    public bool IsCancelEmote(BattleChara* character, Emote targetEmote) {
        var currentEmote = dataCache.GetEmote(character->EmoteController.EmoteId);
        return currentEmote.HasCancelEmote && currentEmote.EmoteMode.Value.EndEmote.RowId == targetEmote.RowId;
    }

    public void CancelEmote(BattleChara* character) {
        var emoteOption = new PlayEmoteOption { TargetId = 0, Flags = 1 };
        character->EmoteController.PlayEmote(0, &emoteOption);
    }

    public bool IsPlayingEmote(BattleChara* character, ushort emoteId)
        => character->EmoteController.EmoteId == emoteId;

    public bool IsPlayingEmote(BattleChara* character)
        => character->EmoteController.EmoteId != 0;

    public bool IsRepeatingEmote(ushort emote) {
        var conditionMode = (CharacterModes)dataCache.GetEmote(emote).EmoteMode.Value.ConditionMode;
        return conditionMode == CharacterModes.AnimLock || conditionMode == CharacterModes.InPositionLoop || conditionMode == CharacterModes.EmoteLoop;
    }

    public void PlayTimeline(BattleChara* character, ushort timelineId) {
        log.Verbose($"Playing timeline {timelineId} on character {character->GetName()}");
        character->Timeline.PlayActionTimeline(timelineId);
    }

    public bool IsPlayingTimeline(BattleChara* character, ushort timelineId)
        => character->Timeline.TimelineSequencer.TimelineIds.Contains(timelineId);

    public void SetMovementAnimation(BattleChara* character, Animations animation) {

        if (character->Timeline.IsWeaponDrawn) {
            if (animation == Animations.Idle)
                animation = Animations.IdleArmed;
            if (animation == Animations.Walking)
                animation = Animations.WalkingArmed;
            if (animation == Animations.Running)
                animation = Animations.RunningArmed;
        }

        var animationCode = (ushort)animation;
        if (animation == Animations.Idle || animation == Animations.IdleArmed) {
            if (character->Mode != CharacterModes.Normal) {
                log.Verbose($"Resetting character {character->GetName()} to normal mode");
                character->SetMode(CharacterModes.Normal, 0);
                character->Timeline.BaseOverride = 0;
            }
        } else {
            if (character->Timeline.BaseOverride != animationCode) {
                log.Verbose($"Locking character {character->GetName()} to animation {animation}");
                character->SetMode(CharacterModes.AnimLock, 0);
                character->Timeline.BaseOverride = animationCode;
            }
        }
    }

    public Animations GetAnimation(BattleChara* character) {
        var animation = (Animations)character->Timeline.BaseOverride;
        return !Enum.IsDefined(animation) ? Animations.None : animation;
    }

    public void Clone(Character* chara) {
        if (objectTable.LocalPlayer == null) {
            return;
        }

        chara->CharacterSetup.CopyFromCharacter((Character*)objectTable.LocalPlayer.Address, CharacterSetupContainer.CopyFlags.ClassJob);
    }
}
