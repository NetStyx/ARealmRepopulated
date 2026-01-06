using System.Numerics;
using System.Text.Json.Nodes;

namespace ARealmRepopulated.Data.Appearance.Parser;

[AppearanceParser(Priority = 1, Extension = ".chara")]
public class AnamnesisParser : IAppearanceFileParser {

    public static readonly string AnamnesisFileIdentification = "Anamnesis Character File";

    public NpcAppearanceData? TryParse(JsonObject json) {

        if (!json.TryGetPropertyValue("TypeName", out var contentType))
            return null;

        if (contentType == null)
            return null;

        if (!contentType.GetValue<string>().Equals(AnamnesisFileIdentification))
            return null;

        var data = new NpcAppearanceData {
            ModelCharaId = json["ModelType"]!.GetValue<int>(),
            ModelSkeletonId = 0,

            Race = Enum.Parse<NpcRace>(json["Race"]!.GetValue<string>()),
            Tribe = Enum.Parse<NpcTribe>(json["Tribe"]!.GetValue<string>()),
            BodyType = GetBodyType(json["Age"]!.GetValue<string>()),
            Sex = GetGender(json["Gender"]!.GetValue<string>()),

            Height = json["Height"]!.GetValue<byte>(),
            Face = json["Head"]!.GetValue<byte>(),
            HairStyle = json["Hair"]!.GetValue<byte>(),
            Highlights = json["EnableHighlights"]!.GetValue<bool>() ? (byte)128 : (byte)0,
            SkinColor = json["Skintone"]!.GetValue<byte>(),
            EyeColorRight = json["REyeColor"]!.GetValue<byte>(),
            HairColor = json["HairTone"]!.GetValue<byte>(),
            HighlightsColor = json["Highlights"]!.GetValue<byte>(),

            FacialFeatures = GetFacialFeatures(json["FacialFeatures"]!.GetValue<string>()),
            TattooColor = json["LimbalEyes"]!.GetValue<byte>(),

            Eyebrows = json["Eyebrows"]!.GetValue<byte>(),
            EyeColorLeft = json["LEyeColor"]!.GetValue<byte>(),
            EyeShape = json["Eyes"]!.GetValue<byte>(),
            Nose = json["Nose"]!.GetValue<byte>(),
            Jaw = json["Jaw"]!.GetValue<byte>(),
            Lipstick = json["Mouth"]!.GetValue<byte>(),
            LipColorFurPattern = json["LipsToneFurPattern"]!.GetValue<byte>(),
            MuscleMass = json["EarMuscleTailSize"]!.GetValue<byte>(),
            TailShape = json["TailEarsType"]!.GetValue<byte>(),
            BustSize = json["Bust"]!.GetValue<byte>(),
            FacePaint = json["FacePaint"]!.GetValue<byte>(),
            FacePaintColor = json["FacePaintColor"]!.GetValue<byte>(),

            Transparency = json["Transparency"]!.GetValue<ushort>(),

            Glasses = json["Glasses"]!["GlassesId"]!.GetValue<ushort>(),
        };

        data.MainHand = ParseWeapon(json["MainHand"]!.AsObject());
        data.OffHand = ParseWeapon(json["OffHand"]!.AsObject());

        data.HeadGear = ParseEquipment(json["HeadGear"]!.AsObject());
        data.Body = ParseEquipment(json["Body"]!.AsObject());
        data.Hands = ParseEquipment(json["Hands"]!.AsObject());
        data.Legs = ParseEquipment(json["Legs"]!.AsObject());
        data.Feet = ParseEquipment(json["Feet"]!.AsObject());
        data.Ears = ParseEquipment(json["Ears"]!.AsObject());
        data.Neck = ParseEquipment(json["Neck"]!.AsObject());
        data.Wrists = ParseEquipment(json["Wrists"]!.AsObject());
        data.LeftRing = ParseEquipment(json["LeftRing"]!.AsObject());
        data.RightRing = ParseEquipment(json["RightRing"]!.AsObject());

        return data;
    }

    private static NpcBodyType GetBodyType(string ageString)
        => ageString.ToLower() switch {
            "none" => NpcBodyType.Unknown,
            "normal" => NpcBodyType.Normal,
            "old" => NpcBodyType.Old,
            "young" => NpcBodyType.Young,
            _ => NpcBodyType.Unknown,
        };

    private static NpcSex GetGender(string genderString)
        => genderString.ToLower() switch {
            "feminine" => NpcSex.Female,
            _ => NpcSex.Male,
        };

    private static WeaponModel ParseWeapon(JsonObject obj)
        => new() {
            ModelSetId = obj["ModelSet"]!.GetValue<ushort>(),
            Base = obj["ModelBase"]!.GetValue<ushort>(),
            Variant = obj["ModelVariant"]!.GetValue<ushort>(),
            Stain0 = obj["DyeId"]!.GetValue<byte>(),
            Stain1 = obj["DyeId2"]!.GetValue<byte>()
        };

    private static EquipmentModel ParseEquipment(JsonObject obj)
        => new() {
            ModelId = obj["ModelBase"]!.GetValue<ushort>(),
            Variant = obj["ModelVariant"]!.GetValue<byte>(),
            Stain0 = obj["DyeId"]!.GetValue<byte>(),
            Stain1 = obj["DyeId2"]!.GetValue<byte>()
        };

    private static byte GetFacialFeatures(string facialFeature) {
        var values = facialFeature.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        byte result = 0;
        foreach (var val in values) {

            switch (val.ToLower()) {
                case "first":
                    result |= 1;
                    break;

                case "second":
                    result |= 2;
                    break;

                case "third":
                    result |= 4;
                    break;

                case "fourth":
                    result |= 8;
                    break;

                case "fifth":
                    result |= 16;
                    break;

                case "sixth":
                    result |= 32;
                    break;

                case "seventh":
                    result |= 64;
                    break;

                case "legacytattoo":
                    result |= 128;
                    break;
            }

        }
        return result;
    }

    private static Vector3 GetVector(string vectorString) {
        var values = vectorString.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (values == null || values.Length != 3)
            return Vector3.Zero;

        return new Vector3(float.Parse(values[0]), float.Parse(values[1]), float.Parse(values[2]));
    }

}
