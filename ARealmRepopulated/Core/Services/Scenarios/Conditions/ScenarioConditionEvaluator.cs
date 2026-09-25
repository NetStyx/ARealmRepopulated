using ARealmRepopulated.Data.Scenarios;

namespace ARealmRepopulated.Core.Services.Scenarios.Conditions;

public readonly record struct IngameConditionSnapshot(int EorzeaTime, byte Weather);
public sealed class CustomConditionSnapshot(IReadOnlyDictionary<ScenarioChanceCondition, double> chanceRolls) {
    
    public static CustomConditionSnapshot Deterministic { get; } = new(new Dictionary<ScenarioChanceCondition, double>());

    public double GetRoll(ScenarioChanceCondition condition)
        => chanceRolls.GetValueOrDefault(condition, 0d);
}

public static class ScenarioConditionEvaluator {

    public const int HoursPerDay = 24;
    public const int EorzeaSecondsPerHour = 3600;

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
        ScenarioChanceCondition chance => custom.GetRoll(chance) < chance.Percent,
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

    public static int ToEorzeaHour(long eorzeaTime)
        => WrapHour((int)(eorzeaTime / EorzeaSecondsPerHour));

    public static int WrapHour(int hour)
        => ((hour % HoursPerDay) + HoursPerDay) % HoursPerDay;
}
