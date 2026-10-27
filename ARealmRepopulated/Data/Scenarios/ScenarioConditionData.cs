using System.Text.Json.Serialization;

namespace ARealmRepopulated.Data.Scenarios;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$condition")]
[JsonDerivedType(typeof(ScenarioEorzeaTimeCondition), typeDiscriminator: "EorzeaTime")]
[JsonDerivedType(typeof(ScenarioWeatherCondition), typeDiscriminator: "Weather")]
[JsonDerivedType(typeof(ScenarioChanceCondition), typeDiscriminator: "Chance")]
public abstract class ScenarioCondition {

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool Negate { get; set; } = false;

    [JsonIgnore]
    public abstract bool IsConfigured { get; }
}

public class ScenarioEorzeaTimeCondition : ScenarioCondition {
    public TimeOnly Start { get; set; } = new(18, 0);
    public TimeOnly End { get; set; } = new(6, 0);

    [JsonIgnore]
    public override bool IsConfigured => Start != End;

    public override string ToString() => $"EorzeaTime [{Start.Hour:00}:{Start.Minute:00}-{End.Hour:00}:{End.Minute:00}]";
}

public class ScenarioWeatherCondition : ScenarioCondition {
    public List<byte> WeatherIds { get; set; } = [];

    [JsonIgnore]
    public override bool IsConfigured => WeatherIds.Count > 0;

    public override string ToString() => $"Weather [{string.Join(",", WeatherIds)}]";
}

public class ScenarioChanceCondition : ScenarioCondition {
    public float Percent { get; set; } = 50f;

    public float RollInterval { get; set; } = 1f;

    [JsonIgnore]
    public override bool IsConfigured => true;

    public override string ToString() => $"Chance [{Percent}% every {RollInterval}s]";
}
