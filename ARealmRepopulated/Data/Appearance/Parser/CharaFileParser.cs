using ARealmRepopulated.Core.Json;
using Dalamud.Plugin.Services;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ARealmRepopulated.Data.Appearance.Parser;

/// <summary>
/// These are usually generated from brio and anamnesis as far as i can tell and share the same format ... to an extent.
/// </summary>
[AppearanceParser(Priority = 1, Extension = ".chara")]
public class CharaFileParser(IPluginLog log) : IAppearanceFileParser {

    private static JsonSerializerOptions JsonOptions => new(JsonSerializerDefaults.Web) {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public NpcAppearanceData? TryParse(byte[] data) {

        string rawData;
        JsonObject jsonData;
        try {

            rawData = Encoding.UTF8.GetString(data);
            var parsedSpan = new Span<byte>(new byte[4096]);
            if (Convert.TryFromBase64String(rawData, parsedSpan, out var writtenData)) {
                rawData = Encoding.UTF8.GetString(parsedSpan[..writtenData]);
            }
        } catch (Exception ex) {
            log.Error(ex, "Could not parse the given structure");
            return null;
        }

        try {
            jsonData = JsonSerializer.Deserialize<JsonObject>(rawData, JsonOptions) ?? [];
        } catch (Exception ex) {
            log.Error(ex, "Could not parse the given JSON structure");
            return null;
        }

        if (jsonData == null) {
            log.Error("Received no valid json object");
            return null;
        }

        try {
            return TryParseInternal(jsonData);
        } catch (Exception ex) {
            log.Warning(ex, "Failed to parse chara file");
            return null;
        }
    }

    private static NpcAppearanceData? TryParseInternal(JsonObject json) {

        var data = new NpcAppearanceData {
            ModelCharaId = json["ModelType"]!.GetInt(),
            ModelSkeletonId = 0,

            Race = json["Race"]!.GetEnum<NpcRace>(),
            Tribe = json["Tribe"]!.GetEnum<NpcTribe>(),
            BodyType = GetBodyType(json["Age"]!.GetString()),
            Sex = GetGender(json["Gender"]!.GetString()),

            Height = json["Height"]!.GetByte(),
            Face = json["Head"]!.GetByte(),
            HairStyle = json["Hair"]!.GetByte(),
            Highlights = json["EnableHighlights"]!.GetValue<bool>() ? (byte)128 : (byte)0,
            SkinColor = json["Skintone"]!.GetByte(),
            EyeColorRight = json["REyeColor"]!.GetByte(),
            HairColor = json["HairTone"]!.GetByte(),
            HighlightsColor = json["Highlights"]!.GetByte(),

            FacialFeatures = GetFacialFeatures(json["FacialFeatures"]!.GetString()),
            TattooColor = json["LimbalEyes"]!.GetByte(),

            Eyebrows = json["Eyebrows"]!.GetByte(),
            EyeColorLeft = json["LEyeColor"]!.GetByte(),
            EyeShape = json["Eyes"]!.GetByte(),
            Nose = json["Nose"]!.GetByte(),
            Jaw = json["Jaw"]!.GetByte(),
            Lipstick = json["Mouth"]!.GetByte(),
            LipColorFurPattern = json["LipsToneFurPattern"]!.GetByte(),
            MuscleMass = json["EarMuscleTailSize"]!.GetByte(),
            TailShape = json["TailEarsType"]!.GetByte(),
            BustSize = json["Bust"]!.GetByte(),
            FacePaint = json["FacePaint"]!.GetByte(),
            FacePaintColor = json["FacePaintColor"]!.GetByte(),

            Transparency = json["Transparency"]!.GetValue<ushort>(),

            Glasses = (json["Glasses"] as JsonObject)?["GlassesId"]?.GetValue<ushort>() ?? 0,
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
}
