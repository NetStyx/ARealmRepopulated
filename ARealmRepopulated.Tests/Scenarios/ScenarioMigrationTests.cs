using ARealmRepopulated.Core.Services.Scenarios.Migrations;
using ARealmRepopulated.Data.Appearance;
using ARealmRepopulated.Tests.Infrastructure;
using Shouldly;
using System.Linq;
using System.Text.Json.Nodes;

namespace ARealmRepopulated.Tests.Scenarios;

public class ScenarioMigrationTests {

    // the key as version 4 files store it, not today's constant
    private const string V4ActorNameKey = "Integration.General.Actor.Name";

    private const string V2ScenarioFile = "v2-1975ef01-990d-44d7-a955-c6fd4b1b3ff2.json";
    private const string V3ActorNamesFile = "v3-b9d6d283-0483-490b-b7b3-7e9d06d90f85.json";
    private const string V4ScenarioFile = "v4-6695e0ff-11d2-4368-97c7-fb881150c0c3.json";
    private const string V4HeightMultipliersFile = "v4-06e8c312-6d3f-4b2b-9fc5-850ed4bc9ffa.json";

    [Theory]
    [InlineData("v2-1975ef01-990d-44d7-a955-c6fd4b1b3ff2.json")]
    [InlineData("v2-2bc60476-07b2-4a78-9b2b-ad6f14f5878b.json")]
    [InlineData("v2-9c382d65-99c1-411a-8cb1-57d15cc74073.json")]
    public void ScenarioMigration_ConvertBase64ToStructuredObject(string fileName) {
        var jsonObject = ReadScenario(fileName);

        new V3ScenarioMigration().Upgrade(jsonObject);
    }

    [Fact]
    public void ScenarioMigration_MovesActorNamePrefixIntoStoredName() {
        var jsonObject = ReadScenario(V3ActorNamesFile);

        new V4ScenarioMigration().Upgrade(jsonObject);

        NpcNamed(jsonObject, "Bramblefox")["AdditionalData"]![V4ActorNameKey]!.GetValue<string>().ShouldBe("Arrp Bramblefox");
    }

    [Theory]
    [InlineData("NoAdditionalData")]
    [InlineData("UnrelatedIntegrationProperty")]
    [InlineData("EmptyActorName")]
    public void ScenarioMigration_WithoutActorName_LeavesAdditionalDataAlone(string npcName) {
        var jsonObject = ReadScenario(V3ActorNamesFile);
        var before = NpcNamed(jsonObject, npcName).ToJsonString();

        new V4ScenarioMigration().Upgrade(jsonObject);

        NpcNamed(jsonObject, npcName).ToJsonString().ShouldBe(before);
    }

    [Fact]
    public void ScenarioMigration_TurnsTheHeightMultiplierIntoTheHeight() {
        var jsonObject = ReadScenario(V4ScenarioFile);

        var dataCache = new TestArrpDataCache(64, 1f);
        new V5ScenarioMigration(dataCache).Upgrade(jsonObject);

        var appearances = AppearancesOf(jsonObject);
        appearances.ShouldAllBe(a => !a.ContainsKey("HeightMultiplier"));
        appearances.Select(a => a["Height"]!.GetValue<byte>()).ShouldBe([(byte)100, (byte)64, (byte)100]);
        appearances.Select(a => a["Scale"]!.GetValue<float>()).ShouldBe([1f, 2f, 1f]);

        dataCache.Lookups.ShouldBe([(0, NpcTribe.Midlander, NpcSex.Female, NpcBodyType.Normal, 2f)]);
    }

    [Fact]
    public void ScenarioMigration_BeyondTheHeightRange_FoldsTheRestIntoTheScale() {
        var jsonObject = ReadScenario(V4ScenarioFile);

        new V5ScenarioMigration(new TestArrpDataCache(100, 4f)).Upgrade(jsonObject);

        var appearance = AppearanceOf(NpcNamed(jsonObject, "Trick"));
        appearance["Height"]!.GetValue<byte>().ShouldBe((byte)100);
        appearance["Scale"]!.GetValue<float>().ShouldBe(8f);
    }

    [Fact]
    public void ScenarioMigration_WithNumericCustomize_ReadsTheIds() {
        var jsonObject = ReadScenario(V4HeightMultipliersFile);

        var dataCache = new TestArrpDataCache(64, 1f);
        new V5ScenarioMigration(dataCache).Upgrade(jsonObject);

        dataCache.Lookups.ShouldContain((0, NpcTribe.Xaela, NpcSex.Male, NpcBodyType.Young, 2f));
        AppearanceOf(NpcNamed(jsonObject, "NumericCustomize"))["Height"]!.GetValue<byte>().ShouldBe((byte)64);
    }

    [Theory]
    [InlineData("UnknownTribe")]
    [InlineData("NonHuman")]
    public void ScenarioMigration_WithoutModelScale_DropsTheHeightMultiplierOnly(string npcName) {
        var jsonObject = ReadScenario(V4HeightMultipliersFile);
        var heightBefore = AppearanceOf(NpcNamed(jsonObject, npcName))["Height"]?.ToJsonString();

        new V5ScenarioMigration(new TestArrpDataCache(64, 1f)).Upgrade(jsonObject);

        var appearance = AppearanceOf(NpcNamed(jsonObject, npcName));
        appearance.ContainsKey("HeightMultiplier").ShouldBeFalse();
        appearance["Height"]?.ToJsonString().ShouldBe(heightBefore);
        appearance["Scale"]!.GetValue<float>().ShouldBe(1.5f);
    }

    [Fact]
    public void ScenarioMigration_FromBeforeVersion4_KeepsTheGamesModelScale() {
        var jsonObject = ReadScenario(V2ScenarioFile);
        new V3ScenarioMigration().Upgrade(jsonObject);
        new V4ScenarioMigration().Upgrade(jsonObject);
        var before = AppearancesOf(jsonObject).Select(a => a.ToJsonString()).ToArray();

        var dataCache = new TestArrpDataCache(64, 1f);
        new V5ScenarioMigration(dataCache).Upgrade(jsonObject);

        AppearancesOf(jsonObject).Select(a => a.ToJsonString()).ShouldBe(before);
        dataCache.Lookups.ShouldBeEmpty();
    }

    private static JsonObject ReadScenario(string fileName)
        => JsonNode.Parse(TestHelper.ReadEmbeddedResource(fileName))!.AsObject();

    private static JsonObject NpcNamed(JsonObject jsonObject, string name)
        => jsonObject["Npcs"]!.AsArray().Single(npc => npc!["Name"]!.GetValue<string>() == name)!.AsObject();

    private static JsonObject AppearanceOf(JsonObject npc)
        => npc["Appearance"]!.AsObject();

    private static JsonObject[] AppearancesOf(JsonObject jsonObject)
        => [.. jsonObject["Npcs"]!.AsArray().Select(npc => AppearanceOf(npc!.AsObject()))];

}
