using ARealmRepopulated.Configuration;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace ARealmRepopulated.Core.IPC.Integrations;

public class CPlus(PluginConfig config, IPluginLog log, IDalamudPluginInterface pluginInterface) : IntegrationPluginBase(pluginInterface, log, InternalName) {

    private const string InternalName = "CustomizePlus";

    public override void PluginActivated()
        => config?.RuntimeConfig.LoadedModdingPlugins.Add(InternalName);

    public override void PluginDeactivated()
        => config?.RuntimeConfig.LoadedModdingPlugins.Remove(InternalName);
}
