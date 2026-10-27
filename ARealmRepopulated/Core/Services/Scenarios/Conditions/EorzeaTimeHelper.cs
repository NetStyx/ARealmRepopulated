namespace ARealmRepopulated.Core.Services.Scenarios.Conditions;

public static class EorzeaTimeHelper {

    // Eorzea time, like real time, consists of 24 hours with 60 minutes of 60 seconds and is delivered as seconds ... so - nothing special.
    // The only difference to real time is that eorzea time runs approximately 20 times faster which does not matter for the calculations here.
    public static TimeOnly ToTimeOfDay(long timeInSeconds)
        => TimeOnly.MinValue.Add(TimeSpan.FromSeconds(timeInSeconds % (24 * 60 * 60)));

    /// <summary>
    /// Inclusive start, exclusive end. A window that ends before it starts wraps around midnight; equal bounds cover the whole day.
    /// </summary>
    public static bool IsWithinWindow(TimeOnly time, TimeOnly start, TimeOnly end)
        => start == end || time.IsBetween(start, end);
}
