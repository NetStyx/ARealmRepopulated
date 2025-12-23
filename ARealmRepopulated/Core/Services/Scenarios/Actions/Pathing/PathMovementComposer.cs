using ARealmRepopulated.Core.Services.Scenarios.Actions.Pathing.Segments;
using System.Numerics;

namespace ARealmRepopulated.Core.Services.Scenarios.Actions.Pathing;

public readonly struct PathSample {
    public Vector3 Position { get; init; }
    public Vector3 Tangent { get; init; }
    public float Yaw { get; init; }
    public int SegmentIndex { get; init; }
    public float LocalT { get; init; }
    public float Speed { get; init; }
}

public class PathMovementComposer {
    private readonly List<IPathSegment> _segments = [];
    private readonly List<float> _cumulativeLengths = [];
    private List<PathSegmentPoint> _sourcePoints = [];

    public float TotalLength { get; private set; }
    public int SegmentCount => _segments.Count;

    private float _tension = 0.5f;

    public float GetSegmentSpeed(int segmentIndex)
         => _segments[segmentIndex].Speed;

    public float GetSegmentStartDistance(int segmentIndex)
        => segmentIndex == 0 ? 0f : _cumulativeLengths[segmentIndex - 1];

    public float GetSegmentEndDistance(int segmentIndex)
        => _cumulativeLengths[segmentIndex];

    public float GetSegmentLength(int segmentIndex)
        => _segments[segmentIndex].Length;

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
        _segments.Clear();
        _cumulativeLengths.Clear();
        TotalLength = 0f;

        if (points is null || points.Count < 2)
            return;

        _tension = Math.Clamp(tension, 0f, 1f);
        _sourcePoints = [.. points.Select(p => new PathSegmentPoint { Speed = p.Speed, Point = p.Point })];
        var flattened = points.Select(p => new PathSegmentPoint { Speed = p.Speed, Point = new Vector3(p.Point.X, 0f, p.Point.Z) }).ToList();

        if (ArePointsColinear(flattened)) {
            BuildLinearSegments(flattened);
        } else {
            BuildSplineSegments(flattened, samplesPerCurveSegment);
        }
    }

    public PathSample EvaluateSample(float distance) {
        if (_segments.Count == 0 || _sourcePoints.Count < 2) {
            return new PathSample {
                Position = default,
                Tangent = Vector3.UnitZ,
                SegmentIndex = 0,
                LocalT = 0f,
                Speed = 0f,
                Yaw = 0f,
            };
        }

        distance = Math.Clamp(distance, 0f, TotalLength);

        var segIndex = FindSegmentIndexByDistance(distance);
        var segStart = GetSegmentStartDistance(segIndex);
        var segLen = GetSegmentLength(segIndex);
        var localDist = distance - segStart;

        var localT = 0f;
        if (segLen > 1e-6f)
            localT = localDist / segLen;

        localT = float.Clamp(localT, 0f, 1f);

        var seg = _segments[segIndex];
        var t = seg.GetTForDistance(localDist);

        seg.Evaluate(t, out var pos, out var tan);

        var (y0, y1) = GetSegmentHeights(segIndex);
        pos.Y = float.Lerp(y0, y1, localT);

        // horizontal tangent only
        tan.Y = 0f;

        var dir = Vector3.Normalize(tan);
        var yaw = (float)Math.Atan2(dir.X, dir.Z); // [-pi, +pi], 0 = +Z
        var speed = seg.Speed;

        return new PathSample {
            Position = pos,
            Tangent = tan,
            Yaw = yaw,
            SegmentIndex = segIndex,
            LocalT = localT,
            Speed = speed
        };
    }

    private (float yStart, float yEnd) GetSegmentHeights(int segmentIndex) {
        return (_sourcePoints[segmentIndex].Point.Y,
                _sourcePoints[segmentIndex + 1].Point.Y);
    }

    private void BuildLinearSegments(List<PathSegmentPoint> points) {
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

            var seg = new CardinalSegment(points[i].Speed, p0, p1, p2, p3, samplesPerCurveSegment, _tension);
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
        var baseLenSq = baseDir.LengthSquared();
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
