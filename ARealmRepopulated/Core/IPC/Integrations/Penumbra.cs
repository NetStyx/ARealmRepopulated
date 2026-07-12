using ARealmRepopulated.Configuration;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace ARealmRepopulated.Core.IPC.Integrations;

public class Penumbra(PluginConfig config, IPluginLog log, IDalamudPluginInterface pluginInterface) : IntegrationPluginBase(pluginInterface, log, InternalName) {

    private const string InternalName = "Penumbra";

    public override void PluginActivated()
        => config?.RuntimeConfig.LoadedModdingPlugins.Add(InternalName);

    public override void PluginDeactivated()
        => config?.RuntimeConfig.LoadedModdingPlugins.Remove(InternalName);

}

/*    
// guess what, dalamud offers an easier way of finding out if a plugin is running.

private ICallGateSubscriber<int>? _pnbApiVersion;
private ICallGateSubscriber<object>? _pnbInitialized;

public void Setup() {
    _pnbApiVersion = pluginInterface.GetIpcSubscriber<int>("Penumbra.ApiVersion");
    _pnbInitialized = pluginInterface.GetIpcSubscriber<object>("Penumbra.Initialized");
    _pnbInitialized?.Subscribe(PenumbraInitialized);
    RetrievePenumbraVersion();
}

private void PenumbraInitialized()
    => RetrievePenumbraVersion();

private void RetrievePenumbraVersion() {
    try {
        var pnbApiVersion = _pnbApiVersion?.InvokeFunc();
        if (pnbApiVersion != null) {
            log.Information($"Found penumbra {pnbApiVersion.Value}");
            config?.RuntimeConfig.ModdingToolsInstalled = true;
        }
    } catch (Exception) { }
}

public void Dispose() {
    _pnbInitialized?.Unsubscribe(PenumbraInitialized);
}*/
