using ARealmRepopulated.Data.Appearance;
using ARealmRepopulated.Data.Emotes;
using ARealmRepopulated.Infrastructure;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using Lumina.Excel.Sheets;
using Lumina.Excel.Sheets.Experimental;
using static FFXIVClientStructs.FFXIV.Client.Game.Character.DrawDataContainer;

namespace ARealmRepopulated.Core.Services.Npcs;
public unsafe class NpcAppearanceService(IClientState clientState, ArrpDataCache dataCache)
{

    public enum Animations : ushort
    {
        Idle = 3,
        Walking = 13,
        Running = 22,
        Turning = 13
    }
    
    public void Initialize()
    {

        // nothing to do here anymore
    }

    public void Apply(Character* chara, NpcAppearanceFile file)
    {

        chara->Scale = 1;

        chara->DrawData.CustomizeData.Data[(int)CustomizeIndex.Race] = file.Race.GetValueOrDefault();
        chara->DrawData.CustomizeData.Data[(int)CustomizeIndex.Sex] = file.Sex.GetValueOrDefault();
        chara->DrawData.CustomizeData.Data[(int)CustomizeIndex.BodyType] = file.BodyType.GetValueOrDefault();
        chara->DrawData.CustomizeData.Data[(int)CustomizeIndex.Height] = file.Height.GetValueOrDefault();
        chara->DrawData.CustomizeData.Data[(int)CustomizeIndex.Tribe] = file.Tribe.GetValueOrDefault();
        chara->DrawData.CustomizeData.Data[(int)CustomizeIndex.Face] = file.Face.GetValueOrDefault();
        chara->DrawData.CustomizeData.Data[(int)CustomizeIndex.HairStyle] = file.HairStyle.GetValueOrDefault();
        chara->DrawData.CustomizeData.Data[(int)CustomizeIndex.Highlights] = file.Highlights.GetValueOrDefault();
        chara->DrawData.CustomizeData.Data[(int)CustomizeIndex.SkinColor] = file.SkinColor.GetValueOrDefault();
        chara->DrawData.CustomizeData.Data[(int)CustomizeIndex.EyeColorRight] = file.EyeColorRight.GetValueOrDefault();
        chara->DrawData.CustomizeData.Data[(int)CustomizeIndex.HairColor] = file.HairColor.GetValueOrDefault();
        chara->DrawData.CustomizeData.Data[(int)CustomizeIndex.HighlightsColor] = file.HighlightsColor.GetValueOrDefault();
        chara->DrawData.CustomizeData.Data[(int)CustomizeIndex.FacialFeatures] = file.FacialFeatures.GetValueOrDefault();
        chara->DrawData.CustomizeData.Data[(int)CustomizeIndex.TattooColor] = file.TattooColor.GetValueOrDefault();
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

        file.MainHand?.Apply(chara, true);
        file.OffHand?.Apply(chara, false);

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

        /*
        if (!(&chara->DrawData.CustomizeData)->NormalizeCustomizeData(&chara->DrawData.CustomizeData))
        {
            chara->DrawData.CustomizeData = new CustomizeData();
        }*/
    }

    public void Read(Character* chara, NpcAppearanceFile file)
    {

        file.AppearanceId = Guid.NewGuid();
        file.Race = chara->DrawData.CustomizeData.Data[(int)CustomizeIndex.Race];
        file.Sex = chara->DrawData.CustomizeData.Data[(int)CustomizeIndex.Sex];
        file.BodyType = chara->DrawData.CustomizeData.Data[(int)CustomizeIndex.BodyType];
        file.Height = chara->DrawData.CustomizeData.Data[(int)CustomizeIndex.Height];
        file.Tribe = chara->DrawData.CustomizeData.Data[(int)CustomizeIndex.Tribe];
        file.Face = chara->DrawData.CustomizeData.Data[(int)CustomizeIndex.Face];
        file.HairStyle = chara->DrawData.CustomizeData.Data[(int)CustomizeIndex.HairStyle];
        file.Highlights = chara->DrawData.CustomizeData.Data[(int)CustomizeIndex.Highlights];
        file.SkinColor = chara->DrawData.CustomizeData.Data[(int)CustomizeIndex.SkinColor];
        file.EyeColorRight = chara->DrawData.CustomizeData.Data[(int)CustomizeIndex.EyeColorRight];
        file.HairColor = chara->DrawData.CustomizeData.Data[(int)CustomizeIndex.HairColor];
        file.HighlightsColor = chara->DrawData.CustomizeData.Data[(int)CustomizeIndex.HighlightsColor];
        file.FacialFeatures = chara->DrawData.CustomizeData.Data[(int)CustomizeIndex.FacialFeatures];
        file.TattooColor = chara->DrawData.CustomizeData.Data[(int)CustomizeIndex.TattooColor];
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

    public void PlayEmote(BattleChara* character, ushort emote)
    {

        var emoteEntry = dataCache.GetEmote(emote);                
        if (character->Timeline.TimelineSequencer.TimelineIds[0] != emoteEntry.ActionTimeline[0].RowId)
        {
            if (emoteEntry.EmoteMode.RowId != 0)
            {
                character->SetMode(CharacterModes.EmoteLoop, (byte)emoteEntry.EmoteMode.RowId);
            }            
        }

        character->Timeline.PlayActionTimeline((ushort)emoteEntry.ActionTimeline[0].RowId, 0);        
    }

    public bool IsRepeatingEmote(ushort emote)
    {
        var emoteEntry = dataCache.GetEmote(emote);
        return emoteEntry.EmoteMode.RowId != 0;
    }

    public void PlayTimeline(BattleChara* character, ushort timelineId)
    {
        //if (character->Timeline.TimelineSequencer.TimelineIds[0] != timelineId)
        //{
        //var actionTimeline = dataManager.GetExcelSheet<ActionTimeline>();
        //var timelineData = actionTimeline.GetRow(timelineId);

        //character->Timeline.PlayActionTimeline

        //character->SetMode(CharacterModes.Normal, timelineData.ActionTimelineIDMode);

        //character->Timeline.PlayActionTimeline(, timelineData.);

        //}
    }

    public bool IsPlayingEmote(BattleChara* character, ushort emote)
    {
        return character->Timeline.TimelineSequencer.TimelineIds[0] == dataCache.GetEmote(emote).ActionTimeline[0].RowId;
    }

    public void SetAnimation(BattleChara* character, Animations animation)
    {
        character->SetMode(CharacterModes.AnimLock, 0);
        character->Timeline.BaseOverride = (ushort)animation;
    }

    public void Clone(Character* chara)
    {

        if (clientState.LocalPlayer == null)
        {
            return;
        }

        chara->CharacterSetup.CopyFromCharacter((Character*)clientState.LocalPlayer.Address, CharacterSetupContainer.CopyFlags.ClassJob);

    }

    public void SetName(Character* chara)
    {
        var name = $"ARRP {chara->ObjectIndex}";
        for (var x = 0; x < name.Length; x++)
        {
            chara->Name[x] = (byte)name[x];
        }
        chara->Name[name.Length] = 0;
    }

}
