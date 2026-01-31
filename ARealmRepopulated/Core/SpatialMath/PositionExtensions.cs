using System.Numerics;
using CsVector3 = FFXIVClientStructs.FFXIV.Common.Math.Vector3;

namespace ARealmRepopulated.Core.SpatialMath;

public static class PositionExtensions {
    public static float DirectionTo(this CsVector3 from, CsVector3 to)
        => MathF.Atan2(to.X - from.X, to.Z - from.Z);

    public static float DirectionTo(this Vector3 from, Vector3 to)
        => MathF.Atan2(to.X - from.X, to.Z - from.Z);

    public static Vector3 Forward(this Vector3 from, float directionInRad, float distance) {
        var forward = new Vector3(MathF.Sin(directionInRad), 0f, MathF.Cos(directionInRad));
        return from + (forward * distance);
    }
}
