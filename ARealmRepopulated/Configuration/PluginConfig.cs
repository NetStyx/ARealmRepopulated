using Dalamud.Configuration;
using Dalamud.Plugin;
using System.Text.Json.Serialization;

namespace ARealmRepopulated.Configuration;

[Serializable]
public class PluginConfig : IPluginConfiguration {

    public int Version { get; set; } = 0;

    public bool AutoLoadScenarios { get; set; } = true;

    public bool ShowInDtrBar { get; set; } = true;

    public bool EnableScenarioDebugOverlay { get; set; } = true;

    public bool OnboardingCompleted { get; set; } = false;

    public void Save() {
        Plugin.Services.GetRequiredService<IDalamudPluginInterface>().SavePluginConfig(this);
    }

    [JsonExtensionData]
    internal Dictionary<string, object> AdditionalData { get; set; } = [];

}
