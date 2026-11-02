using System.Text.Json.Nodes;

namespace ARealmRepopulated.Core.Services.Scenarios.Migrations;

[ScenarioMigration(Version = 7, Description = "Store the customize byte of the mouth as Mouth instead of Lipstick, which is only its flag")]
public class V7ScenarioMigration : IScenarioMigration {

    public void Upgrade(JsonObject jsonObject) {
        foreach (var npc in jsonObject["Npcs"]?.AsArray() ?? []) {
            if (npc?["Appearance"] is JsonObject appearance && appearance.Remove("Lipstick", out var mouth))
                appearance["Mouth"] = mouth;
        }
    }
}
