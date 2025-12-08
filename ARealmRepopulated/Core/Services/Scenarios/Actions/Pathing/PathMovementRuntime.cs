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

    public bool IsReady
        => _path.TotalLength > 0;

    public bool IsFinished =>
        _currentDistanceAlongPath >= _path.TotalLength - 1e-4f;

    public NpcSpeed CurrentSpeed
        => !IsFinished ? ResolveSpeed(_path.GetSegmentSpeed(_currentSegmentIndex)) : NpcSpeed.Walking;

    public void Compile(List<PathSegmentPoint> points, float tension = 0f, PathMovementIntegrationMode integrationMode = PathMovementIntegrationMode.CrossSingleBoundary) {
        if (points is null || points.Count < 2)
            throw new ArgumentException("Path movement requires at least 2 points.");

        _path.Calculate(points, tension);

        if (_path.SegmentCount != points.Count - 1) {
            throw new InvalidOperationException(
                $"PathMovementRuntime.SegmentCount ({_path.SegmentCount}) != Points.Count - 1 ({points.Count - 1}). " +
                "Check for duplicate consecutive points or zero-length segments.");
        }

        _integrationMode = integrationMode;
        _currentDistanceAlongPath = 0f;
        _currentSegmentIndex = 0;
    }

    public void SyncToCurrentPosition(Vector3 currentPosition) {
        _currentDistanceAlongPath = ProjectPositionToPath(currentPosition);
        _currentSegmentIndex = _path.FindSegmentIndexByDistance(_currentDistanceAlongPath);
    }

    public void Reset() {
        _currentDistanceAlongPath = 0f;
        _currentSegmentIndex = 0;
    }

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
        _currentSegmentIndex = sample.SegmentIndex;

        yaw = ComputeYawFromDirection(sample.Tangent);
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



    public static float ResolveSpeed(NpcSpeed opt) => opt switch {
        NpcSpeed.Walking => NpcActor.WalkingSpeed,
        NpcSpeed.Running => NpcActor.RunningSpeed,
        _ => NpcActor.WalkingSpeed,
    };

    public static NpcSpeed ResolveSpeed(float speed) => speed switch {
        NpcActor.WalkingSpeed => NpcSpeed.Walking,
        NpcActor.RunningSpeed => NpcSpeed.Running,
        _ => NpcSpeed.Custom
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

    private static float ComputeYawFromDirection(Vector3 dir) {
        dir = Vector3.Normalize(dir);
        return (float)Math.Atan2(dir.X, dir.Z); // [-pi, +pi], 0 = +Z
    }
}
