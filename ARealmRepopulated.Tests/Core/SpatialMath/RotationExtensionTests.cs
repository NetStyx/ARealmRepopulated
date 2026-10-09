using ARealmRepopulated.Core.SpatialMath;
using Shouldly;
using System;

namespace ARealmRepopulated.Tests.Core.SpatialMath;

public class RotationExtensionTests {

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

}
