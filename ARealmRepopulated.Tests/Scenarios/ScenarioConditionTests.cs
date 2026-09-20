using System.Collections.Generic;
using ARealmRepopulated.Core.Services.Scenarios.Conditions;
using ARealmRepopulated.Data.Scenarios;
using Shouldly;

namespace ARealmRepopulated.Tests.Scenarios;

public class ScenarioConditionTests {

    private static readonly ScenarioConditionSnapshot Midnight = new(EorzeaHour: 0, Weather: 1);

    private static bool Evaluate(ScenarioConditionSnapshot snapshot, params ScenarioCondition[] conditions)
        => ScenarioConditionEvaluator.AreConditionsMet(conditions, snapshot, () => 50d);

    [Fact]
    public void AreConditionsMet_WithoutConditions_IsMet()
        => Evaluate(Midnight).ShouldBeTrue();

    [Theory]
    [InlineData(8, 8, 18, true)]
    [InlineData(17, 8, 18, true)]
    [InlineData(18, 8, 18, false)]
    [InlineData(7, 8, 18, false)]
    public void IsWithinHourWindow_WithForwardWindow_UsesInclusiveStartAndExclusiveEnd(int hour, int start, int end, bool expected)
        => ScenarioConditionEvaluator.IsWithinHourWindow(hour, start, end).ShouldBe(expected);

    [Theory]
    [InlineData(18, 18, 6, true)]
    [InlineData(23, 18, 6, true)]
    [InlineData(0, 18, 6, true)]
    [InlineData(5, 18, 6, true)]
    [InlineData(6, 18, 6, false)]
    [InlineData(12, 18, 6, false)]
    public void IsWithinHourWindow_WithWrappingWindow_SpansMidnight(int hour, int start, int end, bool expected)
        => ScenarioConditionEvaluator.IsWithinHourWindow(hour, start, end).ShouldBe(expected);

    [Theory]
    [InlineData(0)]
    [InlineData(12)]
    [InlineData(23)]
    public void IsWithinHourWindow_WithEqualBounds_CoversTheWholeDay(int hour)
        => ScenarioConditionEvaluator.IsWithinHourWindow(hour, 9, 9).ShouldBeTrue();

    [Theory]
    [InlineData(0, 0)]
    [InlineData(3599, 0)]
    [InlineData(3600, 1)]
    [InlineData(23 * 3600, 23)]
    [InlineData(24 * 3600, 0)]
    public void ToEorzeaHour_FoldsTheEorzeanClockIntoAnHourOfDay(long eorzeaTime, int expected)
        => ScenarioConditionEvaluator.ToEorzeaHour(eorzeaTime).ShouldBe(expected);

    [Fact]
    public void AreConditionsMet_WithUnconfiguredWeatherCondition_IgnoresIt() {
        var condition = new ScenarioWeatherCondition();

        condition.IsConfigured.ShouldBeFalse();
        Evaluate(Midnight, condition).ShouldBeTrue();
    }

    [Fact]
    public void AreConditionsMet_WithUnconfiguredTimeCondition_IgnoresIt() {
        var condition = new ScenarioEorzeaTimeCondition { StartHour = 9, EndHour = 9 };

        condition.IsConfigured.ShouldBeFalse();
        Evaluate(Midnight, condition).ShouldBeTrue();
    }

    [Fact]
    public void AreConditionsMet_WithNegatedCondition_FlipsTheResult() {
        var condition = new ScenarioEorzeaTimeCondition { StartHour = 18, EndHour = 6, Negate = true };

        Evaluate(Midnight, condition).ShouldBeFalse();
        Evaluate(Midnight with { EorzeaHour = 12 }, condition).ShouldBeTrue();
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(2, true)]
    [InlineData(3, false)]
    public void AreConditionsMet_WithWeatherCondition_MatchesAnySelectedWeather(byte weather, bool expected) {
        var condition = new ScenarioWeatherCondition { WeatherIds = [1, 2] };

        Evaluate(Midnight with { Weather = weather }, condition).ShouldBe(expected);
    }

    [Fact]
    public void AreConditionsMet_WithSeveralConditions_RequiresAllOfThem() {
        var time = new ScenarioEorzeaTimeCondition { StartHour = 18, EndHour = 6 };
        var weather = new ScenarioWeatherCondition { WeatherIds = [1] };

        Evaluate(Midnight, time, weather).ShouldBeTrue();
        Evaluate(Midnight with { Weather = 7 }, time, weather).ShouldBeFalse();
        Evaluate(Midnight with { EorzeaHour = 12 }, time, weather).ShouldBeFalse();
    }

    [Theory]
    [InlineData(0f, false)]
    [InlineData(50f, false)]
    [InlineData(51f, true)]
    [InlineData(100f, true)]
    public void AreConditionsMet_WithChanceCondition_ComparesAgainstTheRoll(float percent, bool expected) {
        var condition = new ScenarioChanceCondition { Percent = percent };

        ScenarioConditionEvaluator.AreConditionsMet([condition], Midnight, () => 50d).ShouldBe(expected);
    }

    [Fact]
    public void AreDeterministicConditionsMet_TreatsChanceAsMet_SoTheDisplayDoesNotFlicker() {
        ScenarioCondition[] conditions = [
            new ScenarioEorzeaTimeCondition { StartHour = 18, EndHour = 6 },
            new ScenarioChanceCondition { Percent = 1f },
        ];

        ScenarioConditionEvaluator.AreDeterministicConditionsMet(conditions, Midnight).ShouldBeTrue();
        ScenarioConditionEvaluator.AreDeterministicConditionsMet(conditions, Midnight with { EorzeaHour = 12 }).ShouldBeFalse();
    }

    [Fact]
    public void AreConditionsMet_WithSeveralChanceConditions_RollsForEachOfThem() {
        var rolls = new Queue<double>([10d, 90d]);
        ScenarioCondition[] conditions = [
            new ScenarioChanceCondition { Percent = 50f },
            new ScenarioChanceCondition { Percent = 50f },
        ];

        ScenarioConditionEvaluator.AreConditionsMet(conditions, Midnight, rolls.Dequeue).ShouldBeFalse();
        rolls.Count.ShouldBe(0);
    }
}
