using FFXIVClientStructs.FFXIV.Common.Math;

namespace ARealmRepopulated.Core.Math;

public static class PositionExtensions {

    public static float DirectionTo(this Vector3 from, Vector3 to)
        => MathF.Atan2(to.X - from.X, to.Z - from.Z);


}
