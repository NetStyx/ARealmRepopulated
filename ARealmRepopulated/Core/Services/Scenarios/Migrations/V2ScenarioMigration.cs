using System.Text.Json.Nodes;

namespace ARealmRepopulated.Core.Services.Scenarios.Migrations;

[ScenarioMigration(Version = 2, Description = "Migrate from single territory to location object")]
public class V2ScenarioMigration : IScenarioMigration {
    public void Upgrade(JsonObject jsonObject) {

        var currentTerritoryId = jsonObject["TerritoryId"]?.GetValue<int>();

        var locationObject = new JsonObject {
            { "Server", -1 },
            { "Territory", currentTerritoryId },
            { "HousingDivision", -1 },
            { "HousingWard", -1 },
            { "HousingPlot", -1 }
        };

        jsonObject.Add("Location", locationObject);
    }
}