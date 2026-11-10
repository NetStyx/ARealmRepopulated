using ARealmRepopulated.Core.Json;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Common.Math;
using System.Text;
using System.Text.Json;
using static FFXIVClientStructs.FFXIV.Client.Game.Character.DrawDataContainer;

namespace ARealmRepopulated.Data.Appearance;

public enum CustomizeIndex : int {
    Race = 0x00,
    Sex = 0x01,
    BodyType = 0x02,
    Height = 0x03,
    Tribe = 0x04,
    Face = 0x05,
    HairStyle = 0x06,
    Highlights = 0x07,
    SkinColor = 0x08,
    EyeColorRight = 0x09,
    HairColor = 0x0A,
    HighlightsColor = 0x0B,
    FacialFeatures = 0x0C,
    FacialFeaturesColor = 0x0D,
    Eyebrows = 0x0E,
    EyeColorLeft = 0x0F,
    EyeShape = 0x10,
    Nose = 0x11,
    Jaw = 0x12,
    Mouth = 0x13,
    LipColorFurPattern = 0x14,
    MuscleMass = 0x15,
    TailShape = 0x16,
    BustSize = 0x17,
    FacePaint = 0x18,
    FacePaintColor = 0x19
}

// Remember: enum names are used are used during serialization.
public enum NpcRace : byte {
    Unknown = 0,
    Hyur = 1,
    Elezen = 2,
    Lalafel = 3,
    Miqote = 4,
    Roegadyn = 5,
    AuRa = 6,
    Hrothgar = 7,
    Viera = 8
}
public enum NpcSex : byte {
    Male = 0,
    Female = 1
}

public enum NpcTribe : byte {
    Unknown = 0,
    Midlander = 1,
    Highlander = 2,
    Wildwood = 3,
    Duskwight = 4,
    Plainsfolk = 5,
    Dunesfolk = 6,
    SeekerOfTheSun = 7,
    KeeperOfTheMoon = 8,
    SeaWolf = 9,
    Hellsguard = 10,
    Raen = 11,
    Xaela = 12,
    Helions = 13,
    TheLost = 14,
    Rava = 15,
    Veena = 16
}

public enum NpcBodyType : byte {
    Unknown = 0,
    Normal = 1,
    Old = 3,
    Young = 4
}

public record NpcAppearanceData {

    // When multiple actors were defined at the same time without saving, then the shared Default would apply to all of them. 
    // Therefore: create a new instance on every read.
    public static NpcAppearanceData Default => FromResource("DefaultHumanFemale.json")!;

    public const float ScaleMin = 0.01f;
    public const float ScaleMax = 50.0f; // Max scaling seen in BNpc sheets says 42
    public const float ScaleDefault = 1.0f;
    public const float ScaleSoftMin = 0.10f;
    public const float ScaleSoftMax = 5.00f;

    // If this bit is set on the tail byte of a Hyur, it adds 9000 to the model code (insert vegeta.png). See UsesYoungNpcRace.
    private const int YoungNpcRaceBit = 0x80;

    // Some customize bytes use bit 7 to switch their option on or off. Small iris and lipstick for example.
    private const int CustomizationEnabledBit = 0x80;

    public static readonly JsonSerializerOptions SerializerOptions = new() {
        Converters = { new Vector3Converter(), new Vector4Converter() }
    };

    public Guid AppearanceId { get; set; } = Guid.NewGuid();

    public int ModelCharaId { get; set; } = 0;
    public int ModelSkeletonId { get; set; } = 0;

    public NpcRace Race { get; set; }
    public NpcSex Sex { get; set; }
    public NpcBodyType BodyType { get; set; }
    public byte? Height { get; set; }
    public NpcTribe Tribe { get; set; }
    public byte? Face { get; set; }
    public byte? HairStyle { get; set; }
    public byte? Highlights { get; set; }
    public byte? SkinColor { get; set; }
    public byte? EyeColorRight { get; set; }
    public byte? HairColor { get; set; }
    public byte? HighlightsColor { get; set; }
    public byte? FacialFeatures { get; set; }
    public byte? TattooColor { get; set; }
    public byte? Eyebrows { get; set; }
    public byte? EyeColorLeft { get; set; }
    public byte? EyeShape { get; set; }
    public byte? Nose { get; set; }
    public byte? Jaw { get; set; }
    public byte? Mouth { get; set; }
    public byte? LipColorFurPattern { get; set; }
    public byte? MuscleMass { get; set; }
    public byte? TailShape { get; set; }
    public byte? BustSize { get; set; }
    public byte? FacePaint { get; set; }
    public byte? FacePaintColor { get; set; }

    public ushort? Glasses { get; set; }

    public byte? Voice { get; set; }

    public NpcExtendedAppearance? ExtendedAppearance { get; set; }

    public WeaponModel? MainHand { get; set; }
    public WeaponModel? OffHand { get; set; }

    public EquipmentModel? HeadGear { get; set; }
    public EquipmentModel? Body { get; set; }
    public EquipmentModel? Hands { get; set; }
    public EquipmentModel? Legs { get; set; }
    public EquipmentModel? Feet { get; set; }
    public EquipmentModel? Ears { get; set; }
    public EquipmentModel? Neck { get; set; }
    public EquipmentModel? Wrists { get; set; }
    public EquipmentModel? LeftRing { get; set; }
    public EquipmentModel? RightRing { get; set; }

    public float? Transparency { get; set; }

    public float? Scale { get; set; } = ScaleDefault;

    public bool HideWeapons { get; set; } = true;
    public bool HideHeadgear { get; set; } = true;
    public bool CloseVisor { get; set; } = false;
    public bool HideVieraEars { get; set; } = false;

    public byte GetOption(CustomizeIndex index)
        => (byte)(GetCustomize(index) & (SharesByteWithEnabledBit(index) ? ~CustomizationEnabledBit : byte.MaxValue));

    public void SetOption(CustomizeIndex index, byte value) {
        if (SharesByteWithEnabledBit(index)) {
            SetCustomize(index, (byte)((GetCustomize(index) & CustomizationEnabledBit) | value));
            return;
        }

        SetCustomize(index, value);
        ExtendedAppearance?.Forget(index);
    }

    public bool IsOptionEnabled(CustomizeIndex index)
        => (GetCustomize(index) & CustomizationEnabledBit) != 0;

    public void SetOptionEnabled(CustomizeIndex index, bool enabled) {
        var current = GetCustomize(index);
        SetCustomize(index, (byte)(enabled ? current | CustomizationEnabledBit : current & ~CustomizationEnabledBit));
        ExtendedAppearance?.Forget(index);
    }

    public void SetExactColor(CustomizeIndex index, Vector3 color, float defaultOpacity) {

        // lip colors and highlights only show while lipstick and highlights are enabled
        if (index == CustomizeIndex.LipColorFurPattern && !IsOptionEnabled(CustomizeIndex.Mouth)) {
            SetOptionEnabled(CustomizeIndex.Mouth, true);
        } else if (index == CustomizeIndex.HighlightsColor && !IsOptionEnabled(CustomizeIndex.Highlights)) {
            SetOptionEnabled(CustomizeIndex.Highlights, true);
        }

        ExtendedAppearance ??= new NpcExtendedAppearance();
        ExtendedAppearance.SetColor(index, color, defaultOpacity);
    }

    public bool HasFacialFeature(int bit)
        => (FacialFeatures.GetValueOrDefault() & (1 << bit)) != 0;

    public void SetFacialFeature(int bit, bool enabled) {
        var current = FacialFeatures.GetValueOrDefault();
        FacialFeatures = (byte)(enabled ? current | (1 << bit) : current & ~(1 << bit));
    }

    // A menu holds its features as a mask that starts at its own first bit.
    public void EnableFacialFeatures(byte features, int firstBit)
        => FacialFeatures = (byte)(FacialFeatures.GetValueOrDefault() | (features << firstBit));

    /// <summary>
    /// Yes. Thanks SQX. Because we try to cramp everything into existing structured instead of doing it proper we have the situation that 
    /// suddently young hyur npcs with bit 7 of the tail byte are a race of their own, with other models than the rest of the young.
    /// </summary>
    public bool UsesYoungNpcRace()
        => Race == NpcRace.Hyur && BodyType == NpcBodyType.Young && HasYoungNpcRaceBit(TailShape.GetValueOrDefault());

    public static bool HasYoungNpcRaceBit(byte tailShape)
        => (tailShape & YoungNpcRaceBit) != 0;

    public static byte StripYoungNpcRaceBit(byte tailShape)
        => (byte)(tailShape & ~YoungNpcRaceBit);

    public void ResetModel() {
        ModelCharaId = 0;
        ModelSkeletonId = 0;
    }

    /// <summary>
    /// The options whose on/off bit is actuall part of the value.
    /// </summary>
    private static bool SharesByteWithEnabledBit(CustomizeIndex index)
        => index is CustomizeIndex.EyeShape or CustomizeIndex.Mouth or CustomizeIndex.FacePaint;

    private byte GetCustomize(CustomizeIndex index) => index switch {
        CustomizeIndex.Height => Height.GetValueOrDefault(),
        CustomizeIndex.Face => Face.GetValueOrDefault(),
        CustomizeIndex.HairStyle => HairStyle.GetValueOrDefault(),
        CustomizeIndex.Highlights => Highlights.GetValueOrDefault(),
        CustomizeIndex.SkinColor => SkinColor.GetValueOrDefault(),
        CustomizeIndex.EyeColorRight => EyeColorRight.GetValueOrDefault(),
        CustomizeIndex.HairColor => HairColor.GetValueOrDefault(),
        CustomizeIndex.HighlightsColor => HighlightsColor.GetValueOrDefault(),
        CustomizeIndex.FacialFeatures => FacialFeatures.GetValueOrDefault(),
        CustomizeIndex.FacialFeaturesColor => TattooColor.GetValueOrDefault(),
        CustomizeIndex.Eyebrows => Eyebrows.GetValueOrDefault(),
        CustomizeIndex.EyeColorLeft => EyeColorLeft.GetValueOrDefault(),
        CustomizeIndex.EyeShape => EyeShape.GetValueOrDefault(),
        CustomizeIndex.Nose => Nose.GetValueOrDefault(),
        CustomizeIndex.Jaw => Jaw.GetValueOrDefault(),
        CustomizeIndex.Mouth => Mouth.GetValueOrDefault(),
        CustomizeIndex.LipColorFurPattern => LipColorFurPattern.GetValueOrDefault(),
        CustomizeIndex.MuscleMass => MuscleMass.GetValueOrDefault(),
        CustomizeIndex.TailShape => TailShape.GetValueOrDefault(),
        CustomizeIndex.BustSize => BustSize.GetValueOrDefault(),
        CustomizeIndex.FacePaint => FacePaint.GetValueOrDefault(),
        CustomizeIndex.FacePaintColor => FacePaintColor.GetValueOrDefault(),
        _ => throw new ArgumentOutOfRangeException(nameof(index), index, null)
    };

    private void SetCustomize(CustomizeIndex index, byte value) {
        switch (index) {
            case CustomizeIndex.Height: Height = value; break;
            case CustomizeIndex.Face: Face = value; break;
            case CustomizeIndex.HairStyle: HairStyle = value; break;
            case CustomizeIndex.Highlights: Highlights = value; break;
            case CustomizeIndex.SkinColor: SkinColor = value; break;
            case CustomizeIndex.EyeColorRight: EyeColorRight = value; break;
            case CustomizeIndex.HairColor: HairColor = value; break;
            case CustomizeIndex.HighlightsColor: HighlightsColor = value; break;
            case CustomizeIndex.FacialFeatures: FacialFeatures = value; break;
            case CustomizeIndex.FacialFeaturesColor: TattooColor = value; break;
            case CustomizeIndex.Eyebrows: Eyebrows = value; break;
            case CustomizeIndex.EyeColorLeft: EyeColorLeft = value; break;
            case CustomizeIndex.EyeShape: EyeShape = value; break;
            case CustomizeIndex.Nose: Nose = value; break;
            case CustomizeIndex.Jaw: Jaw = value; break;
            case CustomizeIndex.Mouth: Mouth = value; break;
            case CustomizeIndex.LipColorFurPattern: LipColorFurPattern = value; break;
            case CustomizeIndex.MuscleMass: MuscleMass = value; break;
            case CustomizeIndex.TailShape: TailShape = value; break;
            case CustomizeIndex.BustSize: BustSize = value; break;
            case CustomizeIndex.FacePaint: FacePaint = value; break;
            case CustomizeIndex.FacePaintColor: FacePaintColor = value; break;
            default: throw new ArgumentOutOfRangeException(nameof(index), index, null);
        }
    }

    public NpcAppearanceData Copy()
        => this with { ExtendedAppearance = ExtendedAppearance is { } extended ? extended with { } : null };

    public static NpcAppearanceData? FromResource(string fileName) {
        var assembly = typeof(NpcAppearanceData).Assembly;
        var resourceName = assembly.GetManifestResourceNames().FirstOrDefault(f => f.EndsWith(fileName))
            ?? throw new InvalidOperationException($"Resource {fileName} not found");
        using var resourceStream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Could not create resourcestream for file {fileName}");
        return JsonSerializer.Deserialize<NpcAppearanceData>(resourceStream, SerializerOptions)
            ?? throw new InvalidOperationException($"Could not deserialize file {fileName}");
    }

    public static NpcAppearanceData FromBase64(string data) {
        var jsonData = Convert.FromBase64String(data);

        return JsonSerializer.Deserialize<NpcAppearanceData>(jsonData, SerializerOptions)
            ?? throw new InvalidOperationException($"Could not deserialize file ");
    }

    public string ToBase64() {
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(this, SerializerOptions)));
    }
}

[Serializable]
public record WeaponModel {
    public ushort ModelSetId { get; init; }
    public ushort Base { get; init; }
    public ushort Variant { get; init; }
    public byte Stain0 { get; init; }
    public byte Stain1 { get; init; }

    public WeaponModel() { }
    public WeaponModel(ulong modelKey) {
        var parts = ItemModelData.DecodeWeapon(modelKey);
        ModelSetId = parts.ModelSet;
        Base = parts.ModelBase;
        Variant = parts.ModelVariant;
    }

    public ulong ToModelKey()
        => ItemModelData.CalculateModel(ModelSetId, Base, Variant);

    public unsafe void Apply(Character* actor, bool isMainHand) {

        if (ModelSetId == 0)
            return;

        var wep = new WeaponModelId() {
            Id = ModelSetId,
            Type = Base,
            Variant = Variant,
            Stain0 = Stain0,
            Stain1 = Stain1
        };

        actor->DrawData.LoadWeapon(isMainHand ? WeaponSlot.MainHand : WeaponSlot.OffHand, wep, 0, 0, 0, 0, false);
    }

    public static unsafe WeaponModel Read(Character* actor, WeaponSlot slot) {
        var model = actor->DrawData.Weapon(slot);
        var weapon = (WeaponModelId*)&model;

        return new WeaponModel {
            ModelSetId = weapon->Id,
            Base = weapon->Type,
            Variant = weapon->Variant,
            Stain0 = weapon->Stain0,
            Stain1 = weapon->Stain1
        };
    }
}

[Serializable]
public record EquipmentModel {

    public ushort ModelId { get; init; }
    public byte Variant { get; init; }
    public byte Stain0 { get; init; }
    public byte Stain1 { get; init; }

    public EquipmentModel() { }
    public EquipmentModel(ulong modelKey) {
        var parts = ItemModelData.DecodeEquipment(modelKey);
        ModelId = parts.ModelBase;
        Variant = (byte)parts.ModelVariant;
    }

    public ulong ToModelKey()
        => ItemModelData.CalculateModel(0, ModelId, Variant);

    public unsafe void Apply(Character* actor, EquipmentSlot index) {
        var item = new EquipmentModelId() {
            Id = ModelId,
            Variant = Variant,
            Stain0 = Stain0,
            Stain1 = Stain1
        };

        actor->DrawData.Equipment(index) = item;
    }

    public static unsafe EquipmentModel Read(Character* actor, EquipmentSlot index) {
        var model = actor->DrawData.Equipment(index);
        return new EquipmentModel {
            ModelId = model.Id,
            Variant = model.Variant,
            Stain0 = model.Stain0,
            Stain1 = model.Stain1
        };
    }

}
