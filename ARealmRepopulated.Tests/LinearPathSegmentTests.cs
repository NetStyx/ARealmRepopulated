using ARealmRepopulated.Core.Services.Scenarios.Actions.Pathing.Segments;
using Shouldly;
using System;
using System.Numerics;

namespace ARealmRepopulated.Tests;

public class LinearPathSegmentTests {

    [Fact]
    public void Length_IsCorrectForKnownPoints() {
        var a = new Vector3(0f, 0f, 0f);
        var b = new Vector3(3f, 0f, 4f);
        var seg = new LinearPathSegment(1f, a, b);

        seg.Length.ShouldBe(5f, 0.0001f);
    }

    [Fact]
    public void Evaluate_AtStart_ReturnsPointA() {
        var a = new Vector3(1f, 2f, 3f);
        var b = new Vector3(4f, 5f, 6f);
        var seg = new LinearPathSegment(1f, a, b);

        seg.Evaluate(0f, out var position, out _);

        position.X.ShouldBe(a.X, 0.0001f);
        position.Y.ShouldBe(a.Y, 0.0001f);
        position.Z.ShouldBe(a.Z, 0.0001f);
    }

    [Fact]
    public void Evaluate_AtEnd_ReturnsPointB() {
        var a = new Vector3(1f, 2f, 3f);
        var b = new Vector3(4f, 5f, 6f);
        var seg = new LinearPathSegment(1f, a, b);

        seg.Evaluate(1f, out var position, out _);

        position.X.ShouldBe(b.X, 0.0001f);
        position.Y.ShouldBe(b.Y, 0.0001f);
        position.Z.ShouldBe(b.Z, 0.0001f);
    }

    [Fact]
    public void Evaluate_AtMidpoint_ReturnsCenter() {
        var a = new Vector3(0f, 0f, 0f);
        var b = new Vector3(10f, 0f, 0f);
        var seg = new LinearPathSegment(1f, a, b);

        seg.Evaluate(0.5f, out var position, out _);

        position.X.ShouldBe(5f, 0.0001f);
    }

    [Fact]
    public void Evaluate_TangentIsNormalized() {
        var a = new Vector3(0f, 0f, 0f);
        var b = new Vector3(10f, 0f, 0f);
        var seg = new LinearPathSegment(1f, a, b);

        seg.Evaluate(0.5f, out _, out var tangent);

        tangent.Length().ShouldBe(1f, 0.0001f);
    }

    [Fact]
    public void GetTForDistance_ReturnsCorrectT() {
        var a = new Vector3(0f, 0f, 0f);
        var b = new Vector3(10f, 0f, 0f);
        var seg = new LinearPathSegment(1f, a, b);

        seg.GetTForDistance(5f).ShouldBe(0.5f, 0.0001f);
        seg.GetTForDistance(0f).ShouldBe(0f, 0.0001f);
        seg.GetTForDistance(10f).ShouldBe(1f, 0.0001f);
    }

    [Fact]
    public void GetTForDistance_ClampsToRange() {
        var a = new Vector3(0f, 0f, 0f);
        var b = new Vector3(10f, 0f, 0f);
        var seg = new LinearPathSegment(1f, a, b);

        seg.GetTForDistance(-5f).ShouldBe(0f, 0.0001f);
        seg.GetTForDistance(20f).ShouldBe(1f, 0.0001f);
    }

    [Fact]
    public void ZeroLengthSegment_GetTForDistance_ReturnsZero() {
        var point = new Vector3(5f, 5f, 5f);
        var seg = new LinearPathSegment(1f, point, point);

        seg.GetTForDistance(0f).ShouldBe(0f);
    }

    [Fact]
    public void Speed_IsStoredCorrectly() {
        var seg = new LinearPathSegment(3.5f, Vector3.Zero, Vector3.One);
        seg.Speed.ShouldBe(3.5f);
    }

}
