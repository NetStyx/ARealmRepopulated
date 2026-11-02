using ARealmRepopulated.Core.Numbers;
using ARealmRepopulated.Data.Appearance;
using ARealmRepopulated.Infrastructure;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using FFXIVClientStructs.FFXIV.Common.Math;
using FFXIVClientStructs.FFXIV.Shader;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using static FFXIVClientStructs.FFXIV.Client.Game.Character.DrawDataContainer;

namespace ARealmRepopulated.Core.Services.Npcs;

public unsafe class NpcAppearanceService(IObjectTable objectTable, IPluginLog log, ArrpAnimationData animationData, ArrpModelData modelData, ArrpNpcData npcData, ArrpCharacterCreationData creationData) {

    public enum Animations : ushort {
        None = 0,
        Idle = 3,
        IdleArmed = 34,
        Walking = 13,
        WalkingArmed = 41,
        Running = 22,
        RunningArmed = 50,
        Sprinting = 30,
        SprintingArmed = 58
    }

    public void Apply(Character* chara, NpcAppearanceData file) {

        var bodyType = GetSafeBodyType(file);
        var modelCharaId = GetSafeModelCharaId(file, bodyType);

        chara->ModelContainer.ModelCharaId = modelCharaId;
        chara->ModelContainer.ModelSkeletonId = GetSafeSkeletonId(file, modelCharaId);

        chara->DrawData.CustomizeData.Data[(int)CustomizeIndex.Race] = (byte)file.Race;
        chara->DrawData.CustomizeData.Data[(int)CustomizeIndex.Sex] = (byte)file.Sex;
        chara->DrawData.CustomizeData.Data[(int)CustomizeIndex.BodyType] = (byte)bodyType;
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
        chara->DrawData.CustomizeData.Data[(int)CustomizeIndex.Mouth] = file.Mouth.GetValueOrDefault();
        chara->DrawData.CustomizeData.Data[(int)CustomizeIndex.LipColorFurPattern] = file.LipColorFurPattern.GetValueOrDefault();
        chara->DrawData.CustomizeData.Data[(int)CustomizeIndex.MuscleMass] = file.MuscleMass.GetValueOrDefault();
        chara->DrawData.CustomizeData.Data[(int)CustomizeIndex.TailShape] = GetSafeTailShape(file, bodyType);
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

        if (file.Glasses.HasValue)
            chara->DrawData.SetGlasses(0, file.Glasses.Value);

        chara->Scale = file.Scale ?? 1f;

        chara->DrawData.HideWeapons(file.HideWeapons);
        chara->DrawData.HideHeadgear(0, file.HideHeadgear);
        chara->DrawData.HideVieraEars(file.HideVieraEars);
        chara->DrawData.SetVisor(file.CloseVisor);

        // the voice for human models comes from a selection boiled down to a byte in every other case, the sound pack of the model is used.
        chara->Vfx.VoiceId = modelData.IsHumanModel(file.ModelCharaId)
            ? file.Voice ?? 0
            : modelData.GetModelSoundPack(file.ModelCharaId);
    }

    /// <summary>
    /// Checks that the appearance data match the possible ingame representation. If we dont, it might crash the game.
    /// </summary>    
    private NpcBodyType GetSafeBodyType(NpcAppearanceData file) {
        var needsCheck = file.BodyType != NpcBodyType.Unknown && modelData.IsHumanModel(file.ModelCharaId);
        var clanFitsRace = creationData.GetClan(file.Tribe, file.Sex)?.Race == file.Race;
        return needsCheck && !(clanFitsRace && npcData.IsBodyTypeAvailable(file.Tribe, file.Sex, file.BodyType)) ? NpcBodyType.Normal : file.BodyType;
    }

    private int GetSafeModelCharaId(NpcAppearanceData file, NpcBodyType bodyType)
        => !modelData.IsHumanModel(file.ModelCharaId) || npcData.HasAnimationsFor(file.ModelCharaId, file.Tribe, file.Sex, bodyType) ? file.ModelCharaId : 0;

    // The game sets 20000 + the model code on humans, a read of a live actor keeps that. Anything lower on a human is a leftover.
    private int GetSafeSkeletonId(NpcAppearanceData file, int modelCharaId)
        => !modelData.IsHumanModel(modelCharaId) ? modelData.GetModelSkeletonId(modelCharaId)
            : modelCharaId == file.ModelCharaId && file.ModelSkeletonId >= 20000 ? file.ModelSkeletonId
            : 0;

    // The game derives the model code of a human (Human.RaceSexId, the c0101 in its file paths) in Human.SetupIdsAndVfxScale:
    //   var code = BaseCode(tribe, bodyType) + sex * 100;
    //   if (race == Hyur) code += tailShape & 0x80 ? 9000 : 0;
    // Young Hyur npcs use that bit for models of their own, c9104 and c9204. On any other body type it gives a code without
    // equipment data (c9101), and the model setup crashes on it. The game turns an unknown race into a Hyur first.
    // This is as cursed as it gets. 
    private static byte GetSafeTailShape(NpcAppearanceData file, NpcBodyType bodyType) {
        var tailShape = file.TailShape.GetValueOrDefault();
        return file.Race is NpcRace.Hyur or NpcRace.Unknown && bodyType != NpcBodyType.Young ? NpcAppearanceData.StripYoungNpcRaceBit(tailShape) : tailShape;
    }

    public void ApplyExtendedAppearance(Character* chara, NpcAppearanceData file) {

        var human = GetHumanDrawObject(chara);
        if (human == null)
            return;

        var extended = file.ExtendedAppearance;
        if (extended == null || !extended.HasAnyValue)
            return;

        var parameters = GetCustomizeParameters(human);
        if (parameters == null)
            return;

        if (extended.SkinColor.HasValue) {
            parameters->SkinColor.X = extended.SkinColor.Value.X;
            parameters->SkinColor.Y = extended.SkinColor.Value.Y;
            parameters->SkinColor.Z = extended.SkinColor.Value.Z;
        }

        if (extended.MuscleTone.HasValue)
            parameters->SkinColor.W = extended.MuscleTone.Value;

        if (extended.MouthColor.HasValue) {
            parameters->LipColor.X = extended.MouthColor.Value.X;
            parameters->LipColor.Y = extended.MouthColor.Value.Y;
            parameters->LipColor.Z = extended.MouthColor.Value.Z;
            parameters->LipColor.W = extended.MouthColor.Value.W;
        }

        if (extended.HairColor.HasValue) {
            parameters->MainColor.X = extended.HairColor.Value.X;
            parameters->MainColor.Y = extended.HairColor.Value.Y;
            parameters->MainColor.Z = extended.HairColor.Value.Z;
        }

        // Without highlights the game uses the hair color for both, otherwise the old color stays on the strands.
        if (!file.IsOptionEnabled(CustomizeIndex.Highlights)) {
            parameters->MeshColor.X = parameters->MainColor.X;
            parameters->MeshColor.Y = parameters->MainColor.Y;
            parameters->MeshColor.Z = parameters->MainColor.Z;
        } else if (extended.HairHighlight.HasValue) {
            parameters->MeshColor.X = extended.HairHighlight.Value.X;
            parameters->MeshColor.Y = extended.HairHighlight.Value.Y;
            parameters->MeshColor.Z = extended.HairHighlight.Value.Z;
        }

        if (extended.LeftEyeColor.HasValue) {
            parameters->LeftColor.X = extended.LeftEyeColor.Value.X;
            parameters->LeftColor.Y = extended.LeftEyeColor.Value.Y;
            parameters->LeftColor.Z = extended.LeftEyeColor.Value.Z;
        }

        if (extended.RightEyeColor.HasValue) {
            parameters->RightColor.X = extended.RightEyeColor.Value.X;
            parameters->RightColor.Y = extended.RightEyeColor.Value.Y;
            parameters->RightColor.Z = extended.RightEyeColor.Value.Z;
        }

        if (extended.FeatureColor.HasValue) {
            parameters->OptionColor.X = extended.FeatureColor.Value.X;
            parameters->OptionColor.Y = extended.FeatureColor.Value.Y;
            parameters->OptionColor.Z = extended.FeatureColor.Value.Z;
        }
    }

    private static Human* GetHumanDrawObject(Character* chara) {

        if (chara == null || chara->DrawObject == null)
            return null;

        var characterBase = (CharacterBase*)chara->DrawObject;
        return characterBase->GetModelType() == CharacterBase.ModelType.Human ? (Human*)characterBase : null;
    }

    private static CustomizeParameter* GetCustomizeParameters(Human* human) {

        var constantBuffer = human->CustomizeParameterCBuffer;
        if (constantBuffer == null)
            return null;

        var source = constantBuffer->TryGetSourcePointer();
        if (source == null || constantBuffer->ByteSize < sizeof(CustomizeParameter))
            return null;

        return (CustomizeParameter*)source;
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
        file.Mouth = chara->DrawData.CustomizeData.Data[(int)CustomizeIndex.Mouth];
        file.LipColorFurPattern = chara->DrawData.CustomizeData.Data[(int)CustomizeIndex.LipColorFurPattern];
        file.MuscleMass = chara->DrawData.CustomizeData.Data[(int)CustomizeIndex.MuscleMass];
        file.TailShape = chara->DrawData.CustomizeData.Data[(int)CustomizeIndex.TailShape];
        file.BustSize = chara->DrawData.CustomizeData.Data[(int)CustomizeIndex.BustSize];
        file.FacePaint = chara->DrawData.CustomizeData.Data[(int)CustomizeIndex.FacePaint];
        file.FacePaintColor = chara->DrawData.CustomizeData.Data[(int)CustomizeIndex.FacePaintColor];

        file.HideWeapons = chara->DrawData.IsWeaponHidden;
        file.HideHeadgear = chara->DrawData.IsHatHidden;
        file.CloseVisor = chara->DrawData.IsVisorToggled;
        file.HideVieraEars = chara->DrawData.VieraEarsHidden;

        file.Glasses = chara->DrawData.GlassesIds[0];
        file.Voice = (byte?)chara->Vfx.VoiceId.InRangeOrDefault<ushort>(1, byte.MaxValue);

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

        var human = GetHumanDrawObject(chara);
        if (human == null)
            return;

        var parameters = GetCustomizeParameters(human);
        if (parameters == null)
            return;

        file.ExtendedAppearance = new NpcExtendedAppearance {
            SkinColor = new Vector3(parameters->SkinColor.X, parameters->SkinColor.Y, parameters->SkinColor.Z),
            MuscleTone = parameters->SkinColor.W,
            MouthColor = parameters->LipColor,
            HairColor = parameters->MainColor,
            HairHighlight = parameters->MeshColor,
            LeftEyeColor = new Vector3(parameters->LeftColor.X, parameters->LeftColor.Y, parameters->LeftColor.Z),
            RightEyeColor = new Vector3(parameters->RightColor.X, parameters->RightColor.Y, parameters->RightColor.Z),
            FeatureColor = parameters->OptionColor,
        };
    }

    public void Read(NpcLookup npc, NpcAppearanceData file) {
        if (npc.EventNpc is { } eventNpc) {
            Read(eventNpc, file);
        } else if (npc.BattleNpc is { } battleNpc) {
            Read(battleNpc, file);
        }
    }

    public void Read(BNpcBase npcBase, NpcAppearanceData file) {

        file.Scale = npcBase.Scale;

        // need to figure out how the modelchara type relates to .. everything ... and where i get the skeleton from
        if (npcBase.ModelChara.IsValid && npcBase.ModelChara.RowId != 0 && npcBase.ModelChara.Value is var modelChara) {
            file.ModelCharaId = (int)modelChara.RowId;
            file.ModelSkeletonId = modelData.GetModelSkeletonId((int)modelChara.RowId);
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
            file.EyeColorRight = customize.EyeHeterochromia;
            file.HairColor = customize.HairColor;
            file.HighlightsColor = customize.HairHighlightColor;
            file.FacialFeatures = customize.FacialFeature;
            file.TattooColor = customize.FacialFeatureColor;
            file.Eyebrows = customize.Eyebrows;
            file.EyeColorLeft = customize.EyeColor;
            file.EyeShape = customize.EyeShape;
            file.Nose = customize.Nose;
            file.Jaw = customize.Jaw;
            file.Mouth = customize.Mouth;
            file.LipColorFurPattern = customize.LipColor;
            file.MuscleMass = customize.BustOrTone1;
            file.TailShape = customize.ExtraFeature1;
            file.BustSize = customize.ExtraFeature2OrBust;
            file.FacePaint = customize.FacePaint;
            file.FacePaintColor = customize.FacePaintColor;
        }

        if (npcBase.NpcEquip.IsValid && npcBase.NpcEquip.RowId != 0 && npcBase.NpcEquip.Value is var equip) {
            Read(equip, file);
            file.Glasses = (ushort)equip.Glasses[0].RowId;
            HideEmptySlots(file);
        }
    }

    private static void Read(NpcEquip equip, NpcAppearanceData file) {
        file.MainHand = ReadWeapon(equip.ModelMainHand, equip.DyeMainHand, equip.Dye2MainHand);
        file.OffHand = ReadWeapon(equip.ModelOffHand, equip.DyeOffHand, equip.Dye2OffHand);
        file.HeadGear = ReadGear(equip.ModelHead, equip.DyeHead, equip.Dye2Head);
        file.Body = ReadGear(equip.ModelBody, equip.DyeBody, equip.Dye2Body);
        file.Hands = ReadGear(equip.ModelHands, equip.DyeHands, equip.Dye2Hands);
        file.Legs = ReadGear(equip.ModelLegs, equip.DyeLegs, equip.Dye2Legs);
        file.Feet = ReadGear(equip.ModelFeet, equip.DyeFeet, equip.Dye2Feet);
        file.Ears = ReadGear(equip.ModelEars, equip.DyeEars, equip.Dye2Ears);
        file.Neck = ReadGear(equip.ModelNeck, equip.DyeNeck, equip.Dye2Neck);
        file.Wrists = ReadGear(equip.ModelWrists, equip.DyeWrists, equip.Dye2Wrists);
        file.LeftRing = ReadGear(equip.ModelLeftRing, equip.DyeLeftRing, equip.Dye2LeftRing);
        file.RightRing = ReadGear(equip.ModelRightRing, equip.DyeRightRing, equip.Dye2RightRing);
        file.CloseVisor = equip.Visor;
        file.HideVieraEars = equip.VisorToggled;
    }

    // sheets clear a slot with uint.MaxValue.
    private static EquipmentModel ReadGear(uint modelKey, RowRef<Stain> dye, RowRef<Stain> dye2)
        => modelKey == uint.MaxValue ? new EquipmentModel() : new EquipmentModel(modelKey) { Stain0 = (byte)dye.RowId, Stain1 = (byte)dye2.RowId };

    private static WeaponModel? ReadWeapon(ulong modelKey, RowRef<Stain> dye, RowRef<Stain> dye2)
        => modelKey is 0 or uint.MaxValue ? null : new WeaponModel(modelKey) { Stain0 = (byte)dye.RowId, Stain1 = (byte)dye2.RowId };

    private static void HideEmptySlots(NpcAppearanceData file) {
        file.HideWeapons = file.MainHand == null;
        file.HideHeadgear = file.HeadGear is null or { ModelId: 0 };
    }

    public void Read(ENpcBase npc, NpcAppearanceData file) {

        file.Scale = npc.Scale > 0 ? npc.Scale : NpcAppearanceData.ScaleDefault;

        file.ModelCharaId = (int)npc.ModelChara.RowId;
        file.ModelSkeletonId = modelData.GetModelSkeletonId(file.ModelCharaId);

        file.Race = (NpcRace)npc.Race.RowId;
        file.Sex = (NpcSex)npc.Gender;
        file.BodyType = (NpcBodyType)npc.BodyType;
        file.Height = npc.Height;
        file.Tribe = (NpcTribe)npc.Tribe.RowId;
        file.Face = npc.Face;
        file.HairStyle = npc.HairStyle;
        file.Highlights = npc.HairHighlight;
        file.SkinColor = npc.SkinColor;
        file.EyeColorRight = npc.EyeHeterochromia;
        file.HairColor = npc.HairColor;
        file.HighlightsColor = npc.HairHighlightColor;
        file.FacialFeatures = npc.FacialFeature;
        file.TattooColor = npc.FacialFeatureColor;
        file.Eyebrows = npc.Eyebrows;
        file.EyeColorLeft = npc.EyeColor;
        file.EyeShape = npc.EyeShape;
        file.Nose = npc.Nose;
        file.Jaw = npc.Jaw;
        file.Mouth = npc.Mouth;
        file.LipColorFurPattern = npc.LipColor;
        file.MuscleMass = npc.BustOrTone1;
        file.TailShape = npc.ExtraFeature1;
        file.BustSize = npc.ExtraFeature2OrBust;
        file.FacePaint = npc.FacePaint;
        file.FacePaintColor = npc.FacePaintColor;

        // facewear comes from the npc alone, the sheets have no name for this column yet
        file.Glasses = npc.Unknown_70_1;

        // The NpcEquip row is only the base. The game walks through each slot and priorizes the npc equip. If that is zero it takes the slot value itself.
        // VisorToggled and NotRewriteHeight are the same flag, the game uses it to hide the ears of Viera.
        if (npc.NpcEquip.RowId != 0 && npc.NpcEquip.ValueNullable is { } equip) {
            Read(equip, file);
        } else {
            file.CloseVisor = npc.Visor;
            file.HideVieraEars = npc.NotRewriteHeight;
        }

        file.MainHand = npc.ModelMainHand != 0 ? ReadWeapon(npc.ModelMainHand, npc.DyeMainHand, npc.Dye2MainHand) : file.MainHand;
        file.OffHand = npc.ModelOffHand != 0 ? ReadWeapon(npc.ModelOffHand, npc.DyeOffHand, npc.Dye2OffHand) : file.OffHand;
        file.HeadGear = npc.ModelHead != 0 ? ReadGear(npc.ModelHead, npc.DyeHead, npc.Dye2Head) : file.HeadGear;
        file.Body = npc.ModelBody != 0 ? ReadGear(npc.ModelBody, npc.DyeBody, npc.Dye2Body) : file.Body;
        file.Hands = npc.ModelHands != 0 ? ReadGear(npc.ModelHands, npc.DyeHands, npc.Dye2Hands) : file.Hands;
        file.Legs = npc.ModelLegs != 0 ? ReadGear(npc.ModelLegs, npc.DyeLegs, npc.Dye2Legs) : file.Legs;
        file.Feet = npc.ModelFeet != 0 ? ReadGear(npc.ModelFeet, npc.DyeFeet, npc.Dye2Feet) : file.Feet;
        file.Ears = npc.ModelEars != 0 ? ReadGear(npc.ModelEars, npc.DyeEars, npc.Dye2Ears) : file.Ears;
        file.Neck = npc.ModelNeck != 0 ? ReadGear(npc.ModelNeck, npc.DyeNeck, npc.Dye2Neck) : file.Neck;
        file.Wrists = npc.ModelWrists != 0 ? ReadGear(npc.ModelWrists, npc.DyeWrists, npc.Dye2Wrists) : file.Wrists;
        file.LeftRing = npc.ModelLeftRing != 0 ? ReadGear(npc.ModelLeftRing, npc.DyeLeftRing, npc.Dye2LeftRing) : file.LeftRing;
        file.RightRing = npc.ModelRightRing != 0 ? ReadGear(npc.ModelRightRing, npc.DyeRightRing, npc.Dye2RightRing) : file.RightRing;

        HideEmptySlots(file);
    }

    private static void PlayEmoteInternal(BattleChara* character, uint emoteId) {

        if (character->EmoteController.IsEmoting() && character->EmoteController.EmoteId == emoteId)
            return;

        var emoteOption = new EmoteController.PlayEmoteOption { TargetId = 0, Flags = 1 };
        character->EmoteController.PlayEmote(emoteId, &emoteOption);
    }

    public void PlayEmote(BattleChara* character, Emote emoteEntry) {

        SetBaseAnimationSpeed(character, 1f);
        PlayEmoteInternal(character, emoteEntry.RowId);

        character->EmoteController.CurrentPoseType = emoteEntry.GetPoseType();
        character->Timeline.IsWeaponDrawn = emoteEntry.DrawsWeapon;
    }

    // copies the behavior of /cpose ... but with a specific pose index to pick, rather then the next on in the cycle.
    public void SetPose(BattleChara* character, PoseType poseType, byte poseState) {

        poseState = animationData.ClampPoseState(poseType, poseState);

        character->EmoteController.CurrentPoseType = poseType;
        character->EmoteController.CPoseState = poseState;

        var poseEmote = animationData.GetPoseStateEmote(poseType, poseState);
        if (poseEmote == 0)
            return;

        log.Verbose($"Playing pose {poseType}/{poseState} via emote {poseEmote} on character {character->GetName()}");

        PlayEmoteInternal(character, poseEmote);
    }

    public void HoldEmotePose(BattleChara* character, Emote emoteEntry, byte poseState) {

        if (!emoteEntry.TryGetPoseType(out var poseType))
            return;

        var poseEmote = animationData.GetPoseStateEmote(poseType, poseState);
        if (poseEmote == 0 || IsPlayingEmote(character, poseEmote) || !HasEnteredEmotePose(character, emoteEntry))
            return;

        SetPose(character, poseType, poseState);

        if (TryGetPostureMode(emoteEntry, out var postureMode)) {
            character->SetMode(postureMode, (byte)emoteEntry.EmoteMode.RowId);
        }
    }

    // The character mode an emote locks the actor into while it holds a pose. Emotes that just play and end have no such mode.    
    private static bool TryGetPostureMode(Emote emoteEntry, out CharacterModes postureMode) {

        postureMode = CharacterModes.None;
        if (!emoteEntry.EmoteMode.IsValid)
            return false;

        postureMode = (CharacterModes)emoteEntry.EmoteMode.Value.ConditionMode;
        return postureMode != CharacterModes.None && postureMode != CharacterModes.Normal;
    }

    private static bool HasEnteredEmotePose(BattleChara* character, Emote emoteEntry)
        => TryGetPostureMode(emoteEntry, out var postureMode) && character->Mode == postureMode;

    public bool IsCancelEmote(BattleChara* character, Emote targetEmote) {
        var currentEmote = animationData.GetEmote(character->EmoteController.EmoteId);
        return currentEmote.HasCancelEmote && currentEmote.EmoteMode.Value.EndEmote.RowId == targetEmote.RowId;
    }

    public void CancelEmote(BattleChara* character)
        => PlayEmoteInternal(character, 0);

    public bool IsPlayingEmote(BattleChara* character, ushort emoteId)
        => character->EmoteController.EmoteId == emoteId;

    public void PlayTimeline(BattleChara* character, ushort timelineId) {
        log.Verbose($"Playing timeline {timelineId} on character {character->GetName()}");
        SetBaseAnimationSpeed(character, 1f);
        character->Timeline.PlayActionTimeline(timelineId);
    }

    public bool IsPlayingTimeline(BattleChara* character, ushort timelineId) {
        var slot = animationData.GetTimelineSlot(timelineId);
        var timelineIds = character->Timeline.TimelineSequencer.TimelineIds;
        return slot < timelineIds.Length && timelineIds[slot] == timelineId;
    }

    public void SetMovementAnimation(BattleChara* character, Animations animation, float animationSpeed = 1f) {

        if (character->Timeline.IsWeaponDrawn) {
            if (animation == Animations.Idle)
                animation = Animations.IdleArmed;
            if (animation == Animations.Walking)
                animation = Animations.WalkingArmed;
            if (animation == Animations.Running)
                animation = Animations.RunningArmed;
            if (animation == Animations.Sprinting)
                animation = Animations.SprintingArmed;
        }

        SetBaseAnimationSpeed(character, animationSpeed);

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

    private const int BaseTimelineSlot = 0;

    public void SetBaseAnimationSpeed(BattleChara* character, float animationSpeed) {
        var timelineSpeeds = character->Timeline.TimelineSequencer.TimelineSpeeds;
        if (timelineSpeeds.Length <= BaseTimelineSlot)
            return;

        if (timelineSpeeds[BaseTimelineSlot] != animationSpeed)
            timelineSpeeds[BaseTimelineSlot] = animationSpeed;
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
