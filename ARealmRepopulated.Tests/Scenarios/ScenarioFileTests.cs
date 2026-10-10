using ARealmRepopulated.Core.Services.Scenarios;
using ARealmRepopulated.Data.Scenarios;
using ARealmRepopulated.Infrastructure;
using ARealmRepopulated.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;

namespace ARealmRepopulated.Tests.Scenarios;

public class ScenarioFileTests {

    [Theory]
    [InlineData("v2-1975ef01-990d-44d7-a955-c6fd4b1b3ff2.json")]
    [InlineData("v2-2bc60476-07b2-4a78-9b2b-ad6f14f5878b.json")]
    [InlineData("v2-9c382d65-99c1-411a-8cb1-57d15cc74073.json")]
    [InlineData("v3-b9d6d283-0483-490b-b7b3-7e9d06d90f85.json")]
    [InlineData("v4-6695e0ff-11d2-4368-97c7-fb881150c0c3.json")]
    public void OlderScenarioFile_LoadsAfterMigration(string fileName) {        
        var services = new ServiceCollection().AddSingleton<ArrpDataCache>(new TestArrpDataCache(64, 1f)).BuildServiceProvider();
        var migrator = new ScenarioMigrator(services, NullPluginLog.Instance);
        migrator.Initialize();

        var file = new FileInfo(Path.GetTempFileName());
        try {
            File.WriteAllText(file.FullName, TestHelper.ReadEmbeddedResource(fileName));

            migrator.Migrate(file, out var metaData).ShouldBeTrue();
            var scenario = JsonSerializer.Deserialize<ScenarioData>(File.ReadAllText(file.FullName), ScenarioFileManager.ScenarioLoadSerializerOptions);

            metaData.Version.ShouldBe(ScenarioMigrator.CurrentScenarioVersion);
            scenario.ShouldNotBeNull();
            scenario.Npcs.ShouldNotBeEmpty();
            scenario.Npcs.SelectMany(npc => npc.Actions).ShouldAllBe(action => action != null);
        } finally {
            file.Delete();
        }
    }

    [Fact]
    public void ScenarioFile_PresetSpeeds_DoNotWriteACustomSpeedKey() {

        var scenario = new ScenarioData();
        var npc = new ScenarioNpcData { Name = "Bramblefox" };
        npc.Actions.Add(new ScenarioNpcMovementAction { TargetPosition = Vector3.One, Speed = NpcSpeed.Walking });
        scenario.Npcs.Add(npc);

        var json = JsonSerializer.Serialize(scenario, ScenarioFileManager.ScenarioLoadSerializerOptions);

        // walking and running stay byte-identical to what older versions of the plugin wrote,
        // which is why adding a custom speed needs no scenario migration
        json.ShouldNotContain("CustomSpeed");
        json.ShouldContain("\"Walking\"");
    }
}
