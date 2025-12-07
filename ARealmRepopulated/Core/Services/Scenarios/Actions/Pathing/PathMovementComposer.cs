using ARealmRepopulated.Core.Services.Scenarios.Actions.Pathing.Segments;
using System.Numerics;

namespace ARealmRepopulated.Core.Services.Scenarios.Actions.Pathing;


public class PathMovementComposer {
    private readonly List<IPathSegment> _segments = new();
    private readonly List<float> _cumulativeLengths = new();
    public float TotalLength { get; private set; }
    public int SegmentCount => _segments.Count;

    private float _tension = 0.5f;

    public float GetSegmentStartDistance(int segmentIndex)
        => segmentIndex == 0 ? 0f : _cumulativeLengths[segmentIndex - 1];

    public float GetSegmentEndDistance(int segmentIndex)
        => _cumulativeLengths[segmentIndex];

    public float GetSegmentSpeed(int segmentIndex)
        => _segments[segmentIndex].Speed;

    public int FindSegmentIndexByDistance(float distance) {
        if (_segments.Count == 0)
            return 0;

        if (distance <= 0f)
            return 0;

        if (distance >= TotalLength)
            return _segments.Count - 1;

        for (var i = 0; i < _cumulativeLengths.Count; i++) {
            if (distance <= _cumulativeLengths[i])
                return i;
        }

        return _segments.Count - 1;
    }

    public void Calculate(List<PathSegmentPoint> points, float tension = 0.5f, int samplesPerCurveSegment = 16) {

        if (points.Count < 1)
            return;

        _tension = Math.Clamp(tension, 0f, 1f);

        if (ArePointsColinear(points)) {
            BuildLinearSegments(points);
        } else {
            BuildSplineSegments(points, samplesPerCurveSegment);
        }
    }

    public void Evaluate(float distance, out Vector3 position, out Vector3 tangent) {
        if (_segments.Count == 0) {
            position = default;
            tangent = Vector3.UnitZ;
            return;
        }

        if (distance <= 0f) {
            _segments[0].Evaluate(0f, out position, out tangent);
            return;
        }

        if (distance >= TotalLength) {
            _segments[^1].Evaluate(1f, out position, out tangent);
            return;
        }

        var segIndex = 0;
        for (var i = 0; i < _cumulativeLengths.Count; i++) {
            if (distance <= _cumulativeLengths[i]) {
                segIndex = i;
                break;
            }
        }

        var prevCum = segIndex == 0 ? 0f : _cumulativeLengths[segIndex - 1];
        var localDist = distance - prevCum;

        var seg = _segments[segIndex];
        var t = seg.GetTForDistance(localDist);

        seg.Evaluate(t, out position, out tangent);
    }



    private void BuildLinearSegments(List<PathSegmentPoint> points) {
        _segments.Clear();
        _cumulativeLengths.Clear();
        TotalLength = 0f;

        for (var i = 0; i < points.Count - 1; i++) {
            var seg = new LinearPathSegment(points[i].Speed, points[i].Point, points[i + 1].Point);
            if (seg.Length <= 1e-6f)
                continue;

            TotalLength += seg.Length;
            _segments.Add(seg);
            _cumulativeLengths.Add(TotalLength);
        }

        if (_segments.Count == 0) {
            var seg = new LinearPathSegment(points[0].Speed, points[0].Point, points[0].Point);
            _segments.Add(seg);
            _cumulativeLengths.Add(0f);
            TotalLength = 0f;
        }
    }

    private void BuildSplineSegments(List<PathSegmentPoint> points, int samplesPerCurveSegment) {
        _segments.Clear();
        _cumulativeLengths.Clear();
        TotalLength = 0f;

        var n = points.Count;

        for (var i = 0; i < n - 1; i++) {
            var p1 = points[i].Point;
            var p2 = points[i + 1].Point;

            var p0 = i == 0
                ? points[0].Point + (points[0].Point - points[1].Point)
                : points[i - 1].Point;

            var p3 = (i + 2 < n)
                ? points[i + 2].Point
                : points[n - 1].Point + (points[n - 1].Point - points[n - 2].Point);

            var seg = new CatmullRomPathSegment(points[i].Speed, p0, p1, p2, p3, samplesPerCurveSegment, _tension);
            if (seg.Length <= 1e-6f)
                continue;

            TotalLength += seg.Length;
            _segments.Add(seg);
            _cumulativeLengths.Add(TotalLength);
        }

        if (_segments.Count == 0) {
            BuildLinearSegments(points);
        }
    }

    private static bool ArePointsColinear(List<PathSegmentPoint> points, float tolerance = 1e-4f) {
        if (points.Count <= 2)
            return true;

        var baseDir = points[^1].Point - points[0].Point;
        var baseLenSq = ((Vector3)baseDir).LengthSquared();
        if (baseLenSq < 1e-8f)
            return false;

        var n = baseDir / MathF.Sqrt(baseLenSq);
        for (var i = 1; i < points.Count - 1; i++) {
            var v = points[i].Point - points[0].Point;
            var cross = Vector3.Cross(n, v);

            if (cross.LengthSquared() > tolerance)
                return false;
        }

        return true;
    }
}
