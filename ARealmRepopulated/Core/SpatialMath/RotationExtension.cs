using System.Numerics;
using CsQuaternion = FFXIVClientStructs.FFXIV.Common.Math.Quaternion;

namespace ARealmRepopulated.Core.SpatialMath;

public static class RotationExtension {

    public static bool AlmostEqual(float currentRotation, float targetRotation, float epsilon = .0001f)
        => MathF.Abs(currentRotation - targetRotation) <= epsilon;

    public static float RotateToward(float currentRotation, float targetRotation, float distance) {
        var deltaRotation = targetRotation - currentRotation;
        deltaRotation = NormalizeRadians(deltaRotation);

        if (MathF.Abs(deltaRotation) <= distance)
            return targetRotation;

        return currentRotation + (MathF.Sign(deltaRotation) * distance);
    }

    public static float NormalizeRadians(float rotation) {
        while (rotation <= -MathF.PI)
            rotation += MathF.Tau;

        while (rotation > MathF.PI)
            rotation -= MathF.Tau;

        return rotation;
    }

    // Taken from the decompiled code of the game. 
    private const float CppEpsilon = 1.1920929e-07f;
    public static float FacingFromRotation(this Quaternion rotation) {
        var forward = Vector3.Transform(new Vector3(0f, 0f, 1f), rotation);

        if (((forward.X * forward.X) + (forward.Z * forward.Z)) <= CppEpsilon)
            return 0f;

        return MathF.Atan2(forward.X, forward.Z);
    }

    public static float FacingFromRotation(this CsQuaternion rotation) {
        var q = new Quaternion(rotation.X, rotation.Y, rotation.Z, rotation.W);
        return q.FacingFromRotation();
    }

}
