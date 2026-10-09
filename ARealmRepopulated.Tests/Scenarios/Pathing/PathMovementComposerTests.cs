using ARealmRepopulated.Core.Services.Scenarios.Actions.Pathing;
using ARealmRepopulated.Core.Services.Scenarios.Actions.Pathing.Segments;
using Shouldly;
using System.Collections.Generic;
using System.Numerics;

namespace ARealmRepopulated.Tests.Scenarios.Pathing;

public class PathMovementComposerTests {

    [Fact]
    public void Calculate_WithNull_DoesNotThrow() {
        var composer = new PathMovementComposer();
        composer.Calculate(null!);

        composer.TotalLength.ShouldBe(0f);
        composer.SegmentCount.ShouldBe(0);
    }

    [Fact]
    public void Calculate_WithSinglePoint_DoesNotThrow() {
        var composer = new PathMovementComposer();
        var points = new List<PathSegmentPoint> {
            new() { Point = new Vector3(1f, 0f, 1f), Speed = 1f },
        };

        composer.Calculate(points);

        composer.TotalLength.ShouldBe(0f);
        composer.SegmentCount.ShouldBe(0);
    }

    [Fact]
    public void FindSegmentIndexByDistance_ReturnsCorrectSegment() {
        var composer = new PathMovementComposer();
        var points = new List<PathSegmentPoint> {
            new() { Point = new Vector3(0f, 0f, 0f), Speed = 1f },
            new() { Point = new Vector3(10f, 0f, 0f), Speed = 1f },
            new() { Point = new Vector3(20f, 0f, 0f), Speed = 1f },
        };

        composer.Calculate(points);

        composer.FindSegmentIndexByDistance(0f).ShouldBe(0);
        composer.FindSegmentIndexByDistance(5f).ShouldBe(0);
        composer.FindSegmentIndexByDistance(15f).ShouldBe(1);
    }

    [Fact]
    public void FindSegmentIndexByDistance_ClampsToEdges() {
        var composer = new PathMovementComposer();
        var points = new List<PathSegmentPoint> {
            new() { Point = new Vector3(0f, 0f, 0f), Speed = 1f },
            new() { Point = new Vector3(10f, 0f, 0f), Speed = 1f },
        };

        composer.Calculate(points);

        composer.FindSegmentIndexByDistance(-10f).ShouldBe(0);
        composer.FindSegmentIndexByDistance(999f).ShouldBe(composer.SegmentCount - 1);
    }

    [Fact]
    public void EvaluateSample_AtEnd_ReturnsEndPosition() {
        var composer = new PathMovementComposer();
        var points = new List<PathSegmentPoint> {
            new() { Point = new Vector3(0f, 0f, 0f), Speed = 1f },
            new() { Point = new Vector3(10f, 0f, 0f), Speed = 1f },
        };

        composer.Calculate(points);

        var sample = composer.EvaluateSample(composer.TotalLength);

        sample.Position.X.ShouldBe(10f, 0.01f);
        sample.Position.Z.ShouldBe(0f, 0.01f);
    }

    [Fact]
    public void EvaluateSample_WithNoSegments_ReturnsDefault() {
        var composer = new PathMovementComposer();

        var sample = composer.EvaluateSample(5f);

        sample.Position.ShouldBe(default);
        sample.Speed.ShouldBe(0f);
    }

}
