using ARealmRepopulated.Configuration;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;

namespace ARealmRepopulated.Core.IPC;

public class Glamourer(PluginConfig config, IPluginLog log, IDalamudPluginInterface pluginInterface) : IIntegrationSetup, IDisposable {

    private ICallGateSubscriber<int>? _glmApiVersion;
    private ICallGateSubscriber<object>? _glmInitialized;

    public void Setup() {
        _glmApiVersion = pluginInterface.GetIpcSubscriber<int>("Glamourer.ApiVersion");
        _glmInitialized = pluginInterface.GetIpcSubscriber<object>("Glamourer.Initialized");
        _glmInitialized?.Subscribe(GlamourerInitialized);
        RetrieveGlamourerVersion();
    }

    private void GlamourerInitialized()
        => RetrieveGlamourerVersion();

    private void RetrieveGlamourerVersion() {
        try {
            var glmApiVersion = _glmApiVersion?.InvokeFunc();
            if (glmApiVersion != null) {
                log.Information($"Found glamourer {glmApiVersion.Value}");
                config?.RuntimeConfig.ModdingToolsInstalled = true;
            }
        } catch (Exception) { }
    }

    public void Dispose() {
        _glmApiVersion?.Unsubscribe(GlamourerInitialized);
    }
}
