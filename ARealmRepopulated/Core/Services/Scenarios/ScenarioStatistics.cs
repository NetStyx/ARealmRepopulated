namespace ARealmRepopulated.Core.Services.Scenarios;

/// <summary>
/// Holds metrics about the scenarios runtime behavior.
/// </summary>
public class ScenarioStatistics {

    private const int RunSampleSize = 10;

    private readonly Queue<TimeSpan> _runTimes = new();
    private TimeSpan _currentRunTime = TimeSpan.Zero;    
    public TimeSpan DelayBetweenRuns { get; set; } = TimeSpan.Zero;
    public TimeSpan AverageRunTime { get; private set; } = TimeSpan.Zero;

    public event Action? OnLoopingTooFast;
    public bool IsLoopingTooFast { get; private set; }

    public void Tick(TimeSpan time)
        => _currentRunTime += time;

    public void Lap() {
        _runTimes.Enqueue(_currentRunTime + DelayBetweenRuns);
        if (_runTimes.Count > RunSampleSize)
            _runTimes.Dequeue();

        _currentRunTime = TimeSpan.Zero;

        AverageRunTime = TimeSpan.FromTicks((long)_runTimes.Average(t => t.Ticks));
        var wasLoopingTooFast = IsLoopingTooFast;
        IsLoopingTooFast = _runTimes.Count == RunSampleSize && AverageRunTime.TotalSeconds < 0.5f;
        if (!wasLoopingTooFast && IsLoopingTooFast)
            OnLoopingTooFast?.Invoke();
    }
}
