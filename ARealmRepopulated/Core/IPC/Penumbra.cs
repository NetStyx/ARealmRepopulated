using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;

namespace ARealmRepopulated.Core.IPC;

public class Penumbra(IPluginLog log, IDalamudPluginInterface pluginInterface) : IIntegrationSetup {

    private ICallGateSubscriber<int, Guid, bool, bool, ValueTuple<int, ValueTuple<Guid, string>?>>? SetCollectionForObject;
    private ICallGateSubscriber<string, List<ValueTuple<Guid, string>>>? GetCollectionsByIdentifier;

    private ICallGateSubscriber<Guid, int, bool, int>? AssignTemporaryCollection;
    private ICallGateSubscriber<string, string, ValueTuple<int, Guid>>? CreateTemporaryCollection;

    public void Setup() {
        GetCollectionsByIdentifier = pluginInterface.GetIpcSubscriber<string, List<ValueTuple<Guid, string>>>("Penumbra.GetCollectionsByIdentifier");
        SetCollectionForObject = pluginInterface.GetIpcSubscriber<int, Guid, bool, bool, ValueTuple<int, ValueTuple<Guid, string>?>>("Penumbra.SetCollectionForObject.V5");
        CreateTemporaryCollection = pluginInterface.GetIpcSubscriber<string, string, ValueTuple<int, Guid>>("Penumbra.CreateTemporaryCollection.V6");
        AssignTemporaryCollection = pluginInterface.GetIpcSubscriber<Guid, int, bool, int>("Penumbra.AssignTemporaryCollection.V5");
    }

    public void SetCollection(ushort index) {

        var tempCollection = CreateTemporaryCollection?.InvokeFunc("arrp", "ARRPTempCollection");
        if (tempCollection != null) {
            AssignTemporaryCollection?.InvokeFunc(tempCollection.Value.Item2, index, true);
        }
        /*
        var collection = GetCollectionsByIdentifier?.InvokeFunc("ARRPCollection");
        if (collection != null && collection.Count > 0) {
            var (guid, name) = collection[0];
            SetCollectionForObject?.InvokeFunc(index, guid, false, false);
        }
        */
    }
}
