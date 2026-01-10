using ARealmRepopulated.Data.Appearance;
using ARealmRepopulated.Infrastructure;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using static FFXIVClientStructs.FFXIV.Client.Game.Character.DrawDataContainer;

namespace ARealmRepopulated.Core.Services.Npcs;

public unsafe class NpcAppearanceService(IObjectTable objectTable, IPluginLog log, ArrpDataCache dataCache) {

    public enum Animations : ushort {
        None = 0,
        Idle = 3,
        Walking = 13,
        Running = 22
    }

    public void Apply(Character* chara, NpcAppearanceData file) {
        chara->Scale = 1;
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
    }

    public void PlayEmote(BattleChara* character, ushort emote) {

        var emoteEntry = dataCache.GetEmote(emote);

        if (character->Mode == CharacterModes.InPositionLoop) {

            var timelineId = (ushort)emoteEntry.ActionTimeline[2].RowId;
            if (timelineId != 0) {

                if (emoteEntry.EmoteMode.Value.EndEmote.RowId == emote) {
                    character->SetMode(CharacterModes.Normal, 0);
                }
                character->Timeline.TimelineSequencer.PlayTimeline(timelineId);
            }

        } else {
            if (character->Timeline.TimelineSequencer.TimelineIds[0] != emoteEntry.ActionTimeline[0].RowId) {
                if (emoteEntry.EmoteMode.Value.ConditionMode != 0) {
                    character->SetMode((CharacterModes)emoteEntry.EmoteMode.Value.ConditionMode, (byte)emoteEntry.EmoteMode.RowId);
                }
            }
            character->Timeline.TimelineSequencer.PlayTimeline((ushort)emoteEntry.ActionTimeline[0].RowId);

            // we control the execution position manually, so reset any draw offset applied by emote
            if (character->DrawOffset != FFXIVClientStructs.FFXIV.Common.Math.Vector3.Zero) {
                character->SetDrawOffset(0, 0, 0);
            }
        }

    }

    public bool IsPlayingEmote(BattleChara* character, ushort emoteId) {
        var emote = dataCache.GetEmote(emoteId);
        var emoteTimelineIds = new List<uint>();
        foreach (var emoteTimeline in emote.ActionTimeline) {
            if (emoteTimeline.Value.RowId == 0)
                continue;

            emoteTimelineIds.Add(emoteTimeline.Value.RowId);
        }

        var timelineSequencer = character->Timeline.TimelineSequencer;
        for (var timelineIndex = 0; timelineIndex < timelineSequencer.TimelineIds.Length; timelineIndex++) {
            if (timelineSequencer.TimelineIds[timelineIndex] == 0)
                continue;

            if (emoteTimelineIds.Contains(timelineSequencer.TimelineIds[timelineIndex]))
                return true;

        }
        return false;
    }

    public bool IsRepeatingEmote(ushort emote) {
        var conditionMode = (CharacterModes)dataCache.GetEmote(emote).EmoteMode.Value.ConditionMode;
        return conditionMode == CharacterModes.AnimLock || conditionMode == CharacterModes.InPositionLoop || conditionMode == CharacterModes.EmoteLoop;
    }

    public void PlayTimeline(BattleChara* character, ushort timelineId) {
        log.Verbose($"Playing timeline {timelineId} on character {character->GetName()}");
        //if (character->Timeline.TimelineSequencer.TimelineIds[0] != timelineId)
        //{
        //var actionTimeline = dataManager.GetExcelSheet<ActionTimeline>();
        //var timelineData = actionTimeline.GetRow(timelineId);

        //character->Timeline.PlayActionTimeline

        //character->SetMode(CharacterModes.Normal, timelineData.ActionTimelineIDMode);

        //character->Timeline.PlayActionTimeline(, timelineData.);

        //}
    }

    public void SetAnimation(BattleChara* character, Animations animation) {
        var animationCode = (ushort)animation;
        if (character->Timeline.BaseOverride != animationCode) {
            character->SetMode(CharacterModes.AnimLock, 0);
            character->Timeline.BaseOverride = (ushort)animation;
        }
    }

    public Animations GetAnimation(BattleChara* character) {
        var animation = (Animations)character->Timeline.BaseOverride;
        return Enum.IsDefined(animation) ? Animations.None : animation;
    }

    public void Clone(Character* chara) {
        if (objectTable.LocalPlayer == null) {
            return;
        }

        chara->CharacterSetup.CopyFromCharacter((Character*)objectTable.LocalPlayer.Address, CharacterSetupContainer.CopyFlags.ClassJob);
    }

    public void SetName(Character* chara) {
        var name = $"ARRP {chara->ObjectIndex}";
        for (var x = 0; x < name.Length; x++) {
            chara->Name[x] = (byte)name[x];
        }
        chara->Name[name.Length] = 0;
    }
}
