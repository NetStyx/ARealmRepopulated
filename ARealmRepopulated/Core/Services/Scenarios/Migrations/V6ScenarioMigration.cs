using System.Text.Json;
using System.Text.Json.Nodes;

namespace ARealmRepopulated.Core.Services.Scenarios.Migrations;

[ScenarioMigration(Version = 6, Description = "Store the Eorzea time window as a time of day instead of whole hours, keep only the first condition of each kind")]
public class V6ScenarioMigration : IScenarioMigration {

    public void Upgrade(JsonObject jsonObject) {
        if (jsonObject["Conditions"] is not JsonArray conditions)
            return;

        var kinds = new HashSet<string>();
        foreach (var condition in conditions.ToList()) {
            if (condition?["$condition"]?.GetValue<string>() is not { } kind)
                continue;

            if (!kinds.Add(kind)) {
                conditions.Remove(condition);
                continue;
            }

            if (kind == "EorzeaTime") {
                MoveHourToTimeOfDay(condition.AsObject(), "StartHour", "Start");
                MoveHourToTimeOfDay(condition.AsObject(), "EndHour", "End");
            }
        }
    }

    private static void MoveHourToTimeOfDay(JsonObject time, string hourKey, string timeKey) {
        if (time[hourKey]?.GetValue<int>() is { } hour)
            time[timeKey] = JsonSerializer.SerializeToNode(TimeOnly.MinValue.AddHours(hour));

        time.Remove(hourKey);
    }
}
