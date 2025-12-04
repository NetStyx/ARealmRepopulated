using ARealmRepopulated.Configuration;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace ARealmRepopulated.Infrastructure;

public class DalamudDiWrapper(
    IDalamudPluginInterface pluginInterface,
    ICommandManager commandManager,
    IClientState clientState,
    IDataManager dataManager,
    IPluginLog log,
    IObjectTable objectTable,
    IFramework framework,
    IGameInteropProvider interopProvider,
    ICondition condition,
    ITargetManager targetManager,
    IDtrBar dtrBar,
    ITextureProvider textureProvider,
    IGameGui gui) {
    public IServiceCollection CreateServiceCollection()
        => new ServiceCollection()
            .AddSingleton(log)
            .AddSingleton(dataManager)
            .AddSingleton(commandManager)
            .AddSingleton(pluginInterface)
            .AddSingleton(clientState)
            .AddSingleton(objectTable)
            .AddSingleton(framework)
            .AddSingleton(interopProvider)
            .AddSingleton(condition)
            .AddSingleton(targetManager)
            .AddSingleton(dtrBar)
            .AddSingleton(gui)
            .AddSingleton(textureProvider)
            .AddSingleton<ArrpGameHooks>()
            .AddSingleton<ArrpEventService>()
            .AddSingleton<ArrpDataCache>()
            .AddSingleton<ArrpDtrControl>()
            .AddSingleton(s => s.GetRequiredService<IDalamudPluginInterface>().GetPluginConfig() as PluginConfig ?? new PluginConfig());
}
