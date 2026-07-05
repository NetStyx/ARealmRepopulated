using ARealmRepopulated.Configuration;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;

namespace ARealmRepopulated.Core.IPC;

public class Penumbra(PluginConfig config, IPluginLog log, IDalamudPluginInterface pluginInterface) : IIntegrationSetup, IDisposable {

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
    }

}
