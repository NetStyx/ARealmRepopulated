using ARealmRepopulated.Core.Numbers;
using Shouldly;

namespace ARealmRepopulated.Tests.Core.Numbers;

public class NumberExtensionsTests {

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    [InlineData(255)]
    public void InRangeOrDefault_WithinBounds_ReturnsTheValue(int value) {
        value.InRangeOrDefault(1, 255).ShouldBe(value);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(256)]
    [InlineData(-1)]
    public void InRangeOrDefault_OutOfBounds_ReturnsNull(int value) {
        value.InRangeOrDefault(1, 255).ShouldBeNull();
    }

    [Fact]
    public void InRangeOrDefault_OutOfBounds_ReturnsTheFallback() {
        300.InRangeOrDefault(1, 255, 42).ShouldBe(42);
    }

    [Fact]
    public void InRangeOrDefault_OutOfBounds_IsNotMovedToTheBound() {
        // unlike Math.Clamp, which would give 1.0
        1.5f.InRangeOrDefault(0f, 1f).ShouldBeNull();
    }
}
