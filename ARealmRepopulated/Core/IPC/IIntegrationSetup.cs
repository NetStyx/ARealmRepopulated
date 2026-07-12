using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace ARealmRepopulated.Core.IPC;

public interface IIntegrationSetup {
    void Setup();
}

public abstract class IntegrationPluginBase(IDalamudPluginInterface pluginInterface, IPluginLog log, string internalName) : IIntegrationSetup, IDisposable {

    public void Setup() {
        pluginInterface.ActivePluginsChanged += _activePluginsChanged;
        var penumbra = pluginInterface.InstalledPlugins.FirstOrDefault(p => p.InternalName.Equals(internalName));
        if (penumbra?.IsLoaded == true) {
            this.PluginActivated();
        }
    }

    private void _activePluginsChanged(IActivePluginsChangedEventArgs pc) {
        if (!pc.AffectedInternalNames.Contains(internalName))
            return;

        if (pc.Kind == PluginListInvalidationKind.Loaded) {
            log.Debug($"Plugin '{internalName}' is active");
            this.PluginActivated();
        } else if (pc.Kind == PluginListInvalidationKind.Unloaded) {
            log.Debug($"Plugin '{internalName}' is deactivated");
            this.PluginDeactivated();
        }
    }

    public abstract void PluginActivated();
    public abstract void PluginDeactivated();

    public void Dispose() {
        GC.SuppressFinalize(this);
        pluginInterface.ActivePluginsChanged -= _activePluginsChanged;
    }

}
