using System.Text.Json;
using System.Text.Json.Nodes;

namespace ARealmRepopulated.Core.Services.Scenarios.Migrations;

[ScenarioMigration(Version = 6, Description = "Store the Eorzea time window as a time of day instead of whole hours")]
public class V6ScenarioMigration : IScenarioMigration {

    public void Upgrade(JsonObject jsonObject) {

        foreach (var condition in jsonObject["Conditions"]?.AsArray() ?? []) {
            if (condition is not JsonObject time || time["$condition"]?.GetValue<string>() != "EorzeaTime")
                continue;

            MoveHourToTimeOfDay(time, "StartHour", "Start");
            MoveHourToTimeOfDay(time, "EndHour", "End");
        }
    }

    private static void MoveHourToTimeOfDay(JsonObject time, string hourKey, string timeKey) {
        if (time[hourKey]?.GetValue<int>() is { } hour)
            time[timeKey] = JsonSerializer.SerializeToNode(TimeOnly.MinValue.AddHours(hour));

        time.Remove(hourKey);
    }
}
