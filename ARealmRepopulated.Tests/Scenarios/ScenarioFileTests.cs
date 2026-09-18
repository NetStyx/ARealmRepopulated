using ARealmRepopulated.Core.Json;
using ARealmRepopulated.Core.Services.Scenarios;
using ARealmRepopulated.Data.Scenarios;
using Shouldly;
using System;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace ARealmRepopulated.Tests.Scenarios;

public class ScenarioFileTests {

    [Fact]
    public void ScenarioFile_IsKeepingDataIntegrityBetweenSerialization() {

        var scenario = new ScenarioData { Title = GetRandomString(), Description = GetRandomString() };

        var npcOne = new ScenarioNpcData { Name = GetRandomString(), Position = GetRandomVector3(), Rotation = GetRandomRadian() };
        npcOne.Actions.Add(new ScenarioNpcWaitingAction { Duration = GetRandomTime() });
        npcOne.Actions.Add(new ScenarioNpcMovementAction { TargetPosition = GetRandomVector3(), Speed = NpcSpeed.Running });
        npcOne.Actions.Add(new ScenarioNpcSyncAction());
        npcOne.Actions.Add(new ScenarioNpcEmoteAction { Duration = GetRandomTime(), Emote = (ushort)Random.Shared.Next(1, 100), NpcTalk = GetRandomString() });
        npcOne.Actions.Add(new ScenarioNpcSpawnAction());
        npcOne.Actions.Add(new ScenarioNpcDespawnAction());

        scenario.Npcs.Add(npcOne);

        var restoredScenario = Recode(scenario);

        restoredScenario.Title.ShouldBe(scenario.Title);
        restoredScenario.Description.ShouldBe(scenario.Description);
        restoredScenario.Npcs.Count.ShouldBe(scenario.Npcs.Count);

        var restoredNpcOne = restoredScenario.Npcs[0];
        restoredNpcOne.Name.ShouldBe(npcOne.Name);
        restoredNpcOne.Appearance.ToBase64().ShouldBe(npcOne.Appearance.ToBase64());
        restoredNpcOne.Position.ShouldBe(npcOne.Position);
        restoredNpcOne.Rotation.ShouldBe(npcOne.Rotation);
        restoredNpcOne.Actions.Count.ShouldBe(npcOne.Actions.Count);

        for (var i = 0; i < npcOne.Actions.Count; i++) {
            var originalAction = npcOne.Actions[i];
            var restoredAction = restoredNpcOne.Actions[i];
            restoredAction.GetType().ShouldBe(originalAction.GetType());
            switch (originalAction) {
                case ScenarioNpcWaitingAction originalWaiting:
                    var restoredWaiting = (ScenarioNpcWaitingAction)restoredAction;
                    restoredWaiting.Duration.ShouldBe(originalWaiting.Duration);
                    break;
                case ScenarioNpcMovementAction originalMovement:
                    var restoredMovement = (ScenarioNpcMovementAction)restoredAction;
                    restoredMovement.TargetPosition.ShouldBe(originalMovement.TargetPosition);
                    restoredMovement.Speed.ShouldBe(NpcSpeed.Running);
                    break;
                case ScenarioNpcEmoteAction originalEmote:
                    var restoredEmote = (ScenarioNpcEmoteAction)restoredAction;
                    restoredEmote.Emote.ShouldBe(originalEmote.Emote);
                    restoredEmote.Loop.ShouldBe(originalEmote.Loop);
                    restoredEmote.Duration.ShouldBe(originalEmote.Duration);
                    restoredEmote.NpcTalk.ShouldBe(originalEmote.NpcTalk);
                    break;

                default:
                    break;
            }
        }

    }

    [Fact]
    public void ScenarioFile_IsKeepingTheDrawOffsetBetweenSerialization() {

        var scenario = new ScenarioData();
        var npc = new ScenarioNpcData { Name = GetRandomString(), DrawOffset = new Vector3(0.15f, 0.85f, -0.25f) };
        scenario.Npcs.Add(npc);

        var restoredScenario = Recode(scenario);

        restoredScenario.Npcs[0].DrawOffset.ShouldBe(npc.DrawOffset);
    }

    [Fact]
    public void ScenarioFile_IsKeepingCustomSpeedsBetweenSerialization() {

        var scenario = new ScenarioData();
        var npc = new ScenarioNpcData { Name = GetRandomString() };
        npc.Actions.Add(new ScenarioNpcMovementAction { TargetPosition = GetRandomVector3(), Speed = NpcSpeed.Custom, CustomSpeed = 4.25f });
        npc.Actions.Add(new ScenarioNpcPathAction {
            Points = [
                new PathMovementPoint { Point = GetRandomVector3(), Speed = NpcSpeed.Custom, CustomSpeed = 0.75f },
                new PathMovementPoint { Point = GetRandomVector3(), Speed = NpcSpeed.Running, CustomSpeed = 0f }
            ]
        });
        scenario.Npcs.Add(npc);

        var restoredScenario = RecodeWithPluginOptions(scenario);

        var restoredMovement = (ScenarioNpcMovementAction)restoredScenario.Npcs[0].Actions[0];
        restoredMovement.Speed.ShouldBe(NpcSpeed.Custom);
        restoredMovement.CustomSpeed.ShouldBe(4.25f);

        var restoredPath = (ScenarioNpcPathAction)restoredScenario.Npcs[0].Actions[1];
        restoredPath.Points[0].Speed.ShouldBe(NpcSpeed.Custom);
        restoredPath.Points[0].CustomSpeed.ShouldBe(0.75f);
        restoredPath.Points[1].Speed.ShouldBe(NpcSpeed.Running);
    }

    [Fact]
    public void ScenarioFile_PresetSpeeds_DoNotWriteACustomSpeedKey() {

        var scenario = new ScenarioData();
        var npc = new ScenarioNpcData { Name = GetRandomString() };
        npc.Actions.Add(new ScenarioNpcMovementAction { TargetPosition = GetRandomVector3(), Speed = NpcSpeed.Walking });
        scenario.Npcs.Add(npc);

        var json = JsonSerializer.Serialize(scenario, ScenarioFileManager.ScenarioLoadSerializerOptions);

        // walking and running stay byte-identical to what older versions of the plugin wrote,
        // which is why adding a custom speed needs no scenario migration
        json.ShouldNotContain("CustomSpeed");
        json.ShouldContain("\"Walking\"");
    }

    private static ScenarioData RecodeWithPluginOptions(ScenarioData data) {
        var json = JsonSerializer.Serialize(data, ScenarioFileManager.ScenarioLoadSerializerOptions);
        json.ShouldNotBeNullOrEmpty();

        var deserialized = JsonSerializer.Deserialize<ScenarioData>(json, ScenarioFileManager.ScenarioLoadSerializerOptions);
        deserialized.ShouldNotBeNull();

        return deserialized;
    }

    private static ScenarioData Recode(ScenarioData data) {
        var options = new JsonSerializerOptions();
        options.WriteIndented = true;
        options.Converters.Add(new Vector3Converter());
        options.TypeInfoResolver = new DefaultJsonTypeInfoResolver { Modifiers = { NullStringModifier.Instance } };
        var serializedStuff = JsonSerializer.Serialize(data, options);
        serializedStuff.ShouldNotBeNullOrEmpty();

        var deserialized = JsonSerializer.Deserialize<ScenarioData>(serializedStuff, options);
        deserialized.ShouldNotBeNull();

        return deserialized;
    }

    private static string GetRandomString()
        => Convert.ToBase64String(Guid.NewGuid().ToByteArray());

    private static Vector3 GetRandomVector3()
        => new(
            Random.Shared.NextSingle() * 1000f,
            Random.Shared.NextSingle() * 1000f,
            Random.Shared.NextSingle() * 1000f);

    private static float GetRandomRadian()
        => Random.Shared.NextSingle() * MathF.PI * 2f;

    private static float GetRandomTime()
        => Random.Shared.NextSingle() * 10;

}
