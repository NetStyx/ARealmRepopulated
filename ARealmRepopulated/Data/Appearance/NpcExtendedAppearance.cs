using FFXIVClientStructs.FFXIV.Common.Math;
using System.Text.Json.Serialization;

namespace ARealmRepopulated.Data.Appearance;

public class NpcExtendedAppearance {

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
}
