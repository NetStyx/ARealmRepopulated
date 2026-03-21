using ARealmRepopulated.Core.SpatialMath;
using Shouldly;
using System;
using System.Numerics;

namespace ARealmRepopulated.Tests;

public class RotationExtensionTests {

    [Theory]
    [InlineData(0f, 0f, true)]
    [InlineData(1.0f, 1.0f + 0.00005f, true)]
    [InlineData(1.0f, 1.001f, false)]
    [InlineData(-1.0f, -1.0f, true)]
    public void AlmostEqual_ReturnsExpected(float a, float b, bool expected) {
        RotationExtension.AlmostEqual(a, b).ShouldBe(expected);
    }

    [Fact]
    public void RotateToward_ReturnsTarget_WhenWithinDistance() {
        var result = RotationExtension.RotateToward(1.0f, 1.05f, 0.1f);
        result.ShouldBe(1.05f);
    }

    [Fact]
    public void RotateToward_StepsTowardTarget_WhenFarAway() {
        var result = RotationExtension.RotateToward(0f, 1.0f, 0.25f);
        result.ShouldBe(0.25f, 0.0001f);
    }

    [Fact]
    public void RotateToward_WrapsAroundPositive() {
        // Current near +PI, target near -PI — should wrap the short way
        var current = MathF.PI - 0.1f;
        var target = -MathF.PI + 0.1f;
        var result = RotationExtension.RotateToward(current, target, 0.05f);
        // Should step forward (positive direction, wrapping around)
        result.ShouldBeGreaterThan(current);
    }

    [Fact]
    public void RotateToward_WrapsAroundNegative() {
        // Current near -PI, target near +PI — should wrap the short way
        var current = -MathF.PI + 0.1f;
        var target = MathF.PI - 0.1f;
        var result = RotationExtension.RotateToward(current, target, 0.05f);
        // Should step backward (negative direction, wrapping around)
        result.ShouldBeLessThan(current);
    }

    [Fact]
    public void AngleTowards_ReturnsCorrectAngle() {
        var from = new Vector3(0f, 0f, 0f);
        var to = new Vector3(1f, 0f, 0f);
        var angle = from.AngleTowards(to);
        // atan2(1, 0) = PI/2
        angle.ShouldBe(MathF.PI / 2f, 0.0001f);
    }

    [Fact]
    public void AngleTowards_PositiveZ_ReturnsZero() {
        var from = new Vector3(0f, 0f, 0f);
        var to = new Vector3(0f, 0f, 1f);
        var angle = from.AngleTowards(to);
        // atan2(0, 1) = 0
        angle.ShouldBe(0f, 0.0001f);
    }

}
