using ARealmRepopulated.Core.IPC;
using ARealmRepopulated.Core.Services.Npcs;
using System.Text.Json.Nodes;

namespace ARealmRepopulated.Core.Services.Scenarios.Migrations;

[ScenarioMigration(Version = 4, Description = "Store the whole actor name instead of the part behind the prefix")]
public class V4ScenarioMigration : IScenarioMigration {

    public void Upgrade(JsonObject jsonObject) {

        foreach (var npc in jsonObject["Npcs"]?.AsArray() ?? []) {
            var additionalData = npc?["AdditionalData"]?.AsObject();
            if (additionalData == null)
                continue;

            var actorName = additionalData[IntegrationProvider.ActorNameConfigKey]?.GetValue<string>() ?? "";
            if (string.IsNullOrWhiteSpace(actorName))
                continue;

            additionalData[IntegrationProvider.ActorNameConfigKey] = ActorName.IntegrationPrefix + actorName;
        }
    }
}
