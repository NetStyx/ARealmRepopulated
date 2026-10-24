using ARealmRepopulated.Core.Services.Scenarios;
using ARealmRepopulated.Core.Services.Scenarios.Conditions;
using ARealmRepopulated.Data.Scenarios;
using Shouldly;
using System;

namespace ARealmRepopulated.Tests.Scenarios;

public class ScenarioConditionTests {

    private static readonly IngameConditionSnapshot Midnight = new(TimeOfDay: TimeOnly.MinValue, Weather: 1);

    private static bool Evaluate(IngameConditionSnapshot ingame, params ScenarioCondition[] conditions)
        => ScenarioConditionEvaluator.AreMet(conditions, ingame);

    [Fact]
    public void AreMet_WithoutConditions_IsMet()
        => Evaluate(Midnight).ShouldBeTrue();

    [Theory]
    [InlineData(8, 0, true)]
    [InlineData(17, 59, true)]
    [InlineData(18, 0, false)]
    [InlineData(7, 59, false)]
    public void IsWithinWindow_WithForwardWindow_UsesInclusiveStartAndExclusiveEnd(int hour, int minute, bool expected)
        => EorzeaTime.IsWithinWindow(new(hour, minute), new(8, 0), new(18, 0)).ShouldBe(expected);

    [Theory]
    [InlineData(18, 30, true)]
    [InlineData(23, 59, true)]
    [InlineData(0, 0, true)]
    [InlineData(6, 14, true)]
    [InlineData(6, 15, false)]
    [InlineData(18, 29, false)]
    public void IsWithinWindow_WithWrappingWindow_SpansMidnight(int hour, int minute, bool expected)
        => EorzeaTime.IsWithinWindow(new(hour, minute), new(18, 30), new(6, 15)).ShouldBe(expected);

    [Theory]
    [InlineData(0, 0)]
    [InlineData(9, 30)]
    [InlineData(12, 0)]
    [InlineData(23, 59)]
    public void IsWithinWindow_WithEqualBounds_CoversTheWholeDay(int hour, int minute)
        => EorzeaTime.IsWithinWindow(new(hour, minute), new(9, 30), new(9, 30)).ShouldBeTrue();

    [Theory]
    [InlineData(0, 0, 0, 0)]
    [InlineData(59, 0, 0, 59)]
    [InlineData(60, 0, 1, 0)]
    [InlineData(23 * 3600 + 59 * 60, 23, 59, 0)]
    [InlineData(24 * 3600, 0, 0, 0)]
    [InlineData(1_000_000L * 24 * 3600 + 90, 0, 1, 30)]
    public void ToTimeOfDay_FoldsTheClockIntoATimeOfDay(long timeInSeconds, int hour, int minute, int second)
        => EorzeaTime.ToTimeOfDay(timeInSeconds).ShouldBe(new TimeOnly(hour, minute, second));

    [Fact]
    public void AreMet_WithUnconfiguredWeatherCondition_IgnoresIt() {
        var condition = new ScenarioWeatherCondition();

        condition.IsConfigured.ShouldBeFalse();
        Evaluate(Midnight, condition).ShouldBeTrue();
    }

    [Fact]
    public void AreMet_WithUnconfiguredTimeCondition_IgnoresIt() {
        var condition = new ScenarioEorzeaTimeCondition { Start = new(9, 0), End = new(9, 0) };

        condition.IsConfigured.ShouldBeFalse();
        Evaluate(Midnight, condition).ShouldBeTrue();
    }

    [Fact]
    public void AreMet_WithNegatedCondition_FlipsTheResult() {
        var condition = new ScenarioEorzeaTimeCondition { Start = new(18, 0), End = new(6, 0), Negate = true };

        Evaluate(Midnight, condition).ShouldBeFalse();
        Evaluate(Midnight with { TimeOfDay = new(12, 0) }, condition).ShouldBeTrue();
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(2, true)]
    [InlineData(3, false)]
    public void AreMet_WithWeatherCondition_MatchesAnySelectedWeather(byte weather, bool expected) {
        var condition = new ScenarioWeatherCondition { WeatherIds = [1, 2] };

        Evaluate(Midnight with { Weather = weather }, condition).ShouldBe(expected);
    }

    [Fact]
    public void AreMet_WithSeveralConditions_RequiresAllOfThem() {
        var time = new ScenarioEorzeaTimeCondition { Start = new(18, 0), End = new(6, 0) };
        var weather = new ScenarioWeatherCondition { WeatherIds = [1] };

        Evaluate(Midnight, time, weather).ShouldBeTrue();
        Evaluate(Midnight with { Weather = 7 }, time, weather).ShouldBeFalse();
        Evaluate(Midnight with { TimeOfDay = new(12, 0) }, time, weather).ShouldBeFalse();
    }

    [Theory]
    [InlineData(0f, false)]
    [InlineData(50f, false)]
    [InlineData(51f, true)]
    [InlineData(100f, true)]
    public void AreMet_WithChanceCondition_ComparesAgainstTheRoll(float percent, bool expected) {
        var condition = new ScenarioChanceCondition { Percent = percent };

        ScenarioConditionEvaluator.AreMet([condition], Midnight, new CustomConditionSnapshot(ChanceRoll: 50d)).ShouldBe(expected);
    }

    [Fact]
    public void AreMet_WithoutRoll_LeavesChanceOut_SoTheDisplayDoesNotFlicker() {
        ScenarioCondition[] conditions = [
            new ScenarioEorzeaTimeCondition { Start = new(18, 0), End = new(6, 0) },
            new ScenarioChanceCondition { Percent = 1f },
        ];

        Evaluate(Midnight, conditions).ShouldBeTrue();
        Evaluate(Midnight with { TimeOfDay = new(12, 0) }, conditions).ShouldBeFalse();
    }

    [Theory]
    [InlineData(0f, false)]
    [InlineData(100f, false)]
    [InlineData(0f, true)]
    [InlineData(100f, true)]
    public void AreMet_WithoutRoll_LeavesChanceOut_EvenWhenInverted(float percent, bool negate)
        => Evaluate(Midnight, new ScenarioChanceCondition { Percent = percent, Negate = negate }).ShouldBeTrue();

    [Fact]
    public void AreMet_WithSeveralChanceConditions_SharesOneRollAndActsAsTheLowestChance() {
        ScenarioCondition[] conditions = [
            new ScenarioChanceCondition { Percent = 50f },
            new ScenarioChanceCondition { Percent = 30f },
        ];

        ScenarioConditionEvaluator.AreMet(conditions, Midnight, new CustomConditionSnapshot(ChanceRoll: 20d)).ShouldBeTrue();
        ScenarioConditionEvaluator.AreMet(conditions, Midnight, new CustomConditionSnapshot(ChanceRoll: 40d)).ShouldBeFalse();
    }

    [Fact]
    public void Tracker_KeepsTheRollUntilTheIntervalHasPassed() {
        var data = new ScenarioData { Conditions = [new ScenarioChanceCondition { Percent = 50f }], ConditionRollInterval = 10f };
        var tracker = new ScenarioConditionTracker(data);

        tracker.Evaluate(Midnight);
        var held = tracker.AreMet;

        // a tracker that re-rolled on every evaluation would flip a 50 percent chance within a few tries
        for (var i = 0; i < 1000; i++) {
            tracker.Tick(TimeSpan.FromMilliseconds(9));
            tracker.Evaluate(Midnight);
            tracker.AreMet.ShouldBe(held);
        }
    }

    [Theory]
    [InlineData(ScenarioEndBehavior.Never, false, false, false)]
    [InlineData(ScenarioEndBehavior.AfterRun, false, true, true)]
    [InlineData(ScenarioEndBehavior.Immediately, true, true, true)]
    public void Tracker_WithFailedConditions_EndsTheWayTheEndBehaviorSays(ScenarioEndBehavior endBehavior, bool endWhileRunning, bool endWhileWaitingEndlessly, bool endWhenFinished) {
        var data = new ScenarioData { Conditions = [new ScenarioEorzeaTimeCondition { Start = new(8, 0), End = new(18, 0) }], EndBehavior = endBehavior };
        var tracker = new ScenarioConditionTracker(data);

        tracker.Evaluate(Midnight);

        tracker.ShouldEnd(ScenarioRunState.Running).ShouldBe(endWhileRunning);
        tracker.ShouldEnd(ScenarioRunState.WaitingEndlessly).ShouldBe(endWhileWaitingEndlessly);
        tracker.ShouldEnd(ScenarioRunState.Finished).ShouldBe(endWhenFinished);
    }

    [Fact]
    public void Tracker_AfterRun_LeavesChanceOutForAnEndlessWait() {
        var data = new ScenarioData { Conditions = [new ScenarioChanceCondition { Percent = 0f }] };
        var tracker = new ScenarioConditionTracker(data);

        tracker.Evaluate(Midnight);

        tracker.AreMet.ShouldBeFalse();
        tracker.ShouldEnd(ScenarioRunState.WaitingEndlessly).ShouldBeFalse();
        tracker.ShouldEnd(ScenarioRunState.Finished).ShouldBeTrue();
    }
}
