using ARealmRepopulated.Data.Scenarios;

namespace ARealmRepopulated.Core.Services.Scenarios.Conditions;

/// <summary>
/// Evaluates the conditions of a loaded scenario and keeps the state needed between evaluations.
/// </summary>
public sealed class ScenarioConditionTracker(ScenarioData data) {

    private readonly ScenarioChanceCondition? _chance = data.Conditions.OfType<ScenarioChanceCondition>().FirstOrDefault();

    private double? _heldRoll;
    private TimeSpan _timeSinceRoll;
    private bool _areIngameConditionsMet;

    public bool HasConditions
        => data.Conditions.Any(c => c.IsConfigured);

    public bool AreConditionsMet { get; private set; }

    public void Tick(TimeSpan time)
        => _timeSinceRoll += time;

    public void Evaluate(IngameConditionSnapshot ingame) {
        _areIngameConditionsMet = ScenarioConditionEvaluator.AreMet(data.Conditions, ingame);

        if (_chance != null && (_heldRoll is null || _timeSinceRoll.TotalSeconds >= _chance.RollInterval)) {
            _heldRoll = Random.Shared.NextDouble() * 100d;
            _timeSinceRoll = TimeSpan.Zero;
        }

        AreConditionsMet = ScenarioConditionEvaluator.AreMet(data.Conditions, ingame, new CustomConditionSnapshot(_heldRoll));
    }

    public bool ShouldEnd(ScenarioRunState run) => data.EndBehavior switch {
        ScenarioEndBehavior.Immediately => !AreConditionsMet,
        ScenarioEndBehavior.AfterRun when run == ScenarioRunState.Finished => !AreConditionsMet,
        ScenarioEndBehavior.AfterRun when run == ScenarioRunState.WaitingEndlessly => !_areIngameConditionsMet,
        _ => false
    };
}
