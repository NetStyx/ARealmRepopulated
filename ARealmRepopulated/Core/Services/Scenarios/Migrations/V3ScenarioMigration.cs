using System.Text;
using System.Text.Json.Nodes;

namespace ARealmRepopulated.Core.Services.Scenarios.Migrations;

[ScenarioMigration(Version = 3, Description = "Convert base64 appearance to real object")]
public class V3ScenarioMigration : IScenarioMigration {
    public void Upgrade(JsonObject jsonObject) {

        foreach (var npc in jsonObject["Npcs"]?.AsArray() ?? []) {
            if (npc == null)
                continue;

            var base64NpcAppearance = npc["Appearance"]?.GetValue<string>() ?? "";
            if (string.IsNullOrWhiteSpace(base64NpcAppearance))
                continue;

            var jsonNpcAppearance = Encoding.UTF8.GetString(Convert.FromBase64String(base64NpcAppearance));

            npc["Appearance"] = JsonNode.Parse(jsonNpcAppearance)?.AsObject();

        }
    }
}
