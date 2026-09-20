using ARealmRepopulated.Data.Scenarios;

namespace ARealmRepopulated.Core.Services.Scenarios.Conditions;

/// <summary>
/// A single reading of everything the conditions can look at, taken once per sweep so every
/// scenario in the same sweep is judged against the same world state.
/// </summary>
public readonly record struct ScenarioConditionSnapshot(int EorzeaHour, byte Weather);

/// <summary>
/// Pure condition logic. Everything that touches the game lives in <see cref="ScenarioConditionService"/>
/// so this part stays testable.
/// </summary>
public static class ScenarioConditionEvaluator {

    public const int HoursPerDay = 24;
    public const int EorzeaSecondsPerHour = 3600;

    /// <summary>
    /// Every configured condition has to hold. An empty list is always met, which is what keeps
    /// scenarios written before conditions existed behaving exactly as before.
    /// </summary>
    public static bool AreConditionsMet(IReadOnlyList<ScenarioCondition> conditions, ScenarioConditionSnapshot snapshot, Func<double> nextRoll) {
        foreach (var condition in conditions) {
            if (!condition.IsConfigured)
                continue;

            if (IsMet(condition, snapshot, nextRoll) == condition.Negate)
                return false;
        }
        return true;
    }

    /// <summary>
    /// For display only: whether everything but luck currently holds. A chance condition has no state
    /// to show, so it counts as met here instead of being re-rolled on every frame the editor draws.
    /// </summary>
    public static bool AreDeterministicConditionsMet(IReadOnlyList<ScenarioCondition> conditions, ScenarioConditionSnapshot snapshot)
        => AreConditionsMet(conditions, snapshot, () => 0d);

    private static bool IsMet(ScenarioCondition condition, ScenarioConditionSnapshot snapshot, Func<double> nextRoll) => condition switch {
        ScenarioEorzeaTimeCondition time => IsWithinHourWindow(snapshot.EorzeaHour, time.StartHour, time.EndHour),
        ScenarioWeatherCondition weather => weather.WeatherIds.Contains(snapshot.Weather),
        ScenarioChanceCondition chance => nextRoll() < chance.Percent,
        _ => true
    };

    /// <summary>
    /// <paramref name="startHour"/> is inclusive, <paramref name="endHour"/> exclusive, and a window
    /// whose end lies before its start wraps around midnight.
    /// </summary>
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
