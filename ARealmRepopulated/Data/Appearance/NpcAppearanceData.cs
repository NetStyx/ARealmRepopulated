using Dalamud.Plugin;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using System.IO;
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
    Lipstick = 0x13,
    LipColorFurPattern = 0x14,
    MuscleMass = 0x15,
    TailShape = 0x16,
    BustSize = 0x17,
    FacePaint = 0x18,
    FacePaintColor = 0x19
}

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

public class NpcAppearanceData {

    public static readonly NpcAppearanceData Default = FromResource("DefaultHumanFemale.json")!;

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
    public byte? Lipstick { get; set; }
    public byte? LipColorFurPattern { get; set; }
    public byte? MuscleMass { get; set; }
    public byte? TailShape { get; set; }
    public byte? BustSize { get; set; }
    public byte? FacePaint { get; set; }
    public byte? FacePaintColor { get; set; }

    public ushort? Glasses { get; set; }

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

    public bool HideWeapons { get; set; } = true;
    public bool HideHeadgear { get; set; } = true;

    public static NpcAppearanceData? FromResource(string fileName) {
        var assembly = typeof(NpcAppearanceData).Assembly;
        var resourceName = assembly.GetManifestResourceNames().FirstOrDefault(f => f.EndsWith(fileName))
            ?? throw new InvalidOperationException($"Resource {fileName} not found");
        using var resourceStream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Could not create resourcestream for file {fileName}");
        return JsonSerializer.Deserialize<NpcAppearanceData>(resourceStream)
            ?? throw new InvalidOperationException($"Could not deserialize file {fileName}");
    }

    public static NpcAppearanceData FromBase64(string data) {
        var jsonData = Convert.FromBase64String(data);

        return JsonSerializer.Deserialize<NpcAppearanceData>(jsonData)
            ?? throw new InvalidOperationException($"Could not deserialize file ");
    }

    public string ToBase64() {
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(this)));
    }

    public void Save(SaveFormat format = SaveFormat.Json) {
        var filePath = Path.Combine(Plugin.Services.GetRequiredService<IDalamudPluginInterface>().GetPluginConfigDirectory(), "appearance", this.AppearanceId.ToString() + ".arrpc");

        string exportData;
        if (format == SaveFormat.Base64) {
            exportData = ToBase64();
        } else {
            exportData = JsonSerializer.Serialize(this);

        }
        File.WriteAllText(filePath, exportData);
    }

    public enum SaveFormat {
        Json,
        Base64
    }
}

[Serializable]
public class WeaponModel {
    public ushort ModelSetId { get; set; }
    public ushort Base { get; set; }
    public ushort Variant { get; set; }
    public byte Stain0 { get; set; }
    public byte Stain1 { get; set; }

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

        actor->DrawData.LoadWeapon(isMainHand ? WeaponSlot.MainHand : WeaponSlot.OffHand, wep, 0, 0, 0, 0);
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
public class EquipmentModel {

    public ushort ModelId { get; set; }
    public byte Variant { get; set; }
    public byte Stain0 { get; set; }
    public byte Stain1 { get; set; }

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
