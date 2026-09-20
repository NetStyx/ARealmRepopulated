using ARealmRepopulated.Core.Services.Npcs;
using ARealmRepopulated.Core.Services.Scenarios.Actions.Pathing.Segments;
using ARealmRepopulated.Data.Scenarios;
using System.Numerics;

namespace ARealmRepopulated.Core.Services.Scenarios.Actions.Pathing;

public enum PathMovementIntegrationMode {
    FastSingleSegment = 0,   // default: never cross a segment boundary in one update
    CrossSingleBoundary = 1  // can cross at most one segment per update
}

public class PathMovementRuntime {
    private PathMovementIntegrationMode _integrationMode = PathMovementIntegrationMode.FastSingleSegment;
    private readonly PathMovementComposer _path = new();

    private float _currentDistanceAlongPath = 0f;
    private int _currentSegmentIndex = 0;

    public bool IsPathReady
        => _path.TotalLength > 0;

    public bool IsFinished =>
        _currentDistanceAlongPath >= _path.TotalLength - 1e-4f;

    public bool IsUserReady { get; set; } = false;
    
    public const float MinPointDistance = 0.01f;

    public Vector3 FirstTargetPoint { get; private set; }

    public float CurrentSpeedValue
        => !IsFinished ? _path.GetSegmentSpeed(_currentSegmentIndex) : NpcActor.WalkingSpeed;
    
    public bool Compile(List<PathSegmentPoint> points, float tension = 0f, PathMovementIntegrationMode integrationMode = PathMovementIntegrationMode.CrossSingleBoundary) {
        if (points is null || points.Count < 2)
            throw new ArgumentException("Path movement requires at least 2 points.");

        points = RemoveStackedPoints(points);
        if (points.Count < 2)
            return false;

        FirstTargetPoint = points[1].Point;
        _path.Calculate(points, tension);

        if (_path.SegmentCount != points.Count - 1) {
            throw new InvalidOperationException($"SegmentCount does not match point count ({_path.SegmentCount} | {points.Count - 1}). Double point entries or zero-length segments?");
        }

        _integrationMode = integrationMode;
        _currentDistanceAlongPath = 0f;
        _currentSegmentIndex = 0;
        return true;
    }

    /// <summary>
    /// Removes points that are too close to each other.
    /// </summary>
    public static List<PathSegmentPoint> RemoveStackedPoints(List<PathSegmentPoint> points) {
        var result = new List<PathSegmentPoint>(points.Count);
        foreach (var point in points) {
            if (result.Count > 0 && IsStacked(result[^1].Point, point.Point))
                continue;

            result.Add(point);
        }
        return result;
    }

    private static bool IsStacked(Vector3 a, Vector3 b)
        => new Vector2(a.X - b.X, a.Z - b.Z).LengthSquared() < MinPointDistance * MinPointDistance;

    /// <summary>
    /// Advance along the precompiled path and get the next position and yaw.
    /// </summary>
    public void Update(float deltaTime, out Vector3 nextPosition, out float yaw) {
        switch (_integrationMode) {
            default:
            case PathMovementIntegrationMode.CrossSingleBoundary:
                UpdateCrossSingleBoundary(deltaTime);
                break;

            case PathMovementIntegrationMode.FastSingleSegment:
                UpdateFastSingleSegment(deltaTime);
                break;
        }

        var sample = _path.EvaluateSample(_currentDistanceAlongPath);

        nextPosition = sample.Position;
        yaw = sample.Yaw;
        _currentSegmentIndex = sample.SegmentIndex;
    }

    public void SyncToCurrentPosition(Vector3 currentPosition) {
        _currentDistanceAlongPath = ProjectPositionToPath(currentPosition);
        _currentSegmentIndex = _path.FindSegmentIndexByDistance(_currentDistanceAlongPath);
    }

    public void Reset() {
        IsUserReady = false;
        _currentDistanceAlongPath = 0f;
        _currentSegmentIndex = 0;
    }

    private void UpdateFastSingleSegment(float deltaTime) {
        if (deltaTime <= 0f || _path.TotalLength <= 1e-6f || IsFinished)
            return;

        var segIndex = _path.FindSegmentIndexByDistance(_currentDistanceAlongPath);
        var speed = _path.GetSegmentSpeed(segIndex);
        if (speed <= 1e-6f)
            return;

        var step = speed * deltaTime;

        var newD = _currentDistanceAlongPath + step;
        if (newD > _path.TotalLength)
            newD = _path.TotalLength;

        _currentDistanceAlongPath = newD;
        _currentSegmentIndex = segIndex;
    }

    private void UpdateCrossSingleBoundary(float deltaTime) {
        if (deltaTime <= 0f || _path.TotalLength <= 1e-6f || IsFinished)
            return;

        var segIndex = _path.FindSegmentIndexByDistance(_currentDistanceAlongPath);
        var segStart = _path.GetSegmentStartDistance(segIndex);
        var segEnd = _path.GetSegmentEndDistance(segIndex);
        var segLen = segEnd - segStart;

        var localDist = _currentDistanceAlongPath - segStart;
        var distToSegEnd = MathF.Max(0f, segLen - localDist);

        var speed = _path.GetSegmentSpeed(segIndex);
        if (speed <= 1e-6f)
            return;

        var maxStep = speed * deltaTime;

        if (maxStep <= distToSegEnd) {
            // stay within current segment
            _currentDistanceAlongPath += maxStep;
            _currentSegmentIndex = segIndex;
        } else {
            // reach end of current segment
            var timeToSegEnd = distToSegEnd / speed;
            var remainingTime = deltaTime - timeToSegEnd;

            var dAtBoundary = segEnd;

            if (remainingTime <= 0f || segIndex == _path.SegmentCount - 1) {
                _currentDistanceAlongPath = dAtBoundary;
                _currentSegmentIndex = segIndex;
            } else {
                var nextSegIndex = segIndex + 1;
                var nextSegStart = _path.GetSegmentStartDistance(nextSegIndex);
                var nextSegEnd = _path.GetSegmentEndDistance(nextSegIndex);
                var nextSegLen = nextSegEnd - nextSegStart;
                var nextSpeed = _path.GetSegmentSpeed(nextSegIndex);

                if (nextSpeed <= 1e-6f) {
                    _currentDistanceAlongPath = dAtBoundary;
                    _currentSegmentIndex = nextSegIndex;
                } else {
                    var stepNext = nextSpeed * remainingTime;
                    if (stepNext > nextSegLen)
                        stepNext = nextSegLen;

                    _currentDistanceAlongPath = dAtBoundary + stepNext;
                    _currentSegmentIndex = nextSegIndex;
                }
            }
        }

        if (_currentDistanceAlongPath > _path.TotalLength)
            _currentDistanceAlongPath = _path.TotalLength;
    }
    
    public static float ResolveSpeed(INpcSpeedSelection selection) => selection.Speed switch {
        NpcSpeed.Walking => NpcActor.WalkingSpeed,
        NpcSpeed.Running => NpcActor.RunningSpeed,
        NpcSpeed.Sprinting => NpcActor.SprintingSpeed,
        NpcSpeed.Custom => MovementMotion.ClampSpeed(selection.CustomSpeed),
        _ => NpcActor.WalkingSpeed,
    };

    private float ProjectPositionToPath(Vector3 position) {
        if (_path.TotalLength <= 1e-6f)
            return 0f;

        var sampleCount = Math.Clamp((int)(_path.TotalLength / 0.5f), 8, 128);

        var bestDistSq = float.MaxValue;
        var bestPathDistance = 0f;

        for (var i = 0; i <= sampleCount; i++) {
            var t = (float)i / sampleCount;
            var d = t * _path.TotalLength;

            var sample = _path.EvaluateSample(d);
            var p = sample.Position;

            var distSq = Vector3.DistanceSquared(p, position);
            if (distSq < bestDistSq) {
                bestDistSq = distSq;
                bestPathDistance = d;
            }
        }

        return bestPathDistance;
    }

}
