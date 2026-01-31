using System.Numerics;
using CsVector3 = FFXIVClientStructs.FFXIV.Common.Math.Vector3;

namespace ARealmRepopulated.Core.SpatialMath;

public static class RotationExtension {
    public static bool AlmostEqual(float currentRotation, float targetRotation, float epsilon = .0001f)
        => MathF.Abs(currentRotation - targetRotation) <= epsilon;

    public static float RotateToward(float currentRotation, float targetRotation, float distance) {
        var deltaRotation = targetRotation - currentRotation;
        if (deltaRotation <= -MathF.PI) {
            deltaRotation += MathF.Tau;
        } else if (deltaRotation > MathF.PI) {
            deltaRotation -= MathF.Tau;
        }

        if (MathF.Abs(deltaRotation) <= distance)
            return targetRotation;

        return currentRotation + (MathF.Sign(deltaRotation) * distance);
    }

    public static float AngleTowards(this Vector3 from, Vector3 to)
        => MathF.Atan2(to.X - from.X, to.Z - from.Z);

    public static float AngleTowards(this CsVector3 from, CsVector3 to)
        => MathF.Atan2(to.X - from.X, to.Z - from.Z);

}
