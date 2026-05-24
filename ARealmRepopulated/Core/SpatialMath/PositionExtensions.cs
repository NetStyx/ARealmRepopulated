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

    public static CsVector3 Forward(this CsVector3 from, float directionInRad, float distance) {
        var forward = new CsVector3(MathF.Sin(directionInRad), 0f, MathF.Cos(directionInRad));
        return from + (forward * distance);
    }

    public static bool IsInCylinderRange(this CsVector3 centerPosition, CsVector3 targetPosition, float radius) {
        var dx = targetPosition.X - centerPosition.X;
        var dz = targetPosition.Z - centerPosition.Z;
        var dy = targetPosition.Y - centerPosition.Y;

        var distanceXzSq = (dx * dx) + (dz * dz);
        var radiusSq = radius * radius;

        return distanceXzSq <= radiusSq && MathF.Abs(dy) <= radius;
    }

    public static float DistanceSquaredTo(this CsVector3 referencePosition, CsVector3 targetPosition) {
        var dx = targetPosition.X - referencePosition.X;
        var dy = targetPosition.Y - referencePosition.Y;
        var dz = targetPosition.Z - referencePosition.Z;

        return (dx * dx) + (dy * dy) + (dz * dz);
    }

    public static float DistanceTo(this CsVector3 referencePosition, CsVector3 targetPosition)
        => MathF.Sqrt(referencePosition.DistanceSquaredTo(targetPosition));

}
