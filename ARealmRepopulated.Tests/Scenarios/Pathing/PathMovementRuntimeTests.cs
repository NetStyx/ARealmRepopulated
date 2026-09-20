using ARealmRepopulated.Core.Services.Scenarios.Actions.Pathing;
using ARealmRepopulated.Core.Services.Scenarios.Actions.Pathing.Segments;
using Shouldly;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace ARealmRepopulated.Tests.Scenarios.Pathing;

public class PathMovementRuntimeTests {

    private static List<PathSegmentPoint> Points(params Vector3[] points)
        => [.. points.Select(p => new PathSegmentPoint { Point = p, Speed = 2.5f })];

    [Fact]
    public void Compile_FirstPointOnActorPosition_CompilesRemainingPath() {
        var runtime = new PathMovementRuntime();
        var actor = new Vector3(10f, 0f, 10f);
        var target = new Vector3(20f, 0f, 10f);

        runtime.Compile(Points(actor, actor, target)).ShouldBeTrue();

        runtime.IsPathReady.ShouldBeTrue();
        runtime.FirstTargetPoint.ShouldBe(target);
    }

    [Fact]
    public void Compile_PointAboveActorPosition_IsTreatedAsSamePoint() {
        var runtime = new PathMovementRuntime();
        var actor = new Vector3(10f, 0f, 10f);

        runtime.Compile(Points(actor, actor + new Vector3(0f, 3f, 0f), new Vector3(20f, 0f, 10f))).ShouldBeTrue();

        runtime.IsPathReady.ShouldBeTrue();
    }

    [Fact]
    public void Compile_OnlyPointOnActorPosition_ReturnsFalse() {
        var runtime = new PathMovementRuntime();
        var actor = new Vector3(10f, 0f, 10f);

        runtime.Compile(Points(actor, actor)).ShouldBeFalse();

        runtime.IsPathReady.ShouldBeFalse();
    }

    [Fact]
    public void RemoveStackedPoints_ConsecutiveDuplicates_KeepsFirstOfEach() {
        var a = new Vector3(0f, 1f, 0f);
        var b = new Vector3(5f, 0f, 0f);

        var result = PathMovementRuntime.RemoveStackedPoints(Points(a, a with { Y = 2f }, b, b, a));

        result.Select(p => p.Point).ShouldBe([a, b, a]);
    }

    [Fact]
    public void Update_ReachingTheEnd_ReturnsFinalPoint() {
        var runtime = new PathMovementRuntime();
        var end = new Vector3(1f, 0f, 0f);
        runtime.Compile(Points(Vector3.Zero, end));

        runtime.Update(10f, out var position, out _);

        runtime.IsFinished.ShouldBeTrue();
        position.X.ShouldBe(end.X, 0.0001f);
        position.Z.ShouldBe(end.Z, 0.0001f);
    }
}
