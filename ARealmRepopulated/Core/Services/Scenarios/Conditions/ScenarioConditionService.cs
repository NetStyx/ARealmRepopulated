using ARealmRepopulated.Data.Scenarios;
using FFXIVClientStructs.FFXIV.Client.Graphics.Environment;
using FFXIVClientStructs.FFXIV.Client.System.Framework;

namespace ARealmRepopulated.Core.Services.Scenarios.Conditions;

/// <summary>
/// Reads the world state the scenario conditions are judged against. Both reads are plain field
/// reads - the Eorzean clock off the framework and the weather the environment is currently
/// rendering, which is the one the player actually sees, overrides included.
/// </summary>
public unsafe class ScenarioConditionService {

    private readonly Random _random = new();

    public ScenarioConditionSnapshot TakeSnapshot() {
        var framework = Framework.Instance();
        var envManager = EnvManager.Instance();

        return new ScenarioConditionSnapshot(
            framework != null ? ScenarioConditionEvaluator.ToEorzeaHour(framework->ClientTime.EorzeaTime) : 0,
            envManager != null ? envManager->ActiveWeather : (byte)0);
    }

    public bool AreConditionsMet(IReadOnlyList<ScenarioCondition> conditions, ScenarioConditionSnapshot snapshot)
        => ScenarioConditionEvaluator.AreConditionsMet(conditions, snapshot, () => _random.NextDouble() * 100d);
}
