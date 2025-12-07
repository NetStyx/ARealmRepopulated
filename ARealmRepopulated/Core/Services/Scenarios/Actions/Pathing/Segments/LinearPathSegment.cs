using System.Numerics;

namespace ARealmRepopulated.Core.Services.Scenarios.Actions.Pathing.Segments;

public class LinearPathSegment : IPathSegment {
    private readonly Vector3 _a;
    private readonly Vector3 _b;
    private readonly Vector3 _dir;
    public float Length { get; }
    public float Speed { get; }

    public LinearPathSegment(float speed, Vector3 a, Vector3 b) {
        Speed = speed;
        _a = a;
        _b = b;
        _dir = b - a;
        Length = _dir.Length();
    }

    public void Evaluate(float t, out Vector3 position, out Vector3 tangent) {
        t = Math.Clamp(t, 0f, 1f);
        position = Vector3.Lerp(_a, _b, t);
        tangent = Length > 1e-6f ? Vector3.Normalize(_dir) : Vector3.UnitZ;
    }

    public float GetTForDistance(float distanceOnSegment) {
        if (Length <= 1e-6f)
            return 0f;

        distanceOnSegment = Math.Clamp(distanceOnSegment, 0f, Length);
        return distanceOnSegment / Length;
    }
}
