using ARealmRepopulated.Core.Services.Scenarios.Migrations;
using ARealmRepopulated.Data.Appearance;
using Shouldly;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace ARealmRepopulated.Tests.Scenarios;

public class ScenarioMigrationTests {

    // the key as version 4 files store it, not today's constant
    private const string V4ActorNameKey = "Integration.General.Actor.Name";

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
        var jsonObject = BuildNpcScenario((V4ActorNameKey, "Bramblefox"));

        new V4ScenarioMigration().Upgrade(jsonObject);

        ActorNameOf(jsonObject).ShouldBe("Arrp Bramblefox");
    }

    [Theory]
    [InlineData(null, null, TestDisplayName = "NoAdditionalData")]
    [InlineData("Integration.Something.Else", "value", TestDisplayName = "UnrelatedIntegrationProperty")]
    [InlineData(V4ActorNameKey, "", TestDisplayName = "EmptyActorName")]
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

    [Fact]
    public void ScenarioMigration_FoldsTheHeightMultiplierIntoTheScale() {
        // a real v4 file: the second actor has Scale 2 and HeightMultiplier 2, the others an explicit null
        var jsonObject = JsonNode.Parse(TestHelper.ReadEmbeddedResource("v4-6695e0ff-11d2-4368-97c7-fb881150c0c3.json"))!.AsObject();

        var customizes = new List<V5ScenarioMigration.V4Customize>();
        V5ScenarioMigration.FoldHeightMultipliers(jsonObject, c => { customizes.Add(c); return 0.5f; });

        var appearances = AppearancesOf(jsonObject);
        appearances.ShouldAllBe(a => !a.ContainsKey("HeightMultiplier"));
        appearances.Select(a => a["Scale"]!.GetValue<float>()).ShouldBe([1f, 8f, 1f]);
        
        customizes.ShouldBe([new V5ScenarioMigration.V4Customize(0, NpcTribe.Midlander, NpcSex.Female, NpcBodyType.Normal, 100)]);
    }

    [Fact]
    public void ScenarioMigration_WithNumericCustomize_ReadsTheIds() {
        // appearances converted from base64 by version 3 store the enums as numbers
        var jsonObject = BuildAppearanceScenario("""{"Tribe": 12, "Sex": 0, "BodyType": 4, "Height": 30, "HeightMultiplier": 2}""");

        var customizes = new List<V5ScenarioMigration.V4Customize>();
        V5ScenarioMigration.FoldHeightMultipliers(jsonObject, c => { customizes.Add(c); return 0.5f; });

        customizes.ShouldBe([new V5ScenarioMigration.V4Customize(0, NpcTribe.Xaela, NpcSex.Male, NpcBodyType.Young, 30)]);
        AppearancesOf(jsonObject)[0]["Scale"]!.GetValue<float>().ShouldBe(4f);
    }

    [Fact]
    public void ScenarioMigration_WithUnknownTribeName_DropsTheHeightMultiplierOnly() {
        // version 4 could not load such a file either
        var jsonObject = BuildAppearanceScenario("""{"Tribe": "Nonsense", "Scale": 1.5, "HeightMultiplier": 2}""");

        V5ScenarioMigration.FoldHeightMultipliers(jsonObject, _ => 0.5f);

        var appearance = AppearancesOf(jsonObject)[0];
        appearance.ContainsKey("HeightMultiplier").ShouldBeFalse();
        appearance["Scale"]!.GetValue<float>().ShouldBe(1.5f);
    }

    [Fact]
    public void ScenarioMigration_WithoutHeightMultiplier_FoldsTheOldDefault() {
        // the property defaulted to 1.0, so a file without it still replaced the game's model scale
        var jsonObject = BuildAppearanceScenario("""{"Scale": 1.5}""");

        V5ScenarioMigration.FoldHeightMultipliers(jsonObject, _ => 0.5f);

        AppearancesOf(jsonObject)[0]["Scale"]!.GetValue<float>().ShouldBe(3f);
    }

    [Fact]
    public void ScenarioMigration_NonHumanModel_DropsTheHeightMultiplierOnly() {
        // the multiplier never reached non-human models
        var jsonObject = BuildAppearanceScenario("""{"ModelCharaId": 1, "Scale": 1.5, "HeightMultiplier": 2}""");

        V5ScenarioMigration.FoldHeightMultipliers(jsonObject, _ => null);

        var appearance = AppearancesOf(jsonObject)[0];
        appearance.ContainsKey("HeightMultiplier").ShouldBeFalse();
        appearance["Scale"]!.GetValue<float>().ShouldBe(1.5f);
    }

    // parsed from text like a real file: nodes built in code hold ints and floats the json helpers do not convert
    private static JsonObject BuildAppearanceScenario(string appearanceJson)
        => JsonNode.Parse($$"""{"Version": 4, "Npcs": [{"Name": "Test", "Appearance": {{appearanceJson}}}]}""")!.AsObject();

    private static JsonObject[] AppearancesOf(JsonObject jsonObject)
        => [.. jsonObject["Npcs"]!.AsArray().Select(npc => npc!["Appearance"]!.AsObject())];

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
        => jsonObject["Npcs"]![0]!["AdditionalData"]![V4ActorNameKey]!.GetValue<string>();

}
