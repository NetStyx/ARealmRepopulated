using ARealmRepopulated.Data.Scenarios;

namespace ARealmRepopulated.Core.Services.Scenarios.Conditions;

/// <summary>
/// Evaluates the conditions of a loaded scenario and keeps the state needed between evaluations.
/// </summary>
public sealed class ScenarioConditionTracker(ScenarioData data) {

    private double? _heldRoll;
    private TimeSpan _timeSinceRoll;
    private bool _areMetIgnoringChance;

    public bool HasConditions
        => data.Conditions.Any(c => c.IsConfigured);

    public bool AreMet { get; private set; }

    public void Tick(TimeSpan time)
        => _timeSinceRoll += time;

    public void Evaluate(IngameConditionSnapshot ingame) {
        if (_heldRoll is null || _timeSinceRoll.TotalSeconds >= data.ConditionRollInterval) {
            _heldRoll = Random.Shared.NextDouble() * 100d;
            _timeSinceRoll = TimeSpan.Zero;
        }

        AreMet = ScenarioConditionEvaluator.AreMet(data.Conditions, ingame, new CustomConditionSnapshot(_heldRoll.Value));
        _areMetIgnoringChance = ScenarioConditionEvaluator.AreMet(data.Conditions, ingame);
    }

    public bool ShouldEnd(ScenarioRunState run) => data.EndBehavior switch {
        ScenarioEndBehavior.Immediately => !AreMet,
        ScenarioEndBehavior.AfterRun when run == ScenarioRunState.Finished => !AreMet,
        ScenarioEndBehavior.AfterRun when run == ScenarioRunState.WaitingEndlessly => !_areMetIgnoringChance,
        _ => false
    };
}
