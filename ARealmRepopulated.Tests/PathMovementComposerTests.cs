using ARealmRepopulated.Core.Services.Scenarios.Actions.Pathing;
using ARealmRepopulated.Core.Services.Scenarios.Actions.Pathing.Segments;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace ARealmRepopulated.Tests;

public class PathMovementComposerTests {

    [Fact]
    public void Calculate_WithColinearPoints_BuildsLinearSegments() {
        var composer = new PathMovementComposer();
        var points = new List<PathSegmentPoint> {
            new() { Point = new Vector3(0f, 0f, 0f), Speed = 1f },
            new() { Point = new Vector3(5f, 0f, 0f), Speed = 1f },
            new() { Point = new Vector3(10f, 0f, 0f), Speed = 1f },
        };

        composer.Calculate(points);

        composer.TotalLength.ShouldBe(10f, 0.01f);
        composer.SegmentCount.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void Calculate_WithNonColinearPoints_BuildsSplineSegments() {
        var composer = new PathMovementComposer();
        var points = new List<PathSegmentPoint> {
            new() { Point = new Vector3(0f, 0f, 0f), Speed = 1f },
            new() { Point = new Vector3(5f, 0f, 5f), Speed = 1f },
            new() { Point = new Vector3(10f, 0f, 0f), Speed = 1f },
        };

        composer.Calculate(points);

        composer.TotalLength.ShouldBeGreaterThan(0f);
        composer.SegmentCount.ShouldBe(2);
    }

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
    public void EvaluateSample_AtStart_ReturnsStartPosition() {
        var composer = new PathMovementComposer();
        var points = new List<PathSegmentPoint> {
            new() { Point = new Vector3(0f, 0f, 0f), Speed = 2f },
            new() { Point = new Vector3(10f, 0f, 0f), Speed = 2f },
        };

        composer.Calculate(points);

        var sample = composer.EvaluateSample(0f);

        sample.Position.X.ShouldBe(0f, 0.01f);
        sample.Position.Z.ShouldBe(0f, 0.01f);
        sample.Speed.ShouldBe(2f);
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

    [Fact]
    public void EvaluateSample_InterpolatesHeight() {
        var composer = new PathMovementComposer();
        var points = new List<PathSegmentPoint> {
            new() { Point = new Vector3(0f, 0f, 0f), Speed = 1f },
            new() { Point = new Vector3(10f, 10f, 0f), Speed = 1f },
        };

        composer.Calculate(points);

        var sample = composer.EvaluateSample(composer.TotalLength * 0.5f);

        // Y should be interpolated between 0 and 10
        sample.Position.Y.ShouldBe(5f, 0.5f);
    }

    [Fact]
    public void GetSegmentSpeed_ReturnsCorrectSpeed() {
        var composer = new PathMovementComposer();
        var points = new List<PathSegmentPoint> {
            new() { Point = new Vector3(0f, 0f, 0f), Speed = 2.5f },
            new() { Point = new Vector3(10f, 0f, 0f), Speed = 6.3f },
            new() { Point = new Vector3(20f, 0f, 0f), Speed = 6.3f },
        };

        composer.Calculate(points);

        composer.GetSegmentSpeed(0).ShouldBe(2.5f);
        composer.GetSegmentSpeed(1).ShouldBe(6.3f);
    }

}
