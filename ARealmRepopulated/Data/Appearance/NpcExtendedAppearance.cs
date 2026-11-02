using FFXIVClientStructs.FFXIV.Common.Math;
using System.Text.Json.Serialization;

namespace ARealmRepopulated.Data.Appearance;

public record NpcExtendedAppearance {

    public Vector3? SkinColor { get; set; }
    public float? MuscleTone { get; set; }
    public Vector4? MouthColor { get; set; }
    public Vector3? HairColor { get; set; }
    public Vector3? HairHighlight { get; set; }
    public Vector3? LeftEyeColor { get; set; }
    public Vector3? RightEyeColor { get; set; }
    public Vector3? FeatureColor { get; set; }

    [JsonIgnore]
    public bool HasAnyValue
        => SkinColor.HasValue
        || MuscleTone.HasValue
        || MouthColor.HasValue
        || HairColor.HasValue
        || HairHighlight.HasValue
        || LeftEyeColor.HasValue
        || RightEyeColor.HasValue
        || FeatureColor.HasValue;

    public Vector3? GetColor(CustomizeIndex index) {
        var stored = index switch {
            CustomizeIndex.SkinColor => SkinColor,
            CustomizeIndex.LipColorFurPattern => MouthColor is { } mouth ? new Vector3(mouth.X, mouth.Y, mouth.Z) : null,
            CustomizeIndex.HairColor => HairColor,
            CustomizeIndex.HighlightsColor => HairHighlight,
            CustomizeIndex.EyeColorLeft => LeftEyeColor,
            CustomizeIndex.EyeColorRight => RightEyeColor,
            CustomizeIndex.FacialFeaturesColor => FeatureColor,
            _ => null
        };

        return stored is { } color ? new Vector3(MathF.Sqrt(color.X), MathF.Sqrt(color.Y), MathF.Sqrt(color.Z)) : null;
    }

    public static bool SupportsColor(CustomizeIndex index)
        => index is CustomizeIndex.SkinColor or CustomizeIndex.LipColorFurPattern or CustomizeIndex.HairColor or CustomizeIndex.HighlightsColor
            or CustomizeIndex.EyeColorLeft or CustomizeIndex.EyeColorRight or CustomizeIndex.FacialFeaturesColor;

    public void SetColor(CustomizeIndex index, Vector3 color, float defaultOpacity) {
        var stored = new Vector3(color.X * color.X, color.Y * color.Y, color.Z * color.Z);
        switch (index) {
            case CustomizeIndex.SkinColor: SkinColor = stored; break;
            case CustomizeIndex.LipColorFurPattern: MouthColor = new Vector4(stored.X, stored.Y, stored.Z, MouthColor?.W ?? defaultOpacity); break;
            case CustomizeIndex.HairColor: HairColor = stored; break;
            case CustomizeIndex.HighlightsColor: HairHighlight = stored; break;
            case CustomizeIndex.EyeColorLeft: LeftEyeColor = stored; break;
            case CustomizeIndex.EyeColorRight: RightEyeColor = stored; break;
            case CustomizeIndex.FacialFeaturesColor: FeatureColor = stored; break;
        }
    }

    // An exact color wins over the palette value
    public void Forget(CustomizeIndex index) {
        switch (index) {
            case CustomizeIndex.SkinColor: SkinColor = null; MuscleTone = null; break;
            case CustomizeIndex.MuscleMass: MuscleTone = null; break;
            case CustomizeIndex.Mouth:
            case CustomizeIndex.LipColorFurPattern: MouthColor = null; break;
            case CustomizeIndex.HairColor: HairColor = null; break;
            case CustomizeIndex.Highlights:
            case CustomizeIndex.HighlightsColor: HairHighlight = null; break;
            case CustomizeIndex.EyeColorLeft: LeftEyeColor = null; break;
            case CustomizeIndex.EyeColorRight: RightEyeColor = null; break;
            case CustomizeIndex.FacialFeaturesColor: FeatureColor = null; break;
        }
    }
}
