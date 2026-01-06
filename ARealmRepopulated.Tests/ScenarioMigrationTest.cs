using ARealmRepopulated.Core.Services.Scenarios.Migrations;
using System.Text.Json.Nodes;

namespace ARealmRepopulated.Tests;

public class ScenarioMigrationTest {

    [Theory]
    [InlineData("v2-1975ef01-990d-44d7-a955-c6fd4b1b3ff2.json")]
    [InlineData("v2-2bc60476-07b2-4a78-9b2b-ad6f14f5878b.json")]
    [InlineData("v2-9c382d65-99c1-411a-8cb1-57d15cc74073.json")]
    public void ScenarioMigration_ConvertBase64ToStructuredObject(string fileName) {
        var fileContent = TestHelper.ReadEmbeddedResource(fileName);
        var jsonObject = JsonNode.Parse(fileContent)!.AsObject();

        new V3ScenarioMigration().Upgrade(jsonObject);
    }

}
