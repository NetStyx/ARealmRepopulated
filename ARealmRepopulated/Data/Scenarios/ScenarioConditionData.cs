using System.Text.Json.Serialization;

namespace ARealmRepopulated.Data.Scenarios;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$condition")]
[JsonDerivedType(typeof(ScenarioEorzeaTimeCondition), typeDiscriminator: "EorzeaTime")]
[JsonDerivedType(typeof(ScenarioWeatherCondition), typeDiscriminator: "Weather")]
[JsonDerivedType(typeof(ScenarioChanceCondition), typeDiscriminator: "Chance")]
public abstract class ScenarioCondition {

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool Negate { get; set; } = false;

    /// <summary>
    /// A condition that was added but never filled in must not lock the scenario out. Every condition
    /// type decides for itself what "not filled in yet" means and is ignored while that holds.
    /// </summary>
    [JsonIgnore]
    public abstract bool IsConfigured { get; }
}

/// <summary>
/// Matches an Eorzean hour window. <see cref="EndHour"/> is exclusive and the window wraps around
/// midnight when it is smaller than <see cref="StartHour"/>.
/// </summary>
public class ScenarioEorzeaTimeCondition : ScenarioCondition {
    public int StartHour { get; set; } = 18;
    public int EndHour { get; set; } = 6;

    [JsonIgnore]
    public override bool IsConfigured => StartHour != EndHour;

    public override string ToString() => $"EorzeaTime [{StartHour:00}:00-{EndHour:00}:00]";
}

/// <summary>
/// Matches while the currently rendered weather is one of <see cref="WeatherIds"/>.
/// </summary>
public class ScenarioWeatherCondition : ScenarioCondition {
    public List<byte> WeatherIds { get; set; } = [];

    [JsonIgnore]
    public override bool IsConfigured => WeatherIds.Count > 0;

    public override string ToString() => $"Weather [{string.Join(",", WeatherIds)}]";
}

/// <summary>
/// Rolls a fresh chance every time the conditions are evaluated.
/// </summary>
public class ScenarioChanceCondition : ScenarioCondition {
    public float Percent { get; set; } = 50f;

    [JsonIgnore]
    public override bool IsConfigured => true;

    public override string ToString() => $"Chance [{Percent}%]";
}
