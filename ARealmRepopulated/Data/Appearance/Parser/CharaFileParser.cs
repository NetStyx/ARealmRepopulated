using ARealmRepopulated.Core.Json;
using Dalamud.Plugin.Services;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ARealmRepopulated.Data.Appearance.Parser;

/// <summary>
/// These are usually generated from brio and anamnesis as far as i can tell and share the same format ... to an extent.
/// </summary>
[AppearanceParser(Priority = 1, Extension = ".chara")]
public class CharaFileParser(IPluginLog log) : IAppearanceFileParser {

    public NpcAppearanceData? TryParse(byte[] data) {

        string rawData;
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
            var appearance = CharaFileReader.Read(rawData);
            if (appearance.ModelCharaId != 0) {                
                log.Warning("The character file uses model {Model}, so its customize data may not apply as stored", [appearance.ModelCharaId]);
            }

            return appearance;
        } catch (InvalidDataException ex) {            
            log.Warning("Could not read the character file: {Reason}", [ex.Message]);
            return null;
        } catch (Exception ex) {
            log.Warning(ex, "Failed to parse chara file");
            return null;
        }
    }
}

public static class CharaFileReader {

    private static JsonSerializerOptions JsonOptions => new(JsonSerializerDefaults.Web) {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static NpcAppearanceData Read(string rawData) {

        JsonObject? json;
        try {
            json = JsonSerializer.Deserialize<JsonObject>(rawData, JsonOptions);
        } catch (JsonException ex) {
            throw new InvalidDataException("the file does not contain readable JSON", ex);
        }

        return Read(json ?? throw new InvalidDataException("the file does not contain a JSON object"));
    }

    public static NpcAppearanceData Read(JsonObject json) {

        var data = new NpcAppearanceData {
            ModelCharaId = json["ModelType"].GetIntOrNull() ?? 0,
            ModelSkeletonId = 0,

            Race = ReadRequired(json, "Race", n => n.GetEnum<NpcRace>()),
            Tribe = ReadRequired(json, "Tribe", n => n.GetEnum<NpcTribe>()),
            Sex = ReadRequired(json, "Gender", n => GetGender(n.GetString())),
            BodyType = GetBodyType(json["Age"].GetStringOrNull()),

            Height = json["Height"].GetByteOrNull(),
            Face = json["Head"].GetByteOrNull(),
            HairStyle = json["Hair"].GetByteOrNull(),
            Highlights = json["EnableHighlights"].GetBoolOrNull() switch {
                true => 128,
                false => 0,
                null => null,
            },
            SkinColor = json["Skintone"].GetByteOrNull(),
            EyeColorRight = json["REyeColor"].GetByteOrNull(),
            HairColor = json["HairTone"].GetByteOrNull(),
            HighlightsColor = json["Highlights"].GetByteOrNull(),

            FacialFeatures = GetFacialFeatures(json["FacialFeatures"].GetStringOrNull()),
            TattooColor = json["LimbalEyes"].GetByteOrNull(),

            Eyebrows = json["Eyebrows"].GetByteOrNull(),
            EyeColorLeft = json["LEyeColor"].GetByteOrNull(),
            EyeShape = json["Eyes"].GetByteOrNull(),
            Nose = json["Nose"].GetByteOrNull(),
            Jaw = json["Jaw"].GetByteOrNull(),
            Lipstick = json["Mouth"].GetByteOrNull(),
            LipColorFurPattern = json["LipsToneFurPattern"].GetByteOrNull(),
            MuscleMass = json["EarMuscleTailSize"].GetByteOrNull(),
            TailShape = json["TailEarsType"].GetByteOrNull(),
            BustSize = json["Bust"].GetByteOrNull(),
            FacePaint = json["FacePaint"].GetByteOrNull(),
            FacePaintColor = json["FacePaintColor"].GetByteOrNull(),
            
            Transparency = json["Transparency"].GetFloatOrNull(),

            Glasses = (json["Glasses"] as JsonObject)?["GlassesId"].GetUShortOrNull()            
        };

        data.MainHand = ReadWeapon(json["MainHand"] as JsonObject);
        data.OffHand = ReadWeapon(json["OffHand"] as JsonObject);

        data.HeadGear = ReadEquipment(json["HeadGear"] as JsonObject);
        data.Body = ReadEquipment(json["Body"] as JsonObject);
        data.Hands = ReadEquipment(json["Hands"] as JsonObject);
        data.Legs = ReadEquipment(json["Legs"] as JsonObject);
        data.Feet = ReadEquipment(json["Feet"] as JsonObject);
        data.Ears = ReadEquipment(json["Ears"] as JsonObject);
        data.Neck = ReadEquipment(json["Neck"] as JsonObject);
        data.Wrists = ReadEquipment(json["Wrists"] as JsonObject);
        data.LeftRing = ReadEquipment(json["LeftRing"] as JsonObject);
        data.RightRing = ReadEquipment(json["RightRing"] as JsonObject);

        data.HeightMultiplier = json["HeightMultiplier"].GetFloatOrNull();

        var extendedAppearance = new NpcExtendedAppearance {
            SkinColor = json["SkinColor"].GetVector3OrNull(),
            MuscleTone = json["MuscleTone"].GetFloatOrNull(),
            MouthColor = json["MouthColor"].GetVector4OrNull(),
            HairColor = json["HairColor"].GetVector3OrNull(),
            HairHighlight = json["HairHighlight"].GetVector3OrNull(),
            LeftEyeColor = json["LeftEyeColor"].GetVector3OrNull(),
            RightEyeColor = json["RightEyeColor"].GetVector3OrNull(),
            FeatureColor = json["LimbalRingColor"].GetVector3OrNull(),
        };
        data.ExtendedAppearance = extendedAppearance.HasAnyValue ? extendedAppearance : null;

        return data;
    }
  
    private static T ReadRequired<T>(JsonObject json, string key, Func<JsonNode, T> read) {
        var node = json[key]
            ?? throw new InvalidDataException($"the '{key}' entry is missing");

        try {
            return read(node);
        } catch (Exception ex) {
            throw new InvalidDataException($"the '{key}' entry holds {node.ToJsonString()}, which is not a value we know", ex);
        }
    }

    private static NpcBodyType GetBodyType(string? ageString)
        => (ageString ?? "").ToLower() switch {
            "none" => NpcBodyType.Unknown,
            "normal" => NpcBodyType.Normal,
            "old" => NpcBodyType.Old,
            "young" => NpcBodyType.Young,
            _ => NpcBodyType.Normal,
        };

    private static NpcSex GetGender(string genderString)
        => genderString.ToLower() switch {
            "feminine" or "female" => NpcSex.Female,
            "masculine" or "male" => NpcSex.Male,
            _ => throw new ArgumentOutOfRangeException(nameof(genderString)),
        };

    private static WeaponModel? ReadWeapon(JsonObject? obj) {
        if (obj == null)
            return null;

        return new WeaponModel {
            ModelSetId = obj["ModelSet"].GetUShortOrNull() ?? 0,
            Base = obj["ModelBase"].GetUShortOrNull() ?? 0,
            Variant = obj["ModelVariant"].GetUShortOrNull() ?? 0,
            Stain0 = obj["DyeId"].GetByteOrNull() ?? 0,
            Stain1 = obj["DyeId2"].GetByteOrNull() ?? 0
        };
    }

    private static EquipmentModel? ReadEquipment(JsonObject? obj) {
        if (obj == null)
            return null;

        return new EquipmentModel {
            ModelId = obj["ModelBase"].GetUShortOrNull() ?? 0,
            Variant = obj["ModelVariant"].GetByteOrNull() ?? 0,
            Stain0 = obj["DyeId"].GetByteOrNull() ?? 0,
            Stain1 = obj["DyeId2"].GetByteOrNull() ?? 0
        };
    }

    private static byte? GetFacialFeatures(string? facialFeature) {
        if (facialFeature == null)
            return null;

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
