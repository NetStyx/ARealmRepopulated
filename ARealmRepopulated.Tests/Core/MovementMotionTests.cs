using ARealmRepopulated.Core.Services.Npcs;
using ARealmRepopulated.Core.Services.Scenarios.Actions.Pathing;
using ARealmRepopulated.Data.Scenarios;
using Shouldly;

namespace ARealmRepopulated.Tests.Core;

public class MovementMotionTests {

    [Fact]
    public void Select_AtWalkingSpeed_PlaysWalkAtNaturalRate() {
        var motion = MovementMotion.Select(NpcActor.WalkingSpeed);

        motion.Animation.ShouldBe(NpcAppearanceService.Animations.Walking);
        motion.AnimationSpeed.ShouldBe(1f, 0.0001f);
    }

    [Fact]
    public void Select_AtRunningSpeed_PlaysRunAtNaturalRate() {
        var motion = MovementMotion.Select(NpcActor.RunningSpeed);

        motion.Animation.ShouldBe(NpcAppearanceService.Animations.Running);
        motion.AnimationSpeed.ShouldBe(1f, 0.0001f);
    }

    [Fact]
    public void Select_AtSprintingSpeed_PlaysSprintAtNaturalRate() {
        var motion = MovementMotion.Select(NpcActor.SprintingSpeed);

        motion.Animation.ShouldBe(NpcAppearanceService.Animations.Sprinting);
        motion.AnimationSpeed.ShouldBe(1f, 0.0001f);
    }

    [Fact]
    public void Select_BelowWalkingSpeed_SlowsTheWalkAnimation() {
        var motion = MovementMotion.Select(1.25f);

        motion.Animation.ShouldBe(NpcAppearanceService.Animations.Walking);
        motion.AnimationSpeed.ShouldBe(0.5f, 0.0001f);
    }

    [Theory]
    [InlineData(0.1f)]
    [InlineData(1f)]
    [InlineData(2.5f)]
    [InlineData(4f)]
    [InlineData(6.3f)]
    [InlineData(9.45f)]
    [InlineData(14f)]
    [InlineData(20f)]
    public void Select_AcrossTheWholeRange_KeepsThePlaybackRateSane(float speed) {
        var motion = MovementMotion.Select(speed);

        // the animation always moves forward, and never so fast that the motion reads as a blur
        motion.AnimationSpeed.ShouldBeGreaterThan(0f);
        motion.AnimationSpeed.ShouldBeLessThanOrEqualTo(2.2f);
    }

    [Fact]
    public void Select_AnimationSpeed_ScalesLinearlyWithTravelSpeed() {
        // double the speed within one motion band, double the playback rate
        var slow = MovementMotion.Select(1.5f);
        var fast = MovementMotion.Select(3f);

        slow.Animation.ShouldBe(fast.Animation);
        (fast.AnimationSpeed / slow.AnimationSpeed).ShouldBe(2f, 0.0001f);
    }

    [Fact]
    public void ClampSpeed_OutOfRangeValues_AreBroughtIntoRange() {
        MovementMotion.ClampSpeed(-5f).ShouldBe(MovementMotion.MinCustomSpeed);
        MovementMotion.ClampSpeed(0f).ShouldBe(MovementMotion.MinCustomSpeed);
        MovementMotion.ClampSpeed(999f).ShouldBe(MovementMotion.MaxCustomSpeed);
    }

    [Fact]
    public void ClampSpeed_NaN_FallsBackToTheDefault() {
        MovementMotion.ClampSpeed(float.NaN).ShouldBe(MovementMotion.DefaultCustomSpeed);
    }

    [Fact]
    public void ClampSpeed_NeverReturnsZero() {
        // a zero speed would stall a movement action forever, parking the scenario on its sync barrier
        MovementMotion.ClampSpeed(0f).ShouldBeGreaterThan(0f);
        MovementMotion.ClampSpeed(float.NegativeInfinity).ShouldBeGreaterThan(0f);
    }

    [Fact]
    public void ResolveSpeed_PresetSelections_IgnoreTheCustomValue() {
        var walking = new PathMovementPoint { Speed = NpcSpeed.Walking, CustomSpeed = 17f };
        var running = new PathMovementPoint { Speed = NpcSpeed.Running, CustomSpeed = 17f };

        PathMovementRuntime.ResolveSpeed(walking).ShouldBe(NpcActor.WalkingSpeed);
        PathMovementRuntime.ResolveSpeed(running).ShouldBe(NpcActor.RunningSpeed);
    }

    [Fact]
    public void ResolveSpeed_Sprinting_PlaysSprintAtNaturalRate() {
        var point = new PathMovementPoint { Speed = NpcSpeed.Sprinting, CustomSpeed = 17f };

        var travelSpeed = PathMovementRuntime.ResolveSpeed(point);
        var motion = MovementMotion.Select(travelSpeed);

        travelSpeed.ShouldBe(NpcActor.SprintingSpeed);
        motion.Animation.ShouldBe(NpcAppearanceService.Animations.Sprinting);
        motion.AnimationSpeed.ShouldBe(1f, 0.0001f);
    }

    [Fact]
    public void ResolveSpeed_CustomSelection_UsesTheCustomValue() {
        var point = new PathMovementPoint { Speed = NpcSpeed.Custom, CustomSpeed = 4.2f };

        PathMovementRuntime.ResolveSpeed(point).ShouldBe(4.2f, 0.0001f);
    }

    [Fact]
    public void ResolveSpeed_CustomSelectionOutOfRange_IsClamped() {
        var tooSlow = new ScenarioNpcMovementAction { Speed = NpcSpeed.Custom, CustomSpeed = 0f };
        var tooFast = new ScenarioNpcMovementAction { Speed = NpcSpeed.Custom, CustomSpeed = 500f };

        PathMovementRuntime.ResolveSpeed(tooSlow).ShouldBe(MovementMotion.MinCustomSpeed);
        PathMovementRuntime.ResolveSpeed(tooFast).ShouldBe(MovementMotion.MaxCustomSpeed);
    }

    [Fact]
    public void ResolveSpeed_MovementActionAndPathPoint_AgreeOnTheSameSelection() {
        var action = new ScenarioNpcMovementAction { Speed = NpcSpeed.Custom, CustomSpeed = 8f };
        var point = new PathMovementPoint { Speed = NpcSpeed.Custom, CustomSpeed = 8f };

        PathMovementRuntime.ResolveSpeed(action).ShouldBe(PathMovementRuntime.ResolveSpeed(point));
    }
}
