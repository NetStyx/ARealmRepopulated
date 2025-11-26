using ARealmRepopulated.Core.Json;
using ARealmRepopulated.Data.Scenarios;
using System;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace ARealmRepopulated.Tests;

public class ScenarioFileTests
{


    [Fact]
    public void ValidateSingleLoopScenario()
    {
        var scenario = new ScenarioData();
        var standingScenarioNpc = new ScenarioNpcData { Name = "BoredGirl", Appearance = "", Position = new Vector3(131.17422f, 40.02f, 3.1433084f), Rotation = -1.5506701f };
        standingScenarioNpc.Actions.Add(new ScenarioNpcEmoteAction { Emote = 295 });
        scenario.Npcs.Add(standingScenarioNpc);

        var movingScenarioNpc = new ScenarioNpcData { Name = "LookoutGirl", Appearance = "", Position = new Vector3(125.87311f, 40.02f, 3.218611f), Rotation = -0.7883096f };
        movingScenarioNpc.Actions.Add(new ScenarioNpcWaitingAction { Duration = 5f, NpcTalk = "Nothing on this side..." });
        movingScenarioNpc.Actions.Add(new ScenarioNpcMovementAction { TargetPosition = new Vector3(107.87578f, 40.02f, -14.861387f) });
        movingScenarioNpc.Actions.Add(new ScenarioNpcRotationAction { TargetRotation = -0.79250574f });
        movingScenarioNpc.Actions.Add(new ScenarioNpcEmoteAction { Emote = 22, Loop = false, Duration = 10 });
        movingScenarioNpc.Actions.Add(new ScenarioNpcWaitingAction { Duration = 5f, NpcTalk = "Just doing rounds is exhausting" });
        movingScenarioNpc.Actions.Add(new ScenarioNpcMovementAction { TargetPosition = new Vector3(125.87311f, 40.02f, 3.218611f) });
        movingScenarioNpc.Actions.Add(new ScenarioNpcRotationAction { TargetRotation = -0.7883096f });
        scenario.Npcs.Add(movingScenarioNpc);

        ValidateScenario(scenario);
    }

    [Fact]
    public void ValidateScenarioFiles()
    {


        var scenario = new ScenarioData { Title = "Two Girls, One Place", Description = "Two girls meet in the middle of their paths to finally figure out who was in the wrong." };

        var meetGirlOne = new ScenarioNpcData { Name = "MeetGirlOne", Appearance = "", Position = new Vector3(130.00665f, 40.02f, 4.126983f), Rotation = -2.3646383f };
        meetGirlOne.Actions.Add(new ScenarioNpcWaitingAction { Duration = 2 });
        meetGirlOne.Actions.Add(new ScenarioNpcMovementAction { TargetPosition = new Vector3(120.02116f, 40.02f, -6.0284934f) });
        meetGirlOne.Actions.Add(new ScenarioNpcSyncAction());

        meetGirlOne.Actions.Add(new ScenarioNpcEmoteAction { Duration = 4, Emote = 3, NpcTalk = "You are late!" });
        meetGirlOne.Actions.Add(new ScenarioNpcWaitingAction { Duration = 2 });
        meetGirlOne.Actions.Add(new ScenarioNpcWaitingAction { Duration = 2, NpcTalk = "I am not standing for this!" });
        meetGirlOne.Actions.Add(new ScenarioNpcSyncAction());

        meetGirlOne.Actions.Add(new ScenarioNpcMovementAction { TargetPosition = new Vector3(130.00665f, 40.02f, 4.126983f) });
        meetGirlOne.Actions.Add(new ScenarioNpcSyncAction());


        var meetGirlTwo = new ScenarioNpcData { Name = "MeetGirlTwo", Appearance = "", Position = new Vector3(112.723595f, 40.02f, -13.586888f), Rotation = 0.776951f };
        meetGirlTwo.Actions.Add(new ScenarioNpcWaitingAction { Duration = 2 });
        meetGirlTwo.Actions.Add(new ScenarioNpcMovementAction { TargetPosition = new Vector3(118.59509f, 40.019997f, -7.478999f) });
        meetGirlTwo.Actions.Add(new ScenarioNpcSyncAction());

        meetGirlTwo.Actions.Add(new ScenarioNpcEmoteAction { Duration = 4, Emote = 171 });
        meetGirlTwo.Actions.Add(new ScenarioNpcWaitingAction { Duration = 2, NpcTalk = "I know, sorry!" });
        meetGirlTwo.Actions.Add(new ScenarioNpcWaitingAction { Duration = 2 });
        meetGirlTwo.Actions.Add(new ScenarioNpcSyncAction());

        meetGirlTwo.Actions.Add(new ScenarioNpcMovementAction { TargetPosition = new Vector3(112.723595f, 40.02f, -13.586888f) });
        meetGirlTwo.Actions.Add(new ScenarioNpcSyncAction());


        scenario.Npcs.Add(meetGirlOne);
        scenario.Npcs.Add(meetGirlTwo);

        ValidateScenario(scenario);
    }


    private void ValidateScenario(ScenarioData data)
    {
        var options = new JsonSerializerOptions();
        options.WriteIndented = true;
        options.Converters.Add(new Vector3Converter());
        options.TypeInfoResolver = new DefaultJsonTypeInfoResolver { Modifiers = { NullStringModifier.Instance } };
        var serializedStuff = JsonSerializer.Serialize(data, options);

        var deserializedObject = JsonSerializer.Deserialize<ScenarioData>(serializedStuff, options);
    }

}
