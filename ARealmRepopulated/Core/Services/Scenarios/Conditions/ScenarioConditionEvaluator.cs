using ARealmRepopulated.Data.Scenarios;

namespace ARealmRepopulated.Core.Services.Scenarios.Conditions;

public readonly record struct IngameConditionSnapshot(int EorzeaTime, byte Weather);
public readonly record struct CustomConditionSnapshot(double ChanceRoll) {
    public static CustomConditionSnapshot Deterministic { get; } = new(ChanceRoll: 0d);
}

public static class ScenarioConditionEvaluator {
    public static bool AreConditionsMet(IReadOnlyList<ScenarioCondition> conditions, IngameConditionSnapshot ingame, CustomConditionSnapshot custom) {
        foreach (var condition in conditions) {
            if (!condition.IsConfigured)
                continue;

            if (IsMet(condition, ingame, custom) == condition.Negate)
                return false;
        }
        return true;
    }

    public static bool AreDeterministicConditionsMet(IReadOnlyList<ScenarioCondition> conditions, IngameConditionSnapshot ingame)
        => AreConditionsMet(conditions, ingame, CustomConditionSnapshot.Deterministic);

    private static bool IsMet(ScenarioCondition condition, IngameConditionSnapshot ingame, CustomConditionSnapshot custom) => condition switch {
        ScenarioEorzeaTimeCondition time => IsWithinHourWindow(ingame.EorzeaTime, time.StartHour, time.EndHour),
        ScenarioWeatherCondition weather => weather.WeatherIds.Contains(ingame.Weather),
        ScenarioChanceCondition chance => custom.ChanceRoll < chance.Percent,
        _ => true
    };

    public static bool IsWithinHourWindow(int hour, int startHour, int endHour) {
        var current = WrapHour(hour);
        var start = WrapHour(startHour);
        var end = WrapHour(endHour);

        if (start == end)
            return true;

        return start < end
            ? current >= start && current < end
            : current >= start || current < end;
    }

    // Eorzea time, like real time, consists of 24 hours with 3600 seconds per hour and is delivered as seconds ... so - nothing special. 
    // The only difference to real time is that eorzea time runs approximately 20 times faster which does not matter for the calculations here.
    public static int ToHours(long timeInSeconds)
        => WrapHour((int)(timeInSeconds / 3600));
    
    public static int WrapHour(int hour)
        => ((hour % 24) + 24) % 24;
}
