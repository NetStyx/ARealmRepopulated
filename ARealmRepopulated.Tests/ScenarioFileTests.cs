using ARealmRepopulated.Core.Json;
using ARealmRepopulated.Data.Scenarios;
using Shouldly;
using System;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace ARealmRepopulated.Tests;

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
