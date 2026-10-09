using ARealmRepopulated.Core.Services.Scenarios.Actions.Pathing.Segments;
using Shouldly;
using System.Numerics;

namespace ARealmRepopulated.Tests.Scenarios.Pathing;

public class LinearPathSegmentTests {

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

}
