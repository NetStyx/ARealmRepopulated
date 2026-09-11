using ARealmRepopulated.Core.IPC;
using ARealmRepopulated.Core.Services.Scenarios.Migrations;
using Shouldly;
using System.Text.Json.Nodes;

namespace ARealmRepopulated.Tests.Scenarios;

public class ScenarioMigrationTests {

    [Theory]
    [InlineData("v2-1975ef01-990d-44d7-a955-c6fd4b1b3ff2.json")]
    [InlineData("v2-2bc60476-07b2-4a78-9b2b-ad6f14f5878b.json")]
    [InlineData("v2-9c382d65-99c1-411a-8cb1-57d15cc74073.json")]
    public void ScenarioMigration_ConvertBase64ToStructuredObject(string fileName) {
        var fileContent = TestHelper.ReadEmbeddedResource(fileName);
        var jsonObject = JsonNode.Parse(fileContent)!.AsObject();

        new V3ScenarioMigration().Upgrade(jsonObject);
    }

    [Fact]
    public void ScenarioMigration_MovesActorNamePrefixIntoStoredName() {
        var jsonObject = BuildNpcScenario((IntegrationProvider.ActorNameConfigKey, "Bramblefox"));

        new V4ScenarioMigration().Upgrade(jsonObject);

        ActorNameOf(jsonObject).ShouldBe("Arrp Bramblefox");
    }

    [Theory]
    [InlineData(null, null, TestDisplayName = "NoAdditionalData")]
    [InlineData("Integration.Something.Else", "value", TestDisplayName = "UnrelatedIntegrationProperty")]
    [InlineData(IntegrationProvider.ActorNameConfigKey, "", TestDisplayName = "EmptyActorName")]
    public void ScenarioMigration_WithoutActorName_LeavesAdditionalDataAlone(string? key, string? value) {
        var jsonObject = key == null ? BuildNpcScenario() : BuildNpcScenario((key, value!));
        var before = jsonObject.ToJsonString();

        new V4ScenarioMigration().Upgrade(jsonObject);

        jsonObject.ToJsonString().ShouldBe(before);
    }

    [Fact]
    public void ScenarioMigration_NpcWithoutAdditionalData_IsSkipped() {
        var jsonObject = JsonNode.Parse("""{"Version": 3, "Npcs": [{"Name": "Test"}]}""")!.AsObject();

        Should.NotThrow(() => new V4ScenarioMigration().Upgrade(jsonObject));
    }

    private static JsonObject BuildNpcScenario(params (string Key, string Value)[] additionalData) {
        var npcData = new JsonObject();
        foreach (var (key, value) in additionalData) {
            npcData[key] = value;
        }

        return new JsonObject {
            ["Version"] = 3,
            ["Npcs"] = new JsonArray(new JsonObject { ["Name"] = "Test", ["AdditionalData"] = npcData }),
        };
    }

    private static string ActorNameOf(JsonObject jsonObject)
        => jsonObject["Npcs"]![0]!["AdditionalData"]![IntegrationProvider.ActorNameConfigKey]!.GetValue<string>();

}
