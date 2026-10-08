using ARealmRepopulated.Core.IPC;
using ARealmRepopulated.Core.Services.Npcs;
using ARealmRepopulated.Core.Services.Scenarios;
using ARealmRepopulated.Data.Appearance;
using ARealmRepopulated.Data.Scenarios;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Common.Math;
using Shouldly;

namespace ARealmRepopulated.Tests.Scenarios;

public class ScenarioNpcDataExtensionsTests {

    private const ushort TestHomeWorld = 33;
    private const ushort TestCurrentWorld = 44;

    private static ScenarioNpcData BuildNpc(params (string Key, string Value)[] additionalData) {
        var npc = new ScenarioNpcData();
        foreach (var (key, value) in additionalData) {
            npc.AdditionalData[key] = value;
        }
        return npc;
    }

    private static unsafe NpcSpawnOptions ToSpawnOptions(ScenarioNpcData npc) {
        var player = new Character { HomeWorld = TestHomeWorld, CurrentWorld = TestCurrentWorld };
        return npc.ToSpawnOptions(&player);
    }

    [Fact]
    public void ToSpawnOptions_PlacementAndAppearance_AreCopied() {
        var appearance = new NpcAppearanceData();
        var npc = new ScenarioNpcData {
            Position = new Vector3(1, 2, 3),
            Rotation = 1.5f,
            DrawOffset = new Vector3(0, 0.25f, 0),
            SnapToSurface = false,
            Appearance = appearance
        };

        var options = ToSpawnOptions(npc);

        options.Position.ShouldBe(new Vector3(1, 2, 3));
        options.Rotation.ShouldBe(1.5f);
        options.DrawOffset.ShouldBe(new Vector3(0, 0.25f, 0));
        options.SnapToSurface.ShouldBeFalse();
        options.Appearance.ShouldBeSameAs(appearance);
    }

    [Fact]
    public void ToSpawnOptions_WithoutActorName_IsPrivateUnnamedInternalBattleNpc() {
        var options = ToSpawnOptions(BuildNpc());

        options.IsPublic.ShouldBeFalse();
        options.Kind.ShouldBe(ObjectKind.BattleNpc);
        options.Name.ShouldBe("");
        options.AppearanceManagement.ShouldBe(AppearanceManagement.Internal);
    }

    [Fact]
    public void ToSpawnOptions_WithoutActorName_KeepsTheNpcWorld() {
        var options = ToSpawnOptions(BuildNpc());

        options.HomeWorld.ShouldBe(NpcSpawnOptions.DefaultWorld);
        options.CurrentWorld.ShouldBe(NpcSpawnOptions.DefaultWorld);
    }

    [Fact]
    public void ToSpawnOptions_WithActorName_IsPublicNamedPc() {
        var options = ToSpawnOptions(BuildNpc((IntegrationProvider.ActorNameConfigKey, "Arrp Bramblefox")));

        options.IsPublic.ShouldBeTrue();
        options.Kind.ShouldBe(ObjectKind.Pc);
        options.Name.ShouldBe("Arrp Bramblefox");
        options.AppearanceManagement.ShouldBe(AppearanceManagement.Internal);
    }

    [Fact]
    public void ToSpawnOptions_WithActorName_TakesThePlayersWorlds() {
        var options = ToSpawnOptions(BuildNpc((IntegrationProvider.ActorNameConfigKey, "Arrp Bramblefox")));

        options.HomeWorld.ShouldBe(TestHomeWorld);
        options.CurrentWorld.ShouldBe(TestCurrentWorld);
    }

    [Fact]
    public void ToSpawnOptions_WithActorNameAndExternalAppearance_IsExternallyManaged() {
        var options = ToSpawnOptions(BuildNpc(
            (IntegrationProvider.ActorNameConfigKey, "Arrp Bramblefox"),
            (IntegrationProvider.ExternalAppearanceConfigKey, "true")));

        options.AppearanceManagement.ShouldBe(AppearanceManagement.External);
    }

    [Theory]
    [InlineData("", TestDisplayName = "Empty")]
    [InlineData("false", TestDisplayName = "False")]
    [InlineData("yes", TestDisplayName = "Unparsable")]
    public void ToSpawnOptions_WithActorNameAndNoExternalFlag_IsInternallyManaged(string externalValue) {
        var options = ToSpawnOptions(BuildNpc(
            (IntegrationProvider.ActorNameConfigKey, "Arrp Bramblefox"),
            (IntegrationProvider.ExternalAppearanceConfigKey, externalValue)));

        options.AppearanceManagement.ShouldBe(AppearanceManagement.Internal);
    }

    [Fact]
    public void ToSpawnOptions_ExternalFlagWithoutActorName_IsIgnored() {
        var options = ToSpawnOptions(BuildNpc((IntegrationProvider.ExternalAppearanceConfigKey, "true")));

        options.IsPublic.ShouldBeFalse();
        options.Kind.ShouldBe(ObjectKind.BattleNpc);
        options.HomeWorld.ShouldBe(NpcSpawnOptions.DefaultWorld);
        options.AppearanceManagement.ShouldBe(AppearanceManagement.Internal);
    }
}
