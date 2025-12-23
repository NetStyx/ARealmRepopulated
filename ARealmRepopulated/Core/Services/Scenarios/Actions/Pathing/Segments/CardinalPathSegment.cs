using System.Numerics;

namespace ARealmRepopulated.Core.Services.Scenarios.Actions.Pathing.Segments;

public class CardinalSegment : IPathSegment {
    private readonly Vector3 _p0;
    private readonly Vector3 _p1;
    private readonly Vector3 _p2;
    private readonly Vector3 _p3;

    private readonly float[] _sampleTs;
    private readonly float[] _sampleDistances;

    private readonly float _tension;

    public float Length { get; }
    public float Speed { get; }

    public CardinalSegment(float speed, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, int samples, float tension) {
        Speed = speed;
        _p0 = p0;
        _p1 = p1;
        _p2 = p2;
        _p3 = p3;
        _tension = Math.Clamp(tension, 0f, 1f);

        if (samples < 2) samples = 2;

        _sampleTs = new float[samples + 1];
        _sampleDistances = new float[samples + 1];

        _sampleTs[0] = 0f;
        _sampleDistances[0] = 0f;

        var prev = EvaluatePosition(0f);
        var cumulative = 0f;

        for (var i = 1; i <= samples; i++) {
            var t = (float)i / samples;
            _sampleTs[i] = t;

            var pos = EvaluatePosition(t);
            cumulative += (pos - prev).Length();
            _sampleDistances[i] = cumulative;

            prev = pos;
        }

        Length = cumulative;
    }

    public void Evaluate(float t, out Vector3 position, out Vector3 tangent) {
        t = Math.Clamp(t, 0f, 1f);
        position = EvaluatePosition(t);
        tangent = EvaluateTangent(t);

        if (tangent.LengthSquared() > 1e-8f)
            tangent = Vector3.Normalize(tangent);
        else
            tangent = Vector3.UnitZ;
    }

    public float GetTForDistance(float distanceOnSegment) {
        if (Length <= 1e-6f)
            return 0f;

        distanceOnSegment = Math.Clamp(distanceOnSegment, 0f, Length);

        for (var i = 1; i < _sampleDistances.Length; i++) {
            var d0 = _sampleDistances[i - 1];
            var d1 = _sampleDistances[i];

            if (distanceOnSegment <= d1) {
                var t0 = _sampleTs[i - 1];
                var t1 = _sampleTs[i];

                var span = d1 - d0;
                var u = span > 1e-6f ? (distanceOnSegment - d0) / span : 0f;

                return t0 + ((t1 - t0) * u);
            }
        }

        return 1f;
    }

    private Vector3 EvaluatePosition(float t) {
        var t2 = t * t;
        var t3 = t2 * t;

        // Cardinal spline via Hermite form:        
        var s = _tension;
        var k = (1f - s) * 0.5f;

        var m1 = k * (_p2 - _p0);
        var m2 = k * (_p3 - _p1);

        var h00 = (2f * t3) - (3f * t2) + 1f;
        var h10 = t3 - (2f * t2) + t;
        var h01 = (-2f * t3) + (3f * t2);
        var h11 = t3 - t2;

        return (h00 * _p1) + (h10 * m1) + (h01 * _p2) + (h11 * m2);
    }

    private Vector3 EvaluateTangent(float t) {
        var t2 = t * t;

        var s = _tension;
        var k = (1f - s) * 0.5f;

        var m1 = k * (_p2 - _p0);
        var m2 = k * (_p3 - _p1);

        // Derivatives of the Hermite basis functions
        var dh00 = (6f * t2) - (6f * t);
        var dh10 = (3f * t2) - (4f * t) + 1f;
        var dh01 = (-6f * t2) + (6f * t);
        var dh11 = (3f * t2) - (2f * t);

        return (dh00 * _p1) + (dh10 * m1) + (dh01 * _p2) + (dh11 * m2);
    }

    /*
    private Vector3 EvaluatePosition(float t) {
        var t2 = t * t;
        var t3 = t2 * t;

        // Cardinal spline via Hermite form:
        // Tension: 0 = Catmull-Rom (smooth), 1 = linear-ish (sharper)
        var s = _tension;
        var k = (1f - s) * 0.5f;

        var m1 = k * (_p2 - _p0);
        var m2 = k * (_p3 - _p1);

        var h00 = (2f * t3) - (3f * t2) + 1f;
        var h10 = t3 - (2f * t2) + t;
        var h01 = (-2f * t3) + (3f * t2);
        var h11 = t3 - t2;

        return (h00 * _p1) + (h10 * m1) + (h01 * _p2) + (h11 * m2);
    }

    private Vector3 EvaluateTangent(float t) {
        var t2 = t * t;

        var s = _tension;
        var k = (1f - s) * 0.5f;

        var m1 = k * (_p2 - _p0);
        var m2 = k * (_p3 - _p1);

        // Derivatives of the Hermite basis functions
        var dh00 = (6f * t2) - (6f * t);
        var dh10 = (3f * t2) - (4f * t) + 1f;
        var dh01 = (-6f * t2) + (6f * t);
        var dh11 = (3f * t2) - (2f * t);

        return (dh00 * _p1) + (dh10 * m1) + (dh01 * _p2) + (dh11 * m2);
    }     
     
     */

}
