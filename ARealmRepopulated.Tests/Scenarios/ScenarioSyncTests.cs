using ARealmRepopulated.Core.Services.Scenarios;
using ARealmRepopulated.Data.Scenarios;
using Dalamud.Plugin.Services;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace ARealmRepopulated.Tests.Scenarios;

/// <summary>
/// Drives scenarios made of waiting and sync actions only, those never touch the game actor.
/// </summary>
public class ScenarioSyncTests {

    private static readonly TimeSpan Frame = TimeSpan.FromSeconds(1.0 / 60);

    private static ScenarioNpcAction Wait(float seconds, bool enabled = true)
        => new ScenarioNpcWaitingAction { Duration = seconds, Enabled = enabled };

    private static ScenarioNpcAction Sync(bool enabled = true)
        => new ScenarioNpcSyncAction { Enabled = enabled };

    private static ScenarioNpc Actor(params ScenarioNpcAction[] actions) {
        var npc = new ScenarioNpc(NullLog.Instance);
        npc.SetActions([.. actions]);
        return npc;
    }

    private static Scenario CreateScenario(params ScenarioNpc[] actors) {
        var scenario = new Scenario(NullLog.Instance);
        scenario.Npcs.AddRange(actors);
        return scenario;
    }

    /// <summary>
    /// Advances until the scenario finishes, returns the elapsed seconds or null if it did not finish in time.
    /// </summary>
    private static double? RunUntilFinished(Scenario scenario, double maxSeconds = 30) {
        var frames = 0;
        while (frames * Frame.TotalSeconds < maxSeconds) {
            if (scenario.IsFinished)
                return frames * Frame.TotalSeconds;

            scenario.Advance(Frame);
            frames++;
        }
        return null;
    }

    [Fact]
    public void Advance_EqualSyncCounts_Finishes() {
        var scenario = CreateScenario(Actor(Wait(1), Sync(), Wait(1)), Actor(Wait(1), Sync(), Wait(1)));

        RunUntilFinished(scenario).ShouldNotBeNull();
    }

    [Fact]
    public void Advance_ActorWithFewerSyncs_Finishes() {
        var scenario = CreateScenario(Actor(Wait(1), Sync(), Wait(1)), Actor(Wait(1)));

        RunUntilFinished(scenario).ShouldNotBeNull();
    }

    [Fact]
    public void Advance_Sync_StartsNextSegmentForAllActorsTogether() {
        var first = Actor(Wait(1), Sync(), Wait(1));
        var second = Actor(Wait(3), Sync(), Wait(1));
        var scenario = CreateScenario(first, second);

        // the first actor is done with its first wait after one second, but has to wait for the second actor
        var elapsed = 0.0;
        while (elapsed < 2.5) {
            scenario.Advance(Frame);
            elapsed += Frame.TotalSeconds;
        }
        first.CurrentAction.IsSync.ShouldBeTrue();

        // both second segments take one second, so both reach the end of the scenario together
        RunUntilFinished(scenario)!.Value.ShouldBe(1.5, 0.1);
    }

    [Fact]
    public void Advance_DisabledFinalSync_Finishes() {
        var scenario = CreateScenario(Actor(Wait(1), Sync(enabled: false)), Actor(Wait(3)));

        RunUntilFinished(scenario).ShouldNotBeNull();
    }

    [Fact]
    public void Advance_FinishedLoop_StartsOverWithFirstSegment() {
        var actor = Actor(Wait(1), Sync(), Wait(1));
        var scenario = CreateScenario(actor);
        RunUntilFinished(scenario).ShouldNotBeNull();

        typeof(Scenario).GetField("_state", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(scenario).ShouldBeOfType<ScenarioState>().CurrentScenarioSegment = 0;
        scenario.Advance(Frame);

        actor.CurrentAction.Action.ShouldBeOfType<ScenarioNpcWaitingAction>();
        RunUntilFinished(scenario)!.Value.ShouldBe(2, 0.1);
    }

    private class NullLog : DispatchProxy {
        public static readonly IPluginLog Instance = Create<IPluginLog, NullLog>();

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
            => targetMethod!.ReturnType.IsValueType && targetMethod.ReturnType != typeof(void)
                ? Activator.CreateInstance(targetMethod.ReturnType)
                : null;
    }
}
