using ARealmRepopulated.Data.Scenarios;

namespace ARealmRepopulated.Core.Services.Scenarios.Conditions;

public readonly record struct IngameConditionSnapshot(TimeOnly TimeOfDay, byte Weather);
public readonly record struct CustomConditionSnapshot(double ChanceRoll);

public static class ScenarioConditionEvaluator {

    public static bool AreMet(IReadOnlyList<ScenarioCondition> conditions, IngameConditionSnapshot ingame)
        => AreMet(conditions, ingame, custom: null);

    public static bool AreMet(IReadOnlyList<ScenarioCondition> conditions, IngameConditionSnapshot ingame, CustomConditionSnapshot custom)
        => AreMet(conditions, ingame, (CustomConditionSnapshot?)custom);

    private static bool AreMet(IReadOnlyList<ScenarioCondition> conditions, IngameConditionSnapshot ingame, CustomConditionSnapshot? custom) {
        foreach (var condition in conditions) {
            if (!condition.IsConfigured || IsMet(condition, ingame, custom) is not { } isMet)
                continue;

            if (isMet == condition.Negate)
                return false;
        }
        return true;
    }

    // null means the given snapshots cannot decide the condition, so it is skipped rather than inverted
    private static bool? IsMet(ScenarioCondition condition, IngameConditionSnapshot ingame, CustomConditionSnapshot? custom) => condition switch {
        ScenarioEorzeaTimeCondition time => EorzeaTime.IsWithinWindow(ingame.TimeOfDay, time.Start, time.End),
        ScenarioWeatherCondition weather => weather.WeatherIds.Contains(ingame.Weather),
        ScenarioChanceCondition chance => custom is { } roll ? roll.ChanceRoll < chance.Percent : null,
        _ => true
    };
}
