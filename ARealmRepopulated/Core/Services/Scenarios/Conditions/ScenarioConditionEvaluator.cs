using ARealmRepopulated.Data.Scenarios;

namespace ARealmRepopulated.Core.Services.Scenarios.Conditions;

public readonly record struct IngameConditionSnapshot(TimeOnly EorzeaTimeOfDay, byte Weather);
public readonly record struct CustomConditionSnapshot(double? ChanceRoll);

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

    private static bool? IsMet(ScenarioCondition condition, IngameConditionSnapshot ingame, CustomConditionSnapshot? custom) => condition switch {
        ScenarioEorzeaTimeCondition time => EorzeaTimeHelper.IsWithinWindow(ingame.EorzeaTimeOfDay, time.Start, time.End),
        ScenarioWeatherCondition weather => weather.WeatherIds.Contains(ingame.Weather),
        ScenarioChanceCondition chance => custom?.ChanceRoll is { } roll ? roll < chance.Percent : null,
        _ => true
    };
}
