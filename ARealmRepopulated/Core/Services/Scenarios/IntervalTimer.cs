namespace ARealmRepopulated.Core.Services.Scenarios;

/// <summary>
/// Quick helper which returns true when a certain time has passed and then resets itself.
/// </summary>
public sealed class IntervalTimer(TimeSpan interval) {

    private TimeSpan _elapsed;

    public bool Tick(TimeSpan time) {
        _elapsed += time;
        if (_elapsed < interval)
            return false;

        _elapsed = TimeSpan.Zero;
        return true;
    }

    public void Trigger()
        => _elapsed = interval;
}
