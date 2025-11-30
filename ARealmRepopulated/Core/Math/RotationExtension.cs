namespace ARealmRepopulated.Core.Math;
public static class RotationExtension
{
    public static bool AlmostEqual(float currentRotation, float targetRotation, float epsilon = .0001f)
        => MathF.Abs(currentRotation - targetRotation) <= epsilon;


    public static float RotateToward(float currentRotation, float targetRotation, float distance)
    {
        var deltaRotation = targetRotation - currentRotation;
        if (deltaRotation <= -MathF.PI)
        {
            deltaRotation += MathF.Tau;
        }
        else if (deltaRotation > MathF.PI)
        {
            deltaRotation -= MathF.Tau;
        }

        if (MathF.Abs(deltaRotation) <= distance)
            return targetRotation;

        return currentRotation + MathF.Sign(deltaRotation) * distance;
    }

}
