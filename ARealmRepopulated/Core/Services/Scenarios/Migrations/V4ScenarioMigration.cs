using System.Text.Json.Nodes;

namespace ARealmRepopulated.Core.Services.Scenarios.Migrations;

[ScenarioMigration(Version = 4, Description = "Store the whole actor name instead of the part behind the prefix")]
public class V4ScenarioMigration : IScenarioMigration {

    // fixed keys in case they change in the future
    private const string ActorNameKey = "Integration.General.Actor.Name";
    private const string ActorNamePrefix = "Arrp ";

    public void Upgrade(JsonObject jsonObject) {

        foreach (var npc in jsonObject["Npcs"]?.AsArray() ?? []) {
            var additionalData = npc?["AdditionalData"]?.AsObject();
            if (additionalData == null)
                continue;

            var actorName = additionalData[ActorNameKey]?.GetValue<string>() ?? "";
            if (string.IsNullOrWhiteSpace(actorName))
                continue;

            additionalData[ActorNameKey] = ActorNamePrefix + actorName;
        }
    }
}
