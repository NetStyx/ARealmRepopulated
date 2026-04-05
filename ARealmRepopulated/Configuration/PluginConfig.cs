using Dalamud.Configuration;
using Dalamud.Plugin;
using System.Text.Json.Serialization;

namespace ARealmRepopulated.Configuration;

[Serializable]
public class PluginConfig : IPluginConfiguration {

    [JsonIgnore]
    internal IDalamudPluginInterface PluginInterface { get; set; } = null!;

    public int Version { get; set; } = 0;

    public bool AutoLoadScenarios { get; set; } = true;

    public const int MaxActorSoftLimit = 150;
    public const int MaxActorHardLimit = 180;

    private int _actorSoftLimit = MaxActorSoftLimit;
    private int _actorHardLimit = MaxActorHardLimit;

    public int ActorSoftLimit {
        get => _actorSoftLimit;
        set => _actorSoftLimit = Math.Clamp(value, 0, MaxActorSoftLimit);
    }

    public int ActorHardLimit {
        get => _actorHardLimit;
        set => _actorHardLimit = Math.Clamp(value, 0, MaxActorHardLimit);
    }

    public bool ShowInDtrBar { get; set; } = true;

    public bool EnableScenarioDebugOverlay { get; set; } = true;

    public bool OnboardingCompleted { get; set; } = false;

    public void Save() {
        PluginInterface.SavePluginConfig(this);
    }

    [JsonExtensionData]
    internal Dictionary<string, object> AdditionalData { get; set; } = [];

}
