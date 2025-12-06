using System.Text.Json.Nodes;

namespace ARealmRepopulated.Core.Services.Scenarios.Migrations;

[ScenarioMigration(Version = 1)]
public class V1ScenarioMigration : IScenarioMigration {
    public void Upgrade(JsonObject jsonObject) {
        // Just fixes the missing versions.
    }
}