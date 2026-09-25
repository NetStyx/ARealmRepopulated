using ARealmRepopulated.Data.Scenarios;
using FFXIVClientStructs.FFXIV.Client.Graphics.Environment;
using FFXIVClientStructs.FFXIV.Client.System.Framework;

namespace ARealmRepopulated.Core.Services.Scenarios.Conditions;

public unsafe class ScenarioConditionService {

    private readonly Random _random = new();

    public IngameConditionSnapshot TakeIngameSnapshot() {
        var framework = Framework.Instance();
        var envManager = EnvManager.Instance();

        return new(
            framework != null ? ScenarioConditionEvaluator.ToEorzeaHour(framework->ClientTime.EorzeaTime) : 0,
            envManager != null ? envManager->ActiveWeather : (byte)0);
    }

    public CustomConditionSnapshot TakeCustomSnapshot()
        => new(_random.NextDouble() * 100d);

    public bool AreConditionsMet(IReadOnlyList<ScenarioCondition> conditions, IngameConditionSnapshot ingame)
        => ScenarioConditionEvaluator.AreConditionsMet(conditions, ingame, TakeCustomSnapshot());
}
