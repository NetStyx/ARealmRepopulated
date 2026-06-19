using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;

namespace ARealmRepopulated.Core.IPC;

public class Glamourer(IPluginLog log, IDalamudPluginInterface pluginInterface) : IIntegrationSetup, IDisposable {

    private ICallGateSubscriber<Dictionary<Guid, string>>? _getDesigns;
    private ICallGateSubscriber<Guid, int, uint, ulong, int>? _applyDesign;

    private ICallGateSubscriber<nint, int>? _onStateChanged;

    public void Setup() {
        _getDesigns = pluginInterface.GetIpcSubscriber<Dictionary<Guid, string>>("Glamourer.GetDesignList.V2");
        _applyDesign = pluginInterface.GetIpcSubscriber<Guid, int, uint, ulong, int>("Glamourer.ApplyDesign");
        _onStateChanged = pluginInterface.GetIpcSubscriber<nint, int>("Glamourer.StateChanged.V2");
        _onStateChanged?.Subscribe(OnStateChanged);
    }

    private Guid designid = Guid.Empty;
    public void GetDesigns() {
        var designs = _getDesigns?.InvokeFunc();
        foreach (var design in designs ?? []) {
            log.Information($"Design: {design.Key} - {design.Value}");
            designid = design.Key;
        }
    }

    private void OnStateChanged(nint arg) {
        log.Information($"State changed: {arg}");
    }

    public void ApplyDesign(ushort index) {
        _applyDesign?.InvokeFunc(designid, index, 0, 0x7);
    }

    public void Dispose() {
        _onStateChanged?.Unsubscribe(OnStateChanged);
    }
}
