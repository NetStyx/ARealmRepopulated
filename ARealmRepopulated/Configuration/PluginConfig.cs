using Dalamud.Configuration;
using Dalamud.Plugin;
using System.Text.Json.Serialization;

namespace ARealmRepopulated.Configuration;

[Serializable]
public class PluginConfig : IPluginConfiguration {

    [JsonIgnore]
    internal IDalamudPluginInterface PluginInterface { get; set; } = null!;

    [JsonExtensionData]
    internal Dictionary<string, object> AdditionalData { get; set; } = [];

    /// <summary>
    /// Defines the soft limit for the number of actors that can be spawned across all scenarios. Choosen to leave enough room for unrelated client objects. 
    /// This counts only against actors spawned by this plugin and does not take other allocations into account.
    /// </summary>
    public const int MaxActorSoftLimit = 150;

    /// <summary>
    /// Defines the hard limit for the number of actors that can be spawned across all scenarios.
    /// It is used to block creation of additional objects by adding the count of existing client object table entries to the already spawned actors and compare them to this hard limit.       
    /// </summary>
    /// <remarks>
    /// I dont expose these this via the UI but i can be overriden in the configuration if needed.
    /// </remarks>
    /// <example>
    /// If, for whatever reason, there are already 50 objects present in the client object table, only 180 - 50 = 130 actors can be spawned by the plugin before reaching the hard limit even when the soft limit is not yet reached.      
    /// </example>
    public const int MaxActorHardLimit = 180;

    private int _actorSoftLimit = MaxActorSoftLimit;
    private int _actorHardLimit = MaxActorHardLimit;

    public int Version { get; set; } = 0;

    public bool AutoLoadScenarios { get; set; } = true;

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
}
